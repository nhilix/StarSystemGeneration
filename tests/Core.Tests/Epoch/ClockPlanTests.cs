using System;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The clock-span contract (slice MC): a clock comparison is only
/// meaningful at a CONSTANT world-time span, so the span — not the epoch
/// count — is the input, and the two clock values are only ever settable
/// together. Every prior P7 claim in this codebase was made with a
/// throwaway harness that set YearsPerEpoch while leaving EpochCount
/// global, silently comparing 40 world-years against 1000.</summary>
public class ClockPlanTests
{
    [Theory]
    [InlineData(250, 25, 10)]
    [InlineData(250, 5, 50)]
    [InlineData(250, 1, 250)]
    [InlineData(1000, 25, 40)]
    public void EpochsFor_DividesTheSpanByTheStep(
        int worldYears, int yearsPerEpoch, int expected)
        => Assert.Equal(expected, ClockPlan.EpochsFor(worldYears, yearsPerEpoch));

    [Fact]
    public void EpochsFor_RefusesASpanTheClockCannotIntegrateExactly()
        => Assert.Throws<ArgumentException>(
            () => ClockPlan.EpochsFor(250, 7));

    [Theory]
    [InlineData(0, 25)]
    [InlineData(-250, 25)]
    [InlineData(250, 0)]
    [InlineData(250, -5)]
    public void EpochsFor_RefusesNonPositiveInputs(int worldYears, int yearsPerEpoch)
        => Assert.Throws<ArgumentException>(
            () => ClockPlan.EpochsFor(worldYears, yearsPerEpoch));

    [Theory]
    [InlineData(25)]
    [InlineData(5)]
    [InlineData(1)]
    public void Apply_SetsBothClockValues_SoTheSpanIsAlwaysConstant(int clock)
    {
        var config = new EpochSimConfig();
        ClockPlan.Apply(config, worldYears: 250, yearsPerEpoch: clock);
        Assert.Equal(clock, config.Sim.YearsPerEpoch);
        Assert.Equal(250, config.Sim.EpochCount * config.Sim.YearsPerEpoch);
    }

    [Fact]
    public void Apply_LeavesGenerationYearsAlone_TheStepFractionIsTheWholePoint()
    {
        var config = new EpochSimConfig();
        int generation = config.Sim.GenerationYears;
        ClockPlan.Apply(config, 250, 1);
        Assert.Equal(generation, config.Sim.GenerationYears);
        Assert.Equal(1.0 / generation, config.Sim.StepFraction, 12);
    }

    /// <summary>The structural guarantee, measured on the real engine: every
    /// clock in a span ends at the SAME world-year. This is the check that
    /// would have caught a 40-vs-1000-year comparison.</summary>
    [Theory]
    [InlineData(25)]
    [InlineData(5)]
    [InlineData(1)]
    public void AllClocksInASpan_EndAtTheSameWorldYear(int clock)
    {
        var gc = new StarGen.Core.Galaxy.GalaxyConfig
        { MasterSeed = 13, GalaxyRadiusCells = 8 };
        var econfig = new EpochSimConfig { MasterSeed = 13 };
        ClockPlan.Apply(econfig, worldYears: 50, yearsPerEpoch: clock);
        var state = EpochGenesis.Seed(
            StarGen.Core.Galaxy.SkeletonBuilder.Build(gc), econfig);
        new EpochEngine().Run(state);
        Assert.Equal(50, state.WorldYear);
        Assert.Equal(ClockPlan.EpochsFor(50, clock), state.Health.Rows.Count);
        // the row's world_year LABELS THE START of the epoch it summarizes
        // (Snapshot lands before WorldYear advances), so the last row reads
        // span − clock, not span. A reader that treats it as the end-year sees
        // a fake clock-dependence in the time axis itself.
        Assert.Equal(50 - clock,
            state.Health.Rows[state.Health.Rows.Count - 1].WorldYear);
    }

    /// <summary>THE TRAP, RETIRED — and this test now pins its ABSENCE
    /// (slice MC). Genesis used to bake <c>EntryEpoch = entryYear /
    /// YearsPerEpoch</c>: an epoch INDEX, resolved against whatever clock
    /// happened to be set when Seed ran. That made genesis clock-sensitive by
    /// construction, so the clock had to be applied BEFORE it — an ordering
    /// hazard no type could enforce. Seed at 25y and switch to 1y afterwards
    /// and every staggered polity entered 25× too early in world-time: an
    /// actor meant for world-year 225 entered at world-year 9. That is the bug
    /// that produced the 2026-07-17 seed-99 investigation's numbers, and it is
    /// why that spec and the baseline spec disagreed 5× on seed 42 and 14,880×
    /// on seed 99 while agreeing to the last decimal on seeds 7/13/2024 —
    /// whose origins all sit at entry year 0, where the two frames coincide.
    ///
    /// <para>Slice MC replaced the field with <c>Actor.EntryYear</c>, a
    /// world-year. Genesis no longer reads the clock AT ALL (its only clock
    /// reference was that division), so the hazard is gone by construction
    /// rather than by discipline — and the engine-level genesis-clock guard
    /// task 2 flagged as urgent is moot for the same reason. The predecessor
    /// test asserted genesis WAS clock-sensitive and said in its own words: "if
    /// this ever stops being true, the ordering hazard is gone and this test
    /// should be retired". It stopped being true. This is the retirement:
    /// the same fixture, the opposite assertion.</para></summary>
    [Fact]
    public void GenesisIsClockInvariant_TheEntryScheduleIsACalendarNotAnIndex()
    {
        var gc = new StarGen.Core.Galaxy.GalaxyConfig
        { MasterSeed = 42, GalaxyRadiusCells = 8 };
        var skeleton = StarGen.Core.Galaxy.SkeletonBuilder.Build(gc);

        var fine = new EpochSimConfig { MasterSeed = 42 };
        ClockPlan.Apply(fine, 250, 1);                    // clock, THEN genesis
        var seededFine = EpochGenesis.Seed(skeleton, fine);

        var late = new EpochSimConfig { MasterSeed = 42 };
        var seededCoarse = EpochGenesis.Seed(skeleton, late);   // genesis at 25y …
        ClockPlan.Apply(late, 250, 1);                           // … clock after

        // the ordering that used to fabricate an economy is now a no-op: the
        // entry schedule is a calendar, and a calendar does not care which
        // step size will read it
        Assert.Equal(seededFine.Actors.Count, seededCoarse.Actors.Count);
        for (int i = 0; i < seededFine.Actors.Count; i++)
            Assert.Equal(seededFine.Actors[i].EntryYear,
                         seededCoarse.Actors[i].EntryYear);
        // and the schedule is denominated in world-years, not epochs: the
        // spread spans the emergence window itself, undivided
        int window = fine.Genesis.EmergenceWindowYears;
        Assert.All(seededFine.Actors, a => Assert.InRange(a.EntryYear, 0, window));
        Assert.True(seededFine.Actors[seededFine.Actors.Count - 1].EntryYear
                    > window / 25,
            "entry years still look divided by the genesis clock");
    }

    /// <summary>The gate honors the calendar (slice MC): an actor enters on
    /// the first step whose world-year has reached its EntryYear, whatever the
    /// integration step. At the 1y clock that is exact; at a coarser clock the
    /// actor waits for the next step boundary — bounded by the step, never
    /// scaled by it. The defect this replaces multiplied the schedule by
    /// 25/YearsPerEpoch, admitting ~25× fewer polities at 1y.</summary>
    [Theory]
    [InlineData(25)]
    [InlineData(1)]
    public void EntryLandsOnTheCalendarYear_WithinOneStep_AtEveryClock(int clock)
    {
        var gc = new StarGen.Core.Galaxy.GalaxyConfig
        { MasterSeed = 42, GalaxyRadiusCells = 8 };
        var econfig = new EpochSimConfig { MasterSeed = 42 };
        ClockPlan.Apply(econfig, worldYears: 250, yearsPerEpoch: clock);
        var state = EpochGenesis.Seed(
            StarGen.Core.Galaxy.SkeletonBuilder.Build(gc), econfig);
        var scheduled = new System.Collections.Generic.Dictionary<int, int>();
        foreach (var a in state.Actors) scheduled[a.Id] = a.EntryYear;
        new EpochEngine().Run(state);

        int checkedEntries = 0;
        foreach (var e in state.Log.Events)
        {
            if (e.Type != WorldEventType.PolityEmerged) continue;
            foreach (int id in e.Actors)
            {
                if (!scheduled.TryGetValue(id, out int entryYear)) continue;
                // never early, and never more than one step late
                Assert.InRange(e.WorldYear, entryYear, entryYear + clock - 1);
                checkedEntries++;
            }
        }
        Assert.True(checkedEntries > 0, "no genesis polity entered at all");
    }
}
