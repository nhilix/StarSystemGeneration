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
    public double HomeworldRatePerCell { get; set; } = 0.02;     // ~28 polities at radius 21
    public double TraversabilityThreshold { get; set; } = 0.25;
}
