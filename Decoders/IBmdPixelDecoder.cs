namespace C2_BMD_to_sprites.Decoders
{
    internal interface IBmdPixelDecoder
    {
        void DecodeRow(ReadOnlySpan<byte> rowStream, int targetWidth, ref int x, Action<int, int> emitPixel);
    }
}