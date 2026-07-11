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

    // ---- the poi registry panel ----

    public static string RenderPois(SimState state)
    {
        var sb = new StringBuilder();
        int live = 0;
        foreach (var poi in state.Pois)
            if (!poi.Depleted) live++;
        sb.AppendLine(Invariant($"points of interest: {live} anchored ")
            + Invariant($"(of {state.Pois.Count} ever compiled)"));
        foreach (var poi in state.Pois)                   // id order (P6)
        {
            sb.Append(Invariant($"  #{poi.Id} {TypeName(poi)} at ")
                + Invariant($"({poi.Hex.Q},{poi.Hex.R}) · ")
                + Invariant($"magnitude {poi.Magnitude:0.#} · since ")
                + SimTraceView.YearLabel(poi.FoundedYear));
            if (poi.Type == PoiType.Battlefield)
                sb.Append(Invariant($" · salvage {poi.SalvageRemaining:0.#} hulls"));
            if (poi.Dormant) sb.Append(" · DORMANT");
            if (poi.Depleted) sb.Append(" [faded]");
            sb.AppendLine();
            if (poi.ParticipantActorIds.Count > 0)
            {
                sb.Append("    history of:");
                foreach (var id in poi.ParticipantActorIds)
                    sb.Append(Invariant($" {state.Actors[id].Name} (#{id})"));
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    public static string RenderPoi(SimState state, int poiId)
    {
        if (poiId < 0 || poiId >= state.Pois.Count) return "no such POI";
        var poi = state.Pois[poiId];
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"#{poi.Id} {TypeName(poi)} at ")
            + Invariant($"({poi.Hex.Q},{poi.Hex.R}) — since ")
            + SimTraceView.YearLabel(poi.FoundedYear)
            + (poi.Depleted ? " [faded]" : ""));
        sb.AppendLine(Invariant($"  magnitude {poi.Magnitude:0.#}")
            + (poi.Type == PoiType.Battlefield
                ? Invariant($" · salvage remaining {poi.SalvageRemaining:0.#} ")
                  + Invariant($"hulls ({poi.HullsSalvaged} drawn)")
                : "")
            + (poi.Dormant ? " · DORMANT — something is still awake" : ""));
        foreach (var id in poi.SourceEventIds)
            if (id >= 0 && id < state.Log.Events.Count)
                sb.AppendLine("  " + SimTraceView.Describe(state.Log.Events[(int)id]));
        return sb.ToString();
    }

    private static string TypeName(PoiRecord poi) => poi.Type switch
    {
        PoiType.Battlefield => "battlefield",
        PoiType.Ruins => "ruins",
        PoiType.RuinedCapital => "ruined capital",
        PoiType.Memorial => poi.Detail == 1
            ? "memorial (suppression)" : "memorial (famine)",
        PoiType.PrecursorSite => "precursor site",
        _ => "poi",
    };
}
