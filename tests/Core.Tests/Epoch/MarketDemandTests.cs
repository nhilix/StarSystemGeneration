using System;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice D task 3 — demand assembles, price adjusts, clearing and
/// consequences (economy/markets.md §2/3/5): band demand from C's profiles ×
/// config rates × segment budgets, priority-order clearing, rate-limited
/// price drift, famine/SoL outcomes, black-book conversion, and the conserved
/// revenue distribution.</summary>
public class MarketDemandTests
{
    private static (SimState State, Port Port, PopulationSegment Seg) Fixture(
        double segmentSize = 3.0, double wealth = 1000.0)
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var port = new Port(0, actor.Id, actor.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        int species = state.PolityOf(actor.Id).SpeciesId;
        var seg = new PopulationSegment(0, port.Id, species, species, segmentSize)
        { Wealth = wealth };
        state.Segments.Add(seg);
        state.WorldYear = 100;
        return (state, port, seg);
    }

    private static MarketStepScratch Step(SimState state,
                                          bool distribute = true)
    {
        var scratch = new MarketStepScratch(state);
        MarketEngine.SupplyLands(state, scratch);
        MarketEngine.AssembleDemand(state, scratch);
        MarketEngine.AdjustPrices(state, scratch);
        MarketEngine.Clear(state, scratch);
        if (distribute) MarketEngine.DistributePools(state, scratch);
        return scratch;
    }

    [Fact]
    public void Clearing_ServesSubsistenceBeforeLuxury()
    {
        var (state, _, seg) = Fixture(wealth: 3.0);   // poverty: eat, don't shop
        var m = state.Markets[0];
        m.Deposit((int)GoodId.Provisions, 1000, 0.5);
        m.Deposit((int)GoodId.Luxuries, 1000, 0.5);
        m.Deposit((int)GoodId.Narcotics, 1000, 0.5);

        Step(state);

        Assert.True(m.LastCleared[(int)GoodId.Provisions] > 0);
        Assert.Equal(0.0, m.LastCleared[(int)GoodId.Luxuries]);
        Assert.Equal(0.0, m.LastCleared[(int)GoodId.Narcotics]);
        Assert.True(seg.Wealth < 3.0);                // they paid for dinner
    }

    [Fact]
    public void UnmetSubsistence_StagesAFamineEvent()
    {
        var (state, port, seg) = Fixture();           // empty market, no farms
        Step(state);

        Assert.True(seg.LastSubsistence < 1.0);
        bool famine = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.FamineStruck
                && e.Location.Equals(port.Hex)) famine = true;
        Assert.True(famine);
    }

    [Fact]
    public void OrganicBaseline_SoftensTheShortfall()
    {
        // no market supply at all: subsistence farming still feeds a little
        var (state, _, seg) = Fixture();
        Step(state);
        Assert.True(seg.LastSubsistence > 0.0,
            "baseline self-supply should be nonzero on a homeworld");
    }

    [Fact]
    public void SoL_RisesWhenServed_FallsWhenStarved()
    {
        var (fed, _, fedSeg) = Fixture();
        var fm = fed.Markets[0];
        fm.Deposit((int)GoodId.Provisions, 1000, 0.5);
        fm.Deposit((int)GoodId.ConsumerGoods, 1000, 0.5);
        fm.Deposit((int)GoodId.Medicine, 1000, 0.5);
        fm.Deposit((int)GoodId.Machinery, 1000, 0.5); // lithic SoL draw
        fm.Deposit((int)GoodId.Fuel, 1000, 0.5);      // machine subsistence
        fm.Deposit((int)GoodId.Compute, 1000, 0.5);
        double before = fedSeg.SoL;
        Step(fed);
        Assert.True(fedSeg.SoL > before, "served SoL band should raise SoL");

        var (starved, _, poorSeg) = Fixture();        // empty market
        double sBefore = poorSeg.SoL;
        Step(starved);
        Assert.True(poorSeg.SoL < sBefore, "starved SoL band should sink SoL");
    }

    [Fact]
    public void Price_RisesUnderExcessDemand_RateLimited()
    {
        var (state, _, _) = Fixture(segmentSize: 6.0);
        var m = state.Markets[0];
        m.Deposit((int)GoodId.Provisions, 0.5, 0.5);  // scraps vs real hunger
        double before = m.Price[(int)GoodId.Provisions];

        Step(state);

        double after = m.Price[(int)GoodId.Provisions];
        Assert.True(after > before);
        double cap = Math.Exp(state.Config.Economy.PriceDriftMaxPerYear
                              * state.Config.Sim.YearsPerEpoch);
        Assert.True(after <= before * cap + 1e-9, $"{after} vs cap {before * cap}");
    }

    [Fact]
    public void Price_FallsUnderGlut_NeverBelowTheFloor()
    {
        var (state, _, _) = Fixture(segmentSize: 0.01);
        var m = state.Markets[0];
        m.Deposit((int)GoodId.Ore, 100000, 0.5);      // nobody wants this much
        double before = m.Price[(int)GoodId.Ore];

        for (int i = 0; i < 50; i++) Step(state);

        Assert.True(m.Price[(int)GoodId.Ore] < before);
        Assert.True(m.Price[(int)GoodId.Ore] > 0);
    }

    [Fact]
    public void ProhibitedGoods_ConvertToBlackBookDemand()
    {
        var (state, port, _) = Fixture();
        var m = state.Markets[0];
        m.Deposit((int)GoodId.Provisions, 1000, 0.5);
        m.Deposit((int)GoodId.Narcotics, 1000, 0.5);
        var law = new System.Collections.Generic.Dictionary<int, LegalityLevel>
        { [(int)GoodId.Narcotics] = LegalityLevel.Prohibited };
        state.Actors[port.OwnerActorId].Policies =
            PolityPolicies.Default with { LawCode = law };

        Step(state);

        Assert.Equal(0.0, m.LastCleared[(int)GoodId.Narcotics]);
        Assert.True(m.BlackBookDemand[(int)GoodId.Narcotics] > 0);
        Assert.True(m.BlackBookPrice[(int)GoodId.Narcotics]
                    > m.Price[(int)GoodId.Narcotics]);   // high margins
    }

    [Fact]
    public void GenesisController_WritesASpeciesLawCode()
    {
        var closed = new SpeciesProfile { Id = 0, Name = "Zeal", Openness = 0.2 };
        var view = new PerceptionView(0, 0, new int[0], selfSpecies: closed);
        var decision = new GenesisController(new EpochSimConfig()).Decide(view);
        var policies = Assert.IsType<PolityPolicies>(decision.Policies);
        Assert.Equal(LegalityLevel.Prohibited,
                     policies.LawCode[(int)GoodId.Narcotics]);

        var open = new SpeciesProfile { Id = 1, Name = "Free", Openness = 0.9 };
        var openView = new PerceptionView(1, 0, new int[0], selfSpecies: open);
        var openPolicies = (PolityPolicies)new GenesisController(new EpochSimConfig())
            .Decide(openView).Policies;
        Assert.False(openPolicies.LawCode.ContainsKey((int)GoodId.Narcotics));
    }

    [Fact]
    public void MarketStep_ConservesCredits()
    {
        var (state, port, _) = Fixture();
        Fill(state.Markets[0]);
        state.PolityOf(port.OwnerActorId).Credits = 500;
        // a producing facility so wages, inputs, and payouts all flow
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Refinery, 1,
            port.Hex, port.OwnerActorId, state.WorldYear - 10));

        double before = TotalCredits(state);
        Step(state);
        Assert.Equal(before, TotalCredits(state), 6);
    }

    [Fact]
    public void Wages_ReachTheStaffingSegments()
    {
        var (state, port, seg) = Fixture(wealth: 0.0);
        state.PolityOf(port.OwnerActorId).Credits = 500;
        state.Facilities.Add(new Facility(0, (int)InfraTypeId.Mine, 1,
            port.Hex, port.OwnerActorId, state.WorldYear - 10));

        var scratch = new MarketStepScratch(state);
        MarketEngine.SupplyLands(state, scratch);

        Assert.True(seg.Wealth > 0, "labor share should be paid at production");
    }

    private static void Fill(Market m)
    {
        for (int g = 0; g < Goods.All.Count; g++)
            m.Deposit(g, 200, 0.5);
    }

    private static double TotalCredits(SimState state)
    {
        double total = 0;
        foreach (var p in state.Polities) total += p.Credits;
        foreach (var s in state.Segments) total += s.Wealth;
        return total;
    }
}
