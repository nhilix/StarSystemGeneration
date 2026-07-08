using System.Collections.Generic;
using System.Linq;
using System.Text;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Inspector;

/// <summary>The primary tuning instrument (spec §10): distribution summary over n hexes.</summary>
public static class StatsReport
{
    public static string Build(GalaxyContext galaxy, int startIndex, int n)
    {
        int present = 0, overlays = 0;
        int settledSystems = 0, sapientSystems = 0, namedSystems = 0;
        var arrangements = new Dictionary<StarArrangement, int>();
        var kinds = new Dictionary<BodyKind, int>();
        var settlements = new Dictionary<Settlement, int>();
        var biospheres = new Dictionary<Biosphere, int>();

        for (int i = 0; i < n; i++)
        {
            var coord = GalaxyEnumerator.SpiralAt(startIndex + i);
            var system = Generator.Generate(galaxy, coord).System;
            if (system == null) continue;
            present++;
            if (system.OverlayId != null) overlays++;
            if (system.GivenName != null) namedSystems++;
            Bump(arrangements, system.Arrangement);

            bool anySettled = false, anySapient = false;
            foreach (var star in system.Stars)
                foreach (var slot in star.Slots)
                {
                    if (slot.Body == null) continue;
                    foreach (var body in slot.Body.Satellites.Prepend(slot.Body))
                    {
                        Bump(kinds, body.Kind);
                        Bump(settlements, body.Settlement);
                        Bump(biospheres, body.Biosphere);
                        if (body.Settlement != Settlement.None) anySettled = true;
                        if (body.Biosphere == Biosphere.Sapient) anySapient = true;
                    }
                }
            if (anySettled) settledSystems++;
            if (anySapient) sapientSystems++;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"hexes: {n}   systems: {present} ({Pct(present, n)})   overlays: {overlays} ({Pct(overlays, present)})");
        sb.AppendLine($"system rollups (% of systems):");
        sb.AppendLine($"  settled          {settledSystems,6}  {Pct(settledSystems, present)}");
        sb.AppendLine($"  sapient life     {sapientSystems,6}  {Pct(sapientSystems, present)}");
        sb.AppendLine($"  named            {namedSystems,6}  {Pct(namedSystems, present)}");
        Section(sb, "arrangements", arrangements, present);
        Section(sb, "body kinds (incl. satellites)", kinds, Total(kinds));
        Section(sb, "biospheres (incl. satellites)", biospheres, Total(biospheres));
        Section(sb, "settlements (incl. satellites)", settlements, Total(settlements));
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
