using System.Collections.Generic;
using StarGen.Core.Epoch;

namespace StarGen.Core.Atlas;

/// <summary>One sparkline bucket: generational-stratum events logged in
/// [StartYear, EndYear); the last bucket also counts events at exactly
/// the live year (the moment being watched belongs to the strip).</summary>
public sealed record DensityBucket(long StartYear, long EndYear, int Count);

/// <summary>K4: the TimelineStrip's data face. Era bands ride EraQueries
/// and keyframe marks ride the TimeMachine; this adds the event-density
/// sparkline — the log's generational stream bucketed per generation.
/// Log-backed, so no keyframe is ever needed to draw the whole strip.</summary>
public static class TimelineQueries
{
    /// <summary>The generational event stream bucketed per generation,
    /// year 0 through the live year. Deep-time strata (cosmic and
    /// evolutionary) stay off the political axis.</summary>
    public static List<DensityBucket> EventDensity(AtlasReadModel model,
        EyeContext eye)
    {
        var state = model.State;
        int years = state.Config.Sim.GenerationYears;
        var buckets = new List<DensityBucket>();
        if (years <= 0 || state.WorldYear <= 0) return buckets;

        int count = (int)(state.WorldYear / years);
        if (state.WorldYear % years != 0) count++;
        for (int i = 0; i < count; i++)
            buckets.Add(new DensityBucket(
                (long)i * years,
                i == count - 1 ? state.WorldYear : (long)(i + 1) * years,
                0));

        foreach (var e in state.Log.Events)
        {
            if (e.Stratum != ClockStratum.Generational || e.WorldYear < 0)
                continue;
            int i = (int)(e.WorldYear / years);
            if (i >= count) i = count - 1;
            buckets[i] = buckets[i] with { Count = buckets[i].Count + 1 };
        }
        return buckets;
    }
}
