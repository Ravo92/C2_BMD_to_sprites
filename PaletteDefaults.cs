using SixLabors.ImageSharp.PixelFormats;

namespace C2_BMD_to_sprites;

internal static class PaletteDefaults
{
    internal static Rgba32[] LoadPcxPalette(string pcxPath)
    {
        byte[] bytes = File.ReadAllBytes(pcxPath);
        if (bytes.Length < 897 || bytes[0] != 10 || bytes[3] != 8 || bytes[65] != 1 || bytes[^769] != 12) throw new InvalidDataException($"Expected an 8-bit single-plane PCX with a 256-color tail palette: {pcxPath}");
        Rgba32[] palette = new Rgba32[256];
        int start = bytes.Length - 768;
        for (int i = 0; i < palette.Length; i++) palette[i] = new Rgba32(bytes[start + i * 3], bytes[start + i * 3 + 1], bytes[start + i * 3 + 2], 255);
        return palette;
    }
}
