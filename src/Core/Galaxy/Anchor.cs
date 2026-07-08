using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Closed, versioned anchor vocabulary (spec §5). One anchor per hex.</summary>
public enum AnchorType { MineralRich, PrecursorSite, Homeworld }

public sealed class Anchor
{
    public AnchorType Type { get; set; }
    public HexCoordinate Hex { get; set; }
    public int SpeciesId { get; set; } = -1;   // homeworlds only
}
