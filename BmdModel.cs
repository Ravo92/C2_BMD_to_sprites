namespace C2_BMD_to_sprites;

internal sealed record BmdHeader(uint Version, uint FirstBobId, uint FrameCount, uint PackedByteCount, uint LineCount, uint GeneratedLineCount, uint EmptyLineCount, uint ReusedLineCount);

internal readonly record struct BmdFrame(uint Type, int XOffset, int YOffset, uint Width, uint Height, uint FirstRowIndex);

internal readonly record struct BmdRow(uint Packed)
{
    internal static BmdRow Empty => new(uint.MaxValue);
    internal bool IsEmpty => Packed == uint.MaxValue;
    internal uint Indent => Packed >> 22;
    internal uint PixelStreamOffset => Packed & 0x3FFFFF;

    internal static BmdRow Create(uint indent, uint offset)
    {
        if (indent > 1023 || offset > 0x3FFFFF) throw new InvalidDataException("Row indentation or pixel offset exceeds the BMD bit field.");
        uint packed = (indent << 22) | offset;
        if (packed == uint.MaxValue) throw new InvalidDataException("Nonempty row collides with the empty-row sentinel.");
        return new BmdRow(packed);
    }
}

internal sealed record BmdFile(BmdHeader Header, BmdFrame[] Frames, byte[] Pixels, BmdRow[] Rows, uint[] SectionVersions, byte[] TrailingData);

internal sealed class BmdFramePixels
{
    internal int Width { get; }
    internal int Height { get; }
    internal ushort[] Values { get; }
    internal bool[] Occupied { get; }

    internal BmdFramePixels(int width, int height)
    {
        if (width < 0 || height < 0 || width > 8192 || height > 8192 || (long)width * height > 16_777_216) throw new InvalidDataException($"Unsupported frame dimensions {width}x{height}.");
        Width = width;
        Height = height;
        Values = new ushort[checked(width * height)];
        Occupied = new bool[Values.Length];
    }
}
