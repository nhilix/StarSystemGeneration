using System.Collections.Generic;
using StarGen.Core.Epoch;

namespace StarGen.Core.Atlas;

/// <summary>One living-roster row (`characters` parity).</summary>
public sealed record RosterRow(int Id, string Name, CharacterRole Role,
    NotableType Notable, int PolityId, long Age, double Renown);

/// <summary>A life reconstructed from the log (`bio` parity, P8): no
/// extra authoring — born, rose, led, fell.</summary>
public sealed record BioCard(int Id, string Name, string SpeciesName,
    bool Alive, long BirthYear, long DeathYear, long Age,
    CharacterRole Role, NotableType Notable, string? HouseName,
    int PolityId, double Renown, double Boldness, double Zeal,
    double Competence, double Ambition, IReadOnlyList<string> Chronicle);

/// <summary>K3: roster links and chronicle notables —
/// InteriorView.RenderCharacters/RenderBiography parity.</summary>
public static class CharacterPanel
{
    /// <summary>The living, id order, optionally one polity's.</summary>
    public static List<RosterRow> Roster(AtlasReadModel model,
        EyeContext eye, int polityId = -1)
    {
        var state = model.State;
        var rows = new List<RosterRow>();
        foreach (var c in state.Characters)               // id order (P6)
        {
            if (polityId >= 0 && c.PolityId != polityId) continue;
            if (!c.Alive) continue;
            rows.Add(new RosterRow(c.Id, c.Name, c.Role, c.Notable,
                c.PolityId, state.WorldYear - c.BirthYear, c.Renown));
        }
        return rows;
    }

    public static BioCard? Bio(AtlasReadModel model, EyeContext eye,
                               int characterId)
    {
        var state = model.State;
        if (characterId < 0 || characterId >= state.Characters.Count)
            return null;
        var c = state.Characters[characterId];
        string species = c.SpeciesId >= 0
            && c.SpeciesId < state.Skeleton.Species.Count
            ? state.Skeleton.Species[c.SpeciesId].Name : "?";
        var chronicle = new List<string>();
        foreach (var e in state.Log.ForCharacter(characterId))
            chronicle.Add(SimTraceView.Describe(e));
        return new BioCard(c.Id, c.Name, species, c.Alive, c.BirthYear,
            c.DeathYear, state.WorldYear - c.BirthYear, c.Role, c.Notable,
            c.DynastyId >= 0 ? state.Dynasties[c.DynastyId].Name : null,
            c.PolityId, c.Renown, c.Boldness, c.Zeal, c.Competence,
            c.Ambition, chronicle);
    }
}
