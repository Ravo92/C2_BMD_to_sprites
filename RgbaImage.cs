using System.Buffers.Binary;
using StbImageSharp;
using StbImageWriteSharp;

namespace C2_BMD_to_sprites;

// Straight RGBA bytes, deliberately independent of the image library. Numeric
// index PNGs must never premultiply alpha or discard hidden RGB channels.
internal record struct Rgba32(byte R, byte G, byte B, byte A);

internal sealed class RgbaImage
{
    internal int Width { get; }
    internal int Height { get; }
    private readonly byte[] data;

    internal RgbaImage(int width, int height) : this(width, height, new byte[checked(width * height * 4)]) { }

    private RgbaImage(int width, int height, byte[] data)
    {
        Width = width;
        Height = height;
        this.data = data;
    }

    internal Rgba32 this[int x, int y]
    {
        get
        {
            int offset = (y * Width + x) * 4;
            return new(data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
        }
        set
        {
            int offset = (y * Width + x) * 4;
            data[offset] = value.R;
            data[offset + 1] = value.G;
            data[offset + 2] = value.B;
            data[offset + 3] = value.A;
        }
    }

    internal static RgbaImage LoadPng(string path, uint expectedWidth, uint expectedHeight)
    {
        using FileStream stream = File.OpenRead(path);
        ValidatePngContainer(stream, path, expectedWidth, expectedHeight);
        stream.Position = 0;
        ImageResult image;
        try { image = ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha); }
        catch (InvalidOperationException ex) { throw new InvalidDataException($"Invalid PNG at {path}: {ex.Message}", ex); }
        if (image.Width != expectedWidth || image.Height != expectedHeight || image.Data.Length != checked(image.Width * image.Height * 4)) throw new InvalidDataException($"PNG dimensions changed while decoding: {path}");
        return new RgbaImage(image.Width, image.Height, image.Data);
    }

    internal void SavePng(Stream stream) => new ImageWriter().WritePng(data, Width, Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);

    // stb reads static PNGs, so reject APNG instead of silently importing only
    // its default image. Check dimensions and chunk boundaries before decoding.
    private static void ValidatePngContainer(Stream stream, string path, uint expectedWidth, uint expectedHeight)
    {
        BmdFramePixels.ValidateDimensions(expectedWidth, expectedHeight);
        Span<byte> signature = stackalloc byte[8];
        stream.ReadExactly(signature);
        if (!signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException($"Expected a PNG image: {path}");
        bool headerSeen = false, pixelsSeen = false;
        Span<byte> chunk = stackalloc byte[8];
        Span<byte> header = stackalloc byte[13];
        while (stream.Position < stream.Length)
        {
            stream.ReadExactly(chunk);
            uint length = BinaryPrimitives.ReadUInt32BigEndian(chunk);
            ReadOnlySpan<byte> kind = chunk[4..];
            if ((long)length + 4 > stream.Length - stream.Position) throw new InvalidDataException($"Truncated PNG chunk: {path}");
            if (kind.SequenceEqual("acTL"u8) || kind.SequenceEqual("fcTL"u8) || kind.SequenceEqual("fdAT"u8)) throw new InvalidDataException($"Expected a single-frame PNG: {path}");
            if (kind.SequenceEqual("IHDR"u8))
            {
                if (headerSeen || length != 13) throw new InvalidDataException($"Invalid or duplicate PNG header: {path}");
                stream.ReadExactly(header);
                uint width = BinaryPrimitives.ReadUInt32BigEndian(header), height = BinaryPrimitives.ReadUInt32BigEndian(header[4..]);
                if (width == 0 || height == 0 || width != expectedWidth || height != expectedHeight) throw new InvalidDataException($"{path}: PNG dimensions differ from the frame metadata.");
                stream.Seek(4, SeekOrigin.Current); // CRC; pixel decompression belongs to stb.
                headerSeen = true;
                continue;
            }
            if (!headerSeen) throw new InvalidDataException($"PNG must start with IHDR: {path}");
            if (kind.SequenceEqual("IDAT"u8)) pixelsSeen = true;
            if (kind.SequenceEqual("IEND"u8))
            {
                if (length != 0 || !pixelsSeen) throw new InvalidDataException($"Invalid PNG end marker: {path}");
                return;
            }
            stream.Seek((long)length + 4, SeekOrigin.Current);
        }
        throw new InvalidDataException($"Missing PNG end marker: {path}");
    }
}
