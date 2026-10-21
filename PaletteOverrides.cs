namespace C2_BMD_to_sprites;

internal sealed class PaletteOverrides
{
    private readonly Dictionary<string, List<OverrideRule>> rulesByFile = new(StringComparer.OrdinalIgnoreCase);

    internal void AddRange(string fileName, int startInclusive, int endInclusive, PaletteKind palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        if (startInclusive < 0 || endInclusive < startInclusive) throw new ArgumentOutOfRangeException(nameof(startInclusive));
        string key = Path.GetFileName(fileName);
        if (!rulesByFile.TryGetValue(key, out List<OverrideRule>? rules)) { rules = []; rulesByFile.Add(key, rules); }
        rules.Add(new OverrideRule(startInclusive, endInclusive, palette));
    }

    internal void AddSingle(string fileName, int id, PaletteKind palette) => AddRange(fileName, id, id, palette);

    internal bool TryResolve(string fileName, uint bobId, out PaletteKind palette)
    {
        palette = PaletteKind.BgNormal;
        if (!rulesByFile.TryGetValue(Path.GetFileName(fileName), out List<OverrideRule>? rules)) return false;
        // Later rules intentionally override earlier ranges.
        for (int i = rules.Count - 1; i >= 0; i--)
        {
            OverrideRule rule = rules[i];
            if (bobId < rule.Start || bobId > rule.End) continue;
            palette = rule.Palette;
            return true;
        }
        return false;
    }

    private readonly record struct OverrideRule(int Start, int End, PaletteKind Palette);
}

internal enum PaletteKind { Frame, BgNormal, Papyrus, IconsLeft, Context, FontWhite }
