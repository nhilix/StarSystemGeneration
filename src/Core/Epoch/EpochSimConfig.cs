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
    public PopulationKnobs Population { get; } = new PopulationKnobs();
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

/// <summary>Genesis-side knobs: only the stub emergence schedule until the
/// real cosmic/evolutionary families land with Slice F. Polity count and
/// seats come from the seeding passes' homeworld anchors.</summary>
public sealed class GenesisKnobs
{
    /// <summary>Latest world-year a polity may enter — staggered emergence
    /// (frame/time.md §Asymmetric emergence).</summary>
    public int EmergenceWindowYears { get; set; } = 500;
}

/// <summary>Economy dials, per world-year where a rate. Slice D: the market
/// engine's absolute rates — C's demand profiles are normalized shares;
/// these knobs supply the per-capita magnitudes they multiply.</summary>
public sealed class EconomyKnobs
{
    /// <summary>War-weariness accrual per world-year at war.</summary>
    public double WarWearinessPerYear { get; set; } = 0.003;
    /// <summary>Fractional durable-stockpile decay per world-year; perishable
    /// goods scale it up in code (provisions rot, alloys do not).</summary>
    public double StockpileDecayPerYear { get; set; } = 0.002;

    // -- Demand: absolute per-capita rates the normalized profiles multiply --
    /// <summary>Subsistence-band units per population unit per world-year
    /// (× embodiment SubsistenceScale) — the famine dial.</summary>
    public double SubsistenceUnitsPerPopPerYear { get; set; } = 0.6;
    /// <summary>Standard-of-living-band units per population unit per world-year.</summary>
    public double SoLUnitsPerPopPerYear { get; set; } = 0.4;
    /// <summary>Luxury-band units per population unit per world-year (elastic).</summary>
    public double LuxuryUnitsPerPopPerYear { get; set; } = 0.15;

    // -- Prices --
    public double BasePriceRaw { get; set; } = 1.0;
    public double BasePriceProcessed { get; set; } = 3.0;
    public double BasePriceCapital { get; set; } = 8.0;
    /// <summary>Rate limit on price drift toward clearing, fractional per
    /// world-year (markets never perfectly clear).</summary>
    public double PriceDriftMaxPerYear { get; set; } = 0.04;
    /// <summary>How strongly outbound lane gradients bid up a hub's price —
    /// the re-export demand term (entrepôts emerge from it).</summary>
    public double ReExportWeight { get; set; } = 0.5;

    // -- Income --
    /// <summary>Share of facility revenue paid to the staffing segments; the
    /// remainder goes to the owner (economy/markets.md §Household income).</summary>
    public double LaborShare { get; set; } = 0.4;
    /// <summary>Credits minted once per polity at entry; conserved
    /// thereafter (P4).</summary>
    public double InitialCreditsPerPolity { get; set; } = 200.0;
    /// <summary>Household credits minted per population unit when a segment
    /// is founded — the other mint; first-epoch purchasing power before any
    /// wages have been earned.</summary>
    public double InitialWealthPerPop { get; set; } = 15.0;

    // -- Freight --
    /// <summary>Credits per unit of goods per hex of lane distance.</summary>
    public double FreightCostPerUnitPerHex { get; set; } = 0.02;
    /// <summary>Fuel demand per unit shipped per hex — movement is never free.</summary>
    public double FuelPerUnitPerHex { get; set; } = 0.005;

    // -- Credit --
    public double LoanRatePerYear { get; set; } = 0.02;
    public int LoanTermYears { get; set; } = 50;

    // -- Facility lifecycle --
    /// <summary>Fractional condition decay per world-year of unmet upkeep.</summary>
    public double ConditionDecayPerYear { get; set; } = 0.01;
    /// <summary>Fractional condition recovery per world-year of met upkeep.</summary>
    public double ConditionRecoveryPerYear { get; set; } = 0.05;
    /// <summary>Config-level producer tech tier until tech domains land
    /// (slice G). 2 = standard capital recipes run, advanced stay gated.</summary>
    public int TechTierStub { get; set; } = 2;
}

/// <summary>Population dials, per world-year where a rate: demographics,
/// migration, and ideology drift (polity/population-and-identity.md). Segment
/// growth base rate and caps stay in ExpansionKnobs where slice B put them.</summary>
public sealed class PopulationKnobs
{
    /// <summary>Fraction of a segment migrating per world-year at full
    /// gradient pull.</summary>
    public double MigrationRatePerYear { get; set; } = 0.002;
    /// <summary>Ideology-axis drift toward lived conditions per world-year.</summary>
    public double IdeologyDriftPerYear { get; set; } = 0.01;
    /// <summary>Fractional segment shrink per world-year of unmet subsistence.</summary>
    public double FamineShrinkPerYear { get; set; } = 0.05;
    /// <summary>Machine-population growth per unit of Machinery/Compute
    /// consumed — their birth rate is fab capacity.</summary>
    public double MachineGrowthPerGoodUnit { get; set; } = 0.1;
    /// <summary>SoL movement toward the cleared-demand fraction per world-year.</summary>
    public double SoLDriftPerYear { get; set; } = 0.02;
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
