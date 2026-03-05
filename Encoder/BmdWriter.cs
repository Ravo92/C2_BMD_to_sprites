using System.Buffers.Binary;

namespace C2_BMD_to_sprites.Encoder
{
    internal static class BmdWriter
    {
        internal static byte[] Write(BmdFrame[] frames, byte[] pixels, BmdRow[] rows)
        {
            ArgumentNullException.ThrowIfNull(frames);

            ArgumentNullException.ThrowIfNull(pixels);

            ArgumentNullException.ThrowIfNull(rows);

            int sec1Len = checked(frames.Length * 24);
            int sec2Len = pixels.Length;
            int sec3Len = checked(rows.Length * 4);

            int total =
                4 + 8 + 4 + 4 + 4 + 4 + 4 + 4 + // global header
                12 + sec1Len +                  // sec1
                12 + sec2Len +                  // sec2
                12 + sec3Len;                   // sec3

            byte[] output = new byte[total];
            Span<byte> span = output.AsSpan();
            int o = 0;

            WriteU32(span, ref o, 1012u);          // magic
            WriteU64(span, ref o, 0ul);            // zero1

            // These three are treated as "counts" in some dumps, but your reader trusts section_length.
            // Still: fill with reasonable values (helps debugging).
            WriteU32(span, ref o, checked((uint)frames.Length)); // sec1Noe
            WriteU32(span, ref o, checked((uint)sec2Len));       // sec2Noe (bytes)
            WriteU32(span, ref o, checked((uint)rows.Length));   // sec3Noe

            WriteU32(span, ref o, 0u);             // unknown1
            WriteU32(span, ref o, 0u);             // unknown2
            WriteU32(span, ref o, 0u);             // zero2

            // Section 1: frames
            WriteSectionHeader(span, ref o, 1001u, 0u, checked((uint)sec1Len));
            for (int i = 0; i < frames.Length; i++)
            {
                BmdFrame f = frames[i];
                WriteU32(span, ref o, f.Type);
                WriteU32(span, ref o, f.Meta1);
                WriteU32(span, ref o, f.Meta2);
                WriteU32(span, ref o, f.Width);
                WriteU32(span, ref o, f.Height);
                WriteU32(span, ref o, f.RowOffsetInSec3);
            }

            // Section 2: pixels
            WriteSectionHeader(span, ref o, 1001u, 0u, checked((uint)sec2Len));
            pixels.AsSpan().CopyTo(span.Slice(o, sec2Len));
            o += sec2Len;

            // Section 3: rows
            WriteSectionHeader(span, ref o, 1001u, 0u, checked((uint)sec3Len));
            for (int i = 0; i < rows.Length; i++)
            {
                BmdRow r = rows[i];

                // Pack exactly like reader:
                // indent = (packed >> 22) & 0x3FF
                // off    = packed & 0x3FFFFF
                uint packed = ((r.Indent & 0x3FFu) << 22) | (r.PixelStreamOffset & 0x3FFFFFu);
                WriteU32(span, ref o, packed);
            }

            if (o != total)
            {
                throw new InvalidOperationException("Internal size mismatch while writing BMD.");
            }

            return output;
        }

        private static void WriteSectionHeader(Span<byte> dst, ref int o, uint magic, uint unknown, uint length)
        {
            WriteU32(dst, ref o, magic);
            WriteU32(dst, ref o, unknown);
            WriteU32(dst, ref o, length);
        }

        private static void WriteU32(Span<byte> dst, ref int o, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(dst.Slice(o, 4), value);
            o += 4;
        }

        private static void WriteU64(Span<byte> dst, ref int o, ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(dst.Slice(o, 8), value);
            o += 8;
        }
    }
}