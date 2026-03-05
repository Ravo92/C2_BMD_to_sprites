namespace C2_BMD_to_sprites
{
    internal static class BmdReader
    {
        internal static BmdFile Read(ReadOnlySpan<byte> data)
        {
            SpanReader r = new(data);

            uint magic = r.ReadU32LE();
            if (magic != 1012u)
            {
                throw new InvalidDataException($"Unexpected magic {magic}, expected 1012.");
            }

            ulong zero1 = r.ReadU64LE();
            _ = zero1;
            _ = r.ReadU32LE();
            uint sec2Noe = r.ReadU32LE();
            uint sec3Noe = r.ReadU32LE();

            uint unknown1 = r.ReadU32LE();
            uint unknown2 = r.ReadU32LE();
            uint zero2 = r.ReadU32LE();
            _ = unknown1;
            _ = unknown2;
            _ = zero2;

            BmdSection sec1 = ReadSectionHeader(ref r, 1001u);
            int frameCountByLength = checked((int)(sec1.SectionLength / 24u));
            int frameCount = frameCountByLength;
            if (frameCount < 0 || sec1.SectionLength % 24u != 0u)
            {
                throw new InvalidDataException($"sec1 section_length {sec1.SectionLength} is not a multiple of 24.");
            }

            BmdFrame[] frames = ReadFrames(ref r, checked((uint)frameCount));

            BmdSection sec2 = ReadSectionHeader(ref r, 1001u);
            int pixelByteLength = checked((int)sec2.SectionLength);

            // Some files store the count also in the global header; keep it as a sanity check only.
            if (sec2Noe != 0u && sec2Noe != sec2.SectionLength)
            {
                // Not fatal; section_length is treated as authoritative.
            }

            byte[] pixels = r.ReadBytesChecked(pixelByteLength);

            BmdSection sec3 = ReadSectionHeader(ref r, 1001u);
            if (sec3.SectionLength % 4u != 0u)
            {
                throw new InvalidDataException($"sec3 section_length {sec3.SectionLength} is not a multiple of 4.");
            }

            int rowCountByLength = checked((int)(sec3.SectionLength / 4u));

            // Again: global header count can be wrong; trust section_length.
            if (sec3Noe != 0u && sec3Noe != (uint)rowCountByLength)
            {
                // Not fatal.
            }

            BmdRow[] rows = ReadRows(ref r, checked((uint)rowCountByLength));

            return new BmdFile(frames, pixels, rows);
        }

        private static BmdSection ReadSectionHeader(ref SpanReader r, uint expectedMagic)
        {
            uint sectionMagic = r.ReadU32LE();
            uint unknown = r.ReadU32LE();
            uint sectionLength = r.ReadU32LE();

            if (sectionMagic != expectedMagic)
            {
                throw new InvalidDataException($"Unexpected section magic {sectionMagic}, expected {expectedMagic}.");
            }

            return new BmdSection(sectionMagic, unknown, sectionLength);
        }

        private static BmdFrame[] ReadFrames(ref SpanReader r, uint count)
        {
            if (count > 1_000_000u)
            {
                throw new InvalidDataException("Unreasonable frame count.");
            }

            BmdFrame[] frames = new BmdFrame[count];

            for (int i = 0; i < frames.Length; i++)
            {
                uint type = r.ReadU32LE();
                uint meta1 = r.ReadU32LE();
                uint meta2 = r.ReadU32LE();
                uint width = r.ReadU32LE();
                uint height = r.ReadU32LE();
                uint off = r.ReadU32LE();

                frames[i] = new BmdFrame(type, meta1, meta2, width, height, off);
            }

            return frames;
        }

        private static BmdRow[] ReadRows(ref SpanReader r, uint count)
        {
            if (count > 20_000_000u)
            {
                throw new InvalidDataException("Unreasonable row count.");
            }

            BmdRow[] rows = new BmdRow[count];

            for (int i = 0; i < rows.Length; i++)
            {
                uint packed = r.ReadU32LE();
                uint indent = (packed >> 22) & 0x3FFu;
                uint off = packed & 0x3FFFFFu;

                rows[i] = new BmdRow(indent, off);
            }

            return rows;
        }
    }
}
