using System.Collections.Generic;
using System.Text;
using StarGen.Core.Epoch;
using static System.FormattableString;

namespace StarGen.Inspector;

/// <summary>Slice I's REPL surfaces over the narrative layer: detected eras,
/// the era-annotated chronicle, and (task 10) belief/news/stance panels.
/// Deterministic text, invariant culture.</summary>
public static class NarrativeView
{
    public static string RenderEras(SimState state)
    {
        var eras = EraDetector.Detect(state);
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"eras: {eras.Count} detected over ")
            + Invariant($"{state.EpochIndex} epochs"));
        foreach (var era in eras)
            sb.AppendLine("  " + EraHeader(era)
                + Invariant($" — epochs {era.StartEpoch}–{era.EndEpoch} (")
                + era.Kind.ToString().ToLowerInvariant() + ")");
        return sb.ToString();
    }

    /// <summary>The chronicle with era headers stitched in where the
    /// generational stream crosses an era boundary.</summary>
    public static string RenderChronicle(SimState state,
                                         IEnumerable<WorldEvent> events)
    {
        var eras = EraDetector.Detect(state);
        int years = state.Config.Sim.YearsPerEpoch;
        var sb = new StringBuilder();
        Era? current = null;
        int shown = 0;
        foreach (var e in events)
        {
            if (e.Stratum == ClockStratum.Generational && e.WorldYear >= 0
                && years > 0)
            {
                var era = EraDetector.EraOf(eras, (int)(e.WorldYear / years));
                if (era != null && era != current)
                {
                    current = era;
                    sb.AppendLine("  ── " + EraHeader(era) + " ──");
                }
            }
            sb.AppendLine("  " + SimTraceView.Describe(e));
            shown++;
        }
        if (shown == 0) sb.AppendLine("  (no events)");
        return sb.ToString();
    }

    private static string EraHeader(Era era) =>
        era.Name + " · " + SimTraceView.YearLabel(era.StartYear) + "–"
        + SimTraceView.YearLabel(era.EndYear);
}
