using System;
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
        // compounded per world-year (fix wave 1), the DecayIdlePools shape
        double expectedLevy = (seg.Wealth - floor)
            * (1.0 - Math.Pow(1.0 - eco.WealthTaxRatePerYear, years));

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

    // fix wave 1 — the levy compounds per world-year, it never scales linearly:
    // a two-year-in-one-step levy is LESS than double a one-year levy (the
    // DecayIdlePools shape). A linear rate*years would drain a coarse step of a
    // larger fraction than the equivalent fine ticks — a P7 tick-honesty break.
    [Fact]
    public void Levy_CompoundsPerYear_NotLinearly()
    {
        double LevyOverYears(int years)
        {
            var (state, seg) = Fixture(wealth: 1000.0);
            state.Config.Economy.WealthTaxRatePerYear = 0.1;
            state.Config.Sim.YearsPerEpoch = years;
            double before = seg.Wealth;
            new MarketsPhase().Run(state);
            return before - seg.Wealth;
        }

        double one = LevyOverYears(1);
        double two = LevyOverYears(2);
        Assert.True(two > one,
            $"two years ({two:0.###}) must levy more than one ({one:0.###})");
        Assert.True(two < 2.0 * one,
            $"compounding: the two-year levy ({two:0.###}) must be under double "
            + $"the one-year levy ({2.0 * one:0.###}), not equal to it");
    }

    // fix wave 1 — clamped by construction: the compound fraction stays in
    // [0,1), so even a punishing rate*years never levies more than the taxable
    // excess and never drives a segment below the exemption floor.
    [Fact]
    public void Levy_NeverExceedsTaxable_AtHighKnobValues()
    {
        var (state, seg) = Fixture(wealth: 1000.0);
        var eco = state.Config.Economy;
        eco.WealthTaxRatePerYear = 0.5;             // rate*years = 12.5 linearly
        double floor = seg.Size * eco.WealthTaxFloorPerPop;
        double taxable = seg.Wealth - floor;
        double before = seg.Wealth;

        new MarketsPhase().Run(state);

        double levy = before - seg.Wealth;
        Assert.True(levy <= taxable + 1e-6,
            $"levy ({levy:0.###}) exceeded the taxable excess ({taxable:0.###})");
        Assert.True(seg.Wealth >= floor - 1e-3,
            $"wealth ({seg.Wealth:0.###}) fell below the floor ({floor:0.###})");
    }
}
