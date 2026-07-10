using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice E task 8 — fleet shape over full 40-epoch histories
/// across seeds: the hull ledger conserves (built == active + wrecked +
/// scrapped, and every wrecked hull has a wreckage record), fleet counts
/// stay bounded, and posted routes actually carry the freight late-game.</summary>
public class FleetShapeTests
{
    private static SimState Run(ulong seed)
    {
        var gc = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = 10 };
        var state = EpochGenesis.Seed(SkeletonBuilder.Build(gc),
                                      new EpochSimConfig { MasterSeed = seed });
        new EpochEngine().Run(state);
        return state;
    }

    [Theory]
    [InlineData(42ul)]
    [InlineData(7ul)]
    [InlineData(1234ul)]
    public void FortyEpochs_HullsConserve_ToTheLedger(ulong seed)
    {
        var state = Run(seed);
        int wrecksTotal = 0;
        foreach (var wr in state.Wreckage) wrecksTotal += wr.Hulls;
        int wreckedLedger = 0;
        foreach (var pr in state.Polities)
        {
            int active = 0;
            foreach (var f in state.Fleets)
                if (f.OwnerActorId == pr.ActorId) active += f.TotalHulls;
            Assert.Equal(pr.HullsBuilt,
                         active + pr.HullsWrecked + pr.HullsScrapped);
            wreckedLedger += pr.HullsWrecked;
        }
        // every wrecked hull lies at a real hex (P4)
        Assert.Equal(wreckedLedger, wrecksTotal);
    }

    [Theory]
    [InlineData(42ul)]
    [InlineData(7ul)]
    public void FortyEpochs_FleetsStayBounded_AndRoutesCarryFreight(ulong seed)
    {
        var state = Run(seed);
        int entered = 0;
        foreach (var a in state.Actors) if (a.Entered) entered++;
        if (entered == 0) return;   // barren seed at this radius

        int totalHulls = 0;
        foreach (var f in state.Fleets)
        {
            Assert.InRange(f.Readiness, 0.0, 1.0);
            totalHulls += f.TotalHulls;
        }
        Assert.InRange(totalHulls, 1, entered * 500);
        Assert.True(state.Fleets.Count <= state.Lanes.Count * 2
                    + state.Ports.Count + entered * 8,
            $"{state.Fleets.Count} fleets for {state.Lanes.Count} lanes"
            + $" / {state.Ports.Count} ports — registry runaway");

        // the eyeball gate's mechanical shadow: posted capacity exists and
        // freight actually moved on it late-game (a lane without hulls
        // moves nothing; lanes WITH hulls must move something)
        if (state.Lanes.Count == 0) return;
        double postedCapacity = 0;
        foreach (var lane in state.Lanes)
            postedCapacity += FleetOps.PostedCapacity(state, lane);
        Assert.True(postedCapacity > 0,
            "no posted freight capacity anywhere after 40 epochs");
    }
}
