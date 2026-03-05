using C2_BMD_to_sprites.Decoders;

namespace C2_BMD_to_sprites.Decoders
{
    internal sealed class BobType2MaskDecoder : IBmdPixelDecoder
    {
        // Format (per row):
        //   [ctrl][ctrl][ctrl]...[0]
        //
        // ctrl:
        //   ctrl == 0                => end of row
        //   (ctrl & 0x80) != 0       => skip segment, len = ctrl & 0x7F
        //   (ctrl & 0x80) == 0       => mask-run segment, len = ctrl (1..127)
        //
        // No payload bytes exist for this type.
        // emitPixel(value, x): value is 255 for masked pixels.
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

                if ((ctrl & 0x80) != 0)
                {
                    int skipLen = ctrl & 0x7F;
                    x += skipLen;

                    if (x >= targetWidth)
                    {
                        return;
                    }

                    continue;
                }

                int runLen = ctrl; // 1..127

                for (int k = 0; k < runLen; k++)
                {
                    if ((uint)x < (uint)targetWidth)
                    {
                        emitPixel(255, x);
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