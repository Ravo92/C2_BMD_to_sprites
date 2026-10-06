using SixLabors.ImageSharp.PixelFormats;

namespace C2_BMD_to_sprites
{
    internal static class GuiPaletteRules
    {
        private static readonly PaletteOverrides Overrides = BuildDefaultOverrides();

        internal static Rgba32[] Resolve(string fileName, int bobId, GuiPaletteSet palettes)
        {
            Overrides.TryResolve(Path.GetFileName(fileName), bobId, out PaletteKind kind);
            return kind switch
            {
                PaletteKind.Frame => palettes.Frame,
                PaletteKind.Papyrus => palettes.Papyrus,
                PaletteKind.IconsLeft => palettes.IconsLeft,
                PaletteKind.Context => palettes.Context,
                PaletteKind.FontWhite => palettes.FontWhite,
                _ => palettes.BgNormal
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

    }
}
