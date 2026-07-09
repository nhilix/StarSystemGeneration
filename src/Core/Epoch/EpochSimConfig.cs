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
    public InfrastructureKnobs Infrastructure { get; } = new InfrastructureKnobs();
    public ExpansionKnobs Expansion { get; } = new ExpansionKnobs();
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

/// <summary>Port/lane physical knobs (frame/space-and-travel.md). Radii and
/// ranges in hexes; the two port growth axes — local service radius and
/// inter-port range — step per tier, independently.</summary>
public sealed class InfrastructureKnobs
{
    /// <summary>Local service radius of a tier-1 port, in hexes.</summary>
    public int ServiceRadiusBaseHexes { get; set; } = 4;
    /// <summary>Additional service radius per tier above 1.</summary>
    public int ServiceRadiusPerTierHexes { get; set; } = 4;
    /// <summary>Inter-port (lane) reach of a tier-1 port, in hexes.</summary>
    public int InterPortRangeBaseHexes { get; set; } = 18;
    /// <summary>Additional inter-port reach per tier above 1.</summary>
    public int InterPortRangePerTierHexes { get; set; } = 8;
    public int MaxPortTier { get; set; } = 3;
    /// <summary>Homeworld ports establish at this tier at emergence — a
    /// civilization at spaceflight is past "outpost".</summary>
    public int HomeworldPortTier { get; set; } = 2;
}

/// <summary>Expansion/colonization dials, per world-year where a rate.
/// StubIncomePerPortPerYear is the pre-market income placeholder that
/// Markets (slice D) replaces.</summary>
public sealed class ExpansionKnobs
{
    public double StubIncomePerPortPerYear { get; set; } = 1.0;
    /// <summary>Expansion points consumed by one colony founding.</summary>
    public double ColonyCost { get; set; } = 15.0;
    /// <summary>Off-lane colonization reach from any owned port, in hexes.</summary>
    public int ColonizationReachHexes { get; set; } = 24;
    /// <summary>Development points to raise a port: cost = base × current tier.</summary>
    public double PortUpgradeCostBase { get; set; } = 40.0;
    /// <summary>Development points per lane built.</summary>
    public double LaneCost { get; set; } = 25.0;
    public double HomeworldSegmentSize { get; set; } = 3.0;
    public double ColonySegmentSize { get; set; } = 0.5;
    /// <summary>Logistic population growth per world-year toward the port-tier cap.</summary>
    public double SegmentGrowthPerYear { get; set; } = 0.01;
    /// <summary>Segment size cap = port tier × this.</summary>
    public double SegmentCapPerTier { get; set; } = 2.0;
}
