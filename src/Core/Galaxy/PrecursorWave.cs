using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Vigor classes (life-and-precursors.md §Precursor waves): grand
/// waves are rare elder races with slow galaxy-scale arcs; minor and pocket
/// waves rise and fall fast in isolated reaches.</summary>
public enum VigorClass { Grand = 0, Minor = 1, Pocket = 2 }

/// <summary>Cause-typed endings — each leaves its residue signature.
/// Absorbed is the inter-wave contact outcome (the wave ends inside the
/// winner's history rather than by its own arc).</summary>
public enum WaveEndCause { War = 0, CascadeCollapse = 1, Transcendence = 2, Plague = 3, Absorbed = 4 }

/// <summary>Typed site vocabulary for the precursor registry. Values are
/// stable — persisted, and consumed as hex-tier anchors via the
/// pre-commitment mechanism.</summary>
public enum PrecursorSiteType
{
    /// <summary>The wave's capital — silent and intact after transcendence,
    /// shattered after war.</summary>
    Capital = 0,
    /// <summary>Ordinary end-state settlement residue; texture varies by
    /// end cause (intact-but-dead, mass graves, abandoned networks).</summary>
    Ruins = 1,
    /// <summary>Inter-wave or civil battle strata; contested provenance.</summary>
    Battlefield = 2,
    /// <summary>A war's or collapse's kill zone — downstream life here was
    /// delayed or erased; the emergence map carries the shadow.</summary>
    SterilizationScar = 3,
    /// <summary>Peak-phase seeding/terraforming/uplift — anomalously rich
    /// biospheres with archaeology readable in the biology.</summary>
    EngineeredBiosphere = 4,
    /// <summary>An empty megastructure — transcendence residue.</summary>
    Megastructure = 5,
}

/// <summary>One typed site in a wave's residue. Dormant sites stay live —
/// war machines, defense grids, functioning megastructures: encounter
/// content, flagged distinctly from inert ruins (registry entries only
/// until slice I digs).</summary>
public sealed class PrecursorSite
{
    public int Id { get; set; }
    public int WaveId { get; set; }
    public PrecursorSiteType Type { get; set; }
    public HexCoordinate Hex { get; set; }
    public bool Dormant { get; set; }
    /// <summary>Mixed provenance: the other wave at a battlefield or
    /// absorption site; -1 for single-provenance sites.</summary>
    public int OtherWaveId { get; set; } = -1;
}

/// <summary>One precursor wave in the registry: an earlier entry on the
/// emergence schedule that rose, spread on the real raster, and ended in
/// deep time. Ruins have real geography because the arc simulated real
/// expansion (ports, lanes, terrain).</summary>
public sealed class PrecursorWave
{
    public int Id { get; set; }
    /// <summary>The sapient origin this wave grew from.</summary>
    public int OriginId { get; set; }
    public string Name { get; set; } = "";
    public VigorClass Class { get; set; }
    /// <summary>Vigor scalar [0,1] within the class.</summary>
    public double Vigor { get; set; }
    public HexCoordinate CapitalHex { get; set; }
    /// <summary>Extent: claimed cells in claim order (capital first).</summary>
    public List<HexCoordinate> Cells { get; } = new();
    /// <summary>Port hexes, parallel to Cells — one port per claimed cell.</summary>
    public List<HexCoordinate> PortHexes { get; } = new();
    /// <summary>Lane network as index pairs into Cells/PortHexes.</summary>
    public List<(int A, int B)> Lanes { get; } = new();
    public long RoseYear { get; set; }
    public long FellYear { get; set; }
    public WaveEndCause EndCause { get; set; }
    /// <summary>Machine-descendant origin seeded by this wave's ending;
    /// -1 = none. Grounds the machine-species lineage in real history.</summary>
    public int DescendantOriginId { get; set; } = -1;
    public List<PrecursorSite> Sites { get; } = new();
}
