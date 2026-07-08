namespace StarGen.Core.Galaxy;

/// <summary>Registry entry. Extinct polities are retained, flagged (spec §7 lifecycle).</summary>
public sealed class Polity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SpeciesId { get; set; }
    public int CapitalCx { get; set; }
    public int CapitalCy { get; set; }
    public bool Extinct { get; set; }
}
