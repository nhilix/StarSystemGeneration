using System.Collections.Generic;
using System.Text;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using static System.FormattableString;

namespace StarGen.Inspector;

/// <summary>The `domain &lt;port&gt;` REPL dump — the Stage 2 eyeball
/// (domain-hex-expansion design §6): a starport's domain as a living region,
/// not a point. Satellite hexes are where this port's facilities sit away
/// from the port hex (Stage 1's per-hex opportunity siting); outposts are
/// where population followed the work and settled (Stage 2's settle
/// election, <see cref="SettleOps"/>). Mirrors <see cref="MarketView"/>'s
/// column/`Invariant($...)` idiom.</summary>
public static class DomainView
{
    public static string Render(SimState state, int portId)
    {
        if (portId < 0 || portId >= state.Ports.Count)
            return $"no port #{portId} (0..{state.Ports.Count - 1})";
        var port = state.Ports[portId];
        var owner = state.Actors[port.OwnerActorId];
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"domain #{portId} — tier {port.Tier} port at ")
            + Invariant($"({port.Hex.Q},{port.Hex.R}), {owner.Name}'s domain, ")
            + Invariant($"founded y{port.FoundedYear}"));

        // ---- satellite hexes: this port's facilities away from the port hex
        // itself, grouped by hex (design §6 "Satellite hexes with their
        // facilities and output").
        var byHex = new Dictionary<HexCoordinate, List<Facility>>();
        foreach (var f in state.Facilities)                // id order (P6)
        {
            if (MarketEngine.AttachedMarketIndex(state, f) != portId) continue;
            if (f.Hex.Equals(port.Hex)) continue;           // the port hex itself
            if (!byHex.TryGetValue(f.Hex, out var list))
                byHex[f.Hex] = list = new List<Facility>();
            list.Add(f);
        }
        sb.AppendLine("satellite hexes:");
        if (byHex.Count == 0) sb.AppendLine("  (no satellite workings)");
        else
        {
            var hexes = new List<HexCoordinate>(byHex.Keys);
            hexes.Sort((a, b) => a.Q != b.Q ? a.Q.CompareTo(b.Q) : a.R.CompareTo(b.R));
            foreach (var hex in hexes)
            {
                sb.AppendLine(Invariant($"  ({hex.Q},{hex.R}):"));
                var list = byHex[hex];
                list.Sort((a, b) => a.Id.CompareTo(b.Id));
                foreach (var f in list)
                {
                    var def = Infrastructure.Get((InfraTypeId)f.TypeId);
                    string status = MarketEngine.IsActive(state, f)
                        ? Invariant($"condition {f.Condition:0.00}")
                        : "under construction";
                    string body = f.Body.IsNone ? "—"
                        : Invariant($"{f.Body.StarIndex}|{f.Body.SlotIndex}");
                    sb.AppendLine(Invariant($"    #{f.Id} {def.Name} t{f.Tier} — ")
                        + Invariant($"{status}, body {body}"));
                }
            }
        }

        // ---- outposts: settlements this port's domain founded when
        // population followed the work (design §6 "Outposts with their
        // resident segments and founding year").
        sb.AppendLine("outposts:");
        bool anyOutpost = false;
        foreach (var o in state.Outposts)                  // id order (P6)
        {
            if (o.ParentPortId != portId) continue;
            anyOutpost = true;
            sb.AppendLine(Invariant($"  #{o.Id} {o.Name} at ({o.Hex.Q},{o.Hex.R}) — ")
                + Invariant($"founded y{o.FoundingYear}")
                + (o.Graduated ? " [graduated]" : ""));
            // Stage 3 candidacy (design §4/§6): graduated outposts stay in the
            // registry as history; a live outpost's standing against the
            // frontier gate comes straight from OutpostOps.FrontierStatus — the
            // binding (nearest) port core, the uniform gate G, and the slack
            // that shows why it reads interior vs frontier.
            sb.AppendLine("    candidacy: " + CandidacyLine(state, o));
            bool anyResident = false;
            foreach (var s in state.Segments)               // id order (P6)
            {
                if (s.PortId != portId || !s.Hex.Equals(o.Hex)
                    || s.Size <= 0.001) continue;
                anyResident = true;
                string species = s.SpeciesId >= 0
                    && s.SpeciesId < state.Skeleton.Species.Count
                    ? state.Skeleton.Species[s.SpeciesId].Name : "?";
                sb.AppendLine(Invariant($"    #{s.Id} {species} — ")
                    + Invariant($"size {s.Size:0.00}, SoL {s.SoL:0.00}"));
            }
            if (!anyResident)
                sb.AppendLine("    (unpeopled — residents relocated or lost)");
        }
        if (!anyOutpost) sb.AppendLine("  (no outposts yet)");

        // ---- events: settle + graduation events for this domain (design §6
        // "Settle and graduation events in the history/news output"). Settles
        // are OutpostFounded events whose outpost belongs to this domain.
        // Graduations reuse the Outposts.Graduated registry as the source of
        // truth (the design's own suggested simplification) matched to the
        // PortEstablished event at that outpost's hex — this tells a
        // graduation's PortEstablished apart from an expedition's without
        // inventing a new event type or payload field.
        sb.AppendLine("events:");
        bool anyEvent = false;
        foreach (var e in state.Log.Events)                 // log order (P6)
        {
            if (e.Type == WorldEventType.OutpostFounded
                && e.Payload is OutpostFoundedPayload op
                && op.OutpostId >= 0 && op.OutpostId < state.Outposts.Count
                && state.Outposts[op.OutpostId].ParentPortId == portId)
            {
                sb.AppendLine("  " + SimTraceView.Describe(e));
                anyEvent = true;
                continue;
            }
            if (e.Type != WorldEventType.PortEstablished) continue;
            foreach (var o in state.Outposts)               // id order (P6)
                if (o.ParentPortId == portId && o.Graduated
                    && o.Hex.Equals(e.Location))
                {
                    sb.AppendLine("  " + SimTraceView.Describe(e));
                    anyEvent = true;
                    break;
                }
        }
        if (!anyEvent) sb.AppendLine("  (no settle or graduation events yet)");

        return sb.ToString();
    }

    /// <summary>The Stage-3 candidacy line for one outpost (design §4/§6): a
    /// graduated outpost is history — mark the port it became; a live outpost
    /// reads its standing straight off <see cref="OutpostOps.FrontierStatus"/>,
    /// showing the binding (nearest) port core's distance against the uniform
    /// gate G and the resulting slack, so the reader can SEE why it is interior
    /// (subordinate, dist &lt; G) vs frontier (candidacy-eligible, dist ≥
    /// G).</summary>
    private static string CandidacyLine(SimState state, Outpost o)
    {
        if (o.Graduated)
        {
            int graduatedPortId = -1;
            foreach (var p in state.Ports)                  // id order (P6)
                if (p.Hex.Equals(o.Hex)) { graduatedPortId = p.Id; break; }
            return graduatedPortId >= 0
                ? Invariant($"graduated → port #{graduatedPortId}")
                : "graduated → port (unresolved)";
        }
        var standing = OutpostOps.FrontierStatus(state, o);
        // no entered port anywhere to clash with — vacuously frontier
        // (OutpostOps.FrontierStatus's documented Slack == int.MaxValue case).
        if (standing.Slack == int.MaxValue)
            return "frontier — eligible (no entered port yet)";
        return standing.IsFrontier
            ? Invariant($"frontier — eligible (dist {standing.PortDistance} ")
                + Invariant($"≥ G {standing.Threshold}, slack {standing.Slack})")
            : Invariant($"interior — subordinate (dist {standing.PortDistance} ")
                + Invariant($"< G {standing.Threshold}, slack {standing.Slack})");
    }
}
