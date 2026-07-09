namespace StarGen.Core.Epoch;

/// <summary>Epoch-sim input: identity + the design's knob families
/// (genesis / economy / sim), seeded defaults. All rates are per world-year —
/// the epoch is an integration step, never a unit (frame/time.md, P7).
/// Artifact-stamped when the new artifact format lands (Slice B).</summary>
public sealed class EpochSimConfig
{
    public ulong MasterSeed { get; set; }
    public SimKnobs Sim { get; } = new SimKnobs();
    public GenesisKnobs Genesis { get; } = new GenesisKnobs();
    public EconomyKnobs Economy { get; } = new EconomyKnobs();
}

/// <summary>Clock and stepping knobs (frame/time.md).</summary>
public sealed class SimKnobs
{
    /// <summary>World-years integrated per generational step: one epoch ≈ a generation.</summary>
    public int YearsPerEpoch { get; set; } = 25;
    /// <summary>Default history depth: 40 epochs ≈ 1,000 years.</summary>
    public int EpochCount { get; set; } = 40;
}

/// <summary>Genesis-side knobs. Slice A carries only the stub emergence
/// schedule; the real cosmic/evolutionary families land with Slice F.</summary>
public sealed class GenesisKnobs
{
    /// <summary>Polities entering via the placeholder emergence schedule
    /// (Slice B replaces stub seeding; Slice F simulates emergence).</summary>
    public int StubPolityCount { get; set; } = 6;
    /// <summary>Latest world-year a stub polity may enter — staggered
    /// emergence (frame/time.md §Asymmetric emergence).</summary>
    public int EmergenceWindowYears { get; set; } = 500;
    /// <summary>Scatter radius for stub homeworld seats, in hexes.</summary>
    public int StubSeatRadiusHexes { get; set; } = 60;
}

/// <summary>Economy dials, per world-year. Defaults re-denominate the
/// prototype's per-epoch values at its 50y step.</summary>
public sealed class EconomyKnobs
{
    /// <summary>War-weariness accrual per world-year at war.</summary>
    public double WarWearinessPerYear { get; set; } = 0.003;
    /// <summary>Fractional military-stockpile decay per world-year.</summary>
    public double StockpileDecayPerYear { get; set; } = 0.002;
    /// <summary>Provisions consumed per population unit per world-year — the famine dial.</summary>
    public double ProvisionsPerPopPerYear { get; set; } = 0.01;
}
