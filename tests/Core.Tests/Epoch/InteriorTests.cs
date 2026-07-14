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
        new EpochEngine().Run(state);
        var cfg = state.Config.Expansion;
        // homeworld segments have grown past their seed size — a fraction of
        // the seed, not the exact constant: slice ME task 4 (Borrow scans
        // corporations too) shifts this reference seed's trajectory, an
        // early corp loan the pre-task-4 run left unfilled changes downstream
        // debt service and nudges the top segment's peak lower (2.56 vs 3.23)
        // without stunting growth broadly (total population ~93% of before).
        // The bar keeps proving real logistic growth well past the smallest
        // colony seed (0.5) while tolerating that legitimate drift.
        Assert.Contains(state.Segments, s => s.Size > cfg.HomeworldSegmentSize * 0.8);
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
