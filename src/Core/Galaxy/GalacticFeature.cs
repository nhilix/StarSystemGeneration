using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>The closed vocabulary of discrete cosmic features
/// (genesis/cosmic-genesis.md §Discrete features). Values are stable —
/// persisted in the features artifact layer.</summary>
public enum GalacticFeatureType
{
    /// <summary>A merger's stellar stream: foreign metallicity signature,
    /// datable starburst cohort along the infall trail.</summary>
    MergerStream = 0,
    /// <summary>Ancient, compact, metal-poor single-cell cluster with
    /// near-zero gas and its own hex-tier star-table bias.</summary>
    GlobularCluster = 1,
    /// <summary>Contiguous high-gas region with active star formation —
    /// emergent at finalization, never placed.</summary>
    EmissionNebula = 2,
    /// <summary>Contiguous high-gas region gone quiet.</summary>
    DarkCloud = 3,
    /// <summary>Recent massive-cohort deaths — a young graveyard glow.</summary>
    SupernovaRemnant = 4,
    /// <summary>One AGN accretion epoch's sterilization/enrichment wave
    /// footprint over the inner disc.</summary>
    AgnOutburst = 5,
}

/// <summary>A sparse, identified, dated cosmic object that interacted with
/// the field stack rather than bypassing it. Strictly 2D hex-radial:
/// off-plane phenomena appear as their lattice projections. Feature cells
/// carry pre-commitment-style overrides for the hex tier (a globular hex
/// rolls on a different star table — consumed by the seeding integration).</summary>
public sealed class GalacticFeature
{
    public int Id { get; set; }
    public GalacticFeatureType Type { get; set; }
    /// <summary>Base name; views render the type suffix ("Heron" → "the
    /// Heron Merger").</summary>
    public string Name { get; set; } = "";
    /// <summary>When it happened or began, in Gyr relative to present day
    /// (negative = ago); 0 for present-day emergent states (nebulae,
    /// supernova remnants).</summary>
    public double DateGyr { get; set; }
    /// <summary>Cell footprint as cell coordinates, in the order the
    /// feature touched them.</summary>
    public List<HexCoordinate> Cells { get; } = new();
}
