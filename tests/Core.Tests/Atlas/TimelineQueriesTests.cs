using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>Slice K4 — the TimelineStrip's data face: the event-density
/// sparkline buckets the log's generational stream per generation (era
/// bands ride EraQueries; keyframe marks ride the TimeMachine itself).</summary>
public class TimelineQueriesTests
{
    private static SimState Settled()
    {
        var state = EpochTestKit.Seeded(42).State;
        state.Config.Sim.EpochCount = 10;
        new EpochEngine().Run(state);
        return state;
    }

    [Fact]
    public void EventDensity_BucketsTheGenerationalStream_PerGeneration()
    {
        var state = Settled();
        var model = new AtlasReadModel(state);
        var eye = EyeContext.God(state.WorldYear);

        var buckets = TimelineQueries.EventDensity(model, eye);

        // one bucket per generation, covering year 0 to the live year
        int generations = (int)(state.WorldYear
            / state.Config.Sim.GenerationYears);
        Assert.Equal(generations, buckets.Count);
        Assert.Equal(0, buckets[0].StartYear);
        Assert.Equal(state.WorldYear, buckets[^1].EndYear);

        // every generational-stratum event lands in exactly one bucket;
        // the deep-time strata stay off the political axis
        int generational = 0;
        foreach (var e in state.Log.Events)
            if (e.Stratum == ClockStratum.Generational && e.WorldYear >= 0)
                generational++;
        int total = 0;
        foreach (var b in buckets) total += b.Count;
        Assert.Equal(generational, total);
        Assert.True(total > 0, "a settled seed-42 world has history");
    }

    [Fact]
    public void FineTicks_LeaveAPartialLastBucket()
    {
        // mid-play the live year sits inside a generation — the last
        // bucket ends at the live year, not the next boundary
        var state = Settled();
        state.Config.Sim.YearsPerEpoch = 1;
        var engine = new EpochEngine();
        for (int i = 0; i < 3; i++) engine.Step(state);
        var model = new AtlasReadModel(state);

        var buckets = TimelineQueries.EventDensity(model,
            EyeContext.God(state.WorldYear));

        int years = state.Config.Sim.GenerationYears;
        Assert.Equal((int)(state.WorldYear / years) + 1, buckets.Count);
        Assert.Equal(state.WorldYear, buckets[^1].EndYear);
        Assert.Equal(state.WorldYear - 3, buckets[^1].StartYear);
    }
}
