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

    private static void StockBuildGoods(Market m, double qty = 500)
    {
        m.Deposit((int)GoodId.Alloys, qty, 0.5);
        m.Deposit((int)GoodId.Machinery, qty, 0.5);
        m.Deposit((int)GoodId.Composites, qty, 0.5);
    }

    [Fact]
    public void Income_SplitsByBudgetWeights_StubGone()
    {
        var (state, _) = Fixture(credits: 100);
        var pr = state.PolityOf(0);
        new AllocationPhase().Run(state);

        var budget = PolityPolicies.Default.Budget;
        Assert.Equal(100 * budget.Expansion, pr.ExpansionPoints, 6);
        // development share accrued, then possibly spent on lanes/tiers/builds —
        // total credits + treasuries never exceed the original 100
        Assert.True(pr.Credits + pr.ExpansionPoints + pr.DevelopmentPoints
                    <= 100 + 1e-9);
        Assert.True(pr.Credits < 100);
    }

    [Fact]
    public void Construction_BuildsAFacility_ConsumingRealGoods()
    {
        var (state, port) = Fixture();
        var pr = state.PolityOf(0);
        pr.DevelopmentPoints = 500;
        var m = state.Markets[0];
        StockBuildGoods(m);
        double alloysBefore = m.Inventory[(int)GoodId.Alloys];

        new AllocationPhase().Run(state);

        Assert.True(state.Facilities.Count > 0, "development should build");
        Assert.True(m.Inventory[(int)GoodId.Alloys] < alloysBefore,
            "construction consumes real goods");
        bool staged = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.FacilityBuilt) staged = true;
        Assert.True(staged);
    }

    [Fact]
    public void Construction_ChasesThePriceSignal()
    {
        var (state, port) = Fixture();
        var pr = state.PolityOf(0);
        pr.DevelopmentPoints = 500;
        var m = state.Markets[0];
        StockBuildGoods(m);
        // consumer goods desperately scarce: the fabricator should win
        m.Price[(int)GoodId.ConsumerGoods] = 60.0;

        new AllocationPhase().Run(state);

        bool builtFabricator = false;
        foreach (var f in state.Facilities)
            if (f.TypeId == (int)InfraTypeId.Fabricator) builtFabricator = true;
        Assert.True(builtFabricator,
            "the scarce good's producer should out-bid other types");
    }

    [Fact]
    public void Construction_RespectsThePortCap()
    {
        var (state, port) = Fixture();
        var pr = state.PolityOf(0);
        pr.DevelopmentPoints = 100000;
        StockBuildGoods(state.Markets[0], 100000);

        for (int i = 0; i < 10; i++) new AllocationPhase().Run(state);

        int cap = port.Tier * state.Config.Infrastructure.FacilitiesPerPortTier;
        Assert.True(state.Facilities.Count <= cap,
            $"{state.Facilities.Count} facilities exceed the tier cap {cap}");
    }

    [Fact]
    public void Construction_SkipsWhenGoodsUnavailable()
    {
        var (state, _) = Fixture();
        state.PolityOf(0).DevelopmentPoints = 500;   // empty market

        new AllocationPhase().Run(state);

        Assert.Empty(state.Facilities);
    }

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
        StockBuildGoods(fed.Markets[0]);
        new AllocationPhase().Run(fed);
        Assert.True(f2.Condition > 0.5, "met upkeep should restore condition");
    }

    [Fact]
    public void Reserves_DecayByPerishability()
    {
        var (state, _) = Fixture();
        var pr = state.PolityOf(0);
        pr.ReserveQty[(int)GoodId.Provisions] = 100;
        pr.ReserveQty[(int)GoodId.Alloys] = 100;

        new AllocationPhase().Run(state);

        Assert.True(pr.ReserveQty[(int)GoodId.Provisions] < 100);
        Assert.True(pr.ReserveQty[(int)GoodId.Alloys] < 100);
        Assert.True(pr.ReserveQty[(int)GoodId.Provisions]
                    < pr.ReserveQty[(int)GoodId.Alloys],
            "provisions rot faster than durables");
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
        Assert.True(state.PolityOf(0).Credits >= 0, "the loan should cover the hole");
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
