using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Phase 1 — news arrives; each actor's believed world updates
/// (P3, slice I): self-facts read fresh (own treasury, ports, designs, own
/// diplomatic gauges), other-side facts read through compressed belief
/// snapshots that refresh at traffic-derived news speed and freeze between
/// refreshes. The contract holds: Intent reads only the view.</summary>
public sealed class PerceptionPhase : ISimPhase
{
    public string Name => "Perception";

    public string Run(SimState state)
    {
        var known = new List<int>();
        foreach (var a in state.Actors)
            if (a.Entered)
                known.Add(a.Id);
        // one delay field per distinct news origin this step, shared
        var fields = new BeliefOps.NewsFieldCache();
        // headline war weights once per polity — truth, sampled into each
        // observer's belief on its own news clock
        var strengths = new Dictionary<int, double>();
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity)
                strengths[a.Id] = FleetOps.WarStrength(state, a.Id);
        // memory fades before this step's news lands
        ReputationOps.DecayStances(state);
        // news arrives first: pulses whose age covers the delay deliver,
        // refreshing beliefs and repricing stances before anyone decides;
        // regional word spreads by contact
        int arrivals = BeliefOps.DeliverPulses(state, strengths, fields);
        ReputationOps.SpreadRegional(state, fields);
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
            // plague on the doorstep is locally observable: open lanes
            // from an own healthy port to an infected one (slice I)
            List<QuarantineCandidate>? frontier = null;
            if (a.Kind == ActorKind.Polity && state.Plagues.Count > 0)
                foreach (var lane in state.Lanes)             // id order (P6)
                {
                    if (lane.QuarantinedUntil >= state.WorldYear) continue;
                    bool aInf = PlagueOps.Afflicted(state, lane.PortAId);
                    bool bInf = PlagueOps.Afflicted(state, lane.PortBId);
                    if (aInf == bInf) continue;   // nothing left to protect
                    int infected = aInf ? lane.PortAId : lane.PortBId;
                    int healthy = aInf ? lane.PortBId : lane.PortAId;
                    if (state.Ports[healthy].OwnerActorId != a.Id) continue;
                    (frontier ??= new List<QuarantineCandidate>())
                        .Add(new QuarantineCandidate(lane.Id, healthy,
                                                     infected));
                }
            double ownCredits = 0;
            List<CorporateBrief>? hosted = null;
            List<RelationBrief>? relations = null;
            List<WarBrief>? wars = null;
            CapabilityBrief? capability = null;
            List<ConstructionCandidate>? constructionCandidates = null;
            List<PortBrief>? ownPortBriefs = null;
            if (a.Kind == ActorKind.Polity)
            {
                ownCredits = state.PolityOf(a.Id).Credits;
                // the capability brief: own-side rates the planner
                // schedules against (spec §2), assembled fresh each step
                capability = CapabilityOps.BriefFor(state, a.Id);
                constructionCandidates =
                    CapabilityOps.ConstructionCandidatesFor(state, a.Id);
                ownPortBriefs = new List<PortBrief>();
                foreach (var port in state.Ports)              // id order (P6)
                {
                    if (port.OwnerActorId != a.Id) continue;
                    int yardTiers = 0;
                    foreach (var f in state.Facilities)
                        if (f.TypeId == (int)Substrate.InfraTypeId.Shipyard
                            && f.OwnerActorId == a.Id
                            && MarketEngine.IsActive(state, f)
                            && MarketEngine.AttachedMarketIndex(state, f)
                                == port.Id)
                            yardTiers += f.Tier;
                    // the stock snapshot makes the brief LOCATED (spec §2,
                    // stage 2): a copy — the view never aliases live state
                    ownPortBriefs.Add(new PortBrief(port.Id, port.Tier,
                        yardTiers, (double[])port.StockQty.Clone()));
                }
                foreach (var corp in state.Corporations)
                    if (corp.Active && corp.HostPolityId == a.Id)
                    {
                        // the books are wherever the headquarters is
                        var cb = BeliefOps.AboutCorporation(state, a.Id,
                                                            corp, fields);
                        (hosted ??= new List<CorporateBrief>())
                            .Add(new CorporateBrief(corp.Id, corp.Name,
                                                    cb.Credits));
                    }
                relations = BuildRelationBriefs(state, a.Id, strengths,
                                                fields);
                foreach (var war in state.Wars)   // id order (P6)
                {
                    if (!war.Active || !war.Involves(a.Id)) continue;
                    bool attackerSide = war.OnAttackerSide(a.Id);
                    // the front reports arrive at news speed: a distant
                    // loser doesn't yet know it is losing (P3 — wars run
                    // past their rational end)
                    var wb = BeliefOps.AboutWar(state, a.Id, war, fields);
                    (wars ??= new List<WarBrief>()).Add(new WarBrief(
                        war.Id, war.Name,
                        attackerSide ? war.DefenderId : war.AttackerId,
                        attackerSide,
                        a.Id == war.AttackerId || a.Id == war.DefenderId,
                        wb.OwnSideExhaustion,
                        wb.OwnSideStrengthShare,
                        wb.ObjectivesTaken, war.Objectives.Count));
                }
            }
            // corps perceive at their scope (contract-economy spec §3,
            // C11): the capability brief plus their home-port investment
            // pick — the standing plan packs against exactly this view
            if (a.Kind == ActorKind.Corporation
                && state.CorporationOf(a.Id) is { Active: true } corpRec)
            {
                capability = CapabilityOps.BriefFor(state, a.Id);
                ownCredits = corpRec.Credits;
                if ((corpRec.Niche is CorporateNiche.Extraction
                        or CorporateNiche.Fabrication)
                    && CorporationOps.WantsFacility(state, corpRec))
                {
                    var pick = CorporationOps.PlannedFacility(state, corpRec);
                    var home = state.Ports[corpRec.HomePortId];
                    constructionCandidates = new List<ConstructionCandidate>
                    {
                        new ConstructionCandidate((int)pick, home.Hex,
                                                  home.Id, 1.0),
                    };
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
                                              wars, frontier,
                                              capability, constructionCandidates,
                                              ownPortBriefs);
            perceiving++;
        }
        string note = $"{perceiving} actors perceive";
        if (arrivals > 0)
            note += $", {arrivals} news " + (arrivals == 1
                ? "arrival" : "arrivals");
        return note;
    }

    /// <summary>One polity's relation briefs: the pair-diplomatic state it
    /// co-owns (gauges, rungs, offers, claims, ties) reads fresh; the other
    /// side's observables (strength, coalition, the casus-belli menu, the
    /// objective enumeration) read through the belief snapshot — stale by
    /// distance, refreshed by traffic (slice I).</summary>
    private static List<RelationBrief>? BuildRelationBriefs(SimState state,
        int selfId, Dictionary<int, double> strengths,
        BeliefOps.NewsFieldCache fields)
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
            var belief = BeliefOps.About(state, selfId, other, strengths,
                                         fields);
            (relations ??= new List<RelationBrief>())
                .Add(new RelationBrief(other, rel.Warmth,
                    rel.Tension, rel.Rung, rel.OfferedRung,
                    rel.OfferedById, held, against,
                    RelationsOps.IdeologyGap(state.PolityOf(selfId),
                                             state.PolityOf(other)),
                    rel.RungYear < 0 ? 0
                        : state.WorldYear - rel.RungYear,
                    belief.Strength,
                    rel.VassalPolityId,
                    RelationsOps.IsDynastic(state, other),
                    rel.DynasticTies,
                    belief.Menu,
                    belief.DefensiveStrength,
                    belief.ObjectiveCandidates,
                    RelationsOps.OverlapShare(state, selfId, other)));
        }
        return relations;
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

/// <summary>Phase 2 — the market step through the ORDER BOOK
/// (contract-economy spec §2), fixed order: arrivals land → quotes decay →
/// supply posts sells → the port posts band bids, projects and procurement
/// post escrowed buys → bridge freight lifts asks toward distant resting
/// bids → books match at maker price → fills route, unfilled escrow
/// refunds, famine/SoL derive from fill fractions. The anonymous shelf is
/// gone; unsold output is somebody's resting ask.</summary>
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
        // stale job postings clear the board before anything sails, and
        // orders past their expiry refund/escheat (spec §2 step 2)
        CourierOps.ExpireOpen(state);
        OrderOps.ExpireOrders(state);
        // in-flight freight sails first (spec §4b): this step's arrivals
        // post on the books and land in the larders BEFORE supply, demand,
        // and the Allocation draws that follow
        ShipmentOps.Advance(state, scratch);
        // resting quotes re-anchor to the market before new output posts
        BookOps.RepriceAsks(state);
        MarketEngine.SupplyLands(state, scratch);
        CorporationOps.SalvageLands(state, scratch);   // salvors strip fields
        MarketEngine.PostBandBids(state, scratch);
        MarketEngine.PostProjectBids(state, scratch);
        MarketEngine.PostProcurementBids(state, scratch);
        MarketEngine.PostRelayBids(state, scratch);
        // consumers who lift asks directly still register their want, so
        // the reference drift prices the scarcity they are about to cause
        MarketEngine.AddConsumptionSignal(state, scratch);
        // spread runs (B2): every posted fleet's owner trades the lane's
        // price gradient with its own capital — absorption reads the dear
        // end's REAL resting bids, the speculative term its reference
        var (shipments, units) = MarketEngine.MoveFreight(state, scratch);
        // the express earn-in clock: consecutive saturated world-years of
        // the lane's posted capacity (lane-economics spec §3.4; the clock
        // accumulates the step's year span so fine ticks track coarse, P7)
        foreach (var lane in state.Lanes)                 // id order (P6)
            lane.SaturatedYears =
                scratch.LaneFleetCapacity[lane.Id] > 0
                && scratch.LaneCapacityUsed[lane.Id]
                   / scratch.LaneFleetCapacity[lane.Id]
                   >= state.Config.Expansion.ExpressSaturationFloor
                ? lane.SaturatedYears + state.Config.Sim.YearsPerEpoch : 0;
        int famines = MarketEngine.MatchAndClear(state, scratch);
        int spanYears = state.Config.Sim.YearsPerEpoch;
        var eco = state.Config.Economy;
        // household wealth above what demand-band consumption can spend
        // just piles up otherwise (nothing else drains it) — the levy
        // recirculates the excess into the sovereign's receipts, mirroring
        // SettleSale's tax-transfer shape (P4: conserved, not minted)
        foreach (var seg in state.Segments)
        {
            double floor = seg.Size * eco.WealthTaxFloorPerPop;
            double taxable = Math.Max(0.0, seg.Wealth - floor);
            // compounded per world-year (P7, fix wave 1): a 25-year step levies
            // exactly what twenty-five 1-year steps would, and the fraction
            // stays in [0,1) so the levy can never exceed the taxable excess
            // (no driving wealth below the floor) — the shape DecayIdlePools uses
            double levy = taxable
                * (1.0 - Math.Pow(Math.Max(0.0, 1.0 - eco.WealthTaxRatePerYear),
                                  spanYears));
            if (levy <= 0) continue;
            seg.Wealth -= levy;
            // the port's sovereign is always the owning polity — a segment's wealth
            // and its sovereign share the same currency, so this is a raw
            // same-currency credit (PolityOf, not the ICreditLedger handle, since
            // Corporation.Credits is read-only)
            var sovereign = state.PolityOf(state.Ports[seg.PortId].OwnerActorId);
            sovereign.Credits += levy;
            sovereign.Receipts += levy;
        }
        foreach (var pr in state.Polities)
            pr.LastIncomePerYear = pr.Receipts / spanYears;
        foreach (var corp in state.Corporations)
            if (corp.Active) corp.LastIncomePerYear = corp.Receipts / spanYears;
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
        int earning = 0, lanesBuilt = 0;
        int hullsLost = 0;
        int defaults = ServiceLoans(state);
        // tribute ships up before anyone budgets: vassals allocate what
        // remains of their receipts (interpolity/relations.md §Vassalage)
        int tributes = FederationOps.PayTribute(state);
        // every polity starts the epoch owing nothing to THIS epoch's base;
        // Borrow then runs at the TOP against each carried-over balance (after
        // this epoch's ServiceLoans/tribute), so a polity that ended last epoch
        // negative seeks financing before its budget is set — and the fresh
        // principal it marks in BorrowedThisEpoch flows through the same epoch's
        // allocation split, funding real investment rather than only the bills
        foreach (var pr in state.Polities) pr.BorrowedThisEpoch = 0;
        int borrowed = Borrow(state);
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
            // budget the epoch's receipts plus any principal borrowed at the
            // top of THIS epoch, not the balance (monetary-equilibrium design
            // §1): reading the stock swept a polity's entire historical treasury
            // into pools every epoch; income alone makes Credits a real
            // accumulating stock. BorrowedThisEpoch lets a fresh loan fund the
            // investment pools the same epoch it is drawn — the money already
            // exists (a conserved lender→borrower transfer), so this routes it
            // through the split, it is not a new mint.
            // the always-on steady mint (Part B / design's third declared channel):
            // a small fraction of THIS polity's own real receipts, minted fresh
            // every epoch so the money supply grows in step with real output rather
            // than only being patched reactively during shortfalls. Recomputed from
            // Receipts each epoch, it never compounds on itself the way runaway loan
            // interest did. Like Borrow's principal and IssueSovereignCredit, the
            // mint lands in Credits (a real holder — that is what makes the supply
            // grow and keeps the residual netting out) AND enters the base, so the
            // same budget split routes it into real investment.
            double steadyIssuance = cfg.Economy.SteadyIssuanceRate
                                    * Math.Max(0.0, pr.Receipts);
            if (steadyIssuance > 0)
            {
                pr.Credits += steadyIssuance;
                state.CumulativeSteadyIssuance += steadyIssuance;
                // per-currency mirror (slice CU-1 task 9): the mint lands in
                // THIS polity's own currency; the per-currency conservation
                // residual nets it out just as the galaxy-wide field does
                if (pr.CurrencyId >= 0)
                    state.CurrencyOf(pr.CurrencyId).CumulativeSteadyIssuance
                        += steadyIssuance;
            }
            double allocatable = Math.Max(0.0,
                pr.Receipts + pr.BorrowedThisEpoch + steadyIssuance);
            pr.ExpansionPoints += allocatable * budget.Expansion;
            pr.DevelopmentPoints += allocatable * budget.Development;
            pr.MilitaryPoints += allocatable * budget.Military;
            // stage 2: the reserve share funds procurement (spec §4b) —
            // Budget.Reserves stops being a dead line
            pr.ReservePoints += allocatable * budget.Reserves;
            pr.Credits -= allocatable
                * (budget.Expansion + budget.Development + budget.Military
                   + budget.Reserves);
            // the appeasement line buys factions off — a treasury→faction
            // flow, conserved (P4); without factions the line stays liquid
            // evaluate BEFORE the compound assignment: these calls can pay
            // pr itself through the book (the polity selling to the state),
            // and `pr.Credits -= Call(...)` reads Credits before the call
            // runs, silently overwriting the in-call mutation (found in
            // slice CE as a credit leak)
            double appeaseSpent = FactionOps.SpendAppeasement(state, pr,
                allocatable * budget.Appeasement, allocatable);
            pr.Credits -= appeaseSpent;
            // research: the standing split converts exotics × compute into
            // ladder progress; the spend pays the feedstock sellers
            double researchSpent = TechOps.Research(state, pr,
                policies.Research, allocatable * budget.Research);
            pr.Credits -= researchSpent;
            // a belligerent raises (or a peaceful polity stands down) its
            // war-economy ramp before the standing plan breaks ground
            // (spec §5)
            SpawnMobilizations(state, pr);
            // state logistics before the works advance (spec §4b): the
            // quartermaster raises shipping orders from the polity's own
            // larders toward every under-covered project site
            ShipmentOps.RaiseRequisitions(state, pr);
            // the standing plan breaks ground on everything due this step —
            // facilities, port raises, hull batches — as construction
            // projects that Advance feeds over their build years (spec §3)
            Groundbreak(state, pr, policies.Plan);
            lanesBuilt += BuildLanes(state, pr, ownPorts);
            FleetOps.ManagePostures(state, pr, ownPorts);
            // with the postures settled the war quartermaster reads the
            // fronts and raises War-priority convoys toward under-stocked
            // forward depots (contract-economy spec §4)
            ShipmentOps.StockDepots(state, pr);
            hullsLost += FleetOps.SupplyFleets(state, pr);
            RunUpkeep(state, pr);
            DecayStockpiles(state, pr, ownPorts);
            // whatever the epoch's works left unspent in the idle pools
            // recirculates into the treasury (design §3) — a recycle into the
            // buffer stock, not a leak
            DecayIdlePools(state, pr);
        }
        // the job board clears: open couriers meet whoever's hulls sit on
        // their first leg — the poster's own marine self-fulfills at cost
        // (contract-economy spec §3)
        CourierOps.AcceptOpen(state);
        // corporations run their portfolios on the same markets (slice G)
        int corporationsActive = CorporationOps.Operate(state);
        // laggards learn from the goods they buy and the wrecks they find
        TechOps.Diffuse(state);
        // every funder's in-flight projects advance one step in priority
        // order (polity groundbreaks above, corp spawns from Operate) —
        // delivered years commission facilities, raise tiers, open lanes
        int completions = ProjectOps.AdvanceAll(state);
        int advances = 0;
        foreach (var staged in state.Staged)
            if (staged.Type == WorldEventType.TechAdvanced) advances++;
        // sovereign issuance runs LAST — after top-of-epoch Borrow and the whole
        // budget/spend loop: peer lending already got first refusal on each
        // carried deficit (Borrow ran at the top, fix wave 1 finding 1), so the
        // bounded second mint (design §5) only backstops whatever is STILL
        // negative at end of epoch — after this epoch's spend, including the
        // principal a fresh loan routed into the budget split. ServiceLoans ran
        // at the top against last epoch's balance, so issuance never covers loan
        // service
        foreach (var pr in state.Polities)                    // actor-id order
            if (state.Actors[pr.ActorId].Entered)
                IssueSovereignCredit(state, pr);
        string note = earning == 0 ? "quiet"
            : $"income allocated for {earning} " + (earning == 1 ? "polity" : "polities");
        if (lanesBuilt > 0) note += $", {lanesBuilt} " + (lanesBuilt == 1 ? "lane built" : "lanes built");
        if (completions > 0) note += $", {completions} project completions";
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

    /// <summary>Upkeep bought off the attached book, pro-rata per good when
    /// scarce — a starving chain recovers together instead of the first
    /// facility by id hogging the machinery while the rest rot. The goods
    /// cost money now: sellers are paid at their asks from the polity's
    /// working capital. Condition drifts toward the met fraction: partial
    /// upkeep holds partial health, never an unrecoverable floor
    /// (assets-and-investment.md §Condition; output scales with condition).</summary>
    private static void RunUpkeep(SimState state, PolityRecord pr)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        // pass 1: total upkeep need and live ask depth per (market, good)
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
                available[key] = BookOps.AskQty(state, mIx, (int)q.Good);
            }
        }
        // pass 2: everyone gets the same fraction of the starting depth
        foreach (var f in state.Facilities)                   // id order (P6)
        {
            if (f.OwnerActorId != pr.ActorId) continue;
            if (!MarketEngine.IsActive(state, f)) continue;
            int mIx = MarketEngine.AttachedMarketIndex(state, f);
            if (mIx < 0) continue;
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
                var (drawn, _, cost) = BookOps.LiftAsks(state, mIx,
                    (int)q.Good, myNeed * fraction, budget: double.MaxValue);
                pr.Credits -= cost;
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

    /// <summary>Perishability, located (spec §4b): each port's stockpile
    /// decays where it sits — provisions rot, medicine ages, durables keep.
    /// Active Depot tiers at the port cut the rot (the controller contract's
    /// "stockpile targets → depots/reserves" mechanism).</summary>
    private static void DecayStockpiles(SimState state, PolityRecord pr,
                                        List<Port> ownPorts)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var port in ownPorts)                        // id order (P6)
        {
            double cut = Math.Pow(eco.DepotDecayFactor,
                MarketEngine.ActiveDepotTiersAt(state, port));
            for (int g = 0; g < port.StockQty.Length; g++)
            {
                if (port.StockQty[g] <= 0) continue;
                double perish =
                    MarketEngine.StockPerishFactor((Substrate.GoodId)g);
                // compounded per world-year (P7, review fix 4): a 25-year
                // step rots exactly what twenty-five 1-year steps rot
                double keep = Math.Pow(Math.Max(0.0,
                    1.0 - eco.StockpileDecayPerYear * perish * cut), years);
                port.StockQty[g] *= keep;
                if (port.StockQty[g] <= 0) port.StockGrade[g] = 0;
            }
        }
    }

    /// <summary>Idle-pool recycle (monetary-equilibrium design §3): whatever
    /// the epoch's works left unspent in the Expansion/Development/Military
    /// pools decays a bounded fraction back into Credits — the Planner accrues
    /// these ~2x faster than it spends them, so idle points would otherwise
    /// park forever. ReservePoints is excluded: it funds physical stockpile
    /// targets with its own perishability decay, not idle cash. Compounded per
    /// world-year like StockpileDecayPerYear (P7): a 25-year step recirculates
    /// exactly what twenty-five 1-year steps would. Conserved — the decayed
    /// points land in Credits, they do not vanish.</summary>
    private static void DecayIdlePools(SimState state, PolityRecord pr)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        double keep = Math.Pow(Math.Max(0.0, 1.0 - eco.PoolIdleDecayPerYear), years);
        double DecayOne(double points)
        {
            double decayed = points * (1.0 - keep);
            pr.Credits += decayed;
            return points - decayed;
        }
        pr.ExpansionPoints = DecayOne(pr.ExpansionPoints);
        pr.DevelopmentPoints = DecayOne(pr.DevelopmentPoints);
        pr.MilitaryPoints = DecayOne(pr.MilitaryPoints);
    }

    /// <summary>Bounded sovereign issuance — the second declared mint
    /// (monetary-equilibrium design §5). Run in its own pass AFTER Borrow (fix
    /// wave 1), so peer lending gets first refusal on every shortfall and the
    /// mint only backstops what stays negative — it sees the true end-of-epoch
    /// shortfall after every bill and every loan. A negative treasury mints up
    /// to a fraction of its own real receipts (weight, not indebtedness — no
    /// moral hazard toward the largest debtor), never the whole hole:
    /// NegativeTreasuries must still breathe. Issuance never covers loan service
    /// by construction (ServiceLoans runs at the top of the phase against last
    /// epoch's balance), so default and collateral seizure stay real.
    /// CumulativeFiatIssued tracks the mint for the conservation residual.</summary>
    private static void IssueSovereignCredit(SimState state, PolityRecord pr)
    {
        if (pr.Credits >= 0) return;
        double shortfall = -pr.Credits;
        double cap = state.Config.Economy.SovereignIssuanceRate
                     * Math.Max(0.0, pr.Receipts);
        double issued = Math.Min(shortfall, cap);
        if (issued <= 0) return;
        pr.Credits += issued;
        state.CumulativeFiatIssued += issued;
        // per-currency mirror (slice CU-1 task 9): sovereign issuance mints
        // into this polity's own currency, so the per-currency residual can
        // net the same mint the galaxy-wide field already tracks
        if (pr.CurrencyId >= 0)
            state.CurrencyOf(pr.CurrencyId).CumulativeFiatIssued += issued;
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
            var lender = state.LedgerOf(loan.LenderActorId);
            // The loan is denominated in the LENDER's currency (design, "Loans
            // across currencies"): a polity lender imposes its own single
            // currency; a multi-currency corporation lends — and denominates —
            // in the borrower's currency it fronted (its Withdraw target at
            // issue). The amortization schedule below computes in that currency.
            int loanCurrencyId = lender is Corporation
                ? borrower.CurrencyId
                : ((PolityRecord)lender).CurrencyId;
            // compounded per world-year (P7): a 25-year step accrues exactly
            // what twenty-five 1-year steps compounding would, not a flat
            // rate*years multiply. amort decays the principal-owed fraction with
            // the DecayIdlePools shape, treating 1/TermYears as the annual
            // paydown rate — so a loan finishes amortizing near TermYears
            // regardless of tick resolution
            double interest = loan.Principal * (Math.Pow(1.0 + loan.RatePerYear, years) - 1.0);
            double amort = loan.Principal
                * (1.0 - Math.Pow(Math.Max(0.0, 1.0 - 1.0 / loan.TermYears), years));
            double payment = interest + amort;           // lender currency
            // FX risk sits with the borrower: convert this epoch's fixed
            // lender-currency payment into the borrower's own currency AT THE
            // CURRENT RATE (not the issuance rate), so a rate drift changes how
            // much of its own currency the same foreign payment costs. A
            // same-currency loan converts 1:1, so the solvency gate below is
            // byte-identical to the pre-currency behavior (ME's mechanism).
            double paymentOwn = state.ConvertCurrency(payment, loanCurrencyId, borrower.CurrencyId);
            if (borrower.Credits >= paymentOwn)
            {
                // full payment: the borrower Withdraws the lender-currency
                // payment (debiting its converted own-currency cost and booking
                // the transfer); the lender banks the lender-currency amount.
                double provided = borrower.Withdraw(state, payment, loanCurrencyId);
                RepayLender(state, lender, provided, loanCurrencyId);
                loan.Principal -= amort;
                if (loan.Principal <= 1e-9) loan.Closed = true;
            }
            else if (borrower.Credits > 0)
            {
                // partial: pay every own-currency credit the borrower holds —
                // worth partLoan in the loan currency — and capitalize the
                // missed interest (all in the loan currency, as Principal is).
                double partOwn = borrower.Credits;
                double partLoan = state.ConvertCurrency(partOwn, borrower.CurrencyId, loanCurrencyId);
                double provided = borrower.Withdraw(state, partLoan, loanCurrencyId);
                RepayLender(state, lender, provided, loanCurrencyId);
                loan.Principal += interest - Math.Min(interest, provided);
                borrower.Credits = 0;
                // a loan whose principal has capitalized past a bounded multiple
                // of its issued size is forced to default rather than compounding
                // toward millions forever — the borrower can still be nominally
                // solvent (some Credits) yet never service a debt this deep, so
                // the ceiling is a second default trigger beside the zero-Credits
                // one, seizing collateral exactly the same way. The ceiling test
                // is entirely in the loan currency, unchanged by the FX layer.
                if (loan.Principal > eco.LoanCapitalizationCeiling * loan.OriginalPrincipal)
                    ForceDefault(state, loan, ref defaults);
            }
            else
                ForceDefault(state, loan, ref defaults);
        }
        return defaults;
    }

    /// <summary>Close a loan as a default: seize the borrower's first facility
    /// for the lender and stage the LoanDefaulted event. Shared by both default
    /// triggers — a borrower with no Credits at all, and one whose principal has
    /// capitalized past the ceiling — so the two paths move money and collateral
    /// identically.</summary>
    private static void ForceDefault(SimState state, Loan loan, ref int defaults)
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

    /// <summary>Credit a loan repayment to the lender, denominated in the loan's
    /// (lender's) currency (<paramref name="currencyId"/>). Routes through
    /// <see cref="ICreditLedger.Deposit"/> for both lender kinds: a corporation
    /// banks that currency into its wallet bucket, a polity banks it into its
    /// single balance (a same-currency Deposit, since the loan denominates in the
    /// polity lender's own currency — no spurious conversion). Symmetric with the
    /// draw-down on the lend side in <see cref="Borrow"/>. The old polity path was
    /// currency-blind raw arithmetic; this routes it through the ledger so a
    /// genuine divergence books the transfer instead of silently leaking.</summary>
    private static void RepayLender(SimState state, ICreditLedger lender,
                                    double amount, int currencyId)
    {
        lender.Deposit(state, amount, currencyId);
    }

    /// <summary>Insolvent polities borrow from whoever holds surplus — the
    /// richest entered candidate, polity or corporation, able to front the
    /// principal twice over (economy/markets.md §Credit: "lenders are
    /// whoever holds surplus").</summary>
    private static int Borrow(SimState state)
    {
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        int issued = 0;
        foreach (var pr in state.Polities)                    // actor-id order
        {
            if (!state.Actors[pr.ActorId].Entered || pr.Credits >= 0) continue;
            // borrower-side creditworthiness (Part A, credit-score gate): a polity
            // already carrying open-loan principal above a bounded multiple of its
            // trailing real income (LastIncomePerYear × years ≈ one epoch's
            // receipts) is locked out of NEW credit until amortization services the
            // debt down. This touches nothing in ServiceLoans — existing loans keep
            // accruing/amortizing/defaulting; it only refuses to pile on MORE.
            // A borrower services every one of its loans in its OWN currency
            // (ServiceLoans debits `borrower.Credits`, a single-currency polity
            // balance), so each open loan's principal is denominated in the
            // borrower's currency. Sum and the debt ceiling therefore share the
            // borrower's currency; both numeraire-convert by the same rate so the
            // creditworthiness comparison is in one common unit (currency-and-FX
            // design "Loans across currencies"). The rate is a common factor here,
            // but the per-loan conversion is the shape the design specifies and is
            // robust if a borrower ever carries a foreign-denominated loan.
            double borrowerRate = state.NumeraireRateOf(pr.CurrencyId);
            double existingPrincipal = 0;
            foreach (var open in state.Loans)                 // id order (P6)
                if (open.BorrowerActorId == pr.ActorId && !open.Closed)
                    existingPrincipal += open.Principal * borrowerRate;
            double debtCeiling = eco.MaxDebtToIncomeRatio
                * Math.Max(0.0, pr.LastIncomePerYear) * years * borrowerRate;
            if (existingPrincipal > debtCeiling) continue;
            // the shortfall-derived draw, in the BORROWER's own currency — the
            // amount the borrower needs to cover its deficit. The loan that funds
            // it denominates in the LENDER's currency (below), so FX risk sits
            // with the borrower (design, "Loans across currencies").
            double borrowerAmount = -pr.Credits * 1.2;
            // rank lenders in a common (numeraire) unit: `borrowerAmount` is in
            // the borrower's currency; a polity candidate's Credits is in its OWN
            // currency; a corporation's Credits is already the numeraire wallet
            // total. Convert every quantity to numeraire before comparing so a
            // rich polity is not passed over for a nominally-larger foreign balance.
            double principalNum = borrowerAmount * borrowerRate;
            ICreditLedger? lender = null;
            int lenderActorId = -1;
            double bestNum = 0;
            foreach (var candidate in state.Polities)
            {
                double candNum = candidate.Credits
                    * state.NumeraireRateOf(candidate.CurrencyId);
                if (candidate.ActorId != pr.ActorId
                    && state.Actors[candidate.ActorId].Entered
                    && candNum >= principalNum * 2
                    && (lender == null || candNum > bestNum))
                { lender = candidate; lenderActorId = candidate.ActorId; bestNum = candNum; }
            }
            // the corp pass runs after the polity pass, so on an exact-credit
            // tie a polity lender is kept over a corp one — a stable, seeded
            // tiebreak that does not depend on actor-id interleaving (schism and
            // graduation polities can be minted after early corps)
            foreach (var candidate in state.Corporations)
            {
                double candNum = candidate.Credits;   // already numeraire
                if (candidate.ActorId != pr.ActorId
                    && state.Actors[candidate.ActorId].Entered
                    && candNum >= principalNum * 2
                    && (lender == null || candNum > bestNum))
                { lender = candidate; lenderActorId = candidate.ActorId; bestNum = candNum; }
            }
            if (lender == null) continue;
            // The loan denominates in the LENDER's currency: a polity lender
            // imposes its own currency (real FX risk on the borrower); a
            // corporation is multi-currency and lends in the borrower's own
            // currency it fronts (it banks whatever it is paid), so its
            // denomination is pr.CurrencyId — the Withdraw target the corp path
            // already used, byte-identical for the single-currency case.
            int lenderCurrencyId = lender is Corporation
                ? pr.CurrencyId
                : ((PolityRecord)lender).CurrencyId;
            // convert the borrower's own-currency need into the lender-currency
            // principal at the issuance rate; THAT converted amount is the debt.
            double principal = state.ConvertCurrency(
                borrowerAmount, pr.CurrencyId, lenderCurrencyId);
            // the lender fronts the principal in its OWN currency: a corporation
            // draws it from its multi-currency wallet (Withdraw's draw-down rule),
            // so the money leaves real Holdings; the lender-selection 2× gate
            // guarantees the wallet covers it, so the draw never caps. A polity
            // lender debits its single balance (may go negative — the existing
            // insolvency path). Both now route through Withdraw, so the polity
            // path books the transfer instead of the old currency-blind arithmetic.
            lender.Withdraw(state, principal, lenderCurrencyId);
            // the borrower receives the proceeds converted BACK into its own
            // currency at the issuance rate — only the DEBT is lender-denominated,
            // not the cash the borrower holds. In the single-currency case this is
            // a same-currency Deposit, byte-identical to the old `Credits += `.
            double proceeds = pr.Deposit(state, principal, lenderCurrencyId);
            // mark THIS epoch's borrowing (in the borrower's own currency) so the
            // same epoch's allocation base can route it into the investment pools.
            pr.BorrowedThisEpoch += proceeds;
            var loan = new Loan(state.Loans.Count, lenderActorId, pr.ActorId,
                principal, eco.LoanRatePerYear, eco.LoanTermYears, state.WorldYear);
            state.Loans.Add(loan);
            issued++;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.LoanIssued,
                new[] { pr.ActorId, lenderActorId },
                state.Actors[pr.ActorId].Seat,
                Magnitude: principal, Valence: 0.0, EventVisibility.Regional,
                new LoanIssuedPayload(loan.Id, lenderActorId, pr.ActorId,
                                      principal)));
        }
        return issued;
    }

    /// <summary>Gate-pair lane construction (lane-economics spec §§1–3), two
    /// passes. **Founding links** first: every isolated own port seeks its
    /// nearest eligible partner (preferring one already on the network) —
    /// the connecting gate is the colonization chain's last step and rides
    /// outside the generational cadence, so a new colony joins the polity's
    /// import/export/migration web as it is founded. **Densification**
    /// second: one extra lane per generation through the full
    /// detour/congestion rule. One funder pays both ends in one step —
    /// half-built gates only ever arise from later destruction.</summary>
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
        // ports already on the network — ANY lane row counts, not only live
        // ones: a founding link's Lane row exists from groundbreaking, so a
        // colony whose founding gate is still building already reads as
        // connected and never spawns a second goods-free pair next step
        // (a conservation hole — the founding link streams no goods) (F5)
        var connected = new HashSet<int>();
        foreach (var lane in state.Lanes)
        { connected.Add(lane.PortAId); connected.Add(lane.PortBId); }

        // ---- pass 1: founding links (isolated ports have no network path,
        // so the detour rule is trivially satisfied and never consulted)
        foreach (var port in ownPorts)                        // id order (P6)
        {
            if (connected.Contains(port.Id)) continue;
            Port? pick = null;
            int pickTier = 0, pickDist = int.MaxValue;
            bool pickConnected = false;
            for (int j = 0; j < ownPorts.Count + pactPorts.Count; j++)
            {
                var other = j < ownPorts.Count ? ownPorts[j]
                    : pactPorts[j - ownPorts.Count];
                if (other.Id == port.Id) continue;
                var (a, b) = port.Id < other.Id ? (port, other) : (other, port);
                if (LaneExists(state, a.Id, b.Id)) continue;
                int dist = HexGrid.Distance(a.Hex, b.Hex);
                int tier = LaneMath.RequiredGateTier(cfg, dist, rangeBonus);
                if (tier < 0) continue;                        // out of reach
                if (!LaneNetwork.HasFreeGateSlot(state, a)
                    || !LaneNetwork.HasFreeGateSlot(state, b)) continue;
                // founding links are goods-free (the expedition shipped the
                // pair's basket) — they only stream wages, so the
                // affordability gate is HALF the pair value (slice CE: dev
                // treasuries buy project goods now, and the full-pair gate
                // left every tenth colony stranded off the network)
                double cost = GateValue(cfg, tier);
                if (pr.DevelopmentPoints < cost) continue;
                bool otherOn = connected.Contains(other.Id);
                if (pick == null || (otherOn && !pickConnected)
                    || (otherOn == pickConnected && (dist < pickDist
                        || (dist == pickDist && other.Id < pick.Id))))
                { pick = other; pickTier = tier; pickDist = dist;
                  pickConnected = otherOn; }
            }
            if (pick == null) continue;
            // founding link: goods-free — the expedition shipped the pair's
            // basket from its staging market (time-and-logistics spec §4)
            BuildLanePair(state, pr, port, pick, pickTier, foundingLink: true);
            connected.Add(port.Id);
            connected.Add(pick.Id);
            built++;
        }

        // ---- pass 2: densification — cheapest eligible pair first, ground
        // broken while the dev treasury can afford another pair (the goods
        // and wages stream over the build years now); the detour/congestion
        // rule and free gate slots are the pace-setters (spec §3)
        while (true)
        {
            Port? bestA = null, bestB = null;
            int bestTier = 0, bestDist = int.MaxValue;
            double bestCost = double.MaxValue;
            for (int i = 0; i < ownPorts.Count; i++)
                for (int j = i + 1; j < ownPorts.Count + pactPorts.Count; j++)
                {
                    var a = ownPorts[i];
                    var b = j < ownPorts.Count ? ownPorts[j]
                        : pactPorts[j - ownPorts.Count];
                    if (a.Id > b.Id) (a, b) = (b, a);
                    if (LaneExists(state, a.Id, b.Id)) continue;
                    int dist = HexGrid.Distance(a.Hex, b.Hex);
                    int tier = LaneMath.RequiredGateTier(cfg, dist, rangeBonus);
                    if (tier < 0) continue;                    // out of reach
                    if (!LaneNetwork.HasFreeGateSlot(state, a)
                        || !LaneNetwork.HasFreeGateSlot(state, b)) continue;
                    if (!LaneNetwork.DirectLaneEligible(state, a.Id, b.Id))
                        continue;
                    double cost = 2.0 * GateValue(cfg, tier);
                    if (pr.DevelopmentPoints < cost) continue;
                    if (cost < bestCost || (cost == bestCost && (dist < bestDist
                        || (dist == bestDist && (bestA == null || a.Id < bestA.Id
                            || (a.Id == bestA.Id && b.Id < bestB!.Id))))))
                    { bestCost = cost; bestDist = dist; bestTier = tier;
                      bestA = a; bestB = b; }
                }
            if (bestA == null) break;
            BuildLanePair(state, pr, bestA, bestB!, bestTier);
            built++;
        }
        return built;
    }

    /// <summary>Break ground on one gate pair (Task 9): the two gate
    /// facilities and the Lane row exist NOW uncommissioned; a construction
    /// project delivers the pair over the gate's build years, streaming its
    /// wages from the dev treasury and drawing the pair basket at the A end.
    /// The LaneOpened event fires at completion, not at groundbreaking — a
    /// half-built highway opens no lane. Founding links spawn goods-free:
    /// the expedition shipped the pair's basket (spec §4).</summary>
    private static void BuildLanePair(SimState state, PolityRecord pr,
                                      Port a, Port b, int tier,
                                      bool foundingLink = false)
    {
        ProjectOps.SpawnGatePair(state, pr.ActorId, pr.ActorId, a, b, tier,
                                 ProjectPriority.Growth, 0, foundingLink);
    }

    /// <summary>Administered founding value of one gate at a tier — the
    /// same founding-price convention CanAfford uses.</summary>
    internal static double GateValue(EpochSimConfig cfg, int tier)
    {
        var def = Substrate.Infrastructure.Get(Substrate.InfraTypeId.Gate);
        double value = 0;
        foreach (var q in def.BuildCost)
            value += q.Quantity * Market.InitialPrice(cfg.Economy, q.Good)
                     * Substrate.Production.TierCostFactor(tier);
        return value;
    }

    /// <summary>A belligerent raises a Mobilization project at its capital
    /// (spec §5): readiness ramps over years while consuming war materiel;
    /// fronts fight at CURRENT readiness — early battles use the standing
    /// force. At peace the ramp decays and in-flight mobilizations cancel.</summary>
    private static void SpawnMobilizations(SimState state, PolityRecord pr)
    {
        var cfg = state.Config;
        int years = cfg.Sim.YearsPerEpoch;
        bool atWar = WarOps.AtWar(state, pr.ActorId);
        if (!atWar)
        {
            foreach (var p in state.Projects)             // id order (P6)
                if (p.InFlight && p.Kind == ProjectKind.Mobilization
                    && p.OwnerActorId == pr.ActorId)
                    ProjectOps.Cancel(state, p);
            pr.Mobilization = Math.Max(0.0, pr.Mobilization
                - cfg.War.DemobilizationPerYear * years);
            return;
        }
        if (pr.Mobilization >= 1.0) return;
        foreach (var p in state.Projects)
            if (p.InFlight && p.Kind == ProjectKind.Mobilization
                && p.OwnerActorId == pr.ActorId) return;  // already raising
        int capital = -1;
        foreach (var port in state.Ports)                 // id order (P6)
            if (port.OwnerActorId == pr.ActorId) { capital = port.Id; break; }
        if (capital < 0) return;
        var proj = ProjectOps.Spawn(state, ProjectKind.Mobilization,
            pr.ActorId, pr.ActorId, capital, state.Ports[capital].Hex,
            cfg.War.MobilizationYears * (1.0 - pr.Mobilization),
            ProjectPriority.War, 0);
        // stamp the war the ramp answers (Project.cs:71 — Mobilization
        // TargetId = war id): the first active war this polity is party to,
        // in war-id order (F8)
        foreach (var w in state.Wars)                     // id order (P6)
            if (w.Active && w.Involves(pr.ActorId)) { proj.TargetId = w.Id; break; }
        proj.PerYearBasket[(int)Substrate.GoodId.Armaments] =
            cfg.War.MobilizationArmamentsPerYear;
        proj.PerYearBasket[(int)Substrate.GoodId.Fuel] =
            cfg.War.MobilizationFuelPerYear;
    }

    /// <summary>Break ground on the standing plan (spec §3, Move 2): each due
    /// entry becomes an in-flight construction project after its truth checks
    /// pass against CURRENT state — a stale plan simply finds its site taken,
    /// its port lost, or its treasury short and skips without charge. Advance
    /// (post-loop) feeds every project over its build years.</summary>
    private static void Groundbreak(SimState state, PolityRecord pr,
                                    StandingPlan plan)
    {
        int spanEnd = state.WorldYear + state.Config.Sim.YearsPerEpoch;
        for (int ix = 0; ix < plan.Entries.Count; ix++)
        {
            var entry = plan.Entries[ix];
            if (entry.StartYear >= spanEnd) continue;   // not due this step
            switch (entry.Kind)
            {
                case PlanEntryKind.Facility:
                    GroundbreakFacility(state, pr, entry, ix);
                    break;
                case PlanEntryKind.PortRaise:
                    GroundbreakPortRaise(state, pr, entry, ix);
                    break;
                case PlanEntryKind.HullBatch:
                    GroundbreakHullBatch(state, pr, entry, ix);
                    break;
            }
        }
    }

    /// <summary>Hull-batch groundbreak: an owned port with a design the
    /// polity still holds (a stale plan's design may have been superseded
    /// or belong to an actor no longer entered) and the military treasury
    /// to cover the administered value — the batch commissions when its
    /// build span delivers (Task 8).</summary>
    private static void GroundbreakHullBatch(SimState state, PolityRecord pr,
                                              PlanEntry entry, int planOrder)
    {
        if (entry.PortId < 0 || entry.PortId >= state.Ports.Count) return;
        var port = state.Ports[entry.PortId];
        if (port.OwnerActorId != pr.ActorId) return;
        if (entry.TypeId < 0 || entry.TypeId >= state.Designs.Count) return;
        var design = state.Designs[entry.TypeId];
        if (design.OwnerActorId != pr.ActorId) return;    // stale plan, not ours
        int count = Math.Max(1, entry.Count);
        double comp = DesignMath.ComponentsPerHull(state.Config.Fleet, design.Size);
        double armaments = DesignMath.ArmamentsPerHull(state.Config.Fleet,
            design.Role, design.Size);
        double value = count * (
            comp * Market.InitialPrice(state.Config.Economy,
                Substrate.GoodId.ShipComponents)
            + armaments * Market.InitialPrice(state.Config.Economy,
                Substrate.GoodId.Armaments));
        if (pr.MilitaryPoints < value) return;
        // yard-capacity truth (F4, plan-mandated): a port runs no more
        // concurrent hull batches than the active own-shipyard tier attached
        // to it — one tier-1 yard = one batch at a time
        int yardTiers = 0;
        foreach (var f in state.Facilities)               // id order (P6)
            if (f.TypeId == (int)Substrate.InfraTypeId.Shipyard
                && f.OwnerActorId == pr.ActorId
                && MarketEngine.IsActive(state, f)
                && MarketEngine.AttachedMarketIndex(state, f) == port.Id)
                yardTiers += f.Tier;
        int inFlightBatches = 0;
        foreach (var p in state.Projects)                 // id order (P6)
            if (p.InFlight && p.Kind == ProjectKind.HullBatch
                && p.PortId == port.Id)
                inFlightBatches++;
        if (inFlightBatches >= yardTiers) return;
        // honor the planner's staggered schedule: break ground at the entry's
        // scheduled year (clamped to now — never before this step), so a
        // coarse step that straddles the start only delivers the overlap (F3)
        ProjectOps.SpawnHullBatch(state, pr.ActorId, port.Id, design, count,
            entry.Priority, planOrder,
            Math.Max(state.WorldYear, entry.StartYear));
    }

    /// <summary>Facility groundbreak: an empty hex on an owned, under-capacity
    /// port with the development treasury to cover the administered value —
    /// the Facility row appears uncommissioned NOW (P1) and commissions when
    /// the build span delivers.</summary>
    private static void GroundbreakFacility(SimState state, PolityRecord pr,
                                            PlanEntry entry, int planOrder)
    {
        var cfg = state.Config;
        if (entry.PortId < 0 || entry.PortId >= state.Ports.Count) return;
        var port = state.Ports[entry.PortId];
        if (port.OwnerActorId != pr.ActorId) return;
        foreach (var f in state.Facilities)                   // id order (P6)
            if (f.Hex.Equals(entry.Hex)) return;              // site taken
        int cap = port.Tier * cfg.Infrastructure.FacilitiesPerPortTier;
        int attached = 0;
        foreach (var f in state.Facilities)                   // id order (P6)
        {
            if (f.TypeId == (int)Substrate.InfraTypeId.Gate) continue;
            if (f.OwnerActorId == pr.ActorId
                && MarketEngine.AttachedMarketIndex(state, f) == port.Id)
                attached++;
        }
        if (attached >= cap) return;
        var def = Substrate.Infrastructure.Get((Substrate.InfraTypeId)entry.TypeId);
        double value = 0;
        foreach (var q in def.BuildCost)
            value += q.Quantity * Market.InitialPrice(cfg.Economy, q.Good);
        if (pr.DevelopmentPoints < value) return;
        // honor the planner's staggered schedule (F3): ground broken at the
        // entry's scheduled year, clamped to now
        ProjectOps.SpawnFacilityConstruction(state, pr.ActorId, pr.ActorId,
            new ConstructionCandidate(entry.TypeId, entry.Hex, entry.PortId,
                                      0.0),
            entry.Priority, planOrder,
            Math.Max(state.WorldYear, entry.StartYear));
    }

    /// <summary>Port-raise groundbreak: an owned port below the tier ceiling
    /// with no raise already in flight and the treasury to cover the base
    /// cost — the tier lifts when the raise span delivers.</summary>
    private static void GroundbreakPortRaise(SimState state, PolityRecord pr,
                                             PlanEntry entry, int planOrder)
    {
        var cfg = state.Config;
        var ex = cfg.Expansion;
        if (entry.PortId < 0 || entry.PortId >= state.Ports.Count) return;
        var port = state.Ports[entry.PortId];
        if (port.OwnerActorId != pr.ActorId
            || port.Tier >= cfg.Infrastructure.MaxPortTier) return;
        foreach (var p in state.Projects)                     // id order (P6)
            if (p.InFlight && p.Kind == ProjectKind.PortRaise
                && p.TargetId == port.Id) return;             // already raising
        double baseCost = ex.PortUpgradeCostBase * port.Tier;
        if (pr.DevelopmentPoints < baseCost) return;
        double years = Math.Max(1.0, ex.PortUpgradeYears);
        // honor the planner's staggered schedule (F3)
        var proj = ProjectOps.SpawnAt(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, port.Id, port.Hex, years, entry.Priority, planOrder,
            Math.Max(state.WorldYear, entry.StartYear));
        proj.TargetId = port.Id;
        proj.PerYearBasket[(int)Substrate.GoodId.Alloys] =
            ex.PortUpgradeAlloysPerYearPerTier * port.Tier;
        proj.PerYearBasket[(int)Substrate.GoodId.Machinery] =
            ex.PortUpgradeMachineryPerYearPerTier * port.Tier;
        // exotics enter at tier 2+: a frontier port's first raise is
        // conventional engineering — refined exotics are deep-space-tier
        // infrastructure (slice CE amendment; a tier-1 colony could never
        // source them and every frontier raise starved to abandonment)
        proj.PerYearBasket[(int)Substrate.GoodId.RefinedExotics] =
            ex.PortUpgradeExoticsPerYearPerTier * (port.Tier - 1);
        proj.WagesPerYear = baseCost / years;
    }

    private static bool LaneExists(SimState state, int aId, int bId)
    {
        foreach (var l in state.Lanes)
            if (l.PortAId == aId && l.PortBId == bId) return true;
        return false;
    }
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
        int warsDeclared = 0, quarantines = 0;
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
                if (act is QuarantineAct quarantine
                    && PlagueOps.Quarantine(state, quarantine))
                    quarantines++;
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
        if (quarantines > 0)
            note += $", {quarantines} " + (quarantines == 1
                ? "lane quarantined" : "lanes quarantined");
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
        // world-time founding cadence (stage 2, P7): the controller
        // commits one founding per DECISION, so a finer clock would found
        // more often over the same world-years — the truth check holds
        // fire while the polity's last expedition is younger than the
        // cadence window (in flight, arrived, or turned back alike)
        foreach (var p in state.Projects)                 // id order (P6)
            if (p.Kind == ProjectKind.ColonyExpedition
                && p.OwnerActorId == act.ActorId
                && state.WorldYear - p.StartedYear
                   < cfg.Expansion.FoundingCadenceYears) return false;
        if (!state.Skeleton.TryGetCell(HexGrid.CellOf(act.Target), out var cell)
            || cell.IsVoid) return false;
        foreach (var p in state.Ports)
            if (p.Hex.Equals(act.Target)) return false;   // hex taken (or lost the collision)
        // a rival convoy already sailing for the hex wins the race here,
        // not two years out at a turn-back: perception's contention filter
        // can't see same-step spawns, but Resolution runs in actor order —
        // the earlier actor's expedition exists by now (slice CE, closing
        // the T2 turn-back-contention flag)
        foreach (var p in state.Projects)                 // id order (P6)
            if (p.InFlight && p.Kind == ProjectKind.ColonyExpedition
                && p.Hex.Equals(act.Target)) return false;
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
        double fuelNeed = state.Config.Fleet.FuelPerHullPerHexMoved * offLane;
        var (_, _, fuelCost) = BookOps.LiftAsks(state, staging.Id,
            (int)Substrate.GoodId.Fuel, fuelNeed, budget: double.MaxValue);
        record.Credits -= fuelCost;
        // the crossing takes world-time now: the founding body (port,
        // market, colony, founding facilities, the convoy's docking, the
        // chronicle, the encroachment bumps) lands when the expedition
        // arrives (ProjectOps.CompleteExpedition — Task 9)
        var expedition = ProjectOps.SpawnExpedition(state, act.ActorId,
            staging.Id, act.Target, convoy.Id, offLane);
        // the departure basket includes the founding gate PAIR's goods
        // (time-and-logistics spec §4, "Founding links get subsumed"),
        // sized to the link the crossing actually needs (stage 2:
        // TierCostFactor at dispatch, mirroring BuildLanes' tier pick): a
        // best-effort draw at the staging market, like the fuel — clamped
        // to what's there, no hard gate. The drawn kit RIDES the
        // expedition as cargo (PerYearBasket doubles as the hold for
        // travel kinds — they draw nothing en route) and comes home with
        // a turned-back convoy.
        var gateDef = Substrate.Infrastructure.Get(Substrate.InfraTypeId.Gate);
        int linkTier = LaneMath.RequiredGateTier(cfg, offLane,
            TechOps.AstroRadiusBonus(state, act.ActorId));
        if (linkTier < 0) linkTier = 3;   // farther than any gate: max kit
        double kitScale = Substrate.Production.TierCostFactor(linkTier);
        foreach (var q in gateDef.BuildCost)
        {
            // the kit is BOUGHT off the staging book now (best-effort,
            // clamped to what's for sale — no hard gate, like the fuel)
            var (drawn, _, kitCost) = BookOps.LiftAsks(state, staging.Id,
                (int)q.Good, 2.0 * q.Quantity * kitScale,
                budget: double.MaxValue);
            record.Credits -= kitCost;
            if (drawn <= 0) continue;
            expedition.PerYearBasket[(int)q.Good] = drawn;
        }
        return true;
    }
}

/// <summary>Phase 6 — interiors and demographics: the causal emergence
/// schedule fires entries (frame/time.md §Asymmetric emergence — slice F
/// retired the stub), homeworld founding (a polity enters by establishing
/// its first port at its seat — homeworlds are simply the first ports),
/// factions, characters, tech, corporations, growth, and migration.</summary>
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
            // entry is a calendar date: EntryEpoch counts generations
            // from year zero, whatever the integration step (P7, slice J)
            if (a.Entered || a.Retired
                || a.EntryEpoch * state.Config.Sim.GenerationYears
                   > state.WorldYear)
                continue;
            a.Entered = true;
            entered++;
            // every polity mints its own currency at founding (slice CU-1
            // genesis): this is the primary genesis point (original and
            // native-born polities), wired before the credit endowment below
            // so the treasury is denominated in a real currency from birth
            state.FoundCurrency(a.Id);
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

        // contagion first: plagues outbreak, ride the lanes, and take their
        // toll before anyone flees or grows (slice I)
        var (plagueOutbreaks, plagueSpread, plaguesBurnedOut) =
            PlagueOps.Step(state);
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
        if (plagueOutbreaks > 0)
            note += $", {plagueOutbreaks} plague "
                + (plagueOutbreaks == 1 ? "outbreak" : "outbreaks");
        if (plagueSpread > 0)
            note += $", plague spreads to {plagueSpread} "
                + (plagueSpread == 1 ? "port" : "ports");
        if (plaguesBurnedOut > 0)
            note += $", {plaguesBurnedOut} " + (plaguesBurnedOut == 1
                ? "plague burns out" : "plagues burn out");
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
        // blockades stop refugees as surely as freight (real interdiction)
        var severed = FleetOps.SeveredLaneIds(state);
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
                if (severed.Contains(lane.Id)) continue;
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
/// one log; public events over the magnitude floor emit news pulses that
/// arrive in future steps by distance and traffic (slice I). Chronicle runs
/// last so next step's news is this step's history.</summary>
public sealed class ChroniclePhase : ISimPhase
{
    public string Name => "Chronicle";

    public string Run(SimState state)
    {
        int pulses = 0;
        foreach (var e in state.Staged)
            Finalize(state, e, ref pulses);
        int count = state.Staged.Count;
        state.Staged.Clear();
        // the incremental POI compiler reads the epoch's finalized residue
        // and anchors it — the map is always current (slice I)
        var compiled = PoiCompiler.Compile(state);
        foreach (var e in compiled)
            Finalize(state, e, ref pulses);
        count += compiled.Count;
        string note = count == 1 ? "1 event finalized"
            : $"{count} events finalized";
        if (compiled.Count > 0)
            note += $", {compiled.Count} " + (compiled.Count == 1
                ? "POI compiled" : "POIs compiled");
        if (pulses > 0)
            note += $", {pulses} " + (pulses == 1 ? "pulse" : "pulses")
                + " emitted";
        return note;
    }

    /// <summary>Append one event to the log; public word over the floor
    /// pulses (arriving in future steps by distance and traffic).</summary>
    private static void Finalize(SimState state, StagedEvent e, ref int pulses)
    {
        var appended = state.Log.Append(state.WorldYear, e.Stratum, e.Type,
                         e.Actors, e.Location, e.Magnitude, e.Valence,
                         e.Visibility, e.Payload);
        if (e.Visibility == EventVisibility.Public
            && e.Magnitude >= state.Config.News.PulseMagnitudeFloor)
        {
            state.Pulses.Add(new NewsPulse(state.Pulses.Count, appended.Id,
                e.Location, state.WorldYear, e.Magnitude));
            pulses++;
        }
    }
}
