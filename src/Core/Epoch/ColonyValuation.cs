using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Which expansion move a candidate represents (domain-hex-expansion
/// §4): a colony <b>expedition</b> reaches a fresh hex with a convoy, an
/// outpost <b>graduation</b> promotes an already-settled frontier outpost in
/// place. Both rank in ONE list the controller reads, so a polity weighs reach
/// against infill against the same treasury.</summary>
public enum ColonyCandidateKind { Expedition, Graduation }

/// <summary>A scored expansion target (space-and-travel.md §Colonization: the
/// decision picks from valuations × terrain potentials × reach). Carries its
/// <see cref="Kind"/> so one ranked list mixes expeditions and graduations; a
/// graduation also carries its <see cref="OutpostId"/> and its discounted
/// <see cref="Cost"/> (an expedition's cost is <c>Expansion.ColonyCost</c>).</summary>
public sealed record ColonyCandidate(HexCoordinate Target, double Score,
    ColonyCandidateKind Kind = ColonyCandidateKind.Expedition,
    int OutpostId = -1, double Cost = 0);

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
            // a rival convoy already under way is PUBLIC residue (P1/P3 —
            // en-route expeditions are visible, interceptable state): racing
            // it wastes the expedition on a turn-back (T2 carried flag,
            // closed in slice CE)
            bool contested = false;
            foreach (var p in state.Projects)             // id order (P6)
                if (p.InFlight && p.Kind == ProjectKind.ColonyExpedition
                    && p.Hex.Equals(hex))
                { contested = true; break; }
            if (contested) continue;

            double score = CellTerrainScore(state, cell, capital)
                - cfg.Expansion.EncroachmentPenalty
                  * EncroachedPolities(state, polityId, hex);
            Insert(best, bestSpiral,
                new ColonyCandidate(hex, score, ColonyCandidateKind.Expedition,
                    OutpostId: -1, Cost: cfg.Expansion.ColonyCost),
                cell.SpiralIndex, max);
        }
        // infill candidates: the polity's own mature FRONTIER outposts (design
        // §4), scored on their hex's terrain like an expedition target PLUS a
        // flat infill bonus (a known-good, already-worked site), at their
        // DISCOUNTED cost — so reach and infill rank in one list against the
        // same treasury. The frontier gate (OutpostOps.IsFrontier) IS the
        // anti-clustering guarantee: an interior outpost never enters the list,
        // so a graduated port can never sit inside another port's domain.
        // Determinism: outposts in id order, keyed past every cell's spiral
        // index (GraduationSortKey ≫ any skeleton cell count) so an exact score
        // tie is broken stably and reach wins over infill (P6).
        foreach (var o in state.Outposts)                 // id order (P6)
        {
            if (o.Graduated) continue;
            if (o.ParentPortId < 0 || o.ParentPortId >= state.Ports.Count) continue;
            if (state.Ports[o.ParentPortId].OwnerActorId != polityId) continue;
            if (!OutpostOps.IsFrontier(state, o)) continue;
            if (!state.Skeleton.TryGetCell(HexGrid.CellOf(o.Hex), out var oc)
                || oc.IsVoid) continue;
            double gScore = CellTerrainScore(state, oc, capital)
                            + cfg.Expansion.GraduationScoreBonus;
            Insert(best, bestSpiral,
                new ColonyCandidate(o.Hex, gScore, ColonyCandidateKind.Graduation,
                    o.Id, GraduationCost(state, o)),
                GraduationSortKey + o.Id, max);
        }
        return best;
    }

    /// <summary>Sort-key offset for graduation candidates, past any skeleton
    /// cell's spiral index — keeps the merged expedition+graduation ranking
    /// deterministic (an exact score tie orders graduations by id and lets a
    /// reach candidate win, P6).</summary>
    private const int GraduationSortKey = 1_000_000;

    /// <summary>The terrain half of an expansion score, shared by expedition
    /// and graduation candidates so the two rank on ONE commensurable scale
    /// (design §4): mean density + a metallicity weight + a non-homeworld
    /// anchor bonus + the capital market's price signal. The expedition path
    /// additionally subtracts the encroachment penalty; the graduation path
    /// adds its flat infill bonus.</summary>
    private static double CellTerrainScore(SimState state, RegionCell cell,
                                           Market? capital)
    {
        double score = cell.MeanDensity + 0.3 * cell.Metallicity;
        foreach (var a in cell.Anchors)
            if (a.Type != AnchorType.Homeworld) { score += 0.4; break; }
        if (capital != null)
            score += PriceTerm(state, capital, cell);
        return score;
    }

    /// <summary>An outpost's discounted graduation cost (domain-hex-expansion
    /// §4): <see cref="ExpansionKnobs.ColonyCost"/> reduced by a per-facility
    /// and per-resident-size fraction — the outpost is already half a colony —
    /// floored at <see cref="ExpansionKnobs.GraduationMinCostFraction"/> ×
    /// ColonyCost. A facility-rich or populous outpost costs strictly less than
    /// a bare one, but promotion is never free. This is the number the
    /// controller gates affordability on and the wage stream recycles from
    /// ExpansionPoints (conservation flow #3). Pure and deterministic
    /// (facilities and segments scanned in id order, P6).</summary>
    public static double GraduationCost(SimState state, Outpost outpost)
    {
        var exp = state.Config.Expansion;
        int facilities = 0;
        foreach (var f in state.Facilities)               // id order (P6)
            if (f.Hex.Equals(outpost.Hex)) facilities++;
        double residentSize = 0;
        foreach (var s in state.Segments)                 // id order (P6)
            if (s.Size > 0 && s.Hex.Equals(outpost.Hex)
                && s.PortId == outpost.ParentPortId)
                residentSize += s.Size;
        double discountFrac =
            exp.GraduationCostDiscountPerFacility * facilities
            + exp.GraduationCostDiscountPerResident * residentSize;
        double frac = System.Math.Max(exp.GraduationMinCostFraction,
                                      1.0 - discountFrac);
        return exp.ColonyCost * frac;
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
