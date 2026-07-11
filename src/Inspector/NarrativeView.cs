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
        // era buckets are generations, not integration steps (slice J)
        int years = state.Config.Sim.GenerationYears;
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

    // ---- the open-threads panel (slice J: the world in motion) ----

    /// <summary>The handoff's open-threads surface: what is loaded, half
    /// won, leveraged, burning, or unanswered right now.</summary>
    public static string RenderThreads(SimState state)
    {
        var threads = HandoffView.OpenThreads(state);
        var sb = new StringBuilder();
        sb.AppendLine(Invariant(
            $"the world in motion — {threads.Count} open ")
            + (threads.Count == 1 ? "thread" : "threads")
            + Invariant($" at y{state.WorldYear}"));
        foreach (var t in threads)
            sb.AppendLine(Invariant($"  [{t.Kind,-11}] ") + t.Text);
        if (threads.Count == 0)
            sb.AppendLine("  (a tidied museum — nothing is in motion;"
                          + " this should worry you more than a war)");
        return sb.ToString();
    }

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

    // ---- the belief panel: the fog of war made visible ----

    /// <summary>What one polity believes about the others (or one other),
    /// each line beside the truth — the visible gap between what happened
    /// and who knows it yet (perception-and-news.md P1).</summary>
    public static string RenderBeliefs(SimState state, int observerId,
                                       int subjectId = -1)
    {
        if (observerId < 0 || observerId >= state.Actors.Count)
            return "no such actor";
        var observer = state.Actors[observerId];
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"what {observer.Name} (#{observerId}) believes:"));
        int shown = 0;
        foreach (var b in observer.Beliefs.Polities.Values) // subject order
        {
            if (subjectId >= 0 && b.SubjectId != subjectId) continue;
            shown++;
            long stale = state.WorldYear - b.HeardYear;
            double truth = FleetOps.WarStrength(state, b.SubjectId);
            sb.AppendLine(Invariant($"  #{b.SubjectId} ")
                + state.Actors[b.SubjectId].Name
                + Invariant($" — strength {b.Strength:0.#} (truth {truth:0.#})")
                + Invariant($" · coalition {b.DefensiveStrength:0.#}")
                + Invariant($" · casus belli menu {b.Menu.Count}")
                + (stale > 0
                    ? Invariant($" · {stale}y stale") : " · fresh"));
        }
        foreach (var wb in observer.Beliefs.Wars.Values)    // war-id order
        {
            if (subjectId >= 0) break;
            var war = state.Wars[wb.WarId];
            if (!war.Active) continue;
            bool attackerSide = war.OnAttackerSide(observerId);
            double truthExhaustion = attackerSide
                ? war.AttackerExhaustion : war.DefenderExhaustion;
            long stale = state.WorldYear - wb.HeardYear;
            sb.AppendLine(Invariant($"  {war.Name}: believed exhaustion ")
                + Invariant($"{wb.OwnSideExhaustion:0.00} (truth {truthExhaustion:0.00})")
                + Invariant($" · strength share {wb.OwnSideStrengthShare:0.00}")
                + (stale > 0
                    ? Invariant($" · the front reports are {stale}y old")
                    : " · fresh from the front"));
            shown++;
        }
        if (shown == 0) sb.AppendLine("  (no beliefs — nobody met yet)");
        return sb.ToString();
    }

    // ---- the news panel: pulses and their journeys ----

    public static string RenderNews(SimState state, int pulseId = -1)
    {
        var sb = new StringBuilder();
        if (pulseId >= 0)
        {
            if (pulseId >= state.Pulses.Count) return "no such pulse";
            var p = state.Pulses[pulseId];
            sb.AppendLine(Invariant($"pulse #{p.Id} — born ")
                + SimTraceView.YearLabel(p.EmitYear)
                + Invariant($" at ({p.Origin.Q},{p.Origin.R}):"));
            sb.AppendLine("  "
                + SimTraceView.Describe(state.Log.Events[(int)p.EventId]));
            foreach (var (actorId, year) in p.Delivered)
                sb.AppendLine("  " + SimTraceView.YearLabel(year)
                    + Invariant($"  reaches {state.Actors[actorId].Name} ")
                    + Invariant($"(#{actorId}, {year - p.EmitYear}y in transit)"));
            if (p.Delivered.Count == 0)
                sb.AppendLine("  (still in transit — nobody has heard)");
            return sb.ToString();
        }
        int live = 0, entered = 0;
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity) entered++;
        foreach (var p in state.Pulses)
            if (state.WorldYear - p.EmitYear <= state.Config.News.PulseMaxYears
                && p.Delivered.Count < entered) live++;
        sb.AppendLine(Invariant($"news pulses: {live} in transit ")
            + Invariant($"(of {state.Pulses.Count} ever emitted; ")
            + Invariant($"`news <id>` for a journey)"));
        for (int i = state.Pulses.Count - 1; i >= 0; i--)
        {
            var p = state.Pulses[i];
            double age = state.WorldYear - p.EmitYear;
            if (age > state.Config.News.PulseMaxYears
                || p.Delivered.Count >= entered) continue;
            sb.AppendLine(Invariant($"  #{p.Id} ({age:0}y out, heard by ")
                + Invariant($"{p.Delivered.Count}/{entered}) ")
                + SimTraceView.Describe(state.Log.Events[(int)p.EventId]));
        }
        return sb.ToString();
    }

    // ---- the stance matrix: reputation per audience ----

    public static string RenderStances(SimState state, int observerId = -1)
    {
        var sb = new StringBuilder();
        int shown = 0;
        foreach (var a in state.Actors)                   // id order (P6)
        {
            if (observerId >= 0 && a.Id != observerId) continue;
            if (a.Beliefs.Stances.Count == 0) continue;
            sb.AppendLine(Invariant($"#{a.Id} {a.Name} holds:"));
            for (int i = 0; i < a.Beliefs.Stances.Count; i++)
            {
                int subject = a.Beliefs.Stances.Keys[i];
                double stance = a.Beliefs.Stances.Values[i];
                sb.AppendLine(Invariant($"  {stance,6:+0.00;-0.00} toward ")
                    + Invariant($"#{subject} {state.Actors[subject].Name}")
                    + (stance <= -0.3 ? " — a monster to this audience"
                        : stance >= 0.3 ? " — a hero to this audience" : ""));
                shown++;
            }
        }
        if (shown == 0) return "no stances yet — the galaxy has heard nothing";
        return "stances (news-arrived judgments; reputation is per audience)\n"
               + sb;
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
