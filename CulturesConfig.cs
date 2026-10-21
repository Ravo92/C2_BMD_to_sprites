using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace C2_BMD_to_sprites;

// Read only the installed INI/CIF data needed for palette discovery. Compiled
// CStringArray lines use 01 for group names and 02 for command lines.
internal static class CulturesConfig
{
    internal static bool Exists(string root, string relativePath) => File.Exists(Path.Combine(root, relativePath)) || File.Exists(Path.ChangeExtension(Path.Combine(root, relativePath), ".cif"));

    internal static List<CulturesConfigGroup> Read(string root, string relativePath)
    {
        string path = Path.Combine(root, relativePath);
        string[] lines;
        if (File.Exists(path)) lines = File.ReadAllLines(path, Encoding.Latin1);
        else
        {
            path = Path.ChangeExtension(path, ".cif");
            if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException($"Compiled configuration is too large: {path}");
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length > 32 * 1024 * 1024 || bytes.Length < 28 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 1021) throw new InvalidDataException($"Unsupported compiled configuration: {path}");
            SpanReader reader = new(bytes.AsSpan(28));
            byte[] offsets = ReadMemory(ref reader);
            reader.ReadBytes(1);
            byte[] text = ReadMemory(ref reader);
            if (offsets.Length % 4 != 0) throw new InvalidDataException($"Invalid CIF line table: {path}");
            lines = new string[offsets.Length / 4];
            for (int i = 0; i < lines.Length; i++)
            {
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(offsets.AsSpan(i * 4));
                if (offset == uint.MaxValue) { lines[i] = ""; continue; }
                if (offset >= text.Length) throw new InvalidDataException($"Invalid CIF text offset: {path}");
                int end = Array.IndexOf(text, (byte)0, (int)offset);
                if (end < 0) throw new InvalidDataException($"Unterminated CIF line: {path}");
                lines[i] = Encoding.Latin1.GetString(text, (int)offset, end - (int)offset);
            }
        }
        List<CulturesConfigGroup> groups = [];
        CulturesConfigGroup? current = null;
        foreach (string source in lines)
        {
            string line = source.Trim();
            if (line.Length == 0) continue;
            if (line[0] == '\x01' || (line[0] == '[' && line.EndsWith(']')))
            {
                current = new CulturesConfigGroup(line[0] == '\x01' ? line[1..].Trim() : line[1..^1].Trim());
                groups.Add(current);
                continue;
            }
            if (line[0] == '\x02') line = line[1..];
            if (current is null || line.StartsWith(';') || line.StartsWith('#') || line.StartsWith("//")) continue;
            string[] tokens = Regex.Matches(line, "\"([^\"]*)\"|(\\S+)").Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).ToArray();
            if (tokens.Length != 0) current.Commands.Add(tokens);
        }
        return groups;
    }

    private static byte[] ReadMemory(ref SpanReader reader)
    {
        if (reader.ReadU32() != 1001) throw new InvalidDataException("Expected a CMemory object in compiled configuration.");
        reader.ReadU32();
        byte[] bytes = reader.ReadBytes(checked((int)reader.ReadU32()));
        byte mask = 0x47, increment = 0x7E;
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(unchecked((byte)(bytes[i] - 1)) ^ mask);
            mask = unchecked((byte)(mask + increment));
            increment = unchecked((byte)(increment + 0x21));
        }
        return bytes;
    }
}

internal sealed class CulturesConfigGroup(string name)
{
    internal string Name { get; } = name;
    internal List<string[]> Commands { get; } = [];
    internal IEnumerable<string[]> Find(string command) => Commands.Where(c => string.Equals(c[0], command, StringComparison.OrdinalIgnoreCase));
    internal string? Value(string command) => Find(command).FirstOrDefault()?.ElementAtOrDefault(1);
}
