namespace StarGen.Core.Epoch;

/// <summary>Species-tagged population quantity, administered per port domain
/// (frame/actors.md: population is substrate, never an actor — it responds
/// statistically and holds no controller). Slice B: size + species per port;
/// the two identity layers, demographics, and migration land with slice D.</summary>
public sealed class PopulationSegment
{
    public int Id { get; }
    /// <summary>The administering port (its domain is where this population lives).</summary>
    public int PortId { get; }
    public int SpeciesId { get; }
    public double Size { get; set; }

    public PopulationSegment(int id, int portId, int speciesId, double size)
    {
        Id = id;
        PortId = portId;
        SpeciesId = speciesId;
        Size = size;
    }
}
