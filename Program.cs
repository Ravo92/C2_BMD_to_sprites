using System.Text.Json;
using C2_BMD_to_sprites.Encoder;

namespace C2_BMD_to_sprites;

internal static class Program
{
    private const string Usage = "Commands:\n  decode <input.bmd> <frames-dir> [gui-palettes-dir] [logical.bmd]\n  encode <template.bmd> <frames-dir> <output.bmd> [gui-palettes-dir] [logical.bmd] [--optimize] [--id-order] [--force]\n  repack <input.bmd> <output.bmd> [--optimize] [--id-order] [--force]\n  rewrite <input.bmd> <output.bmd> [--force]\n  probe <input.bmd> [--optimize] [--id-order]\n  compare <original.bmd> <candidate.bmd>\nWithout a palette directory (or with -) decode/encode uses lossless *.indices.png.\n--optimize enables the Mac generator's pixel-stream and row-table reuse candidate.";

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h") { Console.WriteLine(Usage); return 0; }
            if (args.Skip(1).Any(a => a.StartsWith("--") && a is not ("--optimize" or "--force" or "--id-order"))) throw new ArgumentException("Unknown option.");
            bool optimize = args.Contains("--optimize"), force = args.Contains("--force"), idOrder = args.Contains("--id-order");
            string[] positional = args.Where(a => !a.StartsWith("--")).ToArray();
            string command = positional[0].ToLowerInvariant();
            int count = positional.Length;
            if ((optimize || idOrder) && command is not ("encode" or "repack" or "probe")) throw new ArgumentException("Packing options apply only to encode, repack and probe.");
            if (force && command is not ("encode" or "repack" or "rewrite")) throw new ArgumentException("--force applies only to commands that write a BMD.");
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
                File.WriteAllBytes(positional[3], output);
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
                File.WriteAllBytes(positional[2], output);
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
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or NotSupportedException or OverflowException or SixLabors.ImageSharp.UnknownImageFormatException or SixLabors.ImageSharp.InvalidImageContentException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void WriteReport(byte[] original, byte[] candidate) => Console.WriteLine(JsonSerializer.Serialize(BmdComparison.Describe(original, candidate), new JsonSerializerOptions { WriteIndented = true }));

    private static void EnsureOutput(string input, string output, bool force)
    {
        if (string.Equals(Path.GetFullPath(input), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)) throw new IOException("Output must differ from the input/template path.");
        if (!force && File.Exists(output)) throw new IOException("Output already exists; use --force explicitly to replace it.");
    }
}
