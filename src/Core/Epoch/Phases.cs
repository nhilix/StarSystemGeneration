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
        // headline war weights once per polity — the briefs size threats
        // and protectors by them (perfect-info stub until slice I)
        var strengths = new Dictionary<int, double>();
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity)
                strengths[a.Id] = FleetOps.WarStrength(state, a.Id);
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
            double realmSubsistence = 1.0;
            List<DesignBrief>? designs = null;
            if (a.Kind == ActorKind.Polity)
            {
                designs = CurrentDesignBriefs(state, a.Id);
                int sp = state.PolityOf(a.Id).SpeciesId;
                if (sp >= 0 && sp < state.Skeleton.Species.Count)
                    selfSpecies = state.Skeleton.Species[sp];
                foreach (var p in state.Ports)
                    if (p.OwnerActorId == a.Id) ownPorts++;
                double sizeSum = 0, subSum = 0;
                foreach (var s in state.Segments)
                {
                    if (s.Size <= 0
                        || state.Ports[s.PortId].OwnerActorId != a.Id) continue;
                    sizeSum += s.Size;
                    subSum += s.LastSubsistence * s.Size;
                }
                if (sizeSum > 0) realmSubsistence = subSum / sizeSum;
            }
            var temperament = a.Kind == ActorKind.Polity
                ? Temperament.Compose(state, state.PolityOf(a.Id))
                : Temperament.Neutral;
            double ownCredits = 0;
            List<CorporateBrief>? hosted = null;
            List<RelationBrief>? relations = null;
            List<WarBrief>? wars = null;
            if (a.Kind == ActorKind.Polity)
            {
                ownCredits = state.PolityOf(a.Id).Credits;
                foreach (var corp in state.Corporations)
                    if (corp.Active && corp.HostPolityId == a.Id)
                        (hosted ??= new List<CorporateBrief>())
                            .Add(new CorporateBrief(corp.Id, corp.Name,
                                                    corp.Credits));
                relations = BuildRelationBriefs(state, a.Id, strengths);
                foreach (var war in state.Wars)   // id order (P6)
                {
                    if (!war.Active || !war.Involves(a.Id)) continue;
                    bool attackerSide = war.OnAttackerSide(a.Id);
                    double atStart = attackerSide
                        ? war.AttackerStrengthAtStart
                        : war.DefenderStrengthAtStart;
                    int taken = 0;
                    foreach (var o in war.Objectives)
                        if (o.Status == ObjectiveStatus.Taken) taken++;
                    (wars ??= new List<WarBrief>()).Add(new WarBrief(
                        war.Id, war.Name,
                        attackerSide ? war.DefenderId : war.AttackerId,
                        attackerSide,
                        a.Id == war.AttackerId || a.Id == war.DefenderId,
                        attackerSide ? war.AttackerExhaustion
                            : war.DefenderExhaustion,
                        atStart <= 0 ? 1.0
                            : WarOps.SideStrength(state, war, attackerSide)
                              / atStart,
                        taken, war.Objectives.Count));
                }
            }
            a.Perception = new PerceptionView(a.Id, state.WorldYear, known,
                                              expansion, candidates, selfSpecies,
                                              ownPorts, realmSubsistence, designs,
                                              FleetOps.ColonyHullsInReserve(state, a.Id),
                                              temperament, ownCredits, hosted,
                                              relations,
                                              strengths.TryGetValue(a.Id,
                                                  out double own) ? own : 0,
                                              a.Kind == ActorKind.Polity
                                                  && RelationsOps.IsDynastic(
                                                      state, a.Id),
                                              wars);
            perceiving++;
        }
        return $"{perceiving} actors perceive (perfect-info stub)";
    }

    /// <summary>One polity's relation briefs: the gauges, the table state,
    /// the casus-belli menu, and the mechanical objective enumeration
    /// (choosing is the controller's — P2).</summary>
    private static List<RelationBrief>? BuildRelationBriefs(SimState state,
        int selfId, Dictionary<int, double> strengths)
    {
        List<RelationBrief>? relations = null;
        foreach (var rel in state.Relations)                  // creation order (P6)
        {
            if (!rel.Involves(selfId)
                || !RelationsOps.BothLive(state, rel)) continue;
            int held = 0, against = 0;
            foreach (var c in rel.Claims)
                if (!c.Released)
                {
                    if (c.HolderPolityId == selfId) held++;
                    else against++;
                }
            int other = rel.OtherOf(selfId);
            var menu = new List<CasusBelliOption>();
            foreach (var (cause, subject) in WarOps.Menu(state, selfId, other))
                menu.Add(new CasusBelliOption(cause, subject));
            (relations ??= new List<RelationBrief>())
                .Add(new RelationBrief(other, rel.Warmth,
                    rel.Tension, rel.Rung, rel.OfferedRung,
                    rel.OfferedById, held, against,
                    RelationsOps.IdeologyGap(state.PolityOf(selfId),
                                             state.PolityOf(other)),
                    rel.RungEpoch < 0 ? 0
                        : state.EpochIndex - rel.RungEpoch,
                    strengths.TryGetValue(other, out double os) ? os : 0,
                    rel.VassalPolityId,
                    RelationsOps.IsDynastic(state, other),
                    rel.DynasticTies,
                    menu,
                    DefensiveStrength(state, other, strengths),
                    ObjectiveCandidates(state, selfId, other)));
        }
        return relations;
    }

    /// <summary>What an attacker prices: the target plus everyone bound to
    /// defend it — defense-alliance partners, its vassals, its overlord.</summary>
    private static double DefensiveStrength(SimState state, int polityId,
                                            Dictionary<int, double> strengths)
    {
        double total = strengths.TryGetValue(polityId, out double own) ? own : 0;
        foreach (var rel in state.Relations)                  // creation order (P6)
        {
            if (!rel.Involves(polityId)
                || !RelationsOps.BothLive(state, rel)) continue;
            int other = rel.OtherOf(polityId);
            if (rel.Rung == TreatyRung.DefenseAlliance
                || rel.VassalPolityId >= 0)
                total += strengths.TryGetValue(other, out double s) ? s : 0;
        }
        return total;
    }

    /// <summary>Mechanical war-target enumeration: the other side's nearest
    /// ports (chokepoints first), its busiest lane, and its navy — what a
    /// declaration's objective set is picked from.</summary>
    private static List<WarObjectiveSpec> ObjectiveCandidates(SimState state,
        int selfId, int otherId)
    {
        var candidates = new List<WarObjectiveSpec>();
        var ports = new List<(bool Chokepoint, int Distance, int Id)>();
        foreach (var target in state.Ports)                   // id order (P6)
        {
            if (target.OwnerActorId != otherId) continue;
            int best = int.MaxValue;
            foreach (var own in state.Ports)
                if (own.OwnerActorId == selfId)
                {
                    int d = HexGrid.Distance(own.Hex, target.Hex);
                    if (d < best) best = d;
                }
            bool chokepoint = state.Skeleton.TryGetCell(
                HexGrid.CellOf(target.Hex), out var cell) && cell.IsChokepoint;
            ports.Add((chokepoint, best, target.Id));
        }
        ports.Sort((x, y) => x.Chokepoint != y.Chokepoint
            ? (x.Chokepoint ? -1 : 1)
            : x.Distance != y.Distance ? x.Distance.CompareTo(y.Distance)
            : x.Id.CompareTo(y.Id));
        for (int i = 0; i < ports.Count && i < 3; i++)
            candidates.Add(new WarObjectiveSpec(WarObjectiveType.CapturePort,
                                                ports[i].Id));
        Lane? busiest = null;
        double busiestCapacity = 0;
        foreach (var lane in state.Lanes)                     // id order (P6)
        {
            if (state.Ports[lane.PortAId].OwnerActorId != otherId
                && state.Ports[lane.PortBId].OwnerActorId != otherId) continue;
            double capacity = FleetOps.PostedCapacity(state, lane);
            if (capacity > busiestCapacity)
            { busiestCapacity = capacity; busiest = lane; }
        }
        if (busiest != null)
            candidates.Add(new WarObjectiveSpec(WarObjectiveType.BlockadeLane,
                                                busiest.Id));
        candidates.Add(new WarObjectiveSpec(WarObjectiveType.DestroyFleet,
                                            otherId));
        return candidates;
    }

    /// <summary>Current-mark designs per chassis cell, design-id order —
    /// the briefs ShipbuildingPriorities are keyed by.</summary>
    private static List<DesignBrief> CurrentDesignBriefs(SimState state, int actorId)
    {
        var currentByCell = new Dictionary<(ShipRole, ShipSize), ShipDesign>();
        foreach (var d in state.Designs)                  // id order (P6)
        {
            if (d.OwnerActorId != actorId) continue;
            if (!currentByCell.TryGetValue((d.Role, d.Size), out var held)
                || d.Mark > held.Mark)
                currentByCell[(d.Role, d.Size)] = d;
        }
        var briefs = new List<DesignBrief>(currentByCell.Count);
        foreach (var d in currentByCell.Values)
            briefs.Add(new DesignBrief(d.Id, d.Role, d.Size, d.Mark));
        briefs.Sort((x, y) => x.DesignId.CompareTo(y.DesignId));
        return briefs;
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
        foreach (var pr in state.Polities) pr.Receipts = 0;
        foreach (var corp in state.Corporations) corp.Receipts = 0;
        var scratch = new MarketStepScratch(state);
        MarketEngine.SupplyLands(state, scratch);
        MarketEngine.AssembleDemand(state, scratch);
        MarketEngine.AddIndustrialDemand(state, scratch);
        MarketEngine.AddConstructionPull(state, scratch);
        MarketEngine.AddMilitaryDemand(state, scratch);
        MarketEngine.AddResearchDemand(state, scratch);
        CorporationOps.AddCorporateDemand(state, scratch);
        FleetOps.AddUpkeepDemand(state, scratch);
        MarketEngine.AddReExportDemand(state, scratch);
        // freight before the price drift: the drift reads realized supply —
        // an import-fed port prices its arrivals, a blockaded one their
        // absence (markets.md §The market step; amended in slice D)
        var (shipments, units) = MarketEngine.MoveFreight(state, scratch);
        MarketEngine.AdjustPrices(state, scratch);
        int famines = MarketEngine.Clear(state, scratch);
        MarketEngine.DistributePools(state, scratch);
        int producing = 0;
        foreach (var f in state.Facilities)
            if (MarketEngine.IsActive(state, f)
                && MarketEngine.AttachedMarketIndex(state, f) >= 0) producing++;
        string note = $"{producing} facilities supply {state.Markets.Count} markets";
        if (shipments > 0)
            note += System.FormattableString.Invariant(
                $", {shipments} ") + (shipments == 1 ? "shipment" : "shipments")
                + System.FormattableString.Invariant($" ({units:0} units)");
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
        int hullsLaid = 0, hullsLost = 0;
        int defaults = ServiceLoans(state);
        // tribute ships up before anyone budgets: vassals allocate what
        // remains of their receipts (interpolity/relations.md §Vassalage)
        int tributes = FederationOps.PayTribute(state);
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
            var policies = actor.Policies as PolityPolicies ?? PolityPolicies.Default;
            // standing weights bend toward strong factions' agendas before
            // they spend — pressure is mechanical, bounded by form tolerance
            var budget = FactionOps.PressedBudget(state, pr, policies.Budget);
            // budget the epoch's receipts, not the balance: development is
            // deficit-financed through downturns; credit picks up the slack
            double allocatable = Math.Max(0.0, Math.Max(pr.Credits, pr.Receipts));
            pr.ExpansionPoints += allocatable * budget.Expansion;
            pr.DevelopmentPoints += allocatable * budget.Development;
            pr.MilitaryPoints += allocatable * budget.Military;
            pr.Credits -= allocatable
                * (budget.Expansion + budget.Development + budget.Military);
            // the appeasement line buys factions off — a treasury→faction
            // flow, conserved (P4); without factions the line stays liquid
            pr.Credits -= FactionOps.SpendAppeasement(state, pr,
                allocatable * budget.Appeasement, allocatable);
            // research: the standing split converts exotics × compute into
            // ladder progress; the spend recycles as lab wages (slice G)
            pr.Credits -= TechOps.Research(state, pr, policies.Research,
                allocatable * budget.Research);
            lanesBuilt += BuildLanes(state, pr, ownPorts);
            portsRaised += RaisePorts(state, pr, ownPorts);
            facilitiesBuilt += BuildFacilities(state, pr, ownPorts);
            hullsLaid += FleetOps.BuildFleets(state, pr, ownPorts);
            FleetOps.ManagePostures(state, pr, ownPorts);
            hullsLost += FleetOps.SupplyFleets(state, pr);
            RunUpkeep(state, pr);
            DecayReserves(state, pr);
        }
        // corporations run their portfolios on the same markets (slice G)
        int corporationsActive = CorporationOps.Operate(state);
        // laggards learn from the goods they buy and the wrecks they find
        TechOps.Diffuse(state);
        int advances = 0;
        foreach (var staged in state.Staged)
            if (staged.Type == WorldEventType.TechAdvanced) advances++;
        int borrowed = Borrow(state);
        string note = earning == 0 ? "quiet"
            : $"income allocated for {earning} " + (earning == 1 ? "polity" : "polities");
        if (lanesBuilt > 0) note += $", {lanesBuilt} " + (lanesBuilt == 1 ? "lane built" : "lanes built");
        if (portsRaised > 0) note += $", {portsRaised} " + (portsRaised == 1 ? "port raised" : "ports raised");
        if (facilitiesBuilt > 0) note += $", {facilitiesBuilt} " + (facilitiesBuilt == 1 ? "facility built" : "facilities built");
        if (hullsLaid > 0) note += $", {hullsLaid} " + (hullsLaid == 1 ? "hull laid down" : "hulls laid down");
        if (hullsLost > 0) note += $", {hullsLost} " + (hullsLost == 1 ? "hull lost" : "hulls lost");
        if (corporationsActive > 0)
            note += $", {corporationsActive} " + (corporationsActive == 1
                ? "corporation operates" : "corporations operate");
        if (advances > 0) note += $", {advances} tech " + (advances == 1 ? "advance" : "advances");
        if (borrowed > 0) note += $", {borrowed} " + (borrowed == 1 ? "loan issued" : "loans issued");
        if (defaults > 0) note += $", {defaults} " + (defaults == 1 ? "default" : "defaults");
        if (tributes > 0) note += $", {tributes} " + (tributes == 1 ? "tribute paid" : "tributes paid");
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
        Substrate.InfraTypeId.Fortress,   // Military-tier-gated (slice H)
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
            double bestScore = cfg.Infrastructure.ConstructionScoreFloor;
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
                    // only candidates the polity can actually build compete:
                    // an unaffordable high scorer must not block the port
                    // (an unbuilt shipyard is not a construction plan)
                    if (!CanAfford(state, pr, market, type)) continue;
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
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestType = type;
                        bestHex = PickHex(state, cell, center);
                    }
                }
            }
            if (bestType == Substrate.InfraTypeId.Port) continue;
            var buildDef = Substrate.Infrastructure.Get(bestType);
            double value = 0;
            foreach (var q in buildDef.BuildCost)
                value += q.Quantity * Market.InitialPrice(eco, q.Good);
            foreach (var q in buildDef.BuildCost)
            {
                double fromMarket = market.Draw((int)q.Good, q.Quantity);
                market.LastCleared[(int)q.Good] += fromMarket;
                double fromReserve = q.Quantity - fromMarket;
                if (fromReserve > 0)
                {
                    pr.ReserveQty[(int)q.Good] =
                        Math.Max(0, pr.ReserveQty[(int)q.Good] - fromReserve);
                    if (pr.ReserveQty[(int)q.Good] <= 0)
                        pr.ReserveGrade[(int)q.Good] = 0;
                }
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

    /// <summary>Full build cost physically present (port market + banked
    /// polity reserves) and the treasury covering the administered value —
    /// a state pre-commitment pays founding prices, so scarcity prices
    /// can't price out the very construction that would cure the scarcity.</summary>
    private static bool CanAfford(SimState state, PolityRecord pr,
                                  Market market, Substrate.InfraTypeId type)
    {
        var def = Substrate.Infrastructure.Get(type);
        double value = 0;
        foreach (var q in def.BuildCost)
        {
            if (market.Inventory[(int)q.Good]
                + pr.ReserveQty[(int)q.Good] < q.Quantity) return false;
            value += q.Quantity
                     * Market.InitialPrice(state.Config.Economy, q.Good);
        }
        return pr.DevelopmentPoints >= value;
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

    /// <summary>Upkeep drawn from the attached market, pro-rata per good when
    /// scarce — a starving chain recovers together instead of the first
    /// facility by id hogging the machinery while the rest rot. Condition
    /// drifts toward the met fraction: partial upkeep holds partial health,
    /// never an unrecoverable floor (assets-and-investment.md §Condition;
    /// output scales with condition).</summary>
    private static void RunUpkeep(SimState state, PolityRecord pr)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        // pass 1: total upkeep need and starting stock per (market, good)
        var need = new Dictionary<(int Market, int Good), double>();
        var available = new Dictionary<(int Market, int Good), double>();
        foreach (var f in state.Facilities)                   // id order (P6)
        {
            if (f.OwnerActorId != pr.ActorId) continue;
            if (!MarketEngine.IsActive(state, f)) continue;
            int mIx = MarketEngine.AttachedMarketIndex(state, f);
            if (mIx < 0) continue;
            var def = Substrate.Infrastructure.Get((Substrate.InfraTypeId)f.TypeId);
            double scale = Substrate.Production.TierCostFactor(f.Tier) * years;
            foreach (var q in def.UpkeepPerYear)
            {
                var key = (mIx, (int)q.Good);
                need.TryGetValue(key, out double sum);
                need[key] = sum + q.Quantity * scale;
                available[key] = state.Markets[mIx].Inventory[(int)q.Good];
            }
        }
        // pass 2: everyone gets the same fraction of the starting stock
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
                double myNeed = q.Quantity * scale;
                if (myNeed <= 0) continue;
                var key = (mIx, (int)q.Good);
                double fraction = need[key] <= 0 ? 1.0
                    : Math.Min(1.0, available[key] / need[key]);
                double drawn = market.Draw((int)q.Good, myNeed * fraction);
                market.LastCleared[(int)q.Good] += drawn;
                met = Math.Min(met, drawn / myNeed);
            }
            double target = Math.Max(0.05, met);
            if (target > f.Condition)
                f.Condition = Math.Min(target,
                    f.Condition + eco.ConditionRecoveryPerYear * years);
            else
                f.Condition = Math.Max(target,
                    f.Condition - eco.ConditionDecayPerYear * years);
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

    /// <summary>Missing in-range same-owner pairs — plus pact-partner ports:
    /// a trade pact's lane priority means the builder may pair its ports
    /// with the partner's (interpolity/relations.md §Treaties teeth) —
    /// nearest first (tie: lower ids), built while the development treasury
    /// affords them.</summary>
    private static int BuildLanes(SimState state, PolityRecord pr, List<Port> ownPorts)
    {
        var cfg = state.Config;
        int built = 0;
        // Astrogation stretches the pairing reach (slice G)
        int rangeBonus = TechOps.AstroRangeBonus(state, pr.ActorId);
        // trade-pact partners' ports join the candidate pool (one end of
        // any pair must still be own — nobody builds foreign-foreign)
        var pactPorts = new List<Port>();
        foreach (var port in state.Ports)                     // id order (P6)
        {
            if (port.OwnerActorId == pr.ActorId
                || !state.Actors[port.OwnerActorId].Entered) continue;
            var relation = state.RelationOf(pr.ActorId, port.OwnerActorId);
            if (relation != null && relation.Rung >= TreatyRung.TradePact)
                pactPorts.Add(port);
        }
        while (pr.DevelopmentPoints >= cfg.Expansion.LaneCost)
        {
            Port? bestA = null, bestB = null;
            int bestDist = int.MaxValue;
            for (int i = 0; i < ownPorts.Count; i++)
                for (int j = i + 1; j < ownPorts.Count + pactPorts.Count; j++)
                {
                    var a = ownPorts[i];
                    var b = j < ownPorts.Count ? ownPorts[j]
                        : pactPorts[j - ownPorts.Count];
                    if (a.Id > b.Id) (a, b) = (b, a);
                    if (!LaneMath.InRange(cfg, a, b, rangeBonus)) continue;
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

/// <summary>Phase 5 — acts collide and resolve deterministically. Slice E:
/// founding is physical — a colony convoy (a reserve colony hull staged
/// from the nearest own port, the off-lane leg gated by its endurance)
/// crosses to the target and becomes the port (space-and-travel.md
/// §Colonization, end to end). Collisions on one hex resolve in actor-id
/// order; losers are not charged.</summary>
public sealed class ResolutionPhase : ISimPhase
{
    public string Name => "Resolution";

    public string Run(SimState state)
    {
        int acts = 0, founded = 0, nationalized = 0;
        int signed = 0, broken = 0, vassalized = 0, instruments = 0;
        int warsDeclared = 0;
        HashSet<(int, int)>? concessions = null;
        foreach (var d in state.Decisions)               // actor-id order
            foreach (var act in d.Decision.Acts)
            {
                acts++;
                if (act is FoundColonyAct f && TryFound(state, f)) founded++;
                if (act is NationalizeAct n
                    && CorporationOps.Nationalize(state, n.ActorId,
                                                  n.CorporationId))
                    nationalized++;
                if (act is TreatyAct t)
                    switch (RelationsOps.ResolveTreaty(state, t))
                    {
                        case RelationsOps.TreatyOutcome.Signed: signed++; break;
                        case RelationsOps.TreatyOutcome.Broken: broken++; break;
                    }
                if (act is VassalageAct v && FederationOps.TryBindVassal(state, v))
                    vassalized++;
                if (act is DynasticInstrumentAct dyn
                    && RelationsOps.ResolveDynasticInstrument(state, dyn))
                    instruments++;
                if (act is DeclareWarAct war
                    && WarOps.DeclareWar(state, war) != null)
                    warsDeclared++;
                if (act is SettlementResponseAct sue && sue.Accept)
                    (concessions ??= new HashSet<(int, int)>())
                        .Add((sue.WarId, sue.ActorId));
            }
        // the theater/objective model fights every active war one epoch
        // forward — doctrine posts fleets, engagements resolve on vectors,
        // sieges grind, captures transfer domains (war.md §Conduct)
        int battles = WarConduct.FightWars(state);
        // then the broken sue: settlements read per-objective outcomes
        int settled = WarResolution.Terminate(state, concessions);
        string note = $"{acts} acts, " + (founded == 0 ? "0 resolved"
            : $"{founded} " + (founded == 1 ? "port established" : "ports established"));
        if (battles > 0)
            note += $", {battles} " + (battles == 1 ? "battle" : "battles");
        if (nationalized > 0)
            note += $", {nationalized} " + (nationalized == 1
                ? "corporation nationalized" : "corporations nationalized");
        if (signed > 0)
            note += $", {signed} " + (signed == 1
                ? "treaty signed" : "treaties signed");
        if (broken > 0)
            note += $", {broken} " + (broken == 1
                ? "treaty broken" : "treaties broken");
        if (vassalized > 0)
            note += $", {vassalized} " + (vassalized == 1
                ? "vassalage bound" : "vassalages bound");
        if (instruments > 0)
            note += $", {instruments} dynastic "
                + (instruments == 1 ? "instrument" : "instruments");
        if (warsDeclared > 0)
            note += $", {warsDeclared} " + (warsDeclared == 1
                ? "war declared" : "wars declared");
        if (settled > 0)
            note += $", {settled} " + (settled == 1
                ? "peace settled" : "peaces settled");
        return note;
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

        // the convoy: a colony hull sitting in reserve, staged from the
        // nearest own port (lane hops are fast at this clock; the off-lane
        // crossing gates on the hull's endurance floor). No hull, no colony.
        Port? staging = null;
        foreach (var p in state.Ports)                    // id order (P6)
            if (p.OwnerActorId == act.ActorId
                && (staging == null || HexGrid.Distance(p.Hex, act.Target)
                    < HexGrid.Distance(staging.Hex, act.Target)))
                staging = p;
        FleetRecord? source = null;
        HullGroup? colonyHull = null;
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.OwnerActorId != act.ActorId
                || fleet.Posture != FleetPosture.Reserve) continue;
            foreach (var g in fleet.Hulls)                // design-id order
                if (state.Designs[g.DesignId].Role == ShipRole.Colony)
                { source = fleet; colonyHull = g; break; }
            if (source != null) break;
        }
        if (staging == null || source == null) return false;
        int offLane = HexGrid.Distance(staging.Hex, act.Target);
        if (offLane > FleetOps.EnduranceHexes(state,
                state.Designs[colonyHull!.DesignId])) return false;

        record.ExpansionPoints -= cfg.Expansion.ColonyCost;

        // the convoy departs, burns the crossing's fuel at the staging
        // market (movement is never free), and its hull becomes the colony
        int designId = colonyHull.DesignId;
        double hullGrade = colonyHull.Grade;
        source.RemoveHulls(designId, 1);
        var convoy = new FleetRecord(state.Fleets.Count, act.ActorId, staging.Hex)
        {
            Posture = FleetPosture.Expedition,
            HomePortId = staging.Id,
        };
        convoy.AddHulls(designId, 1, hullGrade);
        state.Fleets.Add(convoy);
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.ConvoyDispatched,
            new[] { act.ActorId }, staging.Hex, Magnitude: offLane,
            Valence: 0.5, EventVisibility.Regional,
            new ConvoyDispatchedPayload(convoy.Id, staging.Id,
                                        act.Target.Q, act.Target.R)));
        var stagingMarket = state.Markets[staging.Id];
        double fuelNeed = state.Config.Fleet.FuelPerHullPerHexMoved * offLane;
        double fuelDrawn = stagingMarket.Draw((int)Substrate.GoodId.Fuel, fuelNeed);
        if (fuelDrawn > 0)
        {
            stagingMarket.LastCleared[(int)Substrate.GoodId.Fuel] += fuelDrawn;
            double fuelCost = fuelDrawn
                * stagingMarket.Price[(int)Substrate.GoodId.Fuel];
            record.Credits -= fuelCost;
            MarketEngine.PayWages(state, staging.Id, fuelCost);
        }
        convoy.Hex = act.Target;
        convoy.RemoveHulls(designId, 1);
        record.HullsScrapped++;   // the colony ship becomes the colony
        var port = new Port(state.Ports.Count, act.ActorId, act.Target,
                            tier: 1, state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, cfg.Economy));
        var colonySegment = new PopulationSegment(state.Segments.Count, port.Id,
            record.SpeciesId, record.SpeciesId, cfg.Expansion.ColonySegmentSize)
        {
            // the expedition cost recycles to the settlers — treasury
            // spending is somebody's income, never destroyed (P4)
            Wealth = cfg.Expansion.ColonyCost,
        };
        // settlers sent by the state carry the official line (slice G)
        if (record.Interior != null)
            for (int ax = 0; ax < 4; ax++)
                colonySegment.Ideology[ax] = record.Interior.OfficialIdeology[ax];
        state.Segments.Add(colonySegment);
        // the expedition ships the equipment for what it came for: the
        // founding facility matches the site's best extraction potential,
        // plus a subsistence farm when that isn't farming — the export
        // earnings are what finance the provisions imports
        var founding = FoundingIndustry(state, act.Target);
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)founding, tier: 1, act.Target, act.ActorId, state.WorldYear));
        if (founding != Substrate.InfraTypeId.AgriComplex)
            state.Facilities.Add(new Facility(state.Facilities.Count,
                (int)Substrate.InfraTypeId.AgriComplex, tier: 1, act.Target,
                act.ActorId, state.WorldYear));
        // the convoy's survivors dock as the colony's first reserve fleet
        convoy.Posture = FleetPosture.Reserve;
        convoy.HomePortId = port.Id;
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.PortEstablished,
            new[] { act.ActorId }, act.Target, Magnitude: 1.0, Valence: 1.0,
            EventVisibility.Public, new PortEstablishedPayload(actor.Name, port.Id)));
        // a founding convoy mints its founder (characters.md §Notables)
        CharacterOps.MintNotable(state, act.ActorId, NotableType.Founder,
                                 act.Target);
        return true;
    }

    /// <summary>The extraction type matching the colony site's strongest
    /// potential. Food security carries a premium: extraction wins only when
    /// it clearly out-values the farmland — otherwise settlers farm.</summary>
    private static Substrate.InfraTypeId FoundingIndustry(SimState state,
                                                          HexCoordinate target)
    {
        var fields = MarketEngine.FieldsAt(state, target);
        var best = Substrate.InfraTypeId.AgriComplex;
        double bar = Substrate.Potentials.Biosphere(fields)
                     * state.Config.Infrastructure.FoodSecurityPremium;
        if (Substrate.Potentials.Ore(fields) > bar)
        { best = Substrate.InfraTypeId.Mine; bar = Substrate.Potentials.Ore(fields); }
        if (Substrate.Potentials.Volatiles(fields) > bar)
        { best = Substrate.InfraTypeId.Skimmer; bar = Substrate.Potentials.Volatiles(fields); }
        if (Substrate.Potentials.Exotics(fields) > bar)
            best = Substrate.InfraTypeId.ExcavationSite;
        return best;
    }
}

/// <summary>Phase 6 — interiors and demographics. Slice B carries the stub
/// emergence schedule (frame/time.md §Asymmetric emergence) and homeworld
/// founding: a polity enters by establishing its first port at its seat —
/// homeworlds are simply the first ports (space-and-travel.md).</summary>
public sealed class InteriorPhase : ISimPhase
{
    public string Name => "Interior";

    /// <summary>Homeworld starter facilities with tiers, extraction before
    /// processing so the chain flows within one market step (facility id =
    /// run order). Agriculture arrives established — a spacefaring species
    /// has long since fed itself, whatever its farmland.</summary>
    private static readonly (StarGen.Core.Substrate.InfraTypeId Type, int Tier)[]
        StarterIndustry =
    {
        (StarGen.Core.Substrate.InfraTypeId.AgriComplex, 2),
        (StarGen.Core.Substrate.InfraTypeId.Mine, 1),
        (StarGen.Core.Substrate.InfraTypeId.Skimmer, 1),
        (StarGen.Core.Substrate.InfraTypeId.Refinery, 1),
        (StarGen.Core.Substrate.InfraTypeId.Foundry, 1),
        // a species that arrived at spaceflight arrived by ship: the yard
        // that built the starter fleet completes the hull chain (slice E)
        (StarGen.Core.Substrate.InfraTypeId.Shipyard, 1),
    };

    public string Run(SimState state)
    {
        // segments founded by this step's entries integrate from the next step
        int preexisting = state.Segments.Count;
        // native emergence dates fire first: free and uplift births become
        // actors the entry loop below founds this same epoch (slice H)
        var (nativeBirths, nativesIntegrated, emergencesSuppressed,
            pendingClients) = NativeOps.Step(state);
        int entered = 0;
        foreach (var a in state.Actors)
        {
            if (a.Entered || a.Retired || a.EntryEpoch > state.EpochIndex)
                continue;
            a.Entered = true;
            entered++;
            var port = new Port(state.Ports.Count, a.Id, a.Seat,
                state.Config.Infrastructure.HomeworldPortTier, state.WorldYear);
            state.Ports.Add(port);
            state.Markets.Add(new Market(port.Id, state.Config.Economy));
            int species = state.PolityOf(a.Id).SpeciesId;
            var homeSegment = new PopulationSegment(state.Segments.Count, port.Id,
                species, species, state.Config.Expansion.HomeworldSegmentSize)
            {
                Wealth = state.Config.Expansion.HomeworldSegmentSize
                         * state.Config.Economy.InitialWealthPerPop,
            };
            // the founding population starts at its species' ideology tilt;
            // the interior seats there too (popular == official at birth).
            // Shape-only test skeletons carry no species — no interior then.
            if (species >= 0 && species < state.Skeleton.Species.Count)
            {
                var tilt = GovernmentForms.SpeciesIdeologyTilt(
                    state.Skeleton.Species[species]);
                for (int ax = 0; ax < 4; ax++) homeSegment.Ideology[ax] = tilt[ax];
                state.Segments.Add(homeSegment);
                InteriorOps.SeatAtEntry(state, state.PolityOf(a.Id));
                CharacterOps.SeatLeadership(state, state.PolityOf(a.Id));
                TechOps.SeedEntryTiers(state, state.PolityOf(a.Id));
            }
            else state.Segments.Add(homeSegment);
            // a civilization at spaceflight arrives with industry: the
            // starter chain (raw → alloys/fuel → machinery) and the one-time
            // credit endowment — the only mint; conserved thereafter (P4)
            state.PolityOf(a.Id).Credits +=
                state.Config.Economy.InitialCreditsPerPolity;
            foreach (var (type, tier) in StarterIndustry)
                state.Facilities.Add(new Facility(state.Facilities.Count,
                    (int)type, tier, a.Seat, a.Id, state.WorldYear));
            // a spacefaring species arrives with its founding design set and
            // the hulls already flying — genesis furniture like the starter
            // industry, no events
            double militancy = species >= 0 && species < state.Skeleton.Species.Count
                ? state.Skeleton.Species[species].Militancy : 0.5;
            DesignRegistry.RegisterEntryDesigns(state, a.Id, militancy);
            FleetOps.SeedStarterFleet(state, a.Id, port, militancy);
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.PolityEmerged,
                new[] { a.Id }, a.Seat, Magnitude: 1.0, Valence: 1.0,
                EventVisibility.Public, new PolityEmergedPayload(a.Name)));
        }

        // uplift-born clients kneel to their hosts now that they exist
        NativeOps.BindClients(state, pendingClients);

        // refugees flee before attrition bites: migration reads last step's
        // market outcomes, demographics apply to whoever stayed
        int migrations = Migrate(state, preexisting);
        int grown = Demographics(state, preexisting);
        DriftIdeology(state, preexisting);
        // lives run, then interests organize, then the polity's inside
        // reads the settled state (successions and pressure land before
        // legitimacy) — slice G
        var (deaths, successions, crises) = CharacterOps.Step(state);
        // the niche watcher raises merchant factions where profit persists
        // unclaimed (economy/corporations.md §Founding) — slice G
        CorporationOps.WatchNiches(state);
        var (factionsFormed, factionsDissolved) = FactionOps.Step(state);
        int interiors = InteriorOps.Recompute(state);
        // the outside: contact, standing claims, warmth/tension — reads the
        // freshly recomputed interiors (ideology gaps, zeal) — slice H
        var (contacts, claimsRaised) = RelationsOps.Step(state);
        // graduation reads the freshly recomputed grip (legitimacy ×
        // enforcement) — new institutions are born at the epoch's end
        var (schisms, coups, revolts) = GraduationOps.Step(state);
        int charters = CorporationOps.CharterCheck(state);

        string note = entered switch
        {
            0 => "quiet",
            1 => "1 polity enters",
            _ => $"{entered} polities enter",
        };
        if (grown > 0)
            note += $", {grown} " + (grown == 1 ? "segment grows" : "segments grow");
        if (migrations > 0)
            note += $", {migrations} " + (migrations == 1 ? "flow migrates" : "flows migrate");
        if (deaths > 0)
            note += $", {deaths} " + (deaths == 1 ? "life ends" : "lives end");
        if (successions > 0)
            note += $", {successions} " + (successions == 1 ? "succession" : "successions")
                    + (crises > 0 ? $" ({crises} contested)" : "");
        if (factionsFormed > 0)
            note += $", {factionsFormed} " + (factionsFormed == 1
                ? "faction coalesces" : "factions coalesce");
        if (factionsDissolved > 0)
            note += $", {factionsDissolved} " + (factionsDissolved == 1
                ? "faction disbands" : "factions disband");
        if (schisms > 0)
            note += $", {schisms} " + (schisms == 1 ? "schism" : "schisms");
        if (coups > 0)
            note += $", {coups} " + (coups == 1 ? "coup" : "coups");
        if (revolts > 0)
            note += $", {revolts} " + (revolts == 1
                ? "revolt crushed" : "revolts crushed");
        if (charters > 0)
            note += $", {charters} " + (charters == 1
                ? "corporation chartered" : "corporations chartered");
        if (contacts > 0)
            note += $", {contacts} first " + (contacts == 1
                ? "contact" : "contacts");
        if (claimsRaised > 0)
            note += $", {claimsRaised} " + (claimsRaised == 1
                ? "claim raised" : "claims raised");
        if (nativeBirths > 0)
            note += $", {nativeBirths} native "
                + (nativeBirths == 1 ? "emergence" : "emergences");
        if (nativesIntegrated > 0)
            note += $", {nativesIntegrated} "
                + (nativesIntegrated == 1 ? "people integrated"
                    : "peoples integrated");
        if (emergencesSuppressed > 0)
            note += $", {emergencesSuppressed} "
                + (emergencesSuppressed == 1 ? "emergence suppressed"
                    : "emergences suppressed");
        if (interiors > 0)
            note += $", {interiors} " + (interiors == 1 ? "interior" : "interiors")
                    + " recomputed";
        return note;
    }

    /// <summary>Growth = f(SoL, provisions access, embodiment) against the
    /// port's shared tier cap; famine shrinks. Machine populations grow by
    /// manufacture — their subsistence IS fab inputs, so industry access
    /// gates their growth entirely; cut off, they age out rather than starve
    /// (population-and-identity.md §Demographics).</summary>
    private static int Demographics(SimState state, int preexisting)
    {
        var cfg = state.Config.Expansion;
        var pop = state.Config.Population;
        int years = state.Config.Sim.YearsPerEpoch;
        int grown = 0;
        for (int i = 0; i < preexisting; i++)             // id order (P6)
        {
            var seg = state.Segments[i];
            if (seg.Size <= 0) continue;
            if (seg.LastSubsistence < pop.FamineLine)
            {
                seg.Size = System.Math.Max(0.0, seg.Size
                    * (1.0 - pop.FamineShrinkPerYear * years
                             * (1.0 - seg.LastSubsistence)));
                continue;
            }
            double cap = state.Ports[seg.PortId].Tier * cfg.SegmentCapPerTier;
            double portTotal = 0;
            foreach (var other in state.Segments)
                if (other.PortId == seg.PortId) portTotal += other.Size;
            if (cap <= 0 || portTotal >= cap) continue;
            double vitality = seg.LastSubsistence * (0.5 + seg.SoL);
            double step = seg.Size * cfg.SegmentGrowthPerYear * years
                          * vitality * (1.0 - portTotal / cap)
                          // medicine and agronomy: the Life domain (slice G)
                          * TechOps.LifeGrowthFactor(state,
                                state.Ports[seg.PortId].OwnerActorId);
            if (step <= 0) continue;
            seg.Size = System.Math.Min(seg.Size + cap - portTotal, seg.Size + step);
            grown++;
        }
        return grown;
    }

    /// <summary>Migration basics: each segment weighs its lane-connected
    /// neighbors by food security and SoL; flows follow the gradient,
    /// identity and per-capita wealth travel with the people, and same
    /// (species, culture) segments merge — anything else is a diaspora.
    /// Refugees (starving segments) flee at many times the base rate and
    /// chronicle an exodus (population-and-identity.md §Migration).</summary>
    private static int Migrate(SimState state, int preexisting)
    {
        var pop = state.Config.Population;
        int years = state.Config.Sim.YearsPerEpoch;
        int flows = 0;
        for (int i = 0; i < preexisting; i++)             // id order (P6)
        {
            var seg = state.Segments[i];
            if (seg.Size <= 0.01) continue;
            double here = Attractiveness(state, seg.PortId);
            bool refugees = seg.LastSubsistence < pop.RefugeeLine;
            int bestPort = -1;
            double bestGradient = pop.MigrationMinGradient;
            foreach (var lane in state.Lanes)             // id order (P6)
            {
                if (state.SeveredLanes.Contains(lane.Id)) continue;
                int other = lane.PortAId == seg.PortId ? lane.PortBId
                    : lane.PortBId == seg.PortId ? lane.PortAId : -1;
                if (other < 0) continue;
                double gradient = Attractiveness(state, other) - here;
                if (gradient > bestGradient) { bestGradient = gradient; bestPort = other; }
            }
            if (bestPort < 0 && refugees)
            {
                // refugees flee wherever ships will take them: off-lane
                // crossings to any same-polity port within reach
                var srcPort = state.Ports[seg.PortId];
                foreach (var port in state.Ports)         // id order (P6)
                {
                    if (port.Id == seg.PortId
                        || port.OwnerActorId != srcPort.OwnerActorId) continue;
                    if (HexGrid.Distance(port.Hex, srcPort.Hex)
                        > state.Config.Expansion.ColonizationReachHexes) continue;
                    double gradient = Attractiveness(state, port.Id) - here;
                    if (gradient > bestGradient)
                    { bestGradient = gradient; bestPort = port.Id; }
                }
            }
            if (bestPort < 0) continue;
            double rate = pop.MigrationRatePerYear * years
                          * (refugees ? pop.RefugeeMultiplier : 1.0);
            double flow = System.Math.Min(seg.Size * 0.5,
                seg.Size * rate * System.Math.Min(1.0, bestGradient));
            // the destination's service capacity limits settlement
            double destCap = state.Ports[bestPort].Tier
                             * state.Config.Expansion.SegmentCapPerTier;
            double destTotal = 0;
            foreach (var s in state.Segments)
                if (s.PortId == bestPort) destTotal += s.Size;
            flow = System.Math.Min(flow, System.Math.Max(0.0, destCap - destTotal));
            if (flow <= 0.001) continue;

            double wealthShare = seg.Wealth * flow / seg.Size;
            seg.Size -= flow;
            seg.Wealth -= wealthShare;
            var home = FindOrFoundSegment(state, bestPort, seg);
            home.Size += flow;
            home.Wealth += wealthShare;
            flows++;
            if (refugees)
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.MigrationWave,
                    new[] { state.Ports[seg.PortId].OwnerActorId },
                    state.Ports[bestPort].Hex, Magnitude: flow, Valence: -0.5,
                    EventVisibility.Regional,
                    new MigrationWavePayload(seg.PortId, bestPort, flow)));
        }
        return flows;
    }

    /// <summary>Food security plus a standard-of-living term — the gradient
    /// migration reads. Empty ports read as open land.</summary>
    private static double Attractiveness(SimState state, int portId)
    {
        double size = 0, food = 0, sol = 0;
        foreach (var s in state.Segments)
            if (s.PortId == portId && s.Size > 0)
            {
                size += s.Size;
                food += s.LastSubsistence * s.Size;
                sol += s.SoL * s.Size;
            }
        if (size <= 0) return 1.0;                        // open land
        return food / size + 0.3 * (sol / size);
    }

    /// <summary>The destination segment of one (species, culture) — merged if
    /// kin already live there, founded as a diaspora otherwise (segments add,
    /// never blend away).</summary>
    private static PopulationSegment FindOrFoundSegment(
        SimState state, int portId, PopulationSegment migrant)
    {
        foreach (var s in state.Segments)                 // id order (P6)
            if (s.PortId == portId && s.SpeciesId == migrant.SpeciesId
                && s.CultureId == migrant.CultureId)
                return s;
        var founded = new PopulationSegment(state.Segments.Count, portId,
            migrant.SpeciesId, migrant.CultureId, 0.0)
        { SoL = migrant.SoL, LastSubsistence = migrant.LastSubsistence };
        for (int a = 0; a < founded.Ideology.Length; a++)
            founded.Ideology[a] = migrant.Ideology[a];
        state.Segments.Add(founded);
        return founded;
    }

    /// <summary>The fast identity layer drifts with lived conditions: famine
    /// turns Sacral and Authoritarian, prosperity Individual and Open
    /// (population-and-identity.md §Ideology).</summary>
    private static void DriftIdeology(SimState state, int preexisting)
    {
        var pop = state.Config.Population;
        int years = state.Config.Sim.YearsPerEpoch;
        double drift = pop.IdeologyDriftPerYear * years;
        for (int i = 0; i < preexisting; i++)             // id order (P6)
        {
            var seg = state.Segments[i];
            if (seg.Size <= 0) continue;
            if (seg.LastSubsistence < pop.HungerIdeologyLine)
            {
                double severity = 1.0 - seg.LastSubsistence;
                Nudge(seg, IdeologyAxis.AuthorityAutonomy, 0.0, drift * severity);
                Nudge(seg, IdeologyAxis.SacralMaterial, 0.0, drift * severity);
            }
            if (seg.SoL > pop.ProsperityIdeologyLine)
            {
                double comfort = seg.SoL - pop.ProsperityIdeologyLine;
                Nudge(seg, IdeologyAxis.CommunalIndividual, 1.0, drift * comfort * 3);
                Nudge(seg, IdeologyAxis.OpenInsular, 0.0, drift * comfort * 3);
            }
        }
    }

    private static void Nudge(PopulationSegment seg, IdeologyAxis axis,
                              double toward, double amount)
    {
        int a = (int)axis;
        seg.Ideology[a] += (toward - seg.Ideology[a])
                           * System.Math.Min(1.0, amount);
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
