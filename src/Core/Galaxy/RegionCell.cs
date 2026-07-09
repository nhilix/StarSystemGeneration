using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

public enum StellarLean { Balanced, YoungBright, OldDim, RemnantGraveyard }

/// <summary>One cell of the natural raster — nature's fields at fixed lattice
/// resolution, existing before any civilization (space-and-travel.md). The
/// lattice carries no political meaning: political and logistical geography
/// derives from the epoch sim's port registry, never from cells.</summary>
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
}
