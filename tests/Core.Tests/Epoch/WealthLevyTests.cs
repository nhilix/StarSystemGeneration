using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice ME task 5 (household wealth recirculation): wealth a
/// segment accrues above what demand-band consumption can spend otherwise
/// piles up forever, so the Markets phase levies the excess into the port
/// owner's receipts — bounded, conserved, mirrors SettleSale's tax shape.</summary>
public class WealthLevyTests
{
    private static (SimState State, PopulationSegment Seg) Fixture(
        double wealth)
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        state.Ports.Add(new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0));
        state.Markets.Add(new Market(0, state.Config.Economy));
        var seg = new PopulationSegment(0, 0, state.PolityOf(0).SpeciesId,
            state.PolityOf(0).SpeciesId, 2.0) { Wealth = wealth };
        state.Segments.Add(seg);
        state.WorldYear = 100;
        return (state, seg);
    }

    [Fact]
    public void WealthAboveFloor_IsLeviedIntoTheSovereignsReceipts()
    {
        var (state, seg) = Fixture(wealth: 1000.0);
        var pr = state.PolityOf(0);
        double creditsBefore = pr.Credits;
        var eco = state.Config.Economy;
        int years = state.Config.Sim.YearsPerEpoch;
        double floor = seg.Size * eco.WealthTaxFloorPerPop;
        double expectedLevy = (seg.Wealth - floor) * eco.WealthTaxRatePerYear * years;

        new MarketsPhase().Run(state);

        // no farm, nothing to buy: the band escrow round-trips back to the
        // segment (BookMarketPhaseTests' BandEscrow_Refunds establishes the
        // same ~1e-3 floating noise floor for this no-supply fixture shape)
        Assert.Equal(1000.0 - expectedLevy, seg.Wealth, 3);
        Assert.Equal(creditsBefore + expectedLevy, pr.Credits, 6);
        Assert.Equal(expectedLevy, pr.Receipts, 6);
    }

    [Fact]
    public void WealthAtOrBelowFloor_IsUntouched()
    {
        var (state, seg) = Fixture(wealth: 30.0);   // floor = 2.0 * 20.0 = 40.0
        double before = seg.Wealth;

        new MarketsPhase().Run(state);

        Assert.Equal(before, seg.Wealth, 3);
        Assert.Equal(0.0, state.PolityOf(0).Receipts, 9);
    }
}
