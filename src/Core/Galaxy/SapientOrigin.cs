using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Where an origin's spaceflight date lands relative to the
/// generational present (life-and-precursors.md §Emergence schedule).</summary>
public enum OriginEra
{
    /// <summary>Spaceflight deep in the past: a precursor wave — it rose,
    /// spread, and ended before the current era.</summary>
    Precursor = 0,
    /// <summary>Spaceflight lands in the current era: enters the epoch sim
    /// on the emergence schedule.</summary>
    Current = 1,
    /// <summary>Sapient by present day but nowhere near spaceflight: a rare
    /// genuine pre-spaceflight native — terrain a polity can encounter.</summary>
    PreSpaceflight = 2,
}

/// <summary>One sapient origin on the emergence schedule — the headline
/// output of the evolutionary clock: where and when life got there first,
/// with the dates that make staggered polity entry causal. Precursors are
/// earlier entries on this same schedule. Persisted in the origins layer.</summary>
public sealed class SapientOrigin
{
    public int Id { get; set; }
    /// <summary>The origin cell (natural raster address).</summary>
    public HexCoordinate CellCoord { get; set; }
    /// <summary>The homeworld hex within that cell.</summary>
    public HexCoordinate Hex { get; set; }
    /// <summary>Deep-time world-years (negative = before present).</summary>
    public long AbiogenesisYear { get; set; }
    public long SapienceYear { get; set; }
    /// <summary>Abiogenesis + maturation (scaled by richness, hospitability,
    /// setbacks). May land past present day (PreSpaceflight).</summary>
    public long SpaceflightYear { get; set; }
    /// <summary>Biosphere richness at sapience registration [0,1] — feeds
    /// maturation quality and entry starting conditions.</summary>
    public double Richness { get; set; }
    /// <summary>Catastrophes the biosphere endured before sapience.</summary>
    public int Setbacks { get; set; }
    public OriginEra Era { get; set; }
    /// <summary>Machine descendants: the precursor wave whose ending seeded
    /// this origin (its homeworld is that wave's capital); -1 = an organic
    /// origin.</summary>
    public int DescendantOfWaveId { get; set; } = -1;
}
