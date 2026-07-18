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
            // Stage 3 (Task 3.3, domain-hex-expansion design §4/§6) fills this
            // slot in: interior (never graduates) vs frontier (candidacy-
            // eligible, distance-to-nearest-port vs the derived gate G). Left
            // labeled on purpose — do NOT compute graduation eligibility here.
            sb.AppendLine("    candidacy: (stage 3)");
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

        return sb.ToString();
    }
}
