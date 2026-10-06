using SixLabors.ImageSharp.PixelFormats;

namespace C2_BMD_to_sprites
{
    internal sealed class GuiPaletteSet
    {
        internal readonly Rgba32[] Frame;
        internal readonly Rgba32[] BgNormal;
        internal readonly Rgba32[] Papyrus;
        internal readonly Rgba32[] IconsLeft;
        internal readonly Rgba32[] Context;
        internal readonly Rgba32[] FontWhite;

        private GuiPaletteSet(Rgba32[] frame, Rgba32[] bgNormal, Rgba32[] papyrus, Rgba32[] iconsLeft, Rgba32[] context, Rgba32[] fontWhite)
        {
            Frame = frame;
            BgNormal = bgNormal;
            Papyrus = papyrus;
            IconsLeft = iconsLeft;
            Context = context;
            FontWhite = fontWhite;
        }

        internal static GuiPaletteSet LoadFromDirectory(string palettesDirectory)
        {
            string frame = Path.Combine(palettesDirectory, "frame.pcx");
            string bgNormal = Path.Combine(palettesDirectory, "bg_normal.pcx");
            string papyrus = Path.Combine(palettesDirectory, "papyrus.pcx");
            string iconsLeft = Path.Combine(palettesDirectory, "IconsLeft.pcx");
            string context = Path.Combine(palettesDirectory, "Context.pcx");
            string fontWhite = Path.Combine(palettesDirectory, "font_white.pcx");

            Rgba32[] palFrame = PaletteDefaults.LoadPcxPalette(frame);
            Rgba32[] palBgNormal = PaletteDefaults.LoadPcxPalette(bgNormal);
            Rgba32[] palPapyrus = PaletteDefaults.LoadPcxPalette(papyrus);
            Rgba32[] palIconsLeft = PaletteDefaults.LoadPcxPalette(iconsLeft);
            Rgba32[] palContext = PaletteDefaults.LoadPcxPalette(context);
            Rgba32[] palFontWhite = PaletteDefaults.LoadPcxPalette(fontWhite);

            return new GuiPaletteSet(palFrame, palBgNormal, palPapyrus, palIconsLeft, palContext, palFontWhite);
        }
    }
}
