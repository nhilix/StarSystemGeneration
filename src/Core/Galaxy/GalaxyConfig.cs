namespace StarGen.Core.Galaxy;

/// <summary>Generation input: identity + tuning knobs (spec §3). Recorded in artifact stamps.</summary>
public sealed class GalaxyConfig
{
    public ulong MasterSeed { get; set; }
    /// <summary>Radius of the circular cell-lattice disc in lattice steps (spec §4).
    /// Default 21 -> 1,615 cells x 91 hexes ~ 147k hexes.</summary>
    public int GalaxyRadiusCells { get; set; } = 21;
    public double MeanDensityTarget { get; set; } = 0.5;
    public int ArmCount { get; set; } = 3;
    public double ArmTightness { get; set; } = 0.35;
    public double ArmWidth { get; set; } = 0.18;
    /// <summary>Arm contrast vs. the smooth disc; 0 = armless elliptical galaxy.</summary>
    public double ArmStrength { get; set; } = 0.9;
    /// <summary>Bright-center bulge sigma in rim-normalized units.</summary>
    public double CoreRadius { get; set; } = 0.18;
    /// <summary>Disc density falloff sigma; higher = flatter, denser rim.</summary>
    public double DiscFalloff { get; set; } = 0.55;
    /// <summary>Scales mineral-rich anchor chance (1 = stock, 0 = none).</summary>
    public double MineralAnchorMultiplier { get; set; } = 1.0;
    /// <summary>Samples precursor-site anchors from the wave registry
    /// (1 = every site anchors, 0 = none).</summary>
    public double PrecursorAnchorMultiplier { get; set; } = 1.0;
    public double TraversabilityThreshold { get; set; } = 0.25;
    public CosmicKnobs Cosmic { get; } = new CosmicKnobs();
    public EvolutionKnobs Evolution { get; } = new EvolutionKnobs();
}

/// <summary>Evolutionary-clock dials (genesis/life-and-precursors.md
/// §Knobs): life, sapience, and the emergence schedule. Indexed by
/// <see cref="GalaxyKnobRegistry"/>, documented in docs/TUNING.md.
/// Precursor-wave dials join with the arc sim.</summary>
public sealed class EvolutionKnobs
{
    /// <summary>Abiogenesis chance per viable cell per evolutionary step —
    /// how eagerly life starts where it can (mean wait ~3 Gyr at 0.012).</summary>
    public double AbiogenesisRate { get; set; } = 0.012;
    /// <summary>Mean abiogenesis → spaceflight duration in Gyr; richness,
    /// hospitability, and setbacks scale it per origin. Must generally
    /// exceed the abiogenesis→sapience lag or the sapience clamp erases
    /// the causal date (task-5 lesson).</summary>
    public double MaturationScaleGyr { get; set; } = 6.0;
    /// <summary>Mass-extinction chance per living cell per step.</summary>
    public double CatastropheFrequency { get; set; } = 0.0015;
    /// <summary>Panspermia chance per living-neighbor pair per step (slow).</summary>
    public double SpreadRate { get; set; } = 0.002;
    /// <summary>Sapience-registration chance per rich stable cell per step —
    /// fast once richness allows, so dates stay causal (viability + growth
    /// decide, not a long lottery). Scales origin count overall; the
    /// *current-era* polity count also hangs on the era horizons and varies
    /// by seed (5–16 at radius 12) — crowded and sparse galaxies are both
    /// real outcomes now, not roll noise around a pinned target.</summary>
    public double SapienceRate { get; set; } = 0.05;

    // -- precursor-wave dials (life-and-precursors.md §Precursor waves) --
    /// <summary>Share of all cells claimable by precursor waves across deep
    /// time — wave count is emergent from this budget, not a flat cap.</summary>
    public double DomainBudgetFraction { get; set; } = 0.5;
    /// <summary>Chance a wave draws the grand class (per-class limit and
    /// budget permitting).</summary>
    public double GrandChance { get; set; } = 0.15;
    /// <summary>Grand waves per galaxy at most (the elder races are few).</summary>
    public double GrandWaveLimit { get; set; } = 3.0;
    /// <summary>Peak-phase biosphere-engineering chance per owned living
    /// cell per step (seeding, terraforming, uplift).</summary>
    public double BioEngineeringRate { get; set; } = 0.03;
    /// <summary>Share of end-state sites that stay live (dormant war
    /// machines, defense grids, functioning megastructures).</summary>
    public double DormantSurvivalRate { get; set; } = 0.08;
}

/// <summary>Cosmic-clock dials (genesis/cosmic-genesis.md §Knobs): the deep-time
/// structure sim's calibration, multipliers around structural bases that live
/// data-as-code in CosmicSim. Every dial is indexed by
/// <see cref="GalaxyKnobRegistry"/> (which drives the artifact's GKNOB lines and
/// the REPL `knobs` command) and documented in docs/TUNING.md. The shape knobs
/// above keep their meaning as potential parameters; MeanDensityTarget
/// normalizes the present-day field.</summary>
public sealed class CosmicKnobs
{
    /// <summary>Expected infalling dwarf mergers per formation history —
    /// the biggest source of seed-to-seed structural variety.</summary>
    public double MergerCount { get; set; } = 2.0;
    /// <summary>Mass scale of merger gas/star injections (1 = stock).</summary>
    public double MergerScale { get; set; } = 1.0;
    /// <summary>Multiplier on the star-formation rate (gas × potential
    /// compression × trigger noise). Higher burns gas earlier.</summary>
    public double StarFormationEfficiency { get; set; } = 1.0;
    /// <summary>Multiplier on metals yielded per dying young cohort —
    /// how fast the galaxy enriches.</summary>
    public double EnrichmentRate { get; set; } = 1.0;
    /// <summary>Globular clusters placed in the earliest steps.</summary>
    public double GlobularCount { get; set; } = 6.0;
    /// <summary>Scales AGN accretion-epoch frequency and wave reach
    /// (0 = a quiet nucleus, life near the core starts on time).</summary>
    public double AgnActivity { get; set; } = 1.0;
}
