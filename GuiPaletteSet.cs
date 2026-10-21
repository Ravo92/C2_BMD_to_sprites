namespace C2_BMD_to_sprites;

internal sealed class GuiPaletteSet
{
    private readonly string directory;
    private readonly Dictionary<string, Rgba32[]> palettes = new(StringComparer.OrdinalIgnoreCase);

    private GuiPaletteSet(string directory) => this.directory = directory;
    internal static GuiPaletteSet LoadFromDirectory(string directory) => new(directory);
    internal Rgba32[] Frame => Load("frame.pcx");
    internal Rgba32[] BgNormal => Load("bg_normal.pcx");
    internal Rgba32[] Papyrus => Load("papyrus.pcx");
    internal Rgba32[] IconsLeft => Load("IconsLeft.pcx");
    internal Rgba32[] Context => Load("Context.pcx");
    internal Rgba32[] FontWhite => Load("font_white.pcx");

    private Rgba32[] Load(string name)
    {
        if (!palettes.TryGetValue(name, out Rgba32[]? palette)) palettes.Add(name, palette = PaletteDefaults.LoadPcxPalette(Path.Combine(directory, name)));
        return palette;
    }
}
