namespace C2_BMD_to_sprites.Encoder;

internal static class BmdWriter
{
    internal static byte[] Write(BmdFile file)
    {
        if (file.Header.FrameCount != file.Frames.Length || file.Header.PackedByteCount != file.Pixels.Length || file.Header.LineCount != file.Rows.Length) throw new InvalidDataException("Header counts do not match the output buffers.");
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(1012u);
        BmdHeader h = file.Header;
        writer.Write(h.Version);
        writer.Write(h.FirstBobId);
        writer.Write(h.FrameCount);
        writer.Write(h.PackedByteCount);
        writer.Write(h.LineCount);
        writer.Write(h.GeneratedLineCount);
        writer.Write(h.EmptyLineCount);
        writer.Write(h.ReusedLineCount);
        if (file.Frames.Length != 0)
        {
            if (file.SectionVersions.Length != 3) throw new InvalidDataException("Expected three memory object versions.");
            WriteSection(writer, file.SectionVersions[0], checked(file.Frames.Length * 24));
            foreach (BmdFrame frame in file.Frames)
            {
                writer.Write(frame.Type);
                writer.Write(frame.XOffset);
                writer.Write(frame.YOffset);
                writer.Write(frame.Width);
                writer.Write(frame.Height);
                writer.Write(frame.FirstRowIndex);
            }
            WriteSection(writer, file.SectionVersions[1], file.Pixels.Length);
            writer.Write(file.Pixels);
            WriteSection(writer, file.SectionVersions[2], checked(file.Rows.Length * 4));
            foreach (BmdRow row in file.Rows) writer.Write(row.Packed);
        }
        writer.Write(file.TrailingData);
        return stream.ToArray();
    }

    private static void WriteSection(BinaryWriter writer, uint version, int size) { writer.Write(1001u); writer.Write(version); writer.Write(checked((uint)size)); }
}
