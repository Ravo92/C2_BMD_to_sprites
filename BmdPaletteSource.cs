using System.Text.Json;
using System.Text.Json.Serialization;

namespace C2_BMD_to_sprites;

// Palette selection is external to BMD: a manager may contain many palette domains.
internal sealed class BmdPaletteSource
{
    private readonly string path;
    private readonly PaletteMap? map;
    private readonly Dictionary<string, Rgba32[]> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly GuiPaletteSet? gui;

    internal BmdPaletteSource(string path)
    {
        this.path = Path.GetFullPath(path);
        if (Directory.Exists(this.path)) gui = GuiPaletteSet.LoadFromDirectory(this.path);
        else if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            map = JsonSerializer.Deserialize<PaletteMap>(File.ReadAllText(this.path), BmdProject.JsonOptions) ?? throw new InvalidDataException("Empty palette map.");
            if (map.Ranges is null || map.Ranges.Any(r => r is null || r.LastBobId < r.FirstBobId || string.IsNullOrWhiteSpace(r.Palette))) throw new InvalidDataException("Invalid palette range.");
            PaletteRange[] ordered = map.Ranges.OrderBy(r => r.FirstBobId).ToArray();
            for (int i = 1; i < ordered.Length; i++) if (ordered[i].FirstBobId <= ordered[i - 1].LastBobId) throw new InvalidDataException("Palette ranges overlap; each bob ID must have one explicit assignment.");
        }
        else Load(this.path);
    }

    internal Rgba32[] Resolve(string logicalName, uint bobId)
    {
        if (gui is not null) return GuiPaletteRules.Resolve(logicalName, bobId, gui);
        if (map is null) return Load(path);
        string? palette = map.Ranges.FirstOrDefault(r => bobId >= r.FirstBobId && bobId <= r.LastBobId)?.Palette ?? map.Default;
        if (string.IsNullOrWhiteSpace(palette)) throw new InvalidDataException($"No palette assigned to bob ID {bobId} in {path}.");
        return Load(Path.GetFullPath(palette, Path.GetDirectoryName(path)!));
    }

    private Rgba32[] Load(string filename)
    {
        if (!cache.TryGetValue(filename, out Rgba32[]? palette)) cache.Add(filename, palette = PaletteDefaults.LoadPcxPalette(filename));
        return palette;
    }
}

internal sealed class PaletteMap
{
    public string? Default { get; set; }
    public PaletteRange[] Ranges { get; set; } = [];
}

internal sealed class PaletteRange
{
    [JsonRequired] public uint FirstBobId { get; set; }
    [JsonRequired] public uint LastBobId { get; set; }
    [JsonRequired] public string Palette { get; set; } = "";
}
