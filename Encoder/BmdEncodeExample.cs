namespace C2_BMD_to_sprites.Encoder;

internal static class BmdEncodeExample
{
    internal static void WriteSingleType1FrameBmd(string outPath, byte[] indexedPixels, int width, int height, byte transparentIndex)
    {
        ArgumentNullException.ThrowIfNull(indexedPixels);
        if (width <= 0 || height <= 0 || indexedPixels.Length != checked(width * height)) throw new ArgumentException("Expected a positive frame and exactly width * height indexed pixels.");
        BmdFramePixels image = new(width, height);
        for (int i = 0; i < indexedPixels.Length; i++) { image.Values[i] = indexedPixels[i]; image.Occupied[i] = indexedPixels[i] != transparentIndex; }
        BmdFrame frame = new(1, 0, 0, (uint)width, (uint)height, 0);
        BmdFile source = new(new BmdHeader(0, 0, 1, 0, 0, 0, 0, 0), [frame], [], [], [0, 0, 0], []);
        File.WriteAllBytes(outPath, BmdWriter.Write(BmdEncoder.Repack(source, false, _ => image)));
    }
}
