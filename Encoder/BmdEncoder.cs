using System.Runtime.InteropServices;
using C2_BMD_to_sprites.Decoders;

namespace C2_BMD_to_sprites.Encoder;

internal static class BmdEncoder
{
    internal static BmdFile Repack(BmdFile source, bool optimize, Func<int, BmdFramePixels>? getPixels = null, bool idOrder = false)
    {
        List<byte> pixels = [];
        List<uint> rows = [];
        BmdFrame[] frames = (BmdFrame[])source.Frames.Clone();
        uint generated = 0, empty = 0, reused = 0;
        // Without row-table reuse, the stored row indices reveal the original AddBob order.
        // Optimized files may lose that history; byte comparisons expose that limitation.
        IEnumerable<int> order = Enumerable.Range(0, frames.Length);
        if (!idOrder) order = order.OrderBy(i => source.Frames[i].FirstRowIndex);
        foreach (int i in order)
        {
            BmdFrame frame = source.Frames[i];
            if (frame.Type == 0) { frames[i] = frame; continue; }
            BmdFramePixels image = getPixels is null ? BmdPixelDecoder.Decode(source, frame) : getPixels(i);
            if (image.Width != frame.Width || image.Height != frame.Height) throw new InvalidDataException($"Frame {i} dimensions changed.");
            uint[] frameRows = new uint[image.Height];
            for (int y = 0; y < image.Height; y++)
            {
                generated = checked(generated + 1);
                byte[] packed = BmdRowEncoder.Encode(frame.Type, image.Values.AsSpan(y * image.Width, image.Width), image.Occupied.AsSpan(y * image.Width, image.Width), out uint indent);
                if (packed.Length == 0) { frameRows[y] = uint.MaxValue; empty = checked(empty + 1); continue; }
                // Mac AddBob searches the complete byte stream, including matches inside earlier runs.
                int offset = optimize ? CollectionsMarshal.AsSpan(pixels).IndexOf(packed) : -1;
                if (offset < 0)
                {
                    offset = pixels.Count;
                    if ((long)offset + packed.Length > 0x3FFFFF) throw new InvalidDataException("Packed stream exceeds the native 22-bit buffer limit.");
                    pixels.AddRange(packed);
                }
                else reused = checked(reused + 1);
                frameRows[y] = BmdRow.Create(indent, checked((uint)offset)).Packed;
            }
            int rowBase = -1;
            if (optimize)
            {
                ReadOnlySpan<uint> existing = CollectionsMarshal.AsSpan(rows);
                for (int start = 0; start <= existing.Length - frameRows.Length; start++)
                {
                    if (existing.Slice(start, frameRows.Length).SequenceEqual(frameRows)) { rowBase = start; break; }
                }
            }
            if (rowBase < 0) { rowBase = rows.Count; rows.AddRange(frameRows); }
            frames[i] = frame with { FirstRowIndex = checked((uint)rowBase) };
        }
        BmdHeader header = source.Header with { FrameCount = checked((uint)frames.Length), PackedByteCount = checked((uint)pixels.Count), LineCount = checked((uint)rows.Count), GeneratedLineCount = generated, EmptyLineCount = empty, ReusedLineCount = reused };
        return new BmdFile(header, frames, pixels.ToArray(), rows.Select(value => new BmdRow(value)).ToArray(), source.SectionVersions, source.TrailingData);
    }
}
