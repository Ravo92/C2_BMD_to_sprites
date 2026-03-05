namespace C2_BMD_to_sprites.Encoder
{
    internal static class BmdRowEncoder
    {
        // Returns:
        //   - packedRow = 0xFFFFFFFF for fully empty row (engine does this too)
        //   - otherwise packedRow = (indent << 22) | offset
        //
        // Writes the row stream into pixelStream and returns offset.
        internal static uint EncodeType1Row(byte[] row, int width, byte transparentIndex, List<byte> pixelStream, out uint indent)
        {
            ArgumentNullException.ThrowIfNull(row);

            ArgumentNullException.ThrowIfNull(pixelStream);

            ArgumentOutOfRangeException.ThrowIfNegative(width);

            if (row.Length < width)
            {
                throw new ArgumentException("Row buffer is smaller than width.");
            }

            int first = FindFirstNotEqual(row, width, transparentIndex);
            if (first < 0)
            {
                indent = 0u;
                return 0xFFFFFFFFu;
            }

            if (first > 1023)
            {
                throw new InvalidDataException("Indent exceeds 10-bit range (0..1023).");
            }

            indent = (uint)first;

            int offset = pixelStream.Count;
            if (offset > 0x3FFFFF)
            {
                throw new InvalidDataException("Pixel stream offset exceeds 22-bit range.");
            }

            int x = first;

            while (x < width)
            {
                // Skip run (transparent)
                if (row[x] == transparentIndex)
                {
                    int len = CountEqual(row, width, x, transparentIndex, 127);
                    pixelStream.Add((byte)(0x80 | (byte)len));
                    x += len;
                    continue;
                }

                // Literal run (non-transparent), payload = len bytes
                int litLen = CountNotEqual(row, width, x, transparentIndex, 127);
                pixelStream.Add((byte)litLen);
                for (int i = 0; i < litLen; i++)
                {
                    pixelStream.Add(row[x + i]);
                }

                x += litLen;
            }

            pixelStream.Add(0); // end of row

            uint packed = (indent << 22) | (uint)offset;
            return packed;
        }

        internal static uint EncodeType2MaskRow(bool[] mask, int width, List<byte> pixelStream, out uint indent)
        {
            ArgumentNullException.ThrowIfNull(mask);

            ArgumentNullException.ThrowIfNull(pixelStream);

            if (mask.Length < width)
            {
                throw new ArgumentException("Mask buffer is smaller than width.");
            }

            int first = FindFirstTrue(mask, width);
            if (first < 0)
            {
                indent = 0u;
                return 0xFFFFFFFFu;
            }

            if (first > 1023)
            {
                throw new InvalidDataException("Indent exceeds 10-bit range (0..1023).");
            }

            indent = (uint)first;

            int offset = pixelStream.Count;
            if (offset > 0x3FFFFF)
            {
                throw new InvalidDataException("Pixel stream offset exceeds 22-bit range.");
            }

            int x = first;

            while (x < width)
            {
                // Skip run (mask off)
                if (!mask[x])
                {
                    int len = CountFalse(mask, width, x, 127);
                    pixelStream.Add((byte)(0x80 | (byte)len));
                    x += len;
                    continue;
                }

                // Run (mask on) — NO payload for type 2
                int runLen = CountTrue(mask, width, x, 127);
                pixelStream.Add((byte)runLen);
                x += runLen;
            }

            pixelStream.Add(0); // end of row

            uint packed = (indent << 22) | (uint)offset;
            return packed;
        }

        // Type 4: two 8-bit channels per pixel, payload pairs for literal runs.
        internal static uint EncodeType4Row(byte[] rowA, byte[] rowB, int width, byte transparentIndexA, List<byte> pixelStream, out uint indent)
        {
            ArgumentNullException.ThrowIfNull(rowA);

            ArgumentNullException.ThrowIfNull(rowB);

            ArgumentNullException.ThrowIfNull(pixelStream);

            if (rowA.Length < width || rowB.Length < width)
            {
                throw new ArgumentException("Row buffers are smaller than width.");
            }

            // For type 4, the engine used param_3 as "transparent" for the first bitmap.
            // Use rowA transparent detection to decide skip vs literal.
            int first = FindFirstNotEqual(rowA, width, transparentIndexA);
            if (first < 0)
            {
                indent = 0u;
                return 0xFFFFFFFFu;
            }

            if (first > 1023)
            {
                throw new InvalidDataException("Indent exceeds 10-bit range (0..1023).");
            }

            indent = (uint)first;

            int offset = pixelStream.Count;
            if (offset > 0x3FFFFF)
            {
                throw new InvalidDataException("Pixel stream offset exceeds 22-bit range.");
            }

            int x = first;

            while (x < width)
            {
                // Skip run based on A channel transparency
                if (rowA[x] == transparentIndexA)
                {
                    int len = CountEqual(rowA, width, x, transparentIndexA, 127);
                    pixelStream.Add((byte)(0x80 | (byte)len));
                    x += len;
                    continue;
                }

                // Literal run: payload pairs A,B
                int litLen = CountNotEqual(rowA, width, x, transparentIndexA, 127);
                pixelStream.Add((byte)litLen);
                for (int i = 0; i < litLen; i++)
                {
                    pixelStream.Add(rowA[x + i]);
                    pixelStream.Add(rowB[x + i]);
                }

                x += litLen;
            }

            pixelStream.Add(0);

            uint packed = (indent << 22) | (uint)offset;
            return packed;
        }

        private static int FindFirstNotEqual(byte[] row, int width, byte value)
        {
            for (int i = 0; i < width; i++)
            {
                if (row[i] != value)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindFirstTrue(bool[] mask, int width)
        {
            for (int i = 0; i < width; i++)
            {
                if (mask[i])
                {
                    return i;
                }
            }

            return -1;
        }

        private static int CountEqual(byte[] row, int width, int start, byte value, int maxLen)
        {
            int len = 0;
            int end = Math.Min(width, start + maxLen);

            for (int i = start; i < end; i++)
            {
                if (row[i] != value)
                {
                    break;
                }

                len++;
            }

            return len;
        }

        private static int CountNotEqual(byte[] row, int width, int start, byte value, int maxLen)
        {
            int len = 0;
            int end = Math.Min(width, start + maxLen);

            for (int i = start; i < end; i++)
            {
                if (row[i] == value)
                {
                    break;
                }

                len++;
            }

            return len;
        }

        private static int CountTrue(bool[] mask, int width, int start, int maxLen)
        {
            int len = 0;
            int end = Math.Min(width, start + maxLen);

            for (int i = start; i < end; i++)
            {
                if (!mask[i])
                {
                    break;
                }

                len++;
            }

            return len;
        }

        private static int CountFalse(bool[] mask, int width, int start, int maxLen)
        {
            int len = 0;
            int end = Math.Min(width, start + maxLen);

            for (int i = start; i < end; i++)
            {
                if (mask[i])
                {
                    break;
                }

                len++;
            }

            return len;
        }
    }
}