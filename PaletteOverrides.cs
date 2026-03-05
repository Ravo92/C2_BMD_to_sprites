namespace C2_BMD_to_sprites
{
    internal sealed class PaletteOverrides
    {
        private readonly Dictionary<string, List<OverrideRule>> _rulesByFile;

        internal PaletteOverrides()
        {
            _rulesByFile = new Dictionary<string, List<OverrideRule>>(StringComparer.OrdinalIgnoreCase);
        }

        internal void AddRange(string fileName, int startInclusive, int endInclusive, PaletteKind palette)
        {
            if (!_rulesByFile.TryGetValue(fileName, out List<OverrideRule> rules))
            {
                rules = [];
                _rulesByFile[fileName] = rules;
            }

            rules.Add(OverrideRule.Range(startInclusive, endInclusive, palette));
        }

        internal void AddSingle(string fileName, int id, PaletteKind palette)
        {
            if (!_rulesByFile.TryGetValue(fileName, out List<OverrideRule> rules))
            {
                rules = [];
                _rulesByFile[fileName] = rules;
            }

            rules.Add(OverrideRule.Single(id, palette));
        }

        internal bool TryResolve(string fileName, int frameIndex, out PaletteKind palette)
        {
            palette = PaletteKind.BgNormal;

            if (!_rulesByFile.TryGetValue(fileName, out List<OverrideRule> rules))
            {
                return false;
            }

            // Last rule wins (easier to patch later)
            for (int i = rules.Count - 1; i >= 0; i--)
            {
                OverrideRule r = rules[i];
                if (r.Matches(frameIndex))
                {
                    palette = r.Palette;
                    return true;
                }
            }

            return false;
        }

        private readonly struct OverrideRule
        {
            internal readonly int Start;
            internal readonly int End;
            internal readonly PaletteKind Palette;

            private OverrideRule(int start, int end, PaletteKind palette)
            {
                Start = start;
                End = end;
                Palette = palette;
            }

            internal static OverrideRule Single(int id, PaletteKind palette)
            {
                return new OverrideRule(id, id, palette);
            }

            internal static OverrideRule Range(int startInclusive, int endInclusive, PaletteKind palette)
            {
                return new OverrideRule(startInclusive, endInclusive, palette);
            }

            internal bool Matches(int id)
            {
                return id >= Start && id <= End;
            }
        }
    }

    internal enum PaletteKind
    {
        Frame,
        BgNormal,
        Papyrus,
        IconsLeft,
        Context,
        FontWhite
    }
}