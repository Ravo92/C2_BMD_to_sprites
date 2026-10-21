using System.Text.Json;

namespace C2_BMD_to_sprites;

internal static class CulturesHousePalettes
{
    internal static string? Create(string root, string inputPath, string? mapPath)
    {
        const string houseConfig = "data/engine2d/inis/houses/houses.ini";
        if (!CulturesConfig.Exists(root, houseConfig)) return null;
        string name = Path.GetFileName(inputPath);
        CulturesConfigGroup[] houses = CulturesConfig.Read(root, houseConfig).Where(g => g.Name.Equals("GfxHouse", StringComparison.OrdinalIgnoreCase) && g.Find("GfxBobLibs").Any(c => c.Length > 1 && Path.GetFileName(c[1].Replace('\\', '/')).Equals(name, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (houses.Length == 0) return null;
        Dictionary<uint, List<string>> assignments = [];
        string? fallback = null;
        foreach (CulturesConfigGroup house in houses)
        {
            // Multiple quoted palettes are selectable variants, not separate
            // RGB channels. Select the first common listed variant per frame.
            List<string> palettes = house.Find("GfxPalette").SelectMany(c => c.Skip(1)).Take(5).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (palettes.Count == 0) throw new InvalidDataException($"House {house.Value("EditName")} has no palette assignment.");
            fallback ??= palettes[0];
            foreach (string[] command in house.Commands)
            {
                IEnumerable<string> ids = command[0].ToLowerInvariant() switch
                {
                    "gfxbobid" => command.Skip(2).Take(1),
                    "gfxdoorbobid" => command.Skip(3).Take(1),
                    // Both construction-layer bob fields reference artwork;
                    // the optional ground/transition image also needs color.
                    "gfxbobconstructionlayer" => command.Skip(3).Take(2),
                    "gfxoverlay" => command.Skip(7),
                    _ => []
                };
                foreach (string id in ids)
                {
                    if (!uint.TryParse(id, out uint bobId)) continue; // Includes the -1 sentinel.
                    if (!assignments.TryGetValue(bobId, out List<string>? existing)) assignments.Add(bobId, [.. palettes]);
                    else
                    {
                        existing.RemoveAll(p => !palettes.Contains(p, StringComparer.OrdinalIgnoreCase));
                        if (existing.Count == 0) throw new InvalidDataException($"Conflicting house palettes for bob ID {bobId}; supply an explicit palette map.");
                    }
                }
            }
        }
        List<CulturesConfigGroup> definitions = CulturesConfig.Read(root, "data/engine2d/inis/palettes/palettes.ini");
        Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
        string PaletteFile(string palette)
        {
            if (!files.TryGetValue(palette, out string? path))
            {
                path = CulturesPaletteDiscovery.ResolvePaletteFile(root, definitions, palette) ?? throw new InvalidDataException($"Missing game palette {palette}.");
                PaletteDefaults.LoadPcxPalette(path);
                files.Add(palette, path);
            }
            return path;
        }
        BmdFile file = BmdReader.Read(File.ReadAllBytes(inputPath));
        PaletteMap map = new() { Default = PaletteFile(fallback!) };
        List<PaletteRange> ranges = [];
        List<uint> unreferenced = [];
        for (int i = 0; i < file.Frames.Length; i++)
        {
            if (file.Frames[i].Type is not (1 or 4)) continue;
            uint id = unchecked(file.Header.FirstBobId + (uint)i);
            if (!assignments.TryGetValue(id, out List<string>? palette)) { unreferenced.Add(id); continue; }
            string path = PaletteFile(palette[0]);
            if (ranges.Count != 0 && ranges[^1].LastBobId != uint.MaxValue && ranges[^1].LastBobId + 1 == id && ranges[^1].Palette == path) ranges[^1].LastBobId = id;
            else ranges.Add(new PaletteRange { FirstBobId = id, LastBobId = id, Palette = path });
        }
        if (mapPath is null) return files.Count == 1 ? map.Default : null;
        map.Ranges = ranges.ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(mapPath))!);
        using (FileStream stream = new(mapPath, FileMode.CreateNew, FileAccess.Write)) JsonSerializer.Serialize(stream, map, BmdProject.JsonOptions);
        Console.WriteLine("House previews select the first common configured palette variant per frame.");
        if (unreferenced.Count != 0) Console.WriteLine($"Frames absent from house configuration use {fallback} as a preview fallback: {string.Join(", ", unreferenced)}.");
        return mapPath;
    }
}
