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
    public int EpochCount { get; set; } = 12;
    public int YearsPerEpoch { get; set; } = 50;
    public double HomeworldRatePerCell { get; set; } = 0.02;     // ~28 polities at radius 21
    public double TraversabilityThreshold { get; set; } = 0.25;
}
