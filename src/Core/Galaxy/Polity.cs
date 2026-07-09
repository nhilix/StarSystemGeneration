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

    // --- Economy state (economy spec §4). Budget splits are transient, not stored. ---
    public double MilitaryStockpile { get; set; }
    public int TechTier { get; set; }
    public double ExoticsInvested { get; set; }
    public double Wealth { get; set; }
    /// <summary>Last epoch's polity-level net per good; war goals and shortage effects read these.</summary>
    public double ProvisionsBalance { get; set; }
    public double OreBalance { get; set; }
    public double ExoticsBalance { get; set; }
}
