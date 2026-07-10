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

    // -- present-day residue of the cosmic sim (slice F, raster v2): every
    // field below is a derivation of the simulated field stack, written by
    // CosmicResidue.Compress — never painted. --
    /// <summary>Gas share of the cell's total baryon mass [0,1].</summary>
    public double GasFraction { get; set; }
    /// <summary>Cohort mix: shares of stellar + remnant mass, summing to 1
    /// where any stars exist. Derives the lean.</summary>
    public double CohortYoung { get; set; }
    public double CohortMid { get; set; }
    public double CohortOld { get; set; }
    public double CohortRemnant { get; set; }
    /// <summary>Metals × remnant processing, normalized [0,1] — the ore
    /// geography traces to actual ancient supernovae.</summary>
    public double MineralRichness { get; set; }
    /// <summary>Present-day star-formation activity, normalized [0,1].</summary>
    public double SfActivity { get; set; }
    /// <summary>Cosmic step when stellar metallicity first crossed the
    /// life-viable floor; -1 = never (habitability history scalar).</summary>
    public int LifeViableStep { get; set; } = -1;
    /// <summary>Cosmic step of the last sterilization event (AGN wave);
    /// -1 = never. Stability-since falls out of it.</summary>
    public int LastSterilizedStep { get; set; } = -1;

    // -- present-day residue of the evolutionary sim (slice F) --
    /// <summary>Present-day biosphere richness [0,1] — 0 where lifeless or
    /// sterilized; replaces painted biosphere/provisions potential.</summary>
    public double BiosphereRichness { get; set; }
    /// <summary>Age of the living biosphere in Gyr (0 where lifeless).
    /// Older → richer, more complex.</summary>
    public double BiosphereAgeGyr { get; set; }
}
