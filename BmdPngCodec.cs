namespace C2_BMD_to_sprites;

internal static class BmdPngCodec
{
    internal static RgbaImage Export(BmdFramePixels pixels, uint type, Rgba32[]? palette)
    {
        RgbaImage image = new(pixels.Width, pixels.Height);
        for (int y = 0; y < pixels.Height; y++)
        {
            for (int x = 0; x < pixels.Width; x++)
            {
                int p = y * pixels.Width + x;
                ushort value = pixels.Values[p];
                image[x, y] = palette is null
                    ? new Rgba32((byte)value, (byte)(value >> 8), 0, pixels.Occupied[p] ? (byte)255 : (byte)0)
                    : PreviewPixel(type, value, pixels.Occupied[p], palette);
            }
        }
        return image;
    }

    internal static BmdFramePixels Import(string path, BmdFrame frame, Rgba32[]? palette, BmdFramePixels? original = null)
    {
        RgbaImage image = RgbaImage.LoadPng(path, frame.Width, frame.Height);
        BmdFramePixels result = new(image.Width, image.Height);
        Dictionary<uint, byte> colorMap = [];
        if (palette is not null) for (int i = 0; i < 256; i++) colorMap.TryAdd(RgbKey(palette[i]), (byte)i);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int p = y * image.Width + x;
                Rgba32 pixel = image[x, y];
                if (palette is null)
                {
                    if (pixel.A is not (0 or 255) || pixel.B != 0 || (frame.Type != 4 && pixel.G != 0)) throw new InvalidDataException($"Invalid raw index PNG at {path}, ({x},{y}).");
                    result.Values[p] = (ushort)(pixel.R | pixel.G << 8);
                    result.Occupied[p] = pixel.A == 255;
                    if (result.Occupied[p] && ((frame.Type == 2 && pixel.R != 255) || (frame.Type == 3 && pixel.R == 255))) throw new InvalidDataException($"Invalid mask/time-mask value at {path}, ({x},{y}).");
                    continue;
                }
                if (original is not null && pixel.Equals(PreviewPixel(frame.Type, original.Values[p], original.Occupied[p], palette)))
                {
                    result.Values[p] = original.Values[p];
                    result.Occupied[p] = original.Occupied[p];
                    continue;
                }
                if (pixel.A == 0) continue;
                result.Occupied[p] = true;
                if (frame.Type == 2)
                {
                    if (pixel.A != 255) throw new InvalidDataException($"Mask pixels must be opaque or transparent at {path}, ({x},{y}).");
                    result.Values[p] = 255;
                }
                else if (frame.Type == 3)
                {
                    if (pixel.A != 255 || pixel.R != pixel.G || pixel.R != pixel.B || pixel.R == 255) throw new InvalidDataException($"Time-mask preview requires opaque gray 0..254 at {path}, ({x},{y}).");
                    result.Values[p] = pixel.R;
                }
                else
                {
                    if (frame.Type == 1 && pixel.A != 255) throw new InvalidDataException($"Type 1 cannot store partial alpha at {path}, ({x},{y}).");
                    // Alpha-only edits must also retain duplicate palette indices.
                    byte index;
                    if (original is not null && original.Occupied[p] && RgbKey(pixel) == RgbKey(palette[original.Values[p] & 255])) index = (byte)original.Values[p];
                    else if (!colorMap.TryGetValue(RgbKey(pixel), out index)) throw new InvalidDataException($"Off-palette RGB at {path}, ({x},{y}); automatic color quantization is disabled.");
                    result.Values[p] = (ushort)(index | (frame.Type == 4 ? pixel.A << 8 : 0));
                }
            }
        }
        return result;
    }

    private static Rgba32 PreviewPixel(uint type, ushort value, bool occupied, Rgba32[] palette)
    {
        if (!occupied) return default;
        if (type == 2) return new Rgba32(0, 0, 0, 255);
        if (type == 3) return new Rgba32((byte)value, (byte)value, (byte)value, 255);
        Rgba32 pixel = palette[value & 255];
        pixel.A = type == 4 ? (byte)(value >> 8) : (byte)255;
        return pixel;
    }

    private static uint RgbKey(Rgba32 pixel) => pixel.R | (uint)pixel.G << 8 | (uint)pixel.B << 16;
}
