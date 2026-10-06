using System.Buffers.Binary;

namespace C2_BMD_to_sprites;

internal static class BmdReader
{
    internal static BmdFile Read(ReadOnlySpan<byte> data)
    {
        SpanReader r = new(data);
        if (r.ReadU32() != 1012) throw new InvalidDataException("Expected CBobManager object ID 1012.");
        BmdHeader header = new(r.ReadU32(), r.ReadU32(), r.ReadU32(), r.ReadU32(), r.ReadU32(), r.ReadU32(), r.ReadU32(), r.ReadU32());
        // Windows 0043997D skips all three memory objects when bobCount is zero.
        if (header.FrameCount == 0) return new BmdFile(header, [], [], [], [], r.ReadRemaining());
        if (header.FrameCount > 1_000_000) throw new InvalidDataException("Unreasonable frame count.");
        byte[][] sections = new byte[3][];
        uint[] versions = new uint[3];
        for (int i = 0; i < 3; i++)
        {
            if (r.ReadU32() != 1001) throw new InvalidDataException($"Expected CMemory object ID 1001 in section {i + 1}.");
            versions[i] = r.ReadU32();
            sections[i] = r.ReadBytes(checked((int)r.ReadU32()));
        }
        if (sections[0].Length != checked((int)header.FrameCount * 24)) throw new InvalidDataException("Frame count disagrees with the 24-byte record section.");
        if (sections[1].Length != header.PackedByteCount || sections[2].Length != (long)header.LineCount * 4) throw new InvalidDataException("Header byte/line counts disagree with memory section sizes.");
        SpanReader framesReader = new(sections[0]);
        BmdFrame[] frames = new BmdFrame[header.FrameCount];
        for (int i = 0; i < frames.Length; i++) frames[i] = new(framesReader.ReadU32(), unchecked((int)framesReader.ReadU32()), unchecked((int)framesReader.ReadU32()), framesReader.ReadU32(), framesReader.ReadU32(), framesReader.ReadU32());
        SpanReader rowsReader = new(sections[2]);
        BmdRow[] rows = new BmdRow[header.LineCount];
        for (int i = 0; i < rows.Length; i++) rows[i] = new(rowsReader.ReadU32());
        BmdFile result = new(header, frames, sections[1], rows, versions, r.ReadRemaining());
        foreach (BmdFrame frame in frames)
        {
            if (frame.Type == 0) continue;
            if ((ulong)frame.FirstRowIndex + frame.Height > (ulong)rows.Length) throw new InvalidDataException("Frame row index range is outside the line table.");
        }
        foreach (BmdRow row in rows) if (!row.IsEmpty && row.PixelStreamOffset >= result.Pixels.Length) throw new InvalidDataException("Nonempty row points outside the pixel stream.");
        return result;
    }
}

internal ref struct SpanReader
{
    private readonly ReadOnlySpan<byte> data;
    private int position;
    internal SpanReader(ReadOnlySpan<byte> data) { this.data = data; position = 0; }
    internal uint ReadU32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadSpan(4));
    internal byte[] ReadBytes(int count) => ReadSpan(count).ToArray();
    internal byte[] ReadRemaining() => ReadBytes(data.Length - position);
    private ReadOnlySpan<byte> ReadSpan(int count)
    {
        if (count < 0 || count > data.Length - position) throw new EndOfStreamException($"Truncated BMD at byte {position} (requested {count}).");
        ReadOnlySpan<byte> value = data.Slice(position, count);
        position += count;
        return value;
    }
}
