using System.Text;
using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using static System.FormattableString;

namespace StarGen.Inspector;

/// <summary>The `domain &lt;port&gt;` REPL dump — the Stage 2 eyeball
/// (domain-hex-expansion design §6): a starport's domain as a living region,
/// not a point. Satellite hexes are where this port's facilities sit away
/// from the port hex (Stage 1's per-hex opportunity siting); outposts are
/// where population followed the work and settled (Stage 2's settle
/// election, <see cref="SettleOps"/>). Mirrors <see cref="MarketView"/>'s
/// column/`Invariant($...)` idiom.
///
/// <para>Slice AC: this is now a pure formatter over
/// <see cref="DomainInteriorQuery"/> — the selection/derivation lives in
/// Core.Atlas so the REPL and a future Unity domain layer read ONE derivation
/// (K3 parity). Output stays byte-identical; only species-name lookup and
/// <see cref="SimTraceView.Describe"/> formatting remain here.</para></summary>
public static class DomainView
{
    public static string Render(SimState state, int portId)
    {
        var card = DomainInteriorQuery.Card(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear), portId);
        if (card == null)
            return $"no port #{portId} (0..{state.Ports.Count - 1})";

        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"domain #{card.PortId} — tier {card.Tier} port at ")
            + Invariant($"({card.Hex.Q},{card.Hex.R}), {card.OwnerName}'s domain, ")
            + Invariant($"founded y{card.FoundedYear}"));

        // ---- satellite hexes: this port's facilities away from the port hex
        // itself, grouped by hex (design §6 "Satellite hexes with their
        // facilities and output").
        sb.AppendLine("satellite hexes:");
        if (card.SatelliteHexes.Count == 0)
            sb.AppendLine("  (no satellite workings)");
        else
            foreach (var sat in card.SatelliteHexes)
            {
                sb.AppendLine(Invariant($"  ({sat.Hex.Q},{sat.Hex.R}):"));
                foreach (var f in sat.Facilities)
                {
                    string status = f.Active
                        ? Invariant($"condition {f.Condition:0.00}")
                        : "under construction";
                    string body = f.Body.IsNone ? "—"
                        : Invariant($"{f.Body.StarIndex}|{f.Body.SlotIndex}");
                    sb.AppendLine(Invariant($"    #{f.Id} {f.TypeName} t{f.Tier} — ")
                        + Invariant($"{status}, body {body}"));
                }
            }

        // ---- outposts: settlements this port's domain founded when
        // population followed the work (design §6 "Outposts with their
        // resident segments and founding year").
        sb.AppendLine("outposts:");
        if (card.Outposts.Count == 0) sb.AppendLine("  (no outposts yet)");
        else
            foreach (var o in card.Outposts)
            {
                sb.AppendLine(Invariant($"  #{o.Id} {o.Name} at ({o.Hex.Q},{o.Hex.R}) — ")
                    + Invariant($"founded y{o.FoundingYear}")
                    + (o.Graduated ? " [graduated]" : ""));
                // Stage 3 candidacy (design §4/§6): graduated outposts stay in
                // the registry as history; a live outpost's standing against the
                // frontier gate comes straight from the query's resolved
                // candidacy — the binding (nearest) port core, the uniform gate
                // G, and the slack that shows why it reads interior vs frontier.
                sb.AppendLine("    candidacy: " + CandidacyLine(o.Candidacy));
                if (o.Residents.Count == 0)
                    sb.AppendLine("    (unpeopled — residents relocated or lost)");
                else
                    foreach (var r in o.Residents)
                    {
                        string species = r.SpeciesId >= 0
                            && r.SpeciesId < state.Skeleton.Species.Count
                            ? state.Skeleton.Species[r.SpeciesId].Name : "?";
                        sb.AppendLine(Invariant($"    #{r.SegmentId} {species} — ")
                            + Invariant($"size {r.Size:0.00}, SoL {r.SoL:0.00}"));
                    }
            }

        // ---- events: settle + graduation events for this domain (design §6
        // "Settle and graduation events in the history/news output"). The query
        // selects them (settles = OutpostFounded in this domain; graduations =
        // PortEstablished at a graduated outpost's hex); we format each.
        sb.AppendLine("events:");
        if (card.Events.Count == 0)
            sb.AppendLine("  (no settle or graduation events yet)");
        else
            foreach (var e in card.Events)
                sb.AppendLine("  " + SimTraceView.Describe(e));

        return sb.ToString();
    }

    /// <summary>The Stage-3 candidacy line for one outpost (design §4/§6),
    /// formatting the query's resolved <see cref="DomainCandidacy"/>: a
    /// graduated outpost is history — mark the port it became; a live outpost
    /// shows the binding (nearest) port core's distance against the uniform gate
    /// G and the resulting slack, so the reader can SEE why it is interior
    /// (subordinate, dist &lt; G) vs frontier (candidacy-eligible, dist ≥
    /// G).</summary>
    private static string CandidacyLine(DomainCandidacy c)
    {
        switch (c.Kind)
        {
            case DomainCandidacyKind.Graduated:
                return c.GraduatedPortId >= 0
                    ? Invariant($"graduated → port #{c.GraduatedPortId}")
                    : "graduated → port (unresolved)";
            case DomainCandidacyKind.FrontierNoPort:
                return "frontier — eligible (no entered port yet)";
            case DomainCandidacyKind.Frontier:
                return Invariant($"frontier — eligible (dist {c.Standing.PortDistance} ")
                    + Invariant($"≥ G {c.Standing.Threshold}, slack {c.Standing.Slack})");
            default: // Interior
                return Invariant($"interior — subordinate (dist {c.Standing.PortDistance} ")
                    + Invariant($"< G {c.Standing.Threshold}, slack {c.Standing.Slack})");
        }
    }
}
