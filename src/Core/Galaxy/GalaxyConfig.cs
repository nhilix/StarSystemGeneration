namespace StarGen.Core.Galaxy;

/// <summary>Generation input: identity + tuning knobs (spec §3). Recorded in artifact stamps.</summary>
public sealed class GalaxyConfig
{
    public ulong MasterSeed { get; set; }
    public int SizeSectors { get; set; } = 10;          // galaxy is SizeSectors x SizeSectors sectors
    public double MeanDensityTarget { get; set; } = 0.5;
    public int ArmCount { get; set; } = 3;
    public double ArmTightness { get; set; } = 0.35;
    public double ArmWidth { get; set; } = 0.18;
    public int EpochCount { get; set; } = 12;
    public int YearsPerEpoch { get; set; } = 50;
    public double HomeworldRatePerSector { get; set; } = 0.25;   // ~1 per 4 sectors (spec §5)
    public double TraversabilityThreshold { get; set; } = 0.25;  // connectivity edge gate (spec §5)

    public int WidthHexes => SizeSectors * 32;
    public int HeightHexes => SizeSectors * 40;
    public int CellsX => SizeSectors * 4;   // 8-hex-wide subsector cells
    public int CellsY => SizeSectors * 4;   // 10-hex-tall subsector cells
}
