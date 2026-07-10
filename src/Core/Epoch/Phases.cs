using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Phase 1 — news arrives; each actor's believed world updates.
/// Slice-A stub: perfect information, the view is rebuilt from truth every
/// step. Compressed belief and news pulses replace this in Slice I; the
/// contract (Intent reads only the view) holds either way.</summary>
public sealed class PerceptionPhase : ISimPhase
{
    public string Name => "Perception";

    public string Run(SimState state)
    {
        var known = new List<int>();
        foreach (var a in state.Actors)
            if (a.Entered)
                known.Add(a.Id);
        int perceiving = 0;
        foreach (var a in state.Actors)
        {
            if (!a.Entered) continue;
            double expansion = a.Kind == ActorKind.Polity
                ? state.PolityOf(a.Id).ExpansionPoints : 0.0;
            var candidates = a.Kind == ActorKind.Polity
                ? ColonyValuation.CandidatesFor(state, a.Id) : null;
            Galaxy.SpeciesProfile? selfSpecies = null;
            int ownPorts = 0;
            if (a.Kind == ActorKind.Polity)
            {
                int sp = state.PolityOf(a.Id).SpeciesId;
                if (sp >= 0 && sp < state.Skeleton.Species.Count)
                    selfSpecies = state.Skeleton.Species[sp];
                foreach (var p in state.Ports)
                    if (p.OwnerActorId == a.Id) ownPorts++;
            }
            a.Perception = new PerceptionView(a.Id, state.WorldYear, known,
                                              expansion, candidates, selfSpecies,
                                              ownPorts);
            perceiving++;
        }
        return $"{perceiving} actors perceive (perfect-info stub)";
    }
}

/// <summary>Phase 2 — the market step in the design's fixed order
/// (economy/markets.md): supply lands → demand assembles → price adjusts →
/// freight moves → clearing and consequences. Slice D task 2 lands supply;
/// the remaining sub-steps attach in order behind it.</summary>
public sealed class MarketsPhase : ISimPhase
{
    public string Name => "Markets";

    public string Run(SimState state)
    {
        if (state.Markets.Count == 0) return "no markets yet";
        foreach (var m in state.Markets)
            System.Array.Clear(m.LastCleared, 0, m.LastCleared.Length);
        var scratch = new MarketStepScratch(state);
        MarketEngine.SupplyLands(state, scratch);
        MarketEngine.AssembleDemand(state, scratch);
        MarketEngine.AddConstructionPull(state, scratch);
        MarketEngine.AddReExportDemand(state, scratch);
        // freight before the price drift: the drift reads realized supply —
        // an import-fed port prices its arrivals, a blockaded one their
        // absence (markets.md §The market step; amended in slice D)
        int shipments = MarketEngine.MoveFreight(state, scratch);
        MarketEngine.AdjustPrices(state, scratch);
        int famines = MarketEngine.Clear(state, scratch);
        MarketEngine.DistributePools(state, scratch);
        int producing = 0;
        foreach (var f in state.Facilities)
            if (MarketEngine.IsActive(state, f)) producing++;
        string note = $"{producing} facilities supply {state.Markets.Count} markets";
        if (shipments > 0)
            note += $", {shipments} " + (shipments == 1 ? "shipment" : "shipments");
        if (famines > 0)
            note += $", {famines} " + (famines == 1 ? "famine" : "famines");
        return note;
    }
}

/// <summary>Phase 3 — standing policies applied mechanically. Slice D: real
/// market income (tax + tariffs + facility revenue, accrued into Credits by
/// the Markets phase) is split by the standing budget weights into the
/// investment treasuries; the development treasury builds lanes, raises port
/// tiers, and executes facility construction against C's siting scores × the
/// local price signal, consuming real goods. Upkeep gates condition, reserves
/// decay by perishability, and simple credit closes the ledger: insolvents
/// borrow from whoever holds surplus, unpayable loans default and seize
/// collateral (assets-and-investment.md, economy/markets.md §Credit).</summary>
public sealed class AllocationPhase : ISimPhase
{
    public string Name => "Allocation";

    public string Run(SimState state)
    {
        var cfg = state.Config;
        int earning = 0, lanesBuilt = 0, portsRaised = 0, facilitiesBuilt = 0;
        int defaults = ServiceLoans(state);
        var ownPorts = new List<Port>();
        foreach (var pr in state.Polities)                    // actor-id order
        {
            var actor = state.Actors[pr.ActorId];
            if (!actor.Entered) continue;
            ownPorts.Clear();
            foreach (var p in state.Ports)
                if (p.OwnerActorId == pr.ActorId) ownPorts.Add(p);
            if (ownPorts.Count == 0) continue;
            earning++;
            var budget = (actor.Policies as PolityPolicies ?? PolityPolicies.Default).Budget;
            double allocatable = Math.Max(0.0, pr.Credits);
            pr.ExpansionPoints += allocatable * budget.Expansion;
            pr.DevelopmentPoints += allocatable * budget.Development;
            pr.Credits -= allocatable * (budget.Expansion + budget.Development);
            lanesBuilt += BuildLanes(state, pr, ownPorts);
            portsRaised += RaisePorts(state, pr, ownPorts);
            facilitiesBuilt += BuildFacilities(state, pr, ownPorts);
            RunUpkeep(state, pr);
            DecayReserves(state, pr);
        }
        int borrowed = Borrow(state);
        string note = earning == 0 ? "quiet"
            : $"income allocated for {earning} " + (earning == 1 ? "polity" : "polities");
        if (lanesBuilt > 0) note += $", {lanesBuilt} " + (lanesBuilt == 1 ? "lane built" : "lanes built");
        if (portsRaised > 0) note += $", {portsRaised} " + (portsRaised == 1 ? "port raised" : "ports raised");
        if (facilitiesBuilt > 0) note += $", {facilitiesBuilt} " + (facilitiesBuilt == 1 ? "facility built" : "facilities built");
        if (borrowed > 0) note += $", {borrowed} " + (borrowed == 1 ? "loan issued" : "loans issued");
        if (defaults > 0) note += $", {defaults} " + (defaults == 1 ? "default" : "defaults");
        return note;
    }

    /// <summary>Producer types buildable by polity investment — the keystone
    /// port comes from colonization, support types await their consumers (H).</summary>
    private static readonly Substrate.InfraTypeId[] BuildableTypes =
    {
        Substrate.InfraTypeId.Mine, Substrate.InfraTypeId.Skimmer,
        Substrate.InfraTypeId.AgriComplex, Substrate.InfraTypeId.ExcavationSite,
        Substrate.InfraTypeId.Refinery, Substrate.InfraTypeId.Chemworks,
        Substrate.InfraTypeId.Fabricator, Substrate.InfraTypeId.ExoticsLab,
        Substrate.InfraTypeId.Foundry, Substrate.InfraTypeId.Shipyard,
        Substrate.InfraTypeId.Arsenal, Substrate.InfraTypeId.ComputeCore,
    };

    /// <summary>One facility per port per epoch, best siting score × price
    /// signal, paid from the development treasury (treasury back to liquid
    /// credits — the real cost is the goods), consuming the build cost from
    /// the port market (assets-and-investment.md §Construction).</summary>
    private static int BuildFacilities(SimState state, PolityRecord pr,
                                       List<Port> ownPorts)
    {
        var cfg = state.Config;
        var eco = cfg.Economy;
        int built = 0;
        foreach (var port in ownPorts)                        // id order (P6)
        {
            int cap = port.Tier * cfg.Infrastructure.FacilitiesPerPortTier;
            int attached = 0;
            foreach (var f in state.Facilities)
                if (f.OwnerActorId == pr.ActorId
                    && MarketEngine.AttachedMarketIndex(state, f) == port.Id)
                    attached++;
            if (attached >= cap) continue;
            var market = state.Markets[port.Id];
            var workforce = MarketEngine.EmbodimentOf(state, pr.SpeciesId);

            Substrate.InfraTypeId bestType = Substrate.InfraTypeId.Port;
            HexCoordinate bestHex = port.Hex;
            double bestScore = 0.12;                          // don't build junk
            foreach (var cell in state.Skeleton.Cells)        // spiral order (P6)
            {
                var center = HexGrid.CellCenter(cell.Coord);
                if (HexGrid.Distance(port.Hex, center)
                    > PortDomains.ServiceRadius(cfg, port.Tier)) continue;
                if (cell.IsVoid) continue;
                var fields = MarketEngine.FieldsAt(state, center);
                var site = new Substrate.CellSite(fields,
                    Connectivity: Math.Min(1.0, LaneCount(state, port.Id) / 4.0),
                    IsPortHeart: cell.Coord.Equals(HexGrid.CellOf(port.Hex)),
                    PortTier: port.Tier, DevelopmentTier: port.Tier,
                    IsChokepoint: cell.IsChokepoint);
                foreach (var type in BuildableTypes)
                {
                    var def = Substrate.Infrastructure.Get(type);
                    double signal = PriceSignal(eco, market, def);
                    double score = Substrate.Siting.Score(type, site, workforce)
                                   * signal;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestType = type;
                        bestHex = PickHex(state, cell, center);
                    }
                }
            }
            if (bestType == Substrate.InfraTypeId.Port) continue;

            // full build cost must be present and affordable — a facility is
            // a pre-commitment, not an IOU
            var buildDef = Substrate.Infrastructure.Get(bestType);
            double value = 0;
            bool available = true;
            foreach (var q in buildDef.BuildCost)
            {
                if (market.Inventory[(int)q.Good] < q.Quantity) available = false;
                value += q.Quantity * market.Price[(int)q.Good];
            }
            if (!available || pr.DevelopmentPoints < value) continue;
            foreach (var q in buildDef.BuildCost)
            {
                market.Draw((int)q.Good, q.Quantity);
                market.LastCleared[(int)q.Good] += q.Quantity;
            }
            pr.DevelopmentPoints -= value;
            MarketEngine.PayWages(state, port.Id, value);  // construction wages
            var facility = new Facility(state.Facilities.Count, (int)bestType,
                tier: 1, bestHex, pr.ActorId, state.WorldYear);
            state.Facilities.Add(facility);
            built++;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.FacilityBuilt,
                new[] { pr.ActorId }, bestHex, Magnitude: 1.0, Valence: 1.0,
                EventVisibility.Regional,
                new FacilityBuiltPayload(facility.Id, facility.TypeId, 1)));
        }
        return built;
    }

    /// <summary>Mean price-over-founding ratio of the type's products,
    /// clamped — scarcity builds its own relief.</summary>
    private static double PriceSignal(EconomyKnobs eco, Market market,
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
    private static HexCoordinate PickHex(SimState state, Galaxy.RegionCell cell,
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

    private static int LaneCount(SimState state, int portId)
    {
        int count = 0;
        foreach (var l in state.Lanes)
            if (l.PortAId == portId || l.PortBId == portId) count++;
        return count;
    }

    /// <summary>Upkeep drawn from the attached market; unmet upkeep decays
    /// condition, met upkeep restores it — output scales with condition
    /// (assets-and-investment.md §Condition).</summary>
    private static void RunUpkeep(SimState state, PolityRecord pr)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var f in state.Facilities)                   // id order (P6)
        {
            if (f.OwnerActorId != pr.ActorId) continue;
            if (!MarketEngine.IsActive(state, f)) continue;
            int mIx = MarketEngine.AttachedMarketIndex(state, f);
            if (mIx < 0) continue;
            var market = state.Markets[mIx];
            var def = Substrate.Infrastructure.Get((Substrate.InfraTypeId)f.TypeId);
            double scale = Substrate.Production.TierCostFactor(f.Tier) * years;
            double met = 1.0;
            foreach (var q in def.UpkeepPerYear)
            {
                double need = q.Quantity * scale;
                if (need <= 0) continue;
                double drawn = market.Draw((int)q.Good, need);
                market.LastCleared[(int)q.Good] += drawn;
                met = Math.Min(met, drawn / need);
            }
            if (met >= 1.0)
                f.Condition = Math.Min(1.0,
                    f.Condition + eco.ConditionRecoveryPerYear * years);
            else
                f.Condition = Math.Max(0.05,
                    f.Condition - eco.ConditionDecayPerYear * years * (1.0 - met));
        }
    }

    /// <summary>Perishability: provisions rot, medicine ages, durables keep —
    /// reserves are a real cost, not free insurance.</summary>
    private static void DecayReserves(SimState state, PolityRecord pr)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        for (int g = 0; g < pr.ReserveQty.Length; g++)
        {
            if (pr.ReserveQty[g] <= 0) continue;
            double perish = (Substrate.GoodId)g switch
            {
                Substrate.GoodId.Provisions => 10.0,
                Substrate.GoodId.Organics => 5.0,
                Substrate.GoodId.Medicine => 3.0,
                _ => 1.0,
            };
            double keep = Math.Max(0.0,
                1.0 - eco.StockpileDecayPerYear * perish * years);
            pr.ReserveQty[g] *= keep;
            if (pr.ReserveQty[g] <= 0) pr.ReserveGrade[g] = 0;
        }
    }

    /// <summary>Interest and amortization flow lender-ward; a borrower who
    /// cannot pay at all defaults: the loan closes, collateral transfers
    /// (a lender can end up owning a foreign mine). Returns defaults.</summary>
    private static int ServiceLoans(SimState state)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        int defaults = 0;
        foreach (var loan in state.Loans)                     // id order (P6)
        {
            if (loan.Closed || loan.Principal <= 0) continue;
            var borrower = state.PolityOf(loan.BorrowerActorId);
            var lender = state.PolityOf(loan.LenderActorId);
            double interest = loan.Principal * loan.RatePerYear * years;
            double amort = loan.Principal * Math.Min(1.0, (double)years / loan.TermYears);
            double payment = interest + amort;
            if (borrower.Credits >= payment)
            {
                borrower.Credits -= payment;
                lender.Credits += payment;
                loan.Principal -= amort;
                if (loan.Principal <= 1e-9) loan.Closed = true;
            }
            else if (borrower.Credits > 0)
            {
                // partial: pay what exists, capitalize the missed interest
                lender.Credits += borrower.Credits;
                loan.Principal += interest - Math.Min(interest, borrower.Credits);
                borrower.Credits = 0;
            }
            else
            {
                loan.Closed = true;
                defaults++;
                Facility? seized = null;
                foreach (var f in state.Facilities)
                    if (f.OwnerActorId == loan.BorrowerActorId) { seized = f; break; }
                if (seized != null) seized.OwnerActorId = loan.LenderActorId;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.LoanDefaulted,
                    new[] { loan.BorrowerActorId, loan.LenderActorId },
                    state.Actors[loan.BorrowerActorId].Seat,
                    Magnitude: loan.Principal, Valence: -1.0,
                    EventVisibility.Public,
                    new LoanDefaultedPayload(loan.Id, loan.LenderActorId,
                                             loan.BorrowerActorId)));
            }
        }
        return defaults;
    }

    /// <summary>Insolvent polities borrow from whoever holds surplus — the
    /// richest entered polity able to front the principal twice over.</summary>
    private static int Borrow(SimState state)
    {
        var eco = state.Config.Economy;
        int issued = 0;
        foreach (var pr in state.Polities)                    // actor-id order
        {
            if (!state.Actors[pr.ActorId].Entered || pr.Credits >= 0) continue;
            double principal = -pr.Credits * 1.2;
            PolityRecord? lender = null;
            foreach (var candidate in state.Polities)
                if (candidate.ActorId != pr.ActorId
                    && state.Actors[candidate.ActorId].Entered
                    && candidate.Credits >= principal * 2
                    && (lender == null || candidate.Credits > lender.Credits))
                    lender = candidate;
            if (lender == null) continue;
            lender.Credits -= principal;
            pr.Credits += principal;
            var loan = new Loan(state.Loans.Count, lender.ActorId, pr.ActorId,
                principal, eco.LoanRatePerYear, eco.LoanTermYears, state.WorldYear);
            state.Loans.Add(loan);
            issued++;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.LoanIssued,
                new[] { pr.ActorId, lender.ActorId },
                state.Actors[pr.ActorId].Seat,
                Magnitude: principal, Valence: 0.0, EventVisibility.Regional,
                new LoanIssuedPayload(loan.Id, lender.ActorId, pr.ActorId,
                                      principal)));
        }
        return issued;
    }

    /// <summary>Missing in-range same-owner pairs, nearest first (tie: lower
    /// ids), built while the development treasury affords them.</summary>
    private static int BuildLanes(SimState state, PolityRecord pr, List<Port> ownPorts)
    {
        var cfg = state.Config;
        int built = 0;
        while (pr.DevelopmentPoints >= cfg.Expansion.LaneCost)
        {
            Port? bestA = null, bestB = null;
            int bestDist = int.MaxValue;
            for (int i = 0; i < ownPorts.Count; i++)
                for (int j = i + 1; j < ownPorts.Count; j++)
                {
                    var a = ownPorts[i]; var b = ownPorts[j];
                    if (a.Id > b.Id) (a, b) = (b, a);
                    if (!LaneMath.InRange(cfg, a, b)) continue;
                    if (LaneExists(state, a.Id, b.Id)) continue;
                    int dist = HexGrid.Distance(a.Hex, b.Hex);
                    if (dist < bestDist
                        || (dist == bestDist && (a.Id < bestA!.Id
                            || (a.Id == bestA.Id && b.Id < bestB!.Id))))
                    { bestDist = dist; bestA = a; bestB = b; }
                }
            if (bestA == null) break;
            pr.DevelopmentPoints -= cfg.Expansion.LaneCost;
            // lane budgets pay the two ports' construction crews
            MarketEngine.PayWages(state, bestA.Id, cfg.Expansion.LaneCost * 0.5);
            MarketEngine.PayWages(state, bestB!.Id, cfg.Expansion.LaneCost * 0.5);
            var lane = new Lane(state.Lanes.Count, bestA.Id, bestB.Id, state.WorldYear);
            state.Lanes.Add(lane);
            built++;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.LaneOpened,
                new[] { pr.ActorId }, Midpoint(bestA.Hex, bestB.Hex),
                Magnitude: 1.0, Valence: 1.0, EventVisibility.Regional,
                new LaneOpenedPayload(bestA.Id, bestB.Id)));
        }
        return built;
    }

    /// <summary>Lowest-tier port first (tie: lowest id); cost = base × current
    /// tier; raised while affordable.</summary>
    private static int RaisePorts(SimState state, PolityRecord pr, List<Port> ownPorts)
    {
        var cfg = state.Config;
        int raised = 0;
        while (true)
        {
            Port? pick = null;
            foreach (var p in ownPorts)
                if (p.Tier < cfg.Infrastructure.MaxPortTier
                    && (pick == null || p.Tier < pick.Tier
                        || (p.Tier == pick.Tier && p.Id < pick.Id)))
                    pick = p;
            if (pick == null) break;
            double cost = cfg.Expansion.PortUpgradeCostBase * pick.Tier;
            if (pr.DevelopmentPoints < cost) break;
            pr.DevelopmentPoints -= cost;
            MarketEngine.PayWages(state, pick.Id, cost);   // builders get paid
            pick.Tier++;
            raised++;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.PortTierRaised,
                new[] { pr.ActorId }, pick.Hex,
                Magnitude: pick.Tier, Valence: 1.0, EventVisibility.Regional,
                new PortTierRaisedPayload(pick.Id, pick.Tier)));
        }
        return raised;
    }

    private static bool LaneExists(SimState state, int aId, int bId)
    {
        foreach (var l in state.Lanes)
            if (l.PortAId == aId && l.PortBId == bId) return true;
        return false;
    }

    /// <summary>Hex-line midpoint (cube lerp at t=0.5) — the lane-opened
    /// event's address.</summary>
    private static HexCoordinate Midpoint(HexCoordinate a, HexCoordinate b) =>
        HexGrid.Round((a.Q + b.Q) * 0.5, (a.R + b.R) * 0.5);
}

/// <summary>Phase 4 — the one controller touchpoint (P2): every entered
/// decision-making actor emits policies + acts from its perceived state.</summary>
public sealed class IntentPhase : ISimPhase
{
    public string Name => "Intent";

    public string Run(SimState state)
    {
        state.Decisions.Clear();
        int acts = 0;
        foreach (var a in state.Actors)
        {
            if (!a.Entered) continue;
            var decision = a.Controller.Decide(a.Perception!);
            a.Policies = decision.Policies;   // standing policies: next step's
                                              // Allocation applies them (Move 1)
            state.Decisions.Add(new ActorDecision(a.Id, decision));
            acts += decision.Acts.Count;
        }
        return $"{state.Decisions.Count} decisions, {acts} acts";
    }
}

/// <summary>Phase 5 — acts collide and resolve deterministically. Slice B
/// resolves FoundColonyAct: claiming space is building a port
/// (space-and-travel.md) — convoyless until slice E gives the journey hulls.
/// Collisions on one hex resolve in actor-id order; losers are not charged.</summary>
public sealed class ResolutionPhase : ISimPhase
{
    public string Name => "Resolution";

    public string Run(SimState state)
    {
        int acts = 0, founded = 0;
        foreach (var d in state.Decisions)               // actor-id order
            foreach (var act in d.Decision.Acts)
            {
                acts++;
                if (act is FoundColonyAct f && TryFound(state, f)) founded++;
            }
        return founded == 0 ? $"{acts} acts, 0 resolved"
            : $"{acts} acts, {founded} " + (founded == 1 ? "port established" : "ports established");
    }

    /// <summary>Every check runs against truth: consequences on truth, even
    /// though the decision ran on perception (Move 2).</summary>
    private static bool TryFound(SimState state, FoundColonyAct act)
    {
        var cfg = state.Config;
        var actor = state.Actors[act.ActorId];
        if (!actor.Entered || actor.Kind != ActorKind.Polity) return false;
        var record = state.PolityOf(act.ActorId);
        if (record.ExpansionPoints < cfg.Expansion.ColonyCost) return false;
        if (!state.Skeleton.TryGetCell(HexGrid.CellOf(act.Target), out var cell)
            || cell.IsVoid) return false;
        foreach (var p in state.Ports)
            if (p.Hex.Equals(act.Target)) return false;   // hex taken (or lost the collision)
        bool inReach = false;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == act.ActorId
                && HexGrid.Distance(p.Hex, act.Target) <= cfg.Expansion.ColonizationReachHexes)
            { inReach = true; break; }
        if (!inReach) return false;

        record.ExpansionPoints -= cfg.Expansion.ColonyCost;
        var port = new Port(state.Ports.Count, act.ActorId, act.Target,
                            tier: 1, state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, cfg.Economy));
        state.Segments.Add(new PopulationSegment(state.Segments.Count, port.Id,
            record.SpeciesId, record.SpeciesId, cfg.Expansion.ColonySegmentSize)
        {
            // the expedition cost recycles to the settlers — treasury
            // spending is somebody's income, never destroyed (P4)
            Wealth = cfg.Expansion.ColonyCost,
        });
        // settlers farm first: every colony founds with a subsistence farm
        // (expedition furniture, like the homeworld starter industry)
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)Substrate.InfraTypeId.AgriComplex, tier: 1, act.Target,
            act.ActorId, state.WorldYear));
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.PortEstablished,
            new[] { act.ActorId }, act.Target, Magnitude: 1.0, Valence: 1.0,
            EventVisibility.Public, new PortEstablishedPayload(actor.Name, port.Id)));
        return true;
    }
}

/// <summary>Phase 6 — interiors and demographics. Slice B carries the stub
/// emergence schedule (frame/time.md §Asymmetric emergence) and homeworld
/// founding: a polity enters by establishing its first port at its seat —
/// homeworlds are simply the first ports (space-and-travel.md).</summary>
public sealed class InteriorPhase : ISimPhase
{
    public string Name => "Interior";

    /// <summary>Homeworld starter facilities, extraction before processing so
    /// the chain flows within one market step (facility id = run order).</summary>
    private static readonly StarGen.Core.Substrate.InfraTypeId[] StarterIndustry =
    {
        StarGen.Core.Substrate.InfraTypeId.AgriComplex,
        StarGen.Core.Substrate.InfraTypeId.Mine,
        StarGen.Core.Substrate.InfraTypeId.Skimmer,
        StarGen.Core.Substrate.InfraTypeId.Refinery,
        StarGen.Core.Substrate.InfraTypeId.Foundry,
    };

    public string Run(SimState state)
    {
        // segments founded by this step's entries integrate from the next step
        int preexisting = state.Segments.Count;
        int entered = 0;
        foreach (var a in state.Actors)
        {
            if (a.Entered || a.EntryEpoch > state.EpochIndex) continue;
            a.Entered = true;
            entered++;
            var port = new Port(state.Ports.Count, a.Id, a.Seat,
                state.Config.Infrastructure.HomeworldPortTier, state.WorldYear);
            state.Ports.Add(port);
            state.Markets.Add(new Market(port.Id, state.Config.Economy));
            int species = state.PolityOf(a.Id).SpeciesId;
            state.Segments.Add(new PopulationSegment(state.Segments.Count, port.Id,
                species, species, state.Config.Expansion.HomeworldSegmentSize)
            {
                Wealth = state.Config.Expansion.HomeworldSegmentSize
                         * state.Config.Economy.InitialWealthPerPop,
            });
            // a civilization at spaceflight arrives with industry: the
            // starter chain (raw → alloys/fuel → machinery) and the one-time
            // credit endowment — the only mint; conserved thereafter (P4)
            state.PolityOf(a.Id).Credits +=
                state.Config.Economy.InitialCreditsPerPolity;
            foreach (var t in StarterIndustry)
                state.Facilities.Add(new Facility(state.Facilities.Count,
                    (int)t, tier: 1, a.Seat, a.Id, state.WorldYear));
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.PolityEmerged,
                new[] { a.Id }, a.Seat, Magnitude: 1.0, Valence: 1.0,
                EventVisibility.Public, new PolityEmergedPayload(a.Name)));
        }

        int grown = 0;
        var cfg = state.Config.Expansion;
        for (int i = 0; i < preexisting; i++)             // id order (P6)
        {
            var seg = state.Segments[i];
            double cap = state.Ports[seg.PortId].Tier * cfg.SegmentCapPerTier;
            if (seg.Size <= 0 || cap <= 0) continue;
            double step = seg.Size * cfg.SegmentGrowthPerYear
                          * state.Config.Sim.YearsPerEpoch * (1.0 - seg.Size / cap);
            if (step == 0) continue;
            seg.Size = System.Math.Min(cap, seg.Size + step);
            grown++;
        }

        string note = entered switch
        {
            0 => "quiet",
            1 => "1 polity enters",
            _ => $"{entered} polities enter",
        };
        if (grown > 0)
            note += $", {grown} " + (grown == 1 ? "segment grows" : "segments grow");
        return note;
    }
}

/// <summary>Phase 7 — events finalized with world-years and appended to the
/// one log. News pulses and map residue attach in later slices; chronicle
/// runs last so next step's news is this step's history.</summary>
public sealed class ChroniclePhase : ISimPhase
{
    public string Name => "Chronicle";

    public string Run(SimState state)
    {
        foreach (var e in state.Staged)
            state.Log.Append(state.WorldYear, e.Stratum, e.Type, e.Actors,
                             e.Location, e.Magnitude, e.Valence, e.Visibility,
                             e.Payload);
        int count = state.Staged.Count;
        state.Staged.Clear();
        return count == 1 ? "1 event finalized" : $"{count} events finalized";
    }
}
