using System;

namespace StarGen.Core.Epoch;

/// <summary>The clock-span contract (slice MC): the ONLY sanctioned way to
/// point the sim at a different integration step.
///
/// <para>P7 says <see cref="SimKnobs.YearsPerEpoch"/> is an integration step
/// and nothing more — the same world, integrated coarsely or finely, should
/// come out the same. Testing that claim means comparing two runs over the
/// SAME world-time span, which means <c>EpochCount × YearsPerEpoch</c> must be
/// held constant. Every P7 measurement this project has made was made with a
/// throwaway harness that set the two independently, and at least two of those
/// harnesses disagree with each other.</para>
///
/// <para>So the span is the input and the epoch count is DERIVED. There is no
/// overload that takes a step without a span: setting the clock without
/// restating the span is the mistake this type exists to make impossible.</para>
///
/// <para>Not a knob — the clock is identity, not calibration
/// (<see cref="KnobRegistry"/>'s doc comment), and lives on the ESIM line.
/// This is the writer of that identity, not a new dial.</para></summary>
public static class ClockPlan
{
    /// <summary>Epochs needed to integrate <paramref name="worldYears"/> at
    /// <paramref name="yearsPerEpoch"/>. Refuses a span the clock cannot
    /// integrate exactly: a remainder means the clocks being compared do not
    /// cover the same world-time, which is precisely the silent error — better
    /// to refuse the experiment than to report a ratio across mismatched spans.</summary>
    public static int EpochsFor(int worldYears, int yearsPerEpoch)
    {
        if (worldYears <= 0)
            throw new ArgumentException(
                $"worldYears must be positive, got {worldYears}",
                nameof(worldYears));
        if (yearsPerEpoch <= 0)
            throw new ArgumentException(
                $"yearsPerEpoch must be positive, got {yearsPerEpoch}",
                nameof(yearsPerEpoch));
        if (worldYears % yearsPerEpoch != 0)
            throw new ArgumentException(
                $"a {yearsPerEpoch}y clock cannot integrate {worldYears} world-years "
                + "exactly — a clock comparison is only meaningful at a constant span",
                nameof(yearsPerEpoch));
        return worldYears / yearsPerEpoch;
    }

    /// <summary>Point a config at <paramref name="yearsPerEpoch"/> over
    /// <paramref name="worldYears"/> — both clock values written together, so
    /// the span survives the change of step.
    /// <see cref="SimKnobs.GenerationYears"/> is deliberately untouched: it is
    /// the calendar unit per-generation intensities scale against, so
    /// <see cref="SimKnobs.StepFraction"/> is exactly what changes when the
    /// clock changes. That IS the experiment.</summary>
    public static void Apply(EpochSimConfig config, int worldYears,
                             int yearsPerEpoch)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        int epochs = EpochsFor(worldYears, yearsPerEpoch);
        config.Sim.YearsPerEpoch = yearsPerEpoch;
        config.Sim.EpochCount = epochs;
    }
}
