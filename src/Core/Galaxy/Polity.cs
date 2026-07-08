using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Registry entry. Extinct polities are retained, flagged (spec §7 lifecycle).</summary>
public sealed class Polity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SpeciesId { get; set; }
    public int CapitalQ { get; set; }
    public int CapitalR { get; set; }
    public HexCoordinate CapitalCoord => new(CapitalQ, CapitalR);
    public bool Extinct { get; set; }
}
