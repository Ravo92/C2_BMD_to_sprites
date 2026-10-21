using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace C2_BMD_to_sprites;

internal static class CulturesPaletteDiscovery
{
    internal static string? Find(string inputPath, string? generatedMapPath = null)
    {
        string? configured = Environment.GetEnvironmentVariable("CULTURES_GAME_DIR");
        if (!string.IsNullOrWhiteSpace(configured)) return FindPalette(Path.GetFullPath(configured), inputPath, generatedMapPath);
        // Prefer a containing installation. For copied BMDs, verify the source
        // bytes before selecting one of several installed Cultures editions.
        foreach (string root in Ancestors(Path.GetDirectoryName(inputPath)!))
        {
            if (!IsGameRoot(root)) continue;
            return FindPalette(root, inputPath, generatedMapPath);
        }
        byte[]? inputHash = null;
        foreach (string root in InstalledGames())
        {
            string source = Path.Combine(root, "data", "engine2d", "bin", "bobs", Path.GetFileName(inputPath));
            if (!File.Exists(source) || new FileInfo(source).Length != new FileInfo(inputPath).Length) continue;
            using FileStream original = File.OpenRead(source);
            if (inputHash is null) { using FileStream input = File.OpenRead(inputPath); inputHash = SHA256.HashData(input); }
            if (!inputHash.AsSpan().SequenceEqual(SHA256.HashData(original))) continue;
            string? palette = FindPalette(root, inputPath, generatedMapPath);
            if (palette is not null) return palette;
        }
        return null;
    }

    private static string? FindPalette(string root, string inputPath, string? generatedMapPath)
    {
        if (!IsGameRoot(root)) return null;
        string name = Path.GetFileName(inputPath);
        if (name.Equals("ls_gui_window.bmd", StringComparison.OrdinalIgnoreCase))
        {
            string directory = Path.Combine(root, "data", "gui", "palettes");
            if (Directory.Exists(directory)) return directory;
        }
        string? houseMap = CulturesHousePalettes.Create(root, inputPath, generatedMapPath);
        if (houseMap is not null) return houseMap;
        if (!CulturesConfig.Exists(root, "data/engine2d/inis/humans/jobgraphics.ini")) return null;
        List<CulturesConfigGroup> jobs = CulturesConfig.Read(root, "data/engine2d/inis/humans/jobgraphics.ini");
        HashSet<string> palettes = new(StringComparer.OrdinalIgnoreCase);
        foreach (CulturesConfigGroup group in jobs.Where(g => g.Name.Equals("jobbasegraphics", StringComparison.OrdinalIgnoreCase)))
        {
            foreach ((string manager, string paletteToken) in new[] { ("gfxbobmanagerbody", "gfxpalettebasebody"), ("gfxbobmanagerhead", "gfxpalettebasehead") })
            {
                // First manager filename is the body/head; a following filename
                // can be a shadow resource and must not inherit its RGB palette.
                if (!group.Find(manager).Any(c => c.Length > 2 && Path.GetFileName(c[2].Replace('\\', '/')).Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                foreach (string[] command in group.Find(paletteToken)) if (command.Length > 1) palettes.Add(command[1]);
            }
        }
        // Different palette domains need per-frame mappings; do not guess one.
        if (palettes.Count != 1) return null;
        return ResolvePaletteFile(root, CulturesConfig.Read(root, "data/engine2d/inis/palettes/palettes.ini"), palettes.Single());
    }

    internal static string? ResolvePaletteFile(string root, List<CulturesConfigGroup> definitions, string palette)
    {
        string? file = definitions.FirstOrDefault(g => g.Name.Equals("GfxPalette256", StringComparison.OrdinalIgnoreCase) && string.Equals(g.Value("editname"), palette, StringComparison.OrdinalIgnoreCase))?.Value("gfxfile");
        if (file is null || Path.IsPathRooted(file)) return null;
        string fullPath = Path.GetFullPath(Path.Combine(root, file.Replace('\\', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)) return null;
        return fullPath;
    }

    private static bool IsGameRoot(string root) => CulturesConfig.Exists(root, "data/engine2d/inis/humans/jobgraphics.ini") || CulturesConfig.Exists(root, "data/engine2d/inis/houses/houses.ini");

    private static IEnumerable<string> Ancestors(string directory)
    {
        for (DirectoryInfo? current = new(directory); current is not null; current = current.Parent) yield return current.FullName;
    }

    private static IEnumerable<string> InstalledGames()
    {
        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);
        foreach (string root in Ancestors(AppContext.BaseDirectory).Concat(Ancestors(Environment.CurrentDirectory))) if (IsGameRoot(root)) roots.Add(root);
        HashSet<string> libraries = new(StringComparer.OrdinalIgnoreCase);
        foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            foreach (string relative in new[] { "Steam", "SteamLibrary", "Program Files (x86)/Steam", "Program Files/Steam" })
            {
                string library = Path.Combine(drive.RootDirectory.FullName, relative);
                if (Directory.Exists(Path.Combine(library, "steamapps"))) libraries.Add(library);
            }
        }
        foreach (string library in libraries.ToArray())
        {
            string manifest = Path.Combine(library, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(manifest)) continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(manifest), "\"path\"\\s*\"([^\"]+)\"")) libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
        }
        foreach (string library in libraries)
        {
            string common = Path.Combine(library, "steamapps", "common");
            if (!Directory.Exists(common)) continue;
            foreach (string game in Directory.EnumerateDirectories(common, "Cultures*")) if (IsGameRoot(game)) roots.Add(game);
        }
        return roots;
    }
}
