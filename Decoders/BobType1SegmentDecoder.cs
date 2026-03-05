using C2_BMD_to_sprites.Decoders;

namespace C2_BMD_to_sprites.Decoders
{
    internal sealed class BobType1SegmentDecoder : IBmdPixelDecoder
    {
        // Format (per row):
        //   [ctrl][payload...][ctrl][payload...]...[0]
        //
        // ctrl:
        //   ctrl == 0                => end of row
        //   (ctrl & 0x80) != 0       => skip segment, len = ctrl & 0x7F, no payload
        //   (ctrl & 0x80) == 0       => literal segment, len = ctrl & 0x7F, payload = len bytes (palette indices)
        //
        // x starts at the row indent (provided by caller).
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
                    // Skip segment
                    x += len;
                    if (x >= targetWidth)
                    {
                        // It's safe to stop early for rendering purposes.
                        // The stream continues, but pixels would be outside the target anyway.
                        // If you need strict consumption, remove this early-exit.
                        return;
                    }
                    continue;
                }

                // Literal segment: len bytes payload
                if (i > rowStream.Length - len)
                {
                    // Truncated stream
                    return;
                }

                for (int k = 0; k < len; k++)
                {
                    int value = rowStream[i];
                    i++;

                    if ((uint)x < (uint)targetWidth)
                    {
                        emitPixel(value, x);
                    }

                    x++;
                    if (x >= targetWidth)
                    {
                        // Early-exit (see comment above)
                        return;
                    }
                }
            }
        }
    }
}