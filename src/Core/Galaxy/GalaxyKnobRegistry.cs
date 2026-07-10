using System;
using System.Collections.Generic;

namespace StarGen.Core.Galaxy;

/// <summary>One galaxy-side calibration dial: dotted name, one-line doc, and
/// live accessors on a GalaxyConfig.</summary>
public sealed record GalaxyKnobDef(
    string Name, string Doc,
    Func<GalaxyConfig, double> Get, Action<GalaxyConfig, double> Set);

/// <summary>The galaxy-side twin of Epoch.KnobRegistry: the single index of
/// every genesis calibration knob (Cosmic, later Evolution families) —
/// name-sorted, fully documented, driving the artifact's GKNOB lines, the
/// REPL `knobs` command, and docs/TUNING.md. A dial must never exist outside
/// this table; GalaxyKnobRegistryTests enforces order, uniqueness, and
/// accessor round-trips. The original shape knobs predate the discipline and
/// stay on the GCONFIG line as identity-adjacent structure.</summary>
public static class GalaxyKnobRegistry
{
    private static GalaxyKnobDef K(string name, string doc,
                                   Func<GalaxyConfig, double> get,
                                   Action<GalaxyConfig, double> set) =>
        new(name, doc, get, set);

    private static readonly GalaxyKnobDef[] Table =
    {
        // ---- Cosmic (deep-time structure sim) ----
        K("Cosmic.AgnActivity",
          "AGN accretion-epoch frequency and sterilization-wave reach",
          c => c.Cosmic.AgnActivity, (c, v) => c.Cosmic.AgnActivity = v),
        K("Cosmic.EnrichmentRate",
          "metals yielded per dying young cohort (enrichment speed)",
          c => c.Cosmic.EnrichmentRate, (c, v) => c.Cosmic.EnrichmentRate = v),
        K("Cosmic.GlobularCount",
          "globular clusters placed in the earliest steps",
          c => c.Cosmic.GlobularCount, (c, v) => c.Cosmic.GlobularCount = v),
        K("Cosmic.MergerCount",
          "expected infalling dwarf mergers per formation history",
          c => c.Cosmic.MergerCount, (c, v) => c.Cosmic.MergerCount = v),
        K("Cosmic.MergerScale",
          "mass scale of merger gas/star injections",
          c => c.Cosmic.MergerScale, (c, v) => c.Cosmic.MergerScale = v),
        K("Cosmic.StarFormationEfficiency",
          "star-formation rate multiplier (higher burns gas earlier)",
          c => c.Cosmic.StarFormationEfficiency,
          (c, v) => c.Cosmic.StarFormationEfficiency = v),
    };

    private static readonly Dictionary<string, GalaxyKnobDef> ByName = Build();

    private static Dictionary<string, GalaxyKnobDef> Build()
    {
        var map = new Dictionary<string, GalaxyKnobDef>(Table.Length);
        foreach (var k in Table) map.Add(k.Name, k);
        return map;
    }

    /// <summary>Every knob, name-sorted (the table is maintained sorted;
    /// tests enforce it).</summary>
    public static IReadOnlyList<GalaxyKnobDef> All => Table;

    public static GalaxyKnobDef? Find(string name) =>
        ByName.TryGetValue(name, out var knob) ? knob : null;
}
