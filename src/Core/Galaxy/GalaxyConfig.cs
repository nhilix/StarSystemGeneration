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
    /// <summary>Scales precursor-site anchor chance (1 = stock, 0 = none).</summary>
    public double PrecursorAnchorMultiplier { get; set; } = 1.0;
    public double HomeworldRatePerCell { get; set; } = 0.008;    // ~13 polities at radius 21
    public double TraversabilityThreshold { get; set; } = 0.25;
    public CosmicKnobs Cosmic { get; } = new CosmicKnobs();
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
