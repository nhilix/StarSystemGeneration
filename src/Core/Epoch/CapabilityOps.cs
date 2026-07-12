using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>A sited, scored construction option — a PERCEIVED candidate
/// (spec §2): the same list the planner AI ranks and a player would read
/// off the economy screen (P2).</summary>
public sealed record ConstructionCandidate(
    int TypeId, HexCoordinate Hex, int PortId, double Score);

/// <summary>One own port as the planner sees it (spec §2). Stock is the
/// located stockpile snapshot (stage 2): the brief is located, so the
/// scheduler sees where goods are and prefers sites near supply.</summary>
public sealed record PortBrief(int PortId, int Tier, int YardTiers,
                               IReadOnlyList<double>? Stock = null);

/// <summary>One in-flight funding obligation: value drawn per world-year
/// (goods at founding prices + wages) and naive years to completion.</summary>
public sealed record CommitmentBrief(double CostPerYear, double YearsRemaining);

/// <summary>The perceived economy-as-rates (spec §2): what the planner
/// schedules against. Own-side facts, assembled fresh each Perception.</summary>
public sealed class CapabilityBrief
{
    public double IncomePerYear { get; }               // trailing (P3)
    public IReadOnlyList<double> GenerationPerYear { get; }  // per good
    public IReadOnlyList<CommitmentBrief> Commitments { get; }
    public double CommittedCostPerYear { get; }        // Σ commitments now

    public CapabilityBrief(double incomePerYear,
        IReadOnlyList<double> generationPerYear,
        IReadOnlyList<CommitmentBrief> commitments)
    {
        IncomePerYear = incomePerYear;
        GenerationPerYear = generationPerYear;
        Commitments = commitments;
        double sum = 0;
        foreach (var c in commitments) sum += c.CostPerYear;
        CommittedCostPerYear = sum;
    }
}

/// <summary>Perception-side capability assembly (spec §2). The candidate
/// scan is the siting-score × price-signal × saturation math that lived in
/// AllocationPhase.BuildFacilities — moved here so deciding what to build
/// is Intent's and executing is Allocation's (Move 1 made honest).</summary>
public static class CapabilityOps
{
    /// <summary>Buildable producer types — the keystone port comes from
    /// colonization, gates from lane construction.</summary>
    internal static readonly Substrate.InfraTypeId[] BuildableTypes =
    {
        Substrate.InfraTypeId.Mine, Substrate.InfraTypeId.Skimmer,
        Substrate.InfraTypeId.AgriComplex, Substrate.InfraTypeId.ExcavationSite,
        Substrate.InfraTypeId.Refinery, Substrate.InfraTypeId.Chemworks,
        Substrate.InfraTypeId.Fabricator, Substrate.InfraTypeId.ExoticsLab,
        Substrate.InfraTypeId.Foundry, Substrate.InfraTypeId.Shipyard,
        Substrate.InfraTypeId.Arsenal, Substrate.InfraTypeId.ComputeCore,
        Substrate.InfraTypeId.Fortress,   // Military-tier-gated (slice H)
    };

    /// <summary>Top 3 candidates per own under-capacity port, score-ranked
    /// (ties: lower TypeId, then cell spiral order). Under-construction
    /// facilities count against the port cap and occupy their hexes — a
    /// plan must not double-book a site.</summary>
    public static List<ConstructionCandidate> ConstructionCandidatesFor(
        SimState state, int actorId)
    {
        var cfg = state.Config;
        var eco = cfg.Economy;
        var pr = state.PolityOf(actorId);
        var result = new List<ConstructionCandidate>();
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId != actorId) continue;
            int cap = port.Tier * cfg.Infrastructure.FacilitiesPerPortTier;
            int attached = 0;
            foreach (var f in state.Facilities)
            {
                // gates draw on their own slot budget, not industry's
                if (f.TypeId == (int)Substrate.InfraTypeId.Gate) continue;
                if (f.OwnerActorId == pr.ActorId
                    && MarketEngine.AttachedMarketIndex(state, f) == port.Id)
                    attached++;
            }
            if (attached >= cap) continue;
            var market = state.Markets[port.Id];
            var workforce = MarketEngine.EmbodimentOf(state, pr.SpeciesId);

            // per-port top 3 (score desc, TypeId asc, hex spiral order)
            var top = new List<ConstructionCandidate>(3);
            foreach (var cell in state.Skeleton.Cells)        // spiral order (P6)
            {
                var center = HexGrid.CellCenter(cell.Coord);
                if (HexGrid.Distance(port.Hex, center)
                    > PortDomains.ServiceRadius(cfg, port.Tier)
                      + TechOps.AstroRadiusBonus(state, pr.ActorId)) continue;
                if (cell.IsVoid) continue;
                var fields = MarketEngine.FieldsAt(state, center);
                var site = new Substrate.CellSite(fields,
                    Connectivity: Math.Min(1.0, LaneCount(state, port.Id) / 4.0),
                    IsPortHeart: cell.Coord.Equals(HexGrid.CellOf(port.Hex)),
                    PortTier: port.Tier, DevelopmentTier: port.Tier,
                    IsChokepoint: cell.IsChokepoint);
                foreach (var type in BuildableTypes)
                {
                    // fortification tiers gate on Military tech
                    // (economy/technology.md) — tier 2 unlocks the type
                    if (type == Substrate.InfraTypeId.Fortress
                        && pr.TechTier[(int)TechDomain.Military] < 2) continue;
                    var def = Substrate.Infrastructure.Get(type);
                    double signal = PriceSignal(eco, market, def);
                    int existing = 0;
                    foreach (var f in state.Facilities)
                        if (f.TypeId == (int)type && f.OwnerActorId == pr.ActorId
                            && MarketEngine.AttachedMarketIndex(state, f) == port.Id)
                            existing++;
                    // saturation: the second of a kind must out-earn a first
                    // of another — ports diversify their chain
                    double score = Substrate.Siting.Score(type, site, workforce)
                                   * signal / (1 + existing);
                    if (score <= cfg.Infrastructure.ConstructionScoreFloor) continue;
                    var hex = PickHex(state, cell, center);
                    InsertTop3(top, new ConstructionCandidate(
                        (int)type, hex, port.Id, score));
                }
            }
            result.AddRange(top);
        }
        return result;
    }

    /// <summary>Own-side capability facts assembled fresh (spec §2): trailing
    /// income, coarse per-good generation from own active facilities, and
    /// the in-flight projects this actor funds. Works for polity and
    /// corporation actor ids alike — Perception wires it for polities only
    /// today; the method itself does not care.</summary>
    public static CapabilityBrief BriefFor(SimState state, int actorId)
    {
        var corp = state.CorporationOf(actorId);
        double incomePerYear = corp != null
            ? corp.LastIncomePerYear
            : state.PolityOf(actorId).LastIncomePerYear;

        var generation = new double[Substrate.Goods.All.Count];
        foreach (var f in state.Facilities)                // id order (P6)
        {
            if (f.OwnerActorId != actorId || !MarketEngine.IsActive(state, f))
                continue;
            var def = Substrate.Infrastructure.Get((Substrate.InfraTypeId)f.TypeId);
            if (def.Produces.Count == 0) continue;
            double perGood = def.BaseOutputPerYear
                * Substrate.Production.TierOutputFactor(f.Tier)
                * f.Condition / def.Produces.Count;
            foreach (var g in def.Produces)
                generation[(int)g] += perGood;
        }

        var commitments = new List<CommitmentBrief>();
        foreach (var p in state.Projects)                  // id order (P6)
        {
            if (!p.InFlight || p.FunderActorId != actorId) continue;
            double costPerYear = p.WagesPerYear;
            for (int g = 0; g < p.PerYearBasket.Length; g++)
            {
                if (p.PerYearBasket[g] == 0) continue;
                costPerYear += p.PerYearBasket[g]
                    * Market.InitialPrice(state.Config.Economy,
                                          (Substrate.GoodId)g);
            }
            commitments.Add(new CommitmentBrief(costPerYear,
                p.YearsRequired - p.YearsDelivered));
        }

        return new CapabilityBrief(incomePerYear, generation, commitments);
    }

    /// <summary>Insert into the per-port top-3 list, keeping it ranked by
    /// (score desc, TypeId asc) and capped at 3 — the direct replacement
    /// for the old single-best `if (score > bestScore)` track.</summary>
    private static void InsertTop3(List<ConstructionCandidate> top,
                                   ConstructionCandidate c)
    {
        int i = 0;
        while (i < top.Count
               && (top[i].Score > c.Score
                   || (top[i].Score == c.Score && top[i].TypeId <= c.TypeId)))
            i++;
        if (i >= 3) return;
        top.Insert(i, c);
        if (top.Count > 3) top.RemoveAt(top.Count - 1);
    }

    private static int LaneCount(SimState state, int portId)
    {
        int count = 0;
        foreach (var l in state.Lanes)
            if (l.PortAId == portId || l.PortBId == portId) count++;
        return count;
    }

    /// <summary>Mean price-over-founding ratio of the type's products,
    /// clamped — scarcity builds its own relief.</summary>
    internal static double PriceSignal(EconomyKnobs eco, Market market,
                                       Substrate.InfraDef def)
    {
        if (def.Produces.Count == 0) return 1.0;
        double sum = 0;
        foreach (var g in def.Produces)
            sum += market.Price[(int)g] / Market.InitialPrice(eco, g);
        double mean = sum / def.Produces.Count;
        return Math.Min(3.0, Math.Max(0.5, mean));
    }

    /// <summary>First anchor hex in the cell free of facilities, else the
    /// cell center — the facility is anchored at groundbreaking (P1).</summary>
    internal static HexCoordinate PickHex(SimState state, Galaxy.RegionCell cell,
                                          HexCoordinate center)
    {
        foreach (var a in cell.Anchors)
        {
            bool taken = false;
            foreach (var f in state.Facilities)
                if (f.Hex.Equals(a.Hex)) { taken = true; break; }
            if (!taken && a.Type != Galaxy.AnchorType.Homeworld) return a.Hex;
        }
        return center;
    }
}
