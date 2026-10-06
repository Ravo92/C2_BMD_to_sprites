using C2_BMD_to_sprites.Decoders;
using C2_BMD_to_sprites.Encoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace C2_BMD_to_sprites;

internal static class BmdConverter
{
    internal static void Export(string inputPath, string outputDirectory, string? palettesDirectory, string logicalName)
    {
        BmdFile file = BmdReader.Read(File.ReadAllBytes(inputPath));
        GuiPaletteSet? palettes = palettesDirectory is null ? null : GuiPaletteSet.LoadFromDirectory(palettesDirectory);
        Directory.CreateDirectory(outputDirectory);
        for (int i = 0; i < file.Frames.Length; i++)
        {
            BmdFrame frame = file.Frames[i];
            if (frame.Type == 0) continue; // Unallocated record, not a guessed image.
            BmdFramePixels decoded = BmdPixelDecoder.Decode(file, frame);
            if (decoded.Width == 0 || decoded.Height == 0) throw new InvalidDataException("Active zero-sized frames cannot be represented as PNG.");
            string stem = FrameStem(logicalName, i);
            using Image<Rgba32> raw = new(decoded.Width, decoded.Height);
            for (int y = 0; y < decoded.Height; y++)
            {
                for (int x = 0; x < decoded.Width; x++)
                {
                    int p = y * decoded.Width + x;
                    ushort value = decoded.Values[p];
                    raw[x, y] = new Rgba32((byte)value, (byte)(value >> 8), 0, decoded.Occupied[p] ? (byte)255 : (byte)0);
                }
            }
            string rawPath = Path.Combine(outputDirectory, stem + ".indices.png");
            EnsureNewOutput(rawPath);
            raw.SaveAsPng(rawPath);
            if (palettes is null) continue;
            Rgba32[] palette = GuiPaletteRules.Resolve(logicalName, checked((int)((ulong)file.Header.FirstBobId + (uint)i)), palettes);
            using Image<Rgba32> preview = new(decoded.Width, decoded.Height);
            for (int y = 0; y < decoded.Height; y++) for (int x = 0; x < decoded.Width; x++) preview[x, y] = PreviewPixel(frame.Type, decoded.Values[y * decoded.Width + x], decoded.Occupied[y * decoded.Width + x], palette);
            string previewPath = Path.Combine(outputDirectory, stem + ".png");
            EnsureNewOutput(previewPath);
            preview.SaveAsPng(previewPath);
        }
    }

    internal static byte[] Import(string templatePath, string framesDirectory, string? palettesDirectory, string logicalName, bool optimize, bool idOrder = false)
    {
        BmdFile template = BmdReader.Read(File.ReadAllBytes(templatePath));
        GuiPaletteSet? palettes = palettesDirectory is null ? null : GuiPaletteSet.LoadFromDirectory(palettesDirectory);
        BmdFramePixels LoadFrame(int frameIndex)
        {
            BmdFrame frame = template.Frames[frameIndex];
            string suffix = palettes is null ? ".indices.png" : ".png";
            string path = Path.Combine(framesDirectory, FrameStem(logicalName, frameIndex) + suffix);
            using Image<Rgba32> image = Image.Load<Rgba32>(path);
            if (image.Width != frame.Width || image.Height != frame.Height) throw new InvalidDataException($"Frame {frameIndex}: PNG dimensions differ from the template.");
            BmdFramePixels result = new(image.Width, image.Height);
            BmdFramePixels? original = palettes is null ? null : BmdPixelDecoder.Decode(template, frame);
            Rgba32[]? palette = palettes is null ? null : GuiPaletteRules.Resolve(logicalName, checked((int)((ulong)template.Header.FirstBobId + (uint)frameIndex)), palettes);
            Dictionary<uint, byte> colorMap = [];
            if (palette is not null)
            {
                // Include index zero. Transparency is a run property, not a palette entry property.
                for (int i = 0; i < 256; i++) colorMap.TryAdd(RgbKey(palette[i]), (byte)i);
            }
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    int p = y * image.Width + x;
                    Rgba32 pixel = image[x, y];
                    if (palette is null)
                    {
                        if (pixel.A is not (0 or 255) || pixel.B != 0 || (frame.Type != 4 && pixel.G != 0)) throw new InvalidDataException($"Invalid raw index PNG at frame {frameIndex}, ({x},{y}).");
                        result.Values[p] = (ushort)(pixel.R | pixel.G << 8);
                        result.Occupied[p] = pixel.A == 255;
                        if (frame.Type == 2 && result.Occupied[p] && pixel.R != 255) throw new InvalidDataException("Mask index PNG must use R=255 for occupied pixels.");
                        continue;
                    }
                    // Unchanged preview pixels keep their original indices, including duplicate colors and alpha-zero literals.
                    if (pixel.Equals(PreviewPixel(frame.Type, original!.Values[p], original.Occupied[p], palette)))
                    {
                        result.Values[p] = original.Values[p];
                        result.Occupied[p] = original.Occupied[p];
                        continue;
                    }
                    if (pixel.A == 0) continue;
                    result.Occupied[p] = true;
                    if (frame.Type == 2)
                    {
                        if (pixel.A != 255) throw new InvalidDataException("Mask PNG pixels must be opaque or transparent.");
                        result.Values[p] = 255;
                    }
                    else if (frame.Type == 3)
                    {
                        if (pixel.A != 255 || pixel.R != pixel.G || pixel.R != pixel.B || pixel.R == 255) throw new InvalidDataException("Time-mask preview must use opaque gray values 0..254 or transparent pixels.");
                        result.Values[p] = pixel.R;
                    }
                    else
                    {
                        if (frame.Type == 1 && pixel.A != 255) throw new InvalidDataException("Type 1 cannot store partial alpha.");
                        if (!colorMap.TryGetValue(RgbKey(pixel), out byte index)) throw new InvalidDataException($"Off-palette RGB at frame {frameIndex}, ({x},{y}); automatic color quantization is disabled.");
                        result.Values[p] = (ushort)(index | (frame.Type == 4 ? pixel.A << 8 : 0));
                    }
                }
            }
            return result;
        }
        return BmdWriter.Write(BmdEncoder.Repack(template, optimize, LoadFrame, idOrder));
    }

    private static Rgba32 PreviewPixel(uint type, ushort value, bool occupied, Rgba32[] palette)
    {
        if (!occupied) return default;
        if (type == 2) return new Rgba32(0, 0, 0, 255);
        if (type == 3) return new Rgba32((byte)value, (byte)value, (byte)value, 255);
        Rgba32 pixel = palette[value & 255];
        pixel.A = type == 4 ? (byte)(value >> 8) : (byte)255;
        return pixel;
    }

    private static uint RgbKey(Rgba32 pixel) => pixel.R | (uint)pixel.G << 8 | (uint)pixel.B << 16;
    private static string FrameStem(string logicalName, int index) => $"{Path.GetFileNameWithoutExtension(logicalName)}_{index:D3}";
    private static void EnsureNewOutput(string path) { if (File.Exists(path)) throw new IOException($"Output already exists: {path}"); }
}
