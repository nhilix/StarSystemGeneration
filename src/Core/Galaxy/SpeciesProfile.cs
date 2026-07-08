namespace StarGen.Core.Galaxy;

public enum Embodiment { TerranAnalog, Aquatic, Cryophilic, Lithic, Hive, Machine }

/// <summary>Simulation-legible species traits (spec §6). Compact by design.</summary>
public sealed class SpeciesProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Embodiment Embodiment { get; set; }
    public double Expansionism { get; set; }
    public double Cohesion { get; set; }
    public double Militancy { get; set; }
    public double Openness { get; set; }
    public double Industry { get; set; }
    public double Adaptability { get; set; }
}
