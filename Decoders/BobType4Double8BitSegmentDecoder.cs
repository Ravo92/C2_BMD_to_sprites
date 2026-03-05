using C2_BMD_to_sprites.Decoders;

namespace C2_BMD_to_sprites.Decoders
{
    internal sealed class BobType4Double8BitSegmentDecoder : IBmdPixelDecoder
    {
        // Same segment control scheme as type 1, but literal payload is 2 bytes per pixel:
        //   byte0, byte1, byte0, byte1, ...
        //
        // Output value is packed into an int:
        //   value = byte0 | (byte1 << 8)
        public void DecodeRow(ReadOnlySpan<byte> rowStream, int targetWidth, ref int x, Action<int, int> emitPixel)
        {
            int i = 0;

            while (i < rowStream.Length)
            {
                byte ctrl = rowStream[i];
                i++;

                if (ctrl == 0)
                {
                    return;
                }

                int len = ctrl & 0x7F;

                if ((ctrl & 0x80) != 0)
                {
                    x += len;
                    if (x >= targetWidth)
                    {
                        return;
                    }
                    continue;
                }

                int payloadBytes = checked(len * 2);
                if (i > rowStream.Length - payloadBytes)
                {
                    // Truncated stream
                    return;
                }

                for (int k = 0; k < len; k++)
                {
                    int b0 = rowStream[i];
                    int b1 = rowStream[i + 1];
                    i += 2;

                    int packed = b0 | (b1 << 8);

                    if ((uint)x < (uint)targetWidth)
                    {
                        emitPixel(packed, x);
                    }

                    x++;
                    if (x >= targetWidth)
                    {
                        return;
                    }
                }
            }
        }
    }
}