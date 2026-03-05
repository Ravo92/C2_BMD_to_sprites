using C2_BMD_to_sprites.Decoders;
using C2_BMD_to_sprites.Encoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using System.Buffers.Binary;

namespace C2_BMD_to_sprites
{
    internal static class BmdConverter
    {
        internal static void ConvertFileToPngFramesUsingGuiRules(string inputBmdPath, string outputDirectory, string palettesDirectory, string logicalBmdName)
        {
            ArgumentException.ThrowIfNullOrEmpty(inputBmdPath);
            ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
            ArgumentException.ThrowIfNullOrEmpty(palettesDirectory);
            ArgumentException.ThrowIfNullOrEmpty(logicalBmdName);

            Directory.CreateDirectory(outputDirectory);

            string logicalFileKey = logicalBmdName.ToLowerInvariant();               // e.g. "ls_gui_window.bmd"
            string logicalBaseName = Path.GetFileNameWithoutExtension(logicalBmdName); // e.g. "ls_gui_window"

            GuiPaletteSet pal = GuiPaletteSet.LoadFromDirectory(palettesDirectory);
            PaletteOverrides overrides = BuildDefaultOverrides();

            Dictionary<uint, IBmdPixelDecoder> decodersByType = new()
            {
                { 0x00000001u, new BobType1SegmentDecoder() },
                { 0x00000002u, new BobType2MaskDecoder() },
                { 0x00000004u, new BobType4Double8BitSegmentDecoder() }
            };

            IBmdPixelDecoder fallbackDecoder = new BobType1SegmentDecoder();

            byte[] fileBytes = File.ReadAllBytes(inputBmdPath);
            BmdFile bmd = BmdReader.Read(fileBytes);

            for (int i = 0; i < bmd.Frames.Length; i++)
            {
                BmdFrame frame = bmd.Frames[i];

                if (!decodersByType.TryGetValue(frame.Type, out IBmdPixelDecoder decoder))
                {
                    decoder = fallbackDecoder;
                }

                // IMPORTANT: use logical key for overrides
                Rgba32[] paletteForFrame = SelectPaletteForFrame(logicalFileKey, i, pal, overrides);

                using Image<Rgba32> image = RenderFrameToImage(bmd, frame, decoder, paletteForFrame);

                // IMPORTANT: stable file name without ".bmd" in the png name
                string outPath = Path.Combine(outputDirectory, $"{logicalBaseName}_{i:D3}.png");
                image.SaveAsPng(outPath);
            }
        }

        private static Rgba32[] SelectPaletteForFrame(string fileNameLower, int frameIndex, GuiPaletteSet pal, PaletteOverrides overrides)
        {
            if (overrides.TryResolve(fileNameLower, frameIndex, out PaletteKind forced))
            {
                return ResolvePaletteKind(forced, pal);
            }

            return pal.BgNormal;
        }

        private static Rgba32[] ResolvePaletteKind(PaletteKind kind, GuiPaletteSet pal)
        {
            return kind switch
            {
                PaletteKind.Frame => pal.Frame,
                PaletteKind.Papyrus => pal.Papyrus,
                PaletteKind.IconsLeft => pal.IconsLeft,
                PaletteKind.Context => pal.Context,
                PaletteKind.FontWhite => pal.FontWhite,
                _ => pal.BgNormal,
            };
        }

        private static PaletteOverrides BuildDefaultOverrides()
        {
            PaletteOverrides o = new();

            o.AddRange("ls_gui_window.bmd", 0, 16, PaletteKind.Frame);
            o.AddRange("ls_gui_window.bmd", 17, 20, PaletteKind.BgNormal);
            o.AddRange("ls_gui_window.bmd", 21, 24, PaletteKind.Frame);
            o.AddSingle("ls_gui_window.bmd", 25, PaletteKind.Papyrus);
            o.AddRange("ls_gui_window.bmd", 26, 41, PaletteKind.BgNormal);
            o.AddRange("ls_gui_window.bmd", 42, 69, PaletteKind.IconsLeft);
            o.AddSingle("ls_gui_window.bmd", 70, PaletteKind.Papyrus);
            o.AddRange("ls_gui_window.bmd", 71, 86, PaletteKind.BgNormal);
            o.AddRange("ls_gui_window.bmd", 87, 95, PaletteKind.IconsLeft);
            o.AddRange("ls_gui_window.bmd", 96, 141, PaletteKind.Context);
            o.AddRange("ls_gui_window.bmd", 142, 144, PaletteKind.BgNormal);
            o.AddSingle("ls_gui_window.bmd", 145, PaletteKind.IconsLeft);
            o.AddRange("ls_gui_window.bmd", 146, 177, PaletteKind.BgNormal);
            o.AddRange("ls_gui_window.bmd", 178, 179, PaletteKind.IconsLeft);
            o.AddSingle("ls_gui_window.bmd", 180, PaletteKind.FontWhite);
            o.AddRange("ls_gui_window.bmd", 181, 185, PaletteKind.IconsLeft);
            o.AddRange("ls_gui_window.bmd", 186, 191, PaletteKind.Frame);
            o.AddSingle("ls_gui_window.bmd", 192, PaletteKind.BgNormal);

            return o;
        }

        private static Image<Rgba32> RenderFrameToImage(BmdFile bmd, BmdFrame frame, IBmdPixelDecoder decoder, Rgba32[] palette)
        {
            if (frame.Width == 0u || frame.Height == 0u)
            {
                return new Image<Rgba32>(1, 1);
            }

            if (frame.Width > 8192u || frame.Height > 8192u)
            {
                throw new InvalidDataException($"Unreasonable frame size {frame.Width}x{frame.Height}.");
            }

            int width = checked((int)frame.Width);
            int height = checked((int)frame.Height);

            Image<Rgba32> image = new(width, height);

            int firstRowIndex = NormalizeRowIndex(frame.RowOffsetInSec3, bmd.Rows.Length);

            if (firstRowIndex < 0 || firstRowIndex + height > bmd.Rows.Length)
            {
                throw new InvalidDataException($"Frame row range out of bounds: start={firstRowIndex}, height={height}, rows={bmd.Rows.Length}, rawOff={frame.RowOffsetInSec3}.");
            }

            const int maxRowBytes = 65536;

            for (int y = 0; y < height; y++)
            {
                BmdRow row = bmd.Rows[firstRowIndex + y];

                if (!TryNormalizePixelOffset(row.PixelStreamOffset, bmd.Pixels.Length, out int rowOffset))
                {
                    continue;
                }

                int available = bmd.Pixels.Length - rowOffset;
                if (available <= 0)
                {
                    continue;
                }

                int take = Math.Min(available, maxRowBytes);
                ReadOnlySpan<byte> rowStream = bmd.Pixels.AsSpan(rowOffset, take);

                int x = checked((int)row.Indent);

                decoder.DecodeRow(rowStream, width, ref x, (value, px) =>
                {
                    if ((uint)px >= (uint)width)
                    {
                        return;
                    }

                    // Type 2: mask/shading bob (no palette indices). Render as alpha for debug.
                    if (frame.Type == 0x00000002u)
                    {
                        byte a = (byte)value;
                        image[px, y] = new Rgba32(0, 0, 0, a);
                        return;
                    }

                    // Type 4: two 8-bit channels packed into int: low=paletteIndex, high=alpha (best-effort).
                    if (frame.Type == 0x00000004u)
                    {
                        int index = value & 0xFF;
                        int alpha = (value >> 8) & 0xFF;

                        if ((uint)index >= 256u)
                        {
                            index = 0;
                        }

                        Rgba32 c = palette[index];
                        c.A = (byte)alpha;
                        image[px, y] = c;
                        return;
                    }

                    // Default: palette index (Type 1)
                    int idx = value;
                    if ((uint)idx >= 256u)
                    {
                        idx = 0;
                    }

                    image[px, y] = palette[idx];
                });
            }

            return image;
        }

        private static int NormalizeRowIndex(uint rawRowOffset, int rowCount)
        {
            // Case A: already an index
            if (rawRowOffset < (uint)rowCount)
            {
                return checked((int)rawRowOffset);
            }

            // Case B: byte offset in sec3 (each row descriptor is 4 bytes)
            if ((rawRowOffset % 4u) == 0u)
            {
                uint idx = rawRowOffset / 4u;
                if (idx < (uint)rowCount)
                {
                    return checked((int)idx);
                }
            }

            throw new InvalidDataException($"Frame row offset cannot be normalized. raw={rawRowOffset}, rows={rowCount}.");
        }

        private static bool TryNormalizePixelOffset(uint rawPixelOffset, int pixelStreamLength, out int offset)
        {
            // Sentinel: max 22-bit value is commonly used as "no data"
            if (rawPixelOffset == 0x3FFFFFu)
            {
                offset = pixelStreamLength;
                return false;
            }

            // Case A: already a byte offset into the sec2 pixel payload
            if (rawPixelOffset < (uint)pixelStreamLength)
            {
                offset = checked((int)rawPixelOffset);
                return true;
            }

            // Case B: some files store an offset including the 12-byte sec2 header.
            // Apply ONLY if the raw offset is out-of-range but becomes valid after subtracting 12.
            if (rawPixelOffset >= 12u)
            {
                uint adj = rawPixelOffset - 12u;
                if (rawPixelOffset >= (uint)pixelStreamLength && adj < (uint)pixelStreamLength)
                {
                    offset = checked((int)adj);
                    return true;
                }
            }

            offset = 0;
            return false;
        }

        internal static void EncodeBmdFromPngFramesUsingGuiRules(string originalBmdPath, string pngFramesDirectory, string outputBmdPath, string palettesDirectory, string logicalBmdName)
        {
            ArgumentException.ThrowIfNullOrEmpty(originalBmdPath);
            ArgumentException.ThrowIfNullOrEmpty(pngFramesDirectory);
            ArgumentException.ThrowIfNullOrEmpty(outputBmdPath);
            ArgumentException.ThrowIfNullOrEmpty(palettesDirectory);
            ArgumentException.ThrowIfNullOrEmpty(logicalBmdName);

            GuiPaletteSet pal = GuiPaletteSet.LoadFromDirectory(palettesDirectory);
            PaletteOverrides overrides = BuildDefaultOverrides();

            byte[] originalBytes = File.ReadAllBytes(originalBmdPath);
            BmdFile original = BmdReader.Read(originalBytes);

            bool rowOffsetIsByteOffset = DetectRowOffsetIsByteOffset(original);
            bool pixelOffsetIncludesSec2Header = DetectPixelOffsetIncludesSec2Header(original);
            uint pixelOffsetBase = pixelOffsetIncludesSec2Header ? 12u : 0u;

            string logicalFileKey = logicalBmdName.ToLowerInvariant();               // "ls_gui_window.bmd"
            string logicalBaseName = Path.GetFileNameWithoutExtension(logicalBmdName); // "ls_gui_window"

            List<byte> pixelStream = new(1024 * 1024);
            List<BmdRow> rows = new(1024 * 16);
            BmdFrame[] outFrames = new BmdFrame[original.Frames.Length];

            for (int frameIndex = 0; frameIndex < original.Frames.Length; frameIndex++)
            {
                BmdFrame srcFrame = original.Frames[frameIndex];

                int width = checked((int)srcFrame.Width);
                int height = checked((int)srcFrame.Height);

                int rowBaseIndex = rows.Count;

                // IMPORTANT: stable lookup
                string pngPath = Path.Combine(pngFramesDirectory, $"{logicalBaseName}_{frameIndex:D3}.png");
                if (!File.Exists(pngPath))
                {
                    throw new FileNotFoundException($"Missing PNG frame '{pngPath}'.");
                }

                // IMPORTANT: use logical key for overrides
                Rgba32[] paletteForFrame = SelectPaletteForFrame(logicalFileKey, frameIndex, pal, overrides);

                using Image<Rgba32> img = Image.Load<Rgba32>(pngPath);

                if (img.Width != width || img.Height != height)
                {
                    throw new InvalidDataException($"PNG size mismatch for frame {frameIndex:D3}. Expected {width}x{height}, got {img.Width}x{img.Height}.");
                }

                Dictionary<uint, byte> exactColorToIndex = new(512);

                for (int y = 0; y < height; y++)
                {
                    if (srcFrame.Type == 0x00000002u)
                    {
                        bool[] mask = new bool[width];

                        for (int x = 0; x < width; x++)
                        {
                            Rgba32 p = img[x, y];
                            mask[x] = p.A != 0;
                        }

                        uint packed2 = BmdRowEncoder.EncodeType2MaskRow(mask, width, pixelStream, out uint indent2);

                        if (packed2 == 0xFFFFFFFFu)
                        {
                            rows.Add(new BmdRow(0u, 0x3FFFFFu));
                        }
                        else
                        {
                            uint off2 = (packed2 & 0x3FFFFFu) + pixelOffsetBase;
                            rows.Add(new BmdRow(indent2, off2));
                        }

                        continue;
                    }

                    if (srcFrame.Type == 0x00000004u)
                    {
                        byte[] rowA = new byte[width];
                        byte[] rowB = new byte[width];

                        for (int x = 0; x < width; x++)
                        {
                            Rgba32 p = img[x, y];

                            if (p.A == 0)
                            {
                                rowA[x] = 0;
                                rowB[x] = 0;
                                continue;
                            }

                            rowA[x] = MapPixelToPaletteIndex(p, paletteForFrame, exactColorToIndex, allowNearestFallback: true);
                            rowB[x] = p.A;
                        }

                        uint packed4 = BmdRowEncoder.EncodeType4Row(rowA, rowB, width, 0, pixelStream, out uint indent4);

                        if (packed4 == 0xFFFFFFFFu)
                        {
                            rows.Add(new BmdRow(0u, 0x3FFFFFu));
                        }
                        else
                        {
                            uint off4 = (packed4 & 0x3FFFFFu) + pixelOffsetBase;
                            rows.Add(new BmdRow(indent4, off4));
                        }

                        continue;
                    }

                    byte[] row = new byte[width];

                    for (int x = 0; x < width; x++)
                    {
                        Rgba32 p = img[x, y];

                        if (p.A == 0)
                        {
                            row[x] = 0;
                            continue;
                        }

                        row[x] = MapPixelToPaletteIndex(p, paletteForFrame, exactColorToIndex, allowNearestFallback: true);
                    }

                    uint packed1 = BmdRowEncoder.EncodeType1Row(row, width, 0, pixelStream, out uint indent1);

                    if (packed1 == 0xFFFFFFFFu)
                    {
                        rows.Add(new BmdRow(0u, 0x3FFFFFu));
                    }
                    else
                    {
                        uint off1 = (packed1 & 0x3FFFFFu) + pixelOffsetBase;
                        rows.Add(new BmdRow(indent1, off1));
                    }
                }

                uint rowOffsetInSec3 = rowOffsetIsByteOffset ? checked((uint)(rowBaseIndex * 4)) : checked((uint)rowBaseIndex);
                outFrames[frameIndex] = new BmdFrame(srcFrame.Type, srcFrame.Meta1, srcFrame.Meta2, srcFrame.Width, srcFrame.Height, rowOffsetInSec3);
            }

            byte[] outPixels = [.. pixelStream];
            BmdRow[] outRows = [.. rows];

            byte[] outBmd = BmdWriter.Write(outFrames, outPixels, outRows);
            File.WriteAllBytes(outputBmdPath, outBmd);
        }

        private static bool DetectRowOffsetIsByteOffset(BmdFile original)
        {
            int rowCount = original.Rows.Length;

            for (int i = 0; i < original.Frames.Length; i++)
            {
                uint raw = original.Frames[i].RowOffsetInSec3;

                if (raw < (uint)rowCount)
                {
                    continue;
                }

                if ((raw % 4u) == 0u)
                {
                    uint idx = raw / 4u;
                    if (idx < (uint)rowCount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool DetectPixelOffsetIncludesSec2Header(BmdFile original)
        {
            int pixelLen = original.Pixels.Length;

            for (int i = 0; i < original.Rows.Length; i++)
            {
                uint raw = original.Rows[i].PixelStreamOffset;

                if (raw == 0x3FFFFFu)
                {
                    continue;
                }

                if (raw >= (uint)pixelLen && raw >= 12u)
                {
                    uint adj = raw - 12u;
                    if (adj < (uint)pixelLen)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static byte MapPixelToPaletteIndex(Rgba32 pixel, Rgba32[] palette, Dictionary<uint, byte> exactCache, bool allowNearestFallback = true)
        {
            uint key = PackRgb(pixel);

            if (exactCache.TryGetValue(key, out byte cached))
            {
                return cached;
            }

            // Exact match (palette indices 1..255; 0 is reserved for transparency in your pipeline)
            for (int i = 1; i < 256; i++)
            {
                Rgba32 c = palette[i];

                if (c.R == pixel.R && c.G == pixel.G && c.B == pixel.B)
                {
                    byte found = (byte)i;
                    exactCache[key] = found; // cache exact only
                    return found;
                }
            }

            if (!allowNearestFallback)
            {
                throw new InvalidDataException($"Pixel color ({pixel.R},{pixel.G},{pixel.B}) is not present in the palette.");
            }

            int bestIndex = 1;
            int bestDist = int.MaxValue;

            for (int i = 1; i < 256; i++)
            {
                Rgba32 c = palette[i];

                int dr = c.R - pixel.R;
                int dg = c.G - pixel.G;
                int db = c.B - pixel.B;

                int dist = (dr * dr) + (dg * dg) + (db * db);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;

                    if (dist == 0)
                    {
                        break;
                    }
                }
            }

            // Intentionally NOT cached: nearest results hide off-palette issues and make debugging harder.
            return (byte)bestIndex;
        }

        private static uint PackRgb(Rgba32 p)
        {
            return p.R | ((uint)p.G << 8) | ((uint)p.B << 16);
        }
    }

    internal readonly struct PaletteVariant
    {
        internal readonly string Suffix;
        internal readonly Rgba32[] Palette;

        internal PaletteVariant(string suffix, Rgba32[] palette)
        {
            Suffix = suffix;
            Palette = palette;
        }
    }

    internal readonly struct BmdSection
    {
        internal readonly uint Magic;
        internal readonly uint Unknown;
        internal readonly uint SectionLength;

        internal BmdSection(uint magic, uint unknown, uint sectionLength)
        {
            Magic = magic;
            Unknown = unknown;
            SectionLength = sectionLength;
        }
    }

    internal readonly struct BmdFile
    {
        internal readonly BmdFrame[] Frames;
        internal readonly byte[] Pixels;
        internal readonly BmdRow[] Rows;

        internal BmdFile(BmdFrame[] frames, byte[] pixels, BmdRow[] rows)
        {
            Frames = frames;
            Pixels = pixels;
            Rows = rows;
        }
    }

    internal readonly struct BmdFrame
    {
        internal readonly uint Type;
        internal readonly uint Meta1;
        internal readonly uint Meta2;
        internal readonly uint Width;
        internal readonly uint Height;
        internal readonly uint RowOffsetInSec3;

        internal BmdFrame(uint type, uint meta1, uint meta2, uint width, uint height, uint rowOffsetInSec3)
        {
            Type = type;
            Meta1 = meta1;
            Meta2 = meta2;
            Width = width;
            Height = height;
            RowOffsetInSec3 = rowOffsetInSec3;
        }
    }

    internal readonly struct BmdRow
    {
        internal readonly uint Indent;
        internal readonly uint PixelStreamOffset;

        internal BmdRow(uint indent, uint pixelStreamOffset)
        {
            Indent = indent;
            PixelStreamOffset = pixelStreamOffset;
        }
    }

    internal ref struct SpanReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _pos;

        internal SpanReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _pos = 0;
        }

        internal uint ReadU32LE()
        {
            ReadOnlySpan<byte> s = ReadSpanChecked(4);
            return BinaryPrimitives.ReadUInt32LittleEndian(s);
        }

        internal ulong ReadU64LE()
        {
            ReadOnlySpan<byte> s = ReadSpanChecked(8);
            return BinaryPrimitives.ReadUInt64LittleEndian(s);
        }

        internal byte[] ReadBytesChecked(int count)
        {
            ReadOnlySpan<byte> s = ReadSpanChecked(count);
            return s.ToArray();
        }

        private ReadOnlySpan<byte> ReadSpanChecked(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            if (_pos > _data.Length - count)
            {
                throw new EndOfStreamException("Unexpected end of BMD stream.");
            }

            ReadOnlySpan<byte> s = _data.Slice(_pos, count);
            _pos += count;
            return s;
        }
    }
}