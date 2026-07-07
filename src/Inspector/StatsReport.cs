using System.Collections.Generic;
using System.Text;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Inspector;

/// <summary>The primary tuning instrument (spec §10): distribution summary over n hexes.</summary>
public static class StatsReport
{
    public static string Build(ulong seed, int startX, int startY, int n, int sectorWidth)
    {
        int present = 0, overlays = 0;
        var arrangements = new Dictionary<StarArrangement, int>();
        var kinds = new Dictionary<BodyKind, int>();
        var settlements = new Dictionary<Settlement, int>();
        var biospheres = new Dictionary<Biosphere, int>();

        int linear = startY * sectorWidth + startX;
        for (int i = 0; i < n; i++, linear++)
        {
            var coord = new HexCoordinate(linear % sectorWidth, linear / sectorWidth);
            var system = Generator.Generate(seed, coord).System;
            if (system == null) continue;
            present++;
            if (system.OverlayId != null) overlays++;
            Bump(arrangements, system.Arrangement);
            foreach (var star in system.Stars)
                foreach (var slot in star.Slots)
                {
                    if (slot.Body == null) continue;
                    Bump(kinds, slot.Body.Kind);
                    Bump(settlements, slot.Body.Settlement);
                    Bump(biospheres, slot.Body.Biosphere);
                }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"hexes: {n}   systems: {present} ({Pct(present, n)})   overlays: {overlays} ({Pct(overlays, present)})");
        Section(sb, "arrangements", arrangements, present);
        Section(sb, "body kinds", kinds, Total(kinds));
        Section(sb, "biospheres", biospheres, Total(biospheres));
        Section(sb, "settlements", settlements, Total(settlements));
        return sb.ToString();
    }

    private static void Bump<T>(Dictionary<T, int> d, T key) where T : notnull =>
        d[key] = d.TryGetValue(key, out var v) ? v + 1 : 1;

    private static int Total<T>(Dictionary<T, int> d) where T : notnull
    {
        int t = 0;
        foreach (var v in d.Values) t += v;
        return t;
    }

    private static string Pct(int part, int whole) =>
        whole == 0 ? "0%" : $"{100.0 * part / whole:F1}%";

    private static void Section<T>(StringBuilder sb, string title, Dictionary<T, int> d, int total)
        where T : notnull
    {
        sb.AppendLine($"{title}:");
        foreach (var kv in d)
            sb.AppendLine($"  {kv.Key,-16} {kv.Value,6}  {Pct(kv.Value, total)}");
    }
}
