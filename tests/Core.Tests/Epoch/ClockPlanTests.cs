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

    /// <summary>THE TRAP, pinned. Genesis bakes each actor's
    /// <c>EntryEpoch = entryYear / YearsPerEpoch</c> (EpochGenesis) — an epoch
    /// INDEX, resolved against the clock that was set when Seed ran. So the
    /// clock must be applied BEFORE genesis. Seed at 25y and switch to 1y
    /// afterwards and every staggered polity enters 25× too early in
    /// world-time: an actor meant for world-year 225 enters at world-year 9.
    ///
    /// <para>This is not hypothetical. It is the bug that produced the
    /// 2026-07-17 seed-99 investigation's numbers, and it is why that spec and
    /// the baseline spec disagree 5× on seed 42 and 14,880× on seed 99 while
    /// agreeing to the last decimal on seeds 7/13/2024 — whose origins all sit
    /// at entry epoch 0, where the two frames coincide. Any harness that sets
    /// the clock after Seed measures a fabricated economy.</para></summary>
    [Fact]
    public void TheClockMustBeAppliedBeforeGenesis_EntryEpochsAreBakedThere()
    {
        var gc = new StarGen.Core.Galaxy.GalaxyConfig
        { MasterSeed = 42, GalaxyRadiusCells = 8 };
        var skeleton = StarGen.Core.Galaxy.SkeletonBuilder.Build(gc);

        var correct = new EpochSimConfig { MasterSeed = 42 };
        ClockPlan.Apply(correct, 250, 1);                 // clock, THEN genesis
        var before = EpochGenesis.Seed(skeleton, correct);

        var late = new EpochSimConfig { MasterSeed = 42 };
        var after = EpochGenesis.Seed(skeleton, late);    // genesis at 25y …
        ClockPlan.Apply(late, 250, 1);                    // … clock too late

        // same seed, same span, same end clock — and a different world,
        // because genesis already resolved the entry schedule
        bool diverged = false;
        for (int i = 0; i < before.Actors.Count; i++)
            if (before.Actors[i].EntryEpoch != after.Actors[i].EntryEpoch)
                diverged = true;
        Assert.True(diverged,
            "genesis is clock-sensitive: if this ever stops being true, the "
            + "ordering hazard is gone and this test should be retired");
        // the entry schedule is denominated in the genesis clock: a 25y-genesis
        // epoch index n is the 1y-genesis window [25n, 25n+24], so reading it
        // as a 1y index lands the actor ~25× early in world-time
        int coarse = after.Actors[1].EntryEpoch;
        int fine = before.Actors[1].EntryEpoch;
        Assert.InRange(fine, 25 * coarse, 25 * coarse + 24);
    }
}
