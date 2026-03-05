using SixLabors.ImageSharp.PixelFormats;

namespace C2_BMD_to_sprites
{
    internal static class PaletteDefaults
    {
        // Mirrors CPicture::l_UnPackPCX: takes the last 768 bytes (256 * RGB) as palette.
        // Index 0 is treated as transparent (common for GUI bobs).
        internal static Rgba32[] LoadPcxPaletteWithTransparentZero(string pcxPath)
        {
            ArgumentNullException.ThrowIfNull(pcxPath);

            byte[] bytes = File.ReadAllBytes(pcxPath);

            if (bytes.Length < 768)
            {
                throw new InvalidDataException("PCX is too small to contain a 256-color palette.");
            }

            int paletteStart = bytes.Length - 768;

            Rgba32[] pal = new Rgba32[256];
            pal[0] = new Rgba32(0, 0, 0, 0);

            int p = paletteStart;
            for (int i = 0; i < 256; i++)
            {
                byte r = bytes[p + 0];
                byte g = bytes[p + 1];
                byte b = bytes[p + 2];
                p += 3;

                if (i == 0)
                {
                    continue;
                }

                pal[i] = new Rgba32(r, g, b, 255);
            }

            return pal;
        }
    }
}