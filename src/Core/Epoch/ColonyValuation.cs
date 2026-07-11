using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>A scored colonization target (space-and-travel.md §Colonization:
/// the decision picks from valuations × terrain potentials × reach).</summary>
public sealed record ColonyCandidate(HexCoordinate Target, double Score);

/// <summary>Colony-target enumeration and scoring over the natural raster.
/// Score = terrain potential with contested-influence friction plus the
/// price signal (slice D): what the capital's market pays for what the cell
/// could produce — scarcity steers expansion. Deterministic: cells scanned
/// in spiral order, ordered by score desc then spiral index.</summary>
public static class ColonyValuation
{
    /// <summary>Targets for a polity: non-void cells within
    /// ColonizationReachHexes of any owned port, without one of our ports in
    /// the cell. Target hex = first anchor hex free of ports, else the cell
    /// center (skipped if a port sits there). Score = MeanDensity
    /// + 0.3×Metallicity + 0.4 if the cell has a non-homeworld anchor
    /// + the price term − EncroachmentPenalty per foreign polity whose
    /// domain the new port's service area would overlap (slice H: settling
    /// someone's sphere is a provocation, not a land rush — only a truly
    /// rich site outweighs the tension it buys; borders stay contiguous).</summary>
    public static IReadOnlyList<ColonyCandidate> CandidatesFor(
        SimState state, int polityId, int max = 8)
    {
        var cfg = state.Config;
        var sk = state.Skeleton;
        var ownPorts = new List<Port>();
        foreach (var p in state.Ports)
            if (p.OwnerActorId == polityId) ownPorts.Add(p);
        var best = new List<ColonyCandidate>();       // kept sorted, capped at max
        var bestSpiral = new List<int>();
        if (ownPorts.Count == 0) return best;
        // the polity prices the frontier through its capital market (first
        // port) — deliberately its OWN fresh view (slice I decision): a
        // valuation surveys nature, and nature doesn't move; nothing to stale
        Market? capital = ownPorts[0].Id < state.Markets.Count
            ? state.Markets[ownPorts[0].Id] : null;

        foreach (var cell in sk.Cells)                // spiral order (P6)
        {
            if (cell.IsVoid) continue;
            var target = PickTarget(state, cell);
            if (target is not HexCoordinate hex) continue;

            bool inReach = false;
            foreach (var p in ownPorts)
                if (HexGrid.Distance(p.Hex, hex) <= cfg.Expansion.ColonizationReachHexes)
                { inReach = true; break; }
            if (!inReach) continue;
            bool ownCell = false;
            foreach (var p in ownPorts)
                if (HexGrid.CellOf(p.Hex).Equals(cell.Coord)) { ownCell = true; break; }
            if (ownCell) continue;

            double score = cell.MeanDensity + 0.3 * cell.Metallicity;
            foreach (var a in cell.Anchors)
                if (a.Type != AnchorType.Homeworld) { score += 0.4; break; }
            score -= cfg.Expansion.EncroachmentPenalty
                     * EncroachedPolities(state, polityId, hex);
            if (capital != null)
                score += PriceTerm(state, capital, cell);

            Insert(best, bestSpiral, new ColonyCandidate(hex, score), cell.SpiralIndex, max);
        }
        return best;
    }

    /// <summary>How many foreign polities a tier-1 port at this hex would
    /// entangle: distinct owners whose ports' service areas would overlap
    /// the newcomer's own (the contested-overlap zones the relations layer
    /// prices as tension).</summary>
    public static int EncroachedPolities(SimState state, int polityId,
                                         HexCoordinate hex)
    {
        var cfg = state.Config;
        int newRadius = PortDomains.ServiceRadius(cfg, 1);
        var owners = new List<int>();
        foreach (var p in state.Ports)                    // id order (P6)
        {
            if (p.OwnerActorId == polityId
                || !state.Actors[p.OwnerActorId].Entered) continue;
            if (HexGrid.Distance(p.Hex, hex)
                > newRadius + PortDomains.ServiceRadius(cfg, p.Tier)
                  + TechOps.AstroRadiusBonus(state, p.OwnerActorId)) continue;
            if (!owners.Contains(p.OwnerActorId)) owners.Add(p.OwnerActorId);
        }
        return owners.Count;
    }

    /// <summary>What the capital market pays for what this cell could
    /// extract: potential × relative price per raw good, modestly weighted —
    /// an ore rush is high metallicity times a hungry foundry belt.</summary>
    private static double PriceTerm(SimState state, Market capital, RegionCell cell)
    {
        var eco = state.Config.Economy;
        var fields = MarketEngine.FieldsAt(state, HexGrid.CellCenter(cell.Coord));
        double term =
            Substrate.Potentials.Ore(fields) * Rel(eco, capital, Substrate.GoodId.Ore)
            + Substrate.Potentials.Volatiles(fields) * Rel(eco, capital, Substrate.GoodId.Volatiles)
            + Substrate.Potentials.Biosphere(fields) * Rel(eco, capital, Substrate.GoodId.Provisions)
            + Substrate.Potentials.Exotics(fields) * Rel(eco, capital, Substrate.GoodId.Exotics);
        return 0.15 * term;
    }

    private static double Rel(EconomyKnobs eco, Market m, Substrate.GoodId g)
    {
        double factor = m.Price[(int)g] / Market.InitialPrice(eco, g);
        return factor < 0.5 ? 0.5 : factor > 3.0 ? 3.0 : factor;
    }

    /// <summary>First anchor hex in the cell with no port on it; else the cell
    /// center if port-free; else nothing. Homeworld anchor hexes are reserved
    /// for their species' emergence — never colony targets (the entering
    /// polity founds its first port there; frame/time.md).</summary>
    private static HexCoordinate? PickTarget(SimState state, RegionCell cell)
    {
        foreach (var a in cell.Anchors)
            if (a.Type != AnchorType.Homeworld && !PortAt(state, a.Hex)) return a.Hex;
        var center = HexGrid.CellCenter(cell.Coord);
        foreach (var a in cell.Anchors)
            if (a.Type == AnchorType.Homeworld && a.Hex.Equals(center)) return null;
        return PortAt(state, center) ? (HexCoordinate?)null : center;
    }

    private static bool PortAt(SimState state, HexCoordinate hex)
    {
        foreach (var p in state.Ports)
            if (p.Hex.Equals(hex)) return true;
        return false;
    }

    private static void Insert(List<ColonyCandidate> best, List<int> spiral,
                               ColonyCandidate c, int spiralIndex, int max)
    {
        int at = best.Count;
        for (int i = 0; i < best.Count; i++)
            if (c.Score > best[i].Score
                || (c.Score == best[i].Score && spiralIndex < spiral[i]))
            { at = i; break; }
        if (at >= max) return;
        best.Insert(at, c);
        spiral.Insert(at, spiralIndex);
        if (best.Count > max) { best.RemoveAt(max); spiral.RemoveAt(max); }
    }
}
