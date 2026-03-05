using C2_BMD_to_sprites.Encoder;

namespace C2_BMD_to_sprites.Encoder
{
    internal static class BmdEncodeExample
    {
        internal static void WriteSingleType1FrameBmd(string outPath, byte[] indexedPixels, int width, int height, byte transparentIndex)
        {
            ArgumentNullException.ThrowIfNull(indexedPixels);

            if (indexedPixels.Length < width * height)
            {
                throw new ArgumentException("indexedPixels too small.");
            }

            List<byte> pixelStream = new(width * height / 2);
            BmdRow[] rows = new BmdRow[height];

            int rowBase = 0;

            for (int y = 0; y < height; y++)
            {
                byte[] row = new byte[width];
                Buffer.BlockCopy(indexedPixels, y * width, row, 0, width);

                uint packed = BmdRowEncoder.EncodeType1Row(row, width, transparentIndex, pixelStream, out uint indent);

                if (packed == 0xFFFFFFFFu)
                {
                    rows[y] = new BmdRow(0u, 0x3FFFFFu); // keep your reader's "no data" notion as well
                    continue;
                }

                uint off = packed & 0x3FFFFFu;
                rows[y] = new BmdRow(indent, off);
            }

            // Frame table: use your current 24-byte layout.
            // Meta1/Meta2 are currently opaque in your reader; leave 0 unless you have meaning for them.
            // RowOffsetInSec3: for this minimal example it's index 0.
            BmdFrame[] frames =
            [
                new BmdFrame(1u, 0u, 0u, checked((uint)width), checked((uint)height), checked((uint)rowBase))
            ];

            byte[] pixels = [.. pixelStream];

            byte[] bmdBytes = BmdWriter.Write(frames, pixels, rows);
            File.WriteAllBytes(outPath, bmdBytes);
        }
    }
}