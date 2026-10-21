using System.Text.Json;
using C2_BMD_to_sprites.Encoder;

namespace C2_BMD_to_sprites;

internal static class Program
{
    private const string DragDropUsage = "Drag a .bmd file onto this EXE, or run:\n  C2_BMD_to_sprites.exe <input.bmd>\nCreates <name>_sprites in the current working directory (then _2, _3, ...).\nColored previews use an adjacent <name>.palettes.json / <name>.pcx,\nthen palette assignments from an installed matching Cultures game.\nCULTURES_GAME_DIR can select a game installation explicitly.\nColored sprites are *.png; their lossless index data is under indices/.\nAn adjacent <name>_s.bmd is exported separately under shadows/.\n\n";
    private const string Usage = "Commands:\n  decode <input.bmd> <frames-dir> [palette-source] [logical.bmd]\n  encode <template.bmd> <frames-dir> <output.bmd> [palette-source] [logical.bmd] [--optimize] [--id-order] [--force]\n  build <frames.json> <output.bmd> [palette-source] [--preview] [--optimize] [--id-order] [--force]\n  repack <input.bmd> <output.bmd> [--optimize] [--id-order] [--force]\n  rewrite <input.bmd> <output.bmd> [--force]\n  probe <input.bmd> [--optimize] [--id-order]\n  compare <original.bmd> <candidate.bmd>\nPalette source: single PCX, JSON map or legacy GUI palette directory.\nWithout a palette source (or with -) decode/encode uses lossless *.indices.png.\nDecode also exports an editable *.frames.json manifest. Build uses raw images by default.\n--preview imports colored images using the manifest palette source or an explicit override.\n--optimize enables the Mac generator's pixel-stream and row-table reuse candidate.";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h") { Console.WriteLine(DragDropUsage + Usage); return 0; }
            if (args.Length == 1 && string.Equals(Path.GetExtension(args[0]), ".bmd", StringComparison.OrdinalIgnoreCase))
            {
                ExportDroppedBmd(args[0]);
                return 0;
            }
            if (args.Skip(1).Any(a => a.StartsWith("--") && a is not ("--optimize" or "--force" or "--id-order" or "--preview"))) throw new ArgumentException("Unknown option.");
            bool optimize = args.Contains("--optimize"), force = args.Contains("--force"), idOrder = args.Contains("--id-order"), preview = args.Contains("--preview");
            string[] positional = args.Where(a => !a.StartsWith("--")).ToArray();
            if (positional.Length == 0) throw new ArgumentException("Missing command.");
            string command = positional[0].ToLowerInvariant();
            int count = positional.Length;
            if ((optimize || idOrder) && command is not ("encode" or "build" or "repack" or "probe")) throw new ArgumentException("Packing options apply only to encode, build, repack and probe.");
            if (force && command is not ("encode" or "build" or "repack" or "rewrite")) throw new ArgumentException("--force applies only to commands that write a BMD.");
            if (preview && command != "build") throw new ArgumentException("--preview applies only to build.");
            if (command == "build" && count is >= 3 and <= 4)
            {
                EnsureOutput(positional[1], positional[2], force);
                byte[] output = BmdProject.Build(positional[1], count == 4 && positional[3] != "-" ? positional[3] : null, preview, optimize, idOrder);
                WriteOutput(positional[2], output, force);
                Console.WriteLine($"Built {BmdReader.Read(output).Frames.Length} frame records.");
                return 0;
            }
            if (command == "decode" && count is >= 3 and <= 5)
            {
                BmdConverter.Export(positional[1], positional[2], count >= 4 && positional[3] != "-" ? positional[3] : null, count == 5 ? positional[4] : Path.GetFileName(positional[1]));
                Console.WriteLine("Frames exported.");
                return 0;
            }
            if (command == "encode" && count is >= 4 and <= 6)
            {
                EnsureOutput(positional[1], positional[3], force);
                byte[] output = BmdConverter.Import(positional[1], positional[2], count >= 5 && positional[4] != "-" ? positional[4] : null, count == 6 ? positional[5] : Path.GetFileName(positional[1]), optimize, idOrder);
                WriteReport(File.ReadAllBytes(positional[1]), output);
                WriteOutput(positional[3], output, force);
                return 0;
            }
            if ((command == "rewrite" || command == "repack") && count == 3)
            {
                EnsureOutput(positional[1], positional[2], force);
                byte[] input = File.ReadAllBytes(positional[1]);
                BmdFile file = BmdReader.Read(input);
                byte[] output = BmdWriter.Write(command == "rewrite" ? file : BmdEncoder.Repack(file, optimize, idOrder: idOrder));
                // Validate and compare before creating a result file.
                WriteReport(input, output);
                WriteOutput(positional[2], output, force);
                return 0;
            }
            if (command == "probe" && count == 2)
            {
                byte[] input = File.ReadAllBytes(positional[1]);
                BmdFile file = BmdReader.Read(input);
                WriteReport(input, BmdWriter.Write(BmdEncoder.Repack(file, optimize, idOrder: idOrder)));
                return 0;
            }
            if (command == "compare" && count == 3)
            {
                byte[] original = File.ReadAllBytes(positional[1]), candidate = File.ReadAllBytes(positional[2]);
                WriteReport(original, candidate);
                return original.AsSpan().SequenceEqual(candidate) ? 0 : 2;
            }
            throw new ArgumentException("Invalid command or argument count.");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or NotSupportedException or OverflowException or JsonException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void WriteReport(byte[] original, byte[] candidate) => Console.WriteLine(JsonSerializer.Serialize(BmdComparison.Describe(original, candidate), new JsonSerializerOptions { WriteIndented = true }));

    private static void ExportDroppedBmd(string input)
    {
        string inputPath = Path.GetFullPath(input);
        string name = Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrWhiteSpace(name)) name = "bmd";
        string outputBase = Path.Combine(Environment.CurrentDirectory, name + "_sprites");
        string output = outputBase;
        for (int suffix = 2; Directory.Exists(output) || File.Exists(output); suffix++) output = outputBase + "_" + suffix;
        // Explicit sidecars take precedence over installed game configuration.
        string paletteBase = Path.Combine(Path.GetDirectoryName(inputPath)!, Path.GetFileNameWithoutExtension(inputPath));
        string? palette = File.Exists(paletteBase + ".palettes.json") ? paletteBase + ".palettes.json" : File.Exists(paletteBase + ".pcx") ? paletteBase + ".pcx" : null;
        if (palette is null)
        {
            try { palette = CulturesPaletteDiscovery.Find(inputPath, Path.Combine(output, "preview.palettes.json")); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException)
            {
                Console.Error.WriteLine($"Automatic palette discovery failed: {ex.Message}");
            }
        }
        BmdConverter.Export(inputPath, output, palette, Path.GetFileName(inputPath), palette is not null || name.EndsWith("_s", StringComparison.OrdinalIgnoreCase) ? "indices" : null);
        Console.WriteLine($"Frames exported to: {output}");
        Console.WriteLine(palette is null ? "No palette found; exported lossless index PNGs. Add a PCX/palette-map sidecar for colored previews." : $"Colored previews use: {palette}");
        try { BmdShadowExporter.ExportAdjacent(inputPath, output); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or OverflowException)
        {
            Console.Error.WriteLine($"Artwork exported, but shadow export failed: {ex.Message}");
        }
    }

    private static void WriteOutput(string output, byte[] bytes, bool force)
    {
        string target = Path.GetFullPath(output);
        string temporary = Path.Combine(Path.GetDirectoryName(target)!, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            // Rename only a fully written file. A failed encode cannot truncate an existing output.
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write)) stream.Write(bytes);
            File.Move(temporary, target, force);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void EnsureOutput(string input, string output, bool force)
    {
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)) throw new IOException("Output must differ from the input/template path.");
        if (!force && File.Exists(output)) throw new IOException("Output already exists; use --force explicitly to replace it.");
    }
}
