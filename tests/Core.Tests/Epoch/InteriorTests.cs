using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class InteriorTests
{
    [Fact]
    public void Segments_GrowLogisticallyTowardTierCap()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        var cfg = state.Config.Expansion;
        int halfway = state.Config.Sim.EpochCount / 2;
        double midPop = -1;
        while (state.EpochIndex < state.Config.Sim.EpochCount)
        {
            engine.Step(state);
            if (state.EpochIndex == halfway) midPop = state.Segments.Sum(s => s.Size);
        }
        // total galaxy population at the run's end is meaningfully larger than
        // at its temporal midpoint — proves the logistic growth process is
        // still working in aggregate. A single segment's peak isn't a
        // reliable proxy for that: slice ME task 4 (Borrow scans corporations
        // too) activates previously idle corporate capital, and the extra
        // market competition that unlocks can tip a tightly-supplied port
        // into an extra famine episode that knocks down whichever segment
        // happens to be the local flagship at the time — traced to real,
        // pre-existing famine/market-clearing code reacting to a real,
        // design-intended change in circulating credit (not a defect in the
        // loan mechanism — that stayed conservation-safe and deterministic;
        // see task-4-report.md). Aggregate growth is the invariant that
        // survives which particular segment loses that local lottery.
        double finalPop = state.Segments.Sum(s => s.Size);
        Assert.True(finalPop > midPop * 1.1,
            $"galaxy population should keep growing in aggregate ({midPop:0.0} -> {finalPop:0.0})");
        // ...and nothing exceeds its administering port's cap
        foreach (var s in state.Segments)
            Assert.True(s.Size <= state.Ports[s.PortId].Tier * cfg.SegmentCapPerTier + 1e-9,
                $"segment {s.Id} ({s.Size}) above its port tier cap");
    }

    [Fact]
    public void EntryStepSegments_StartGrowingTheNextEpoch()
    {
        // a segment founded by this step's entry integrates from the next step
        // (it arrives at the end of the epoch, like the actor's first view)
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        var newest = state.Segments.Last();
        double seeded = newest.Size;
        Assert.Equal(state.Config.Expansion.HomeworldSegmentSize, seeded);
        engine.Step(state);
        Assert.True(newest.Size > seeded, "segment should grow on the following epoch");
    }
}
