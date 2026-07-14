using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice D task 5 — Allocation rework: real market income splits by
/// budget weights (the slice-B stub is gone), facility construction executes
/// siting × price signal against real goods, upkeep gates condition, reserves
/// decay by perishability, and simple credit closes the loop
/// (assets-and-investment.md, economy/markets.md §Credit).</summary>
public class AllocationEconomyTests
{
    private static (SimState State, Port Port) Fixture(double credits = 1000)
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var port = new Port(0, actor.Id, actor.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        int species = state.PolityOf(actor.Id).SpeciesId;
        state.Segments.Add(new PopulationSegment(0, 0, species, species, 3.0));
        state.PolityOf(actor.Id).Credits = credits;
        state.WorldYear = 100;
        return (state, port);
    }

    private static void StockBuildGoods(SimState state, int portId,
                                        double qty = 500)
    {
        // the works feed from the site larder (and their bid-filled yard);
        // the anonymous shelf is gone (contract economy)
        state.Ports[portId].DepositStock((int)GoodId.Alloys, qty, 0.5);
        state.Ports[portId].DepositStock((int)GoodId.Machinery, qty, 0.5);
        state.Ports[portId].DepositStock((int)GoodId.Composites, qty, 0.5);
    }

    [Fact]
    public void Income_SplitsByBudgetWeights_StubGone()
    {
        // slice ME §1: the base now reads Receipts, not the balance — the
        // income is the receipts the Markets phase accrued into Credits, so
        // seed both. Idle-pool decay off to read the raw split (that recycle
        // has its own coverage in AllocationMonetaryTests).
        var (state, _) = Fixture(credits: 100);
        var pr = state.PolityOf(0);
        pr.Receipts = 100;
        state.Config.Economy.PoolIdleDecayPerYear = 0.0;
        // the steady mint (Part B) would widen the base past raw Receipts; it has
        // its own coverage in AllocationMonetaryTests, so read the pure split here
        state.Config.Economy.SteadyIssuanceRate = 0.0;
        new AllocationPhase().Run(state);

        var budget = PolityPolicies.Default.Budget;
        Assert.Equal(100 * budget.Expansion, pr.ExpansionPoints, 6);
        // stage 2: the reserve share accrues too — Budget.Reserves was a
        // dead line until located stockpiles gave it a treasury to fill
        Assert.Equal(100 * budget.Reserves, pr.ReservePoints, 6);
        // development share accrued, then possibly spent on lanes/tiers/builds —
        // total credits + treasuries never exceed the original 100
        Assert.True(pr.Credits + pr.ExpansionPoints + pr.DevelopmentPoints
                    <= 100 + 1e-9);
        Assert.True(pr.Credits < 100);
    }

    /// <summary>Construction-volume test (Task 7): the greedy loop is gone —
    /// AllocationPhase now breaks ground on the standing plan and Advance
    /// feeds the construction project. A Mine builds in 2 years, well inside
    /// one 25-year span, so a single Allocation flows the whole cycle
    /// plan → groundbreak → advance → commission, consuming real goods and
    /// staging FacilityBuilt.</summary>
    [Fact]
    public void Construction_BuildsAFacility_ConsumingRealGoods()
    {
        var (state, port) = Fixture();
        var pr = state.PolityOf(0);
        pr.DevelopmentPoints = 500;
        StockBuildGoods(state, port.Id);
        double alloysBefore = port.StockQty[(int)GoodId.Alloys];
        // the standing plan the groundbreak pass executes: a Mine on the
        // capital, due now (StartYear at or before this span)
        var entry = new PlanEntry(PlanEntryKind.Facility, ProjectPriority.Core,
            state.WorldYear, (int)InfraTypeId.Mine, port.Id, port.Hex, 1);
        state.Actors[0].Policies = PolityPolicies.Default with
        {
            Plan = new StandingPlan(new[] { entry }),
        };

        new AllocationPhase().Run(state);

        Assert.True(state.Facilities.Count > 0, "the plan should break ground");
        Assert.True(port.StockQty[(int)GoodId.Alloys] < alloysBefore,
            "construction consumes real goods");
        bool staged = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.FacilityBuilt) staged = true;
        Assert.True(staged);
    }

    /// <summary>Candidate-scan internals (moved to CapabilityOps, Task 5):
    /// the price-signal term should make the scarce good's producer
    /// outscore every other buildable type at the port.</summary>
    [Fact]
    public void Construction_ChasesThePriceSignal()
    {
        var (state, port) = Fixture();
        var pr = state.PolityOf(0);
        var m = state.Markets[0];
        StockBuildGoods(state, port.Id);
        // consumer goods desperately scarce: the fabricator should win
        m.Price[(int)GoodId.ConsumerGoods] = 60.0;

        var candidates = CapabilityOps.ConstructionCandidatesFor(state, pr.ActorId);

        Assert.True(candidates.Count > 0, "the scan should surface a top candidate");
        Assert.Equal((int)InfraTypeId.Fabricator, candidates[0].TypeId);
    }

    /// <summary>Candidate-scan internals (moved to CapabilityOps, Task 5):
    /// the saturation term (score / (1 + existing)) must diversify the
    /// per-port top pick across successive groundbreaks — without it the
    /// single top scorer would repeat every time.</summary>
    [Fact]
    public void Construction_DiversifiesAcrossTypes()
    {
        var (state, port) = Fixture();
        var pr = state.PolityOf(0);
        StockBuildGoods(state, port.Id, 100000);
        // every product desperately scarce: without the saturation penalty
        // the single top scorer would repeat
        for (int g = 0; g < Goods.All.Count; g++)
            state.Markets[0].Price[g] = Market.InitialPrice(
                state.Config.Economy, (GoodId)g) * 50;

        var types = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < 6; i++)
        {
            var candidates = CapabilityOps.ConstructionCandidatesFor(state, pr.ActorId);
            Assert.True(candidates.Count > 0, $"round {i}: scan found nothing to build");
            var top = candidates[0];
            types.Add(top.TypeId);
            ProjectOps.SpawnFacilityConstruction(state, pr.ActorId, pr.ActorId,
                top, ProjectPriority.Core, planOrder: i);
        }

        Assert.True(types.Count >= 3,
            $"a port should grow a chain, not a monoculture ({types.Count} types)");
    }

    [Fact]
    public void GenesisController_TargetsWarMateriel_ByTemperament()
    {
        var hawk = new SpeciesProfile { Id = 0, Name = "Kri", Militancy = 0.9 };
        var view = new PerceptionView(0, 0, new int[0], selfSpecies: hawk,
                                      ownPortCount: 4);
        var policies = (PolityPolicies)new GenesisController(new EpochSimConfig())
            .Decide(view).Policies;
        Assert.True(policies.StockpileTargets.TryGetValue(
            (int)GoodId.Armaments, out double armaments) && armaments > 0);
        Assert.True(policies.StockpileTargets.ContainsKey((int)GoodId.Machinery));
    }

    /// <summary>Candidate-scan internals (moved to CapabilityOps, Task 5):
    /// a port already at its facility-per-tier cap (uncommissioned sites
    /// count, per spec §2) offers no candidates at all.</summary>
    [Fact]
    public void Construction_RespectsThePortCap()
    {
        var (state, port) = Fixture();
        var pr = state.PolityOf(0);
        int cap = port.Tier * state.Config.Infrastructure.FacilitiesPerPortTier;
        for (int i = 0; i < cap; i++)
            state.Facilities.Add(new Facility(state.Facilities.Count,
                (int)InfraTypeId.Mine, 1, port.Hex, pr.ActorId, state.WorldYear));

        var candidates = CapabilityOps.ConstructionCandidatesFor(state, pr.ActorId);

        Assert.Empty(candidates);
    }

    // Construction_SkipsWhenGoodsUnavailable removed (Task 5): it asserted
    // the old CanAfford market-stock gate, which the brief deletes outright
    // — affordability is now the planner's rate packing + groundbreak's
    // treasury check (spec §2), not the perceived candidate scan's job.

    [Fact]
    public void Upkeep_UnmetDecaysCondition_MetRestoresIt()
    {
        var (starved, portA) = Fixture();
        var f1 = new Facility(0, (int)InfraTypeId.Mine, 1, portA.Hex, 0,
                              builtYear: 50);
        starved.Facilities.Add(f1);                  // empty market: no upkeep
        new AllocationPhase().Run(starved);
        Assert.True(f1.Condition < 1.0, "unmet upkeep should decay condition");

        var (fed, portB) = Fixture();
        var f2 = new Facility(0, (int)InfraTypeId.Mine, 1, portB.Hex, 0,
                              builtYear: 50) { Condition = 0.5 };
        fed.Facilities.Add(f2);
        // upkeep is BOUGHT off the book now: put the goods up for sale
        EpochTestKit.Stock(fed, portB.Id, (int)GoodId.Machinery, 500, 0.5,
            ownerActorId: 1);
        EpochTestKit.Stock(fed, portB.Id, (int)GoodId.Fuel, 500, 0.5,
            ownerActorId: 1);
        EpochTestKit.Stock(fed, portB.Id, (int)GoodId.ShipComponents, 500,
            0.5, ownerActorId: 1);
        new AllocationPhase().Run(fed);
        Assert.True(f2.Condition > 0.5, "met upkeep should restore condition");
    }

    [Fact]
    public void Stockpiles_DecayByPerishability_PerPort()
    {
        var (state, port) = Fixture();
        port.StockQty[(int)GoodId.Provisions] = 100;
        port.StockQty[(int)GoodId.Alloys] = 100;

        new AllocationPhase().Run(state);

        Assert.True(port.StockQty[(int)GoodId.Provisions] < 100);
        Assert.True(port.StockQty[(int)GoodId.Alloys] < 100);
        Assert.True(port.StockQty[(int)GoodId.Provisions]
                    < port.StockQty[(int)GoodId.Alloys],
            "provisions rot faster than durables");
    }

    /// <summary>Review fix 4 (P7): decay compounds per world-year, so a
    /// 25-year step rots exactly what twenty-five 1-year steps rot.</summary>
    [Fact]
    public void StockpileDecay_IsTickInvariant()
    {
        var (coarse, coarsePort) = Fixture();
        coarsePort.StockQty[(int)GoodId.Provisions] = 100;
        new AllocationPhase().Run(coarse);

        var (fine, finePort) = Fixture();
        fine.Config.Sim.YearsPerEpoch = 1;
        finePort.StockQty[(int)GoodId.Provisions] = 100;
        for (int i = 0; i < 25; i++) new AllocationPhase().Run(fine);

        Assert.Equal(coarsePort.StockQty[(int)GoodId.Provisions],
                     finePort.StockQty[(int)GoodId.Provisions], 6);
    }

    /// <summary>The controller contract's "stockpile targets →
    /// depots/reserves" mechanism (spec §4b): an active Depot at the port
    /// cuts the stockpile's decay.</summary>
    [Fact]
    public void Depot_CutsStockpileDecay()
    {
        var (withDepot, port) = Fixture();
        port.StockQty[(int)GoodId.Provisions] = 100;
        withDepot.Facilities.Add(new Facility(0,
            (int)InfraTypeId.Depot, 1, port.Hex, 0, builtYear: 0));
        var (bare, barePort) = Fixture();
        barePort.StockQty[(int)GoodId.Provisions] = 100;

        new AllocationPhase().Run(withDepot);
        new AllocationPhase().Run(bare);

        Assert.True(port.StockQty[(int)GoodId.Provisions]
                    > barePort.StockQty[(int)GoodId.Provisions],
            "a depot should slow the rot");
    }

    [Fact]
    public void Insolvency_BorrowsFromTheRichest_AndServicesTheLoan()
    {
        var (state, _) = Fixture(credits: -50);      // underwater
        var lenderActor = state.Actors[1];
        lenderActor.Entered = true;
        state.PolityOf(1).Credits = 1000;

        new AllocationPhase().Run(state);

        Assert.True(state.Loans.Count == 1, "an insolvent polity should borrow");
        var loan = state.Loans[0];
        Assert.Equal(1, loan.LenderActorId);
        Assert.Equal(0, loan.BorrowerActorId);
        // Borrow ran at the top against the carried -50: principal = 1.2*50 = 60
        // covers the hole (the loan-financing fix then routes that principal
        // into the same epoch's budget split, so Credits itself ends negative
        // again — the mint backstops only the capped residual, tested elsewhere)
        Assert.Equal(60.0, loan.Principal, 6);
        Assert.True(state.PolityOf(1).Credits < 1000, "lender fronts the principal");
        bool staged = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.LoanIssued) staged = true;
        Assert.True(staged);

        // servicing: next allocation pays interest+principal to the lender
        state.PolityOf(0).Credits = 500;
        double lenderBefore = state.PolityOf(1).Credits;
        double principalBefore = loan.Principal;
        new AllocationPhase().Run(state);
        Assert.True(state.PolityOf(1).Credits > lenderBefore, "interest flows");
        Assert.True(loan.Principal < principalBefore, "principal amortizes");
    }

    // Slice ME task 4 — the lender search widens to corporations: no polity
    // holds surplus here, only a corporation does, so the loan must find it.
    [Fact]
    public void Insolvency_BorrowsFromACorporation_WhenOnlyItHoldsCollateral()
    {
        var (state, _) = Fixture(credits: -50);      // underwater
        int corpActor = state.Actors.Count;
        state.Actors.Add(new Actor(corpActor, ActorKind.Corporation, "Vex",
            default, state.EpochIndex,
            new CorporateController(state.Config)) { Entered = true });
        var corp = new Corporation(0, corpActor, "Vex", state.Actors[0].Id,
            CorporateNiche.Freight, homePortId: 0, state.WorldYear)
        { Credits = 1000 };
        state.Corporations.Add(corp);

        new AllocationPhase().Run(state);

        Assert.True(state.Loans.Count == 1, "an insolvent polity should borrow from a corp lender");
        var loan = state.Loans[0];
        Assert.Equal(corpActor, loan.LenderActorId);
        Assert.Equal(0, loan.BorrowerActorId);
        // Borrow ran at the top against the carried -50: principal = 1.2*50 = 60
        // covers the hole (the fix then routes it into this epoch's budget split)
        Assert.Equal(60.0, loan.Principal, 6);
        Assert.True(corp.Credits < 1000, "corp lender fronts the principal");
        bool staged = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.LoanIssued) staged = true;
        Assert.True(staged);

        // servicing: next allocation pays interest+principal to the corp lender
        state.PolityOf(0).Credits = 500;
        double lenderBefore = corp.Credits;
        double principalBefore = loan.Principal;
        new AllocationPhase().Run(state);
        Assert.True(corp.Credits > lenderBefore, "interest flows to the corp");
        Assert.True(loan.Principal < principalBefore, "principal amortizes");
    }

    [Fact]
    public void Default_SeizesAFacility()
    {
        var (state, port) = Fixture(credits: -50);
        var lenderActor = state.Actors[1];
        lenderActor.Entered = true;
        state.PolityOf(1).Credits = 1000;
        var mine = new Facility(0, (int)InfraTypeId.Mine, 1, port.Hex, 0,
                                builtYear: 50);
        state.Facilities.Add(mine);

        new AllocationPhase().Run(state);            // borrows
        var loan = state.Loans[0];
        // hopeless borrower: deeply underwater at the next servicing
        state.PolityOf(0).Credits = -10000;
        new AllocationPhase().Run(state);

        Assert.True(loan.Closed, "an unpayable loan defaults");
        Assert.Equal(1, mine.OwnerActorId);          // the lender owns the mine
        bool staged = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.LoanDefaulted) staged = true;
        Assert.True(staged);
    }

    [Fact]
    public void ColonyValuation_ReadsThePriceSignal()
    {
        var (state, _) = Fixture();
        var baseline = ColonyValuation.CandidatesFor(state, 0);
        if (baseline.Count == 0) return;             // no reachable targets this seed

        // ore scarcity at the capital should raise mineral targets' scores
        state.Markets[0].Price[(int)GoodId.Ore] =
            state.Config.Economy.BasePriceRaw * 10;
        var pricy = ColonyValuation.CandidatesFor(state, 0);

        double sumBefore = 0, sumAfter = 0;
        foreach (var c in baseline) sumBefore += c.Score;
        foreach (var c in pricy) sumAfter += c.Score;
        Assert.True(sumAfter > sumBefore,
            "scarcity should make resource targets more attractive");
    }
}
