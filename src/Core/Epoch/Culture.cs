namespace StarGen.Core.Epoch;

/// <summary>The slow identity layer (polity/population-and-identity.md):
/// named, species-rooted, carrying the syllable flavor that names systems and
/// characters. Cultures spread by migration (segments keep theirs — diasporas
/// fall out of non-blending) and split only over epochs of separation — no
/// split mechanic yet, so id == species id in slice D. Registry in
/// SimState.Cultures, id order (P6).</summary>
public sealed class Culture
{
    public int Id { get; }
    public string Name { get; }
    public int SpeciesId { get; }

    public Culture(int id, string name, int speciesId)
    {
        Id = id;
        Name = name;
        SpeciesId = speciesId;
    }
}
