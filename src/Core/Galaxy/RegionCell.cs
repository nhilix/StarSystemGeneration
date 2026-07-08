using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

public enum StellarLean { Balanced, YoungBright, OldDim, RemnantGraveyard }

public sealed class RegionCell
{
    public int Q { get; set; }
    public int R { get; set; }
    public HexCoordinate Coord => new(Q, R);
    /// <summary>Position in the skeleton's spiral enumeration — the determinism
    /// ordering key (replaces the rectangular linear index).</summary>
    public int SpiralIndex { get; set; }
    public double MeanDensity { get; set; }
    public bool IsVoid { get; set; }
    public bool IsChokepoint { get; set; }
    public StellarLean Lean { get; set; }
    public double Metallicity { get; set; }
    public List<Anchor> Anchors { get; } = new();
    public int OwnerPolityId { get; set; } = -1;   // -1 = unclaimed
    public int DevelopmentTier { get; set; }
    public bool Contested { get; set; }
    public bool WarScarred { get; set; }
}
