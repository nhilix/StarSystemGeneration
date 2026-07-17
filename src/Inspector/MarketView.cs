using System.Text;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using static System.FormattableString;

namespace StarGen.Inspector;

/// <summary>The `market` REPL dump: one port market's per-good state
/// (reference price / ask depth / grade / cleared / black book — the ask
/// column is the live sell orders, contract economy), the households that
/// trade there, and the facilities that supply it.</summary>
public static class MarketView
{
    public static string Render(SimState state, int portId)
    {
        if (portId < 0 || portId >= state.Ports.Count)
            return $"no port #{portId} (0..{state.Ports.Count - 1})";
        var port = state.Ports[portId];
        var market = state.Markets[portId];
        var owner = state.Actors[port.OwnerActorId];
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"market #{portId} — tier {port.Tier} port at ")
            + Invariant($"({port.Hex.Q},{port.Hex.R}), {owner.Name}'s domain, ")
            + Invariant($"founded y{port.FoundedYear}"));

        sb.AppendLine("  good              price    asks  grade        cleared  black book");
        for (int g = 0; g < Goods.All.Count; g++)
        {
            var def = Goods.All[g];
            double askQty = BookOps.AskQty(state, portId, g);
            double askGrade = BookOps.AskGrade(state, portId, g);
            string grade = askQty > 0
                ? Grades.BandOf(askGrade).ToString().ToLowerInvariant()
                  + Invariant($" {askGrade:0.00}")
                : "-";
            string black = market.BlackBookDemand[g] > 0
                ? Invariant($"{market.BlackBookDemand[g]:0.#} @ {market.BlackBookPrice[g]:0.00}")
                : "-";
            sb.AppendLine(Invariant($"  {def.Name,-16} {market.Price[g],6:0.00} ")
                + Invariant($"{askQty,7:0.#}  {grade,-11} ")
                + Invariant($"{market.LastCleared[g],7:0.#}  {black}"));
        }

        sb.AppendLine("segments:");
        bool anySeg = false;
        foreach (var s in state.Segments)
        {
            if (s.PortId != portId || s.Size <= 0.001) continue;
            anySeg = true;
            string culture = s.CultureId >= 0 && s.CultureId < state.Cultures.Count
                ? state.Cultures[s.CultureId].Name : $"culture{s.CultureId}";
            string body = s.Body.IsNone ? "—" : Invariant($"{s.Body.StarIndex}|{s.Body.SlotIndex}");
            sb.AppendLine(Invariant($"  #{s.Id} {culture} — size {s.Size:0.00}, ")
                + Invariant($"SoL {s.SoL:0.00}, wealth {s.Wealth:0.0}, ")
                + Invariant($"subsistence {s.LastSubsistence:0.00}, body {body}"));
        }
        if (!anySeg) sb.AppendLine("  (unpeopled)");

        sb.AppendLine("facilities:");
        bool anyFac = false;
        foreach (var f in state.Facilities)
        {
            if (MarketEngine.AttachedMarketIndex(state, f) != portId) continue;
            anyFac = true;
            var def = Infrastructure.Get((InfraTypeId)f.TypeId);
            string status = MarketEngine.IsActive(state, f)
                ? Invariant($"condition {f.Condition:0.00}")
                : "under construction";
            sb.AppendLine(Invariant($"  #{f.Id} {def.Name} t{f.Tier} at ")
                + Invariant($"({f.Hex.Q},{f.Hex.R}) — {status}"));
        }
        if (!anyFac) sb.AppendLine("  (none)");

        var lanes = new StringBuilder();
        var severed = FleetOps.SeveredLaneIds(state);   // real interdiction (H)
        foreach (var l in state.Lanes)
        {
            if (l.PortAId != portId && l.PortBId != portId) continue;
            int other = l.PortAId == portId ? l.PortBId : l.PortAId;
            if (lanes.Length > 0) lanes.Append(", ");
            lanes.Append(Invariant($"#{other}"));
            if (severed.Contains(l.Id)) lanes.Append(" [CUT]");
        }
        sb.AppendLine("lanes to: " + (lanes.Length > 0 ? lanes.ToString() : "(none)"));
        return sb.ToString();
    }
}
