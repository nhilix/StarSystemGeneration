using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CE C3 (contract-economy spec §2): the Markets phase runs
/// through the order book end to end — production posts owner sell orders,
/// the port posts band bids escrowed from segment wealth, matching feeds
/// the bands, and the famine/SoL consequences derive from FILL fractions.
/// The anonymous shelf is gone: unsold output is visible as resting asks.</summary>
public class BookMarketPhaseTests
{
    /// <summary>One port, one owner polity, an AgriComplex commissioned on
    /// good terrain, and one hungry segment with money.</summary>
    private static (SimState State, PopulationSegment Seg) Fixture(
        bool withFarm)
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        state.Ports.Add(new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0));
        state.Markets.Add(new Market(0, state.Config.Economy));
        var seg = new PopulationSegment(0, 0, state.PolityOf(0).SpeciesId,
            state.PolityOf(0).SpeciesId, 2.0) { Wealth = 1000.0 };
        state.Segments.Add(seg);
        if (withFarm)
        {
            var farm = new Facility(0, (int)InfraTypeId.AgriComplex, 2,
                a0.Seat, a0.Id, builtYear: 0)
            { Condition = 1.0 };
            state.Facilities.Add(farm);
        }
        state.WorldYear = 100;
        return (state, seg);
    }

    [Fact]
    public void SuppliedPort_FeedsItsBands_ThroughTheBook()
    {
        var (state, seg) = Fixture(withFarm: true);

        new MarketsPhase().Run(state);

        // the farm's output went up as ITS OWNER's sell order and the
        // bands ate through the book — no famine, wealth actually spent
        Assert.True(seg.LastSubsistence > 0.5,
            $"fed fraction {seg.LastSubsistence}");
        Assert.True(seg.Wealth < 1000.0, "the bands paid for their food");
        bool ownerSell = false;
        foreach (var o in state.Orders)
            if (o.Side == OrderSide.Sell && o.OwnerActorId == 0
                && o.PortId == 0) ownerSell = true;
        Assert.True(ownerSell, "unsold output rests as the owner's ask");
        // and no band escrow leaked: every live buy order belongs to
        // someone, none to the band poster (they cancel at step end)
        foreach (var o in state.Orders)
            Assert.False(o.Side == OrderSide.Buy && o.EscrowCredits > 0
                && o.ExpiryYear <= state.WorldYear, "stale band escrow");
    }

    [Fact]
    public void BareBook_Starves_AndStagesTheFamine()
    {
        var (state, seg) = Fixture(withFarm: false);

        new MarketsPhase().Run(state);

        Assert.True(seg.LastSubsistence < 0.5,
            $"fed fraction {seg.LastSubsistence}");
        bool famine = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.FamineStruck) famine = true;
        Assert.True(famine, "an unfed port stages its famine");
    }

    [Fact]
    public void BandEscrow_Refunds_WhatTheBookCouldNotFill()
    {
        var (state, seg) = Fixture(withFarm: false);
        double before = seg.Wealth;
        var eco = state.Config.Economy;
        double floor = seg.Size * eco.WealthTaxFloorPerPop;
        // compounded per world-year (fix wave 1), the DecayIdlePools shape
        double expectedLevy = (before - floor)
            * (1.0 - System.Math.Pow(1.0 - eco.WealthTaxRatePerYear,
                                     state.Config.Sim.YearsPerEpoch));

        new MarketsPhase().Run(state);

        // nothing to buy: the posted escrow came back to the segment — the
        // only real drain left is the wealth levy on the excess over the
        // per-capita floor (slice ME task 5), which fires regardless of
        // whether the book had anything to sell
        Assert.Equal(before - expectedLevy, seg.Wealth, 3);
    }
}
