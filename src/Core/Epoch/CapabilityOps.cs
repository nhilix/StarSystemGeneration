using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
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
    /// <summary>Accumulated investment treasuries spread over the planning
    /// horizon — savings exist to be spent (contract economy: receipts are
    /// lean REAL cash flow now, and a planner packing against income alone
    /// deadlocks on top of an idle war chest).</summary>
    public double SavingsPerYear { get; }
    public IReadOnlyList<double> GenerationPerYear { get; }  // per good
    public IReadOnlyList<CommitmentBrief> Commitments { get; }
    public double CommittedCostPerYear { get; }        // Σ commitments now

    public CapabilityBrief(double incomePerYear, double savingsPerYear,
        IReadOnlyList<double> generationPerYear,
        IReadOnlyList<CommitmentBrief> commitments)
    {
        IncomePerYear = incomePerYear;
        SavingsPerYear = savingsPerYear;
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
    /// (ties: lower TypeId, then hex spiral order). The scan is per HEX across
    /// the port's whole domain (domain hex-expansion §2), not per cell: a
    /// body-aware opportunity score sites extraction on the richest free body at
    /// the frontier while support/processing stays anchored at the port hex.
    /// Under-construction facilities count against the port cap and occupy their
    /// hexes — a plan must not double-book a site.</summary>
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
            int radius = PortDomains.ServiceRadius(cfg, port.Tier)
                         + TechOps.AstroRadiusBonus(state, pr.ActorId);
            double connectivity = Math.Min(1.0, LaneCount(state, port.Id) / 4.0);
            var portCell = HexGrid.CellOf(port.Hex);

            // per-port, per-type facts hoisted out of the hex loop: the price
            // signal is market-scoped and the saturation count port-scoped —
            // neither varies hex to hex, so each is assembled once.
            var signal = new double[BuildableTypes.Length];
            var existing = new int[BuildableTypes.Length];
            for (int t = 0; t < BuildableTypes.Length; t++)
                signal[t] = PriceSignal(eco, market,
                    Substrate.Infrastructure.Get(BuildableTypes[t]));
            foreach (var f in state.Facilities)               // id order (P6)
                if (f.OwnerActorId == pr.ActorId
                    && MarketEngine.AttachedMarketIndex(state, f) == port.Id)
                    for (int t = 0; t < BuildableTypes.Length; t++)
                        if (f.TypeId == (int)BuildableTypes[t]) { existing[t]++; break; }

            // per-port top 3 (score desc, TypeId asc, hex spiral order)
            var top = new List<ConstructionCandidate>(3);
            foreach (var cell in state.Skeleton.Cells)        // cell spiral order (P6)
            {
                if (cell.IsVoid) continue;
                var cellCenter = HexGrid.CellCenter(cell.Coord);
                // whole-cell reject: every hex sits within CellRadius of the
                // center, so a center farther than radius + CellRadius can hold
                // no serviced hex — skip it without touching its hexes.
                if (HexGrid.Distance(port.Hex, cellCenter)
                    > radius + HexGrid.CellRadius) continue;
                // FieldsAt is cell-granular (the natural raster's resolution) —
                // every hex in the cell shares these fields; the per-hex signal
                // is the real body the settled/previewed system carries there.
                var fields = MarketEngine.FieldsAt(state, cellCenter);
                var site = new Substrate.CellSite(fields, connectivity,
                    IsPortHeart: cell.Coord.Equals(portCell),
                    PortTier: port.Tier, DevelopmentTier: port.Tier,
                    IsChokepoint: cell.IsChokepoint);
                foreach (var hex in HexGrid.Spiral(cellCenter, HexGrid.CellRadius))
                {                                             // hex spiral order (P6)
                    int hexDist = HexGrid.Distance(port.Hex, hex);
                    if (hexDist > radius) continue;
                    // the scan unit: the hex's system (settled read, else a
                    // roll-free preview) and its port body, resolved once and
                    // reused across every type — no commit, no roll (Stage 1 is
                    // roll-free; the preview is discarded).
                    var system = SystemRegistry.IsSettled(state, hex)
                        ? state.SettledSystems[hex] : PreviewSystem(state, hex);
                    var portBody = BodySiting.PortBody(system);
                    // two discounts on the same hex-hop: the staffing commute
                    // shape (labor) and the hauling proxy (moving output back to
                    // the port market). Extraction pays both; support rides the
                    // commute term only — it sells at the port it sits beside.
                    double proximity =
                        1.0 / (1.0 + eco.StaffingDistanceFalloff * hexDist);
                    double hauling =
                        1.0 / (1.0 + eco.HaulingProxyPerHex * hexDist);
                    // a non-homeworld anchor at this hex is a development bonus,
                    // not the selector (ColonyValuation's +0.4 idiom) — PickHex's
                    // old selecting job, demoted to a score nudge.
                    double anchorBonus = 0.0;
                    foreach (var a in cell.Anchors)
                        if (a.Hex.Equals(hex)
                            && a.Type != Galaxy.AnchorType.Homeworld)
                        { anchorBonus = 0.4; break; }
                    for (int t = 0; t < BuildableTypes.Length; t++)
                    {
                        var type = BuildableTypes[t];
                        // fortification tiers gate on Military tech
                        // (economy/technology.md) — tier 2 unlocks the type
                        if (type == Substrate.InfraTypeId.Fortress
                            && pr.TechTier[(int)TechDomain.Military] < 2) continue;
                        double opportunity;
                        double distance;
                        if (BodySiting.IsExtraction(type))
                        {
                            var body = BestUnclaimedBody(state, hex, type,
                                                         system, portBody);
                            // the generalized overflow case: the type's only
                            // eligible body is already claimed (or the hex bears
                            // none) — it scores zero here, so a free-bodied
                            // neighbor wins automatically.
                            if (body.IsNone) continue;
                            opportunity = ExtractionOpportunity(
                                state, hex, body, type, fields, system);
                            distance = proximity * hauling;
                        }
                        else
                        {
                            // support/processing keeps port-body affinity —
                            // scored on the cell raster + portness, pulled hard
                            // toward the port hex by the commute term so the
                            // industrial core stays anchored at the port.
                            opportunity =
                                Substrate.Siting.Score(type, site, workforce);
                            distance = proximity;
                        }
                        // saturation: the second of a kind must out-earn a first
                        // of another — ports diversify their chain
                        double score = opportunity * signal[t] / (1 + existing[t])
                                       * distance + anchorBonus;
                        if (score <= cfg.Infrastructure.ConstructionScoreFloor)
                            continue;
                        InsertTop3(top, new ConstructionCandidate(
                            (int)type, hex, port.Id, score));
                    }
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
        double drawdown = Math.Max(1.0,
            state.Config.Economy.PlanSavingsDrawdownYears);
        double savingsPerYear = (corp != null
            ? Math.Max(0.0, corp.Credits)
            : Math.Max(0.0, state.PolityOf(actorId).DevelopmentPoints)
              + Math.Max(0.0, state.PolityOf(actorId).MilitaryPoints))
            / drawdown;

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
            // expeditions are paid at the act and their basket is cargo
            // aboard, not a standing rate — no commitment to report
            if (!p.InFlight || p.FunderActorId != actorId
                || p.Kind == ProjectKind.ColonyExpedition) continue;
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

        return new CapabilityBrief(incomePerYear, savingsPerYear, generation,
                                   commitments);
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

    /// <summary>A hex's system for scoring: the settled read, else a roll-free
    /// preview generated and DISCARDED — exactly SystemQuery.At's unsettled
    /// branch without SystemRegistry.Commit, so the hex stays pristine and the
    /// preview is a pure function of (config, hex), repeatable and roll-free.</summary>
    private static StarSystem? PreviewSystem(SimState state, HexCoordinate hex)
    {
        var context = new GalaxyContext(state.Skeleton.Config)
        { Skeleton = state.Skeleton };
        return Generator.Generate(context, hex).System;
    }

    /// <summary>The body an extraction type would claim at a hex, claim-aware —
    /// mirrors ProjectOps.PlaceFacilityBody's per-resource-class claim scan
    /// (BodySiting.CompetesForBody) but commits nothing. None when the hex bears
    /// no eligible body or its only one is already taken by a competitor.</summary>
    private static BodyRef BestUnclaimedBody(SimState state, HexCoordinate hex,
        Substrate.InfraTypeId type, StarSystem? system, BodyRef portBody)
    {
        var claimed = new List<BodyRef>();
        foreach (var other in state.Facilities)               // id order (P6)
            if (other.Hex.Equals(hex) && !other.Body.IsNone
                && BodySiting.CompetesForBody(
                    (Substrate.InfraTypeId)other.TypeId, type))
                claimed.Add(other.Body);
        return BodySiting.Assign(system, type, portBody, claimed);
    }

    /// <summary>The roll-free opportunity of an extraction type at its claimed
    /// body. Depletable (Mine/ExcavationSite): the remaining BodyResources stock
    /// if the body is already worked, else the pre-roll expected mean (never
    /// rolled — Stage 1 is roll-free), both normalized by BodyStockOreScale so a
    /// fresh body reads back its [0,1] raster richness, comparable to the
    /// renewable-yield and support Siting.Score bands. Renewable (Skimmer/
    /// AgriComplex): BodySiting.RenewableYield of the body's real attributes.</summary>
    private static double ExtractionOpportunity(SimState state, HexCoordinate hex,
        BodyRef body, Substrate.InfraTypeId type, Substrate.CellFields fields,
        StarSystem? system)
    {
        var eco = state.Config.Economy;
        if (type == Substrate.InfraTypeId.Mine
            || type == Substrate.InfraTypeId.ExcavationSite)
        {
            double richness = type == Substrate.InfraTypeId.Mine
                ? Substrate.Potentials.Ore(fields)
                : Substrate.Potentials.Exotics(fields);
            double stock =
                state.BodyResources.TryGetValue((hex, body), out var s)
                    ? s.Quantity
                    : eco.BodyStockOreScale * richness;
            double scale = eco.BodyStockOreScale > 0 ? eco.BodyStockOreScale : 1.0;
            return stock / scale;
        }
        return BodySiting.RenewableYield(system, body, type);
    }
}
