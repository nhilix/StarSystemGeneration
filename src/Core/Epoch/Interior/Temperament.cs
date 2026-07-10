using System;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>A polity's decision personality — the temperament composition
/// (polity/factions-and-government.md §The temperament composition):
/// species disposition × official ideology × ruler personality × faction
/// pressure, weighted by government form. Computed each Perception phase and
/// carried on the view; Intent reads THIS, never a fixed species vector —
/// the same nation turns aggressive under a new ruler. The trait axes are
/// the ones controllers consume.</summary>
public sealed record Temperament(
    double Militancy, double Openness, double Expansionism)
{
    public static readonly Temperament Neutral = new(0.5, 0.5, 0.5);

    /// <summary>The species term — disposition straight off the profile.</summary>
    public static Temperament FromSpecies(SpeciesProfile sp) =>
        new(sp.Militancy, sp.Openness, sp.Expansionism);

    /// <summary>The ideology→trait map (structural catalog): authoritarian,
    /// insular societies militarize; the Open pole IS openness; individual,
    /// material societies expand.</summary>
    public static Temperament FromIdeology(double[] axes) => new(
        Militancy: Clamp01(0.5
            + 0.4 * (0.5 - axes[(int)IdeologyAxis.AuthorityAutonomy])
            + 0.3 * (axes[(int)IdeologyAxis.OpenInsular] - 0.5)),
        Openness: Clamp01(1.0 - axes[(int)IdeologyAxis.OpenInsular]),
        Expansionism: Clamp01(0.5
            + 0.3 * (axes[(int)IdeologyAxis.CommunalIndividual] - 0.5)
            + 0.3 * (axes[(int)IdeologyAxis.SacralMaterial] - 0.5)));

    /// <summary>Per-basis faction trait tendencies (structural catalog):
    /// what each interest drags the polity toward when it leans on Intent.</summary>
    private static readonly Temperament[] BasisPull =
    {
        Neutral,                    // ideological: uses its own target instead
        new(0.40, 0.70, 0.50),      // cultural: accommodate, open up
        new(0.50, 0.50, 0.75),      // regional: push the frontier
        new(0.35, 0.75, 0.65),      // corporate: trade wants peace and borders open
        new(0.85, 0.40, 0.60),      // military: the sword sets policy
        new(0.60, 0.25, 0.40),      // sacral: guard the faith, close the gates
    };

    /// <summary>Compose a polity's effective temperament. Weights come from
    /// the government form (autocracy: the ruler dominates; assembly: the
    /// popular line; hive: the species). Falls back gracefully: a vacant
    /// throne reads as the official line, a faction-free polity likewise.</summary>
    public static Temperament Compose(SimState state, PolityRecord pr)
    {
        var interior = pr.Interior;
        var species = pr.SpeciesId >= 0
            && pr.SpeciesId < state.Skeleton.Species.Count
            ? state.Skeleton.Species[pr.SpeciesId] : null;
        if (interior == null)
            return species != null ? FromSpecies(species) : Neutral;

        var form = GovernmentForms.Get(interior.FormId);
        var c = form.Composition;
        var speciesTerm = species != null ? FromSpecies(species) : Neutral;
        var ideologyTerm = FromIdeology(interior.OfficialIdeology);

        Temperament rulerTerm = ideologyTerm;
        if (interior.RulerCharacterId >= 0
            && interior.RulerCharacterId < state.Characters.Count)
        {
            var ruler = state.Characters[interior.RulerCharacterId];
            var seat = FromIdeology(ruler.IdeologyPosition);
            rulerTerm = new Temperament(
                Clamp01(seat.Militancy + 0.4 * (ruler.Boldness - 0.5)),
                Clamp01(seat.Openness - 0.2 * (ruler.Zeal - 0.5)),
                Clamp01(seat.Expansionism + 0.4 * (ruler.Boldness - 0.5)));
        }

        Temperament factionTerm = ideologyTerm;
        double strengthSum = 0;
        double mil = 0, open = 0, exp = 0;
        foreach (var faction in state.Factions)               // id order (P6)
        {
            if (!faction.Active || faction.PolityId != pr.ActorId) continue;
            var pull = faction.Basis == FactionBasis.Ideological
                && faction.IdeologyTarget != null
                ? FromIdeology(faction.IdeologyTarget)
                : BasisPull[(int)faction.Basis];
            mil += pull.Militancy * faction.Strength;
            open += pull.Openness * faction.Strength;
            exp += pull.Expansionism * faction.Strength;
            strengthSum += faction.Strength;
        }
        if (strengthSum > 0)
            factionTerm = new Temperament(mil / strengthSum, open / strengthSum,
                                          exp / strengthSum);

        return new Temperament(
            Blend(speciesTerm.Militancy, ideologyTerm.Militancy,
                  rulerTerm.Militancy, factionTerm.Militancy, c),
            Blend(speciesTerm.Openness, ideologyTerm.Openness,
                  rulerTerm.Openness, factionTerm.Openness, c),
            Blend(speciesTerm.Expansionism, ideologyTerm.Expansionism,
                  rulerTerm.Expansionism, factionTerm.Expansionism, c));
    }

    private static double Blend(double species, double ideology, double ruler,
                                double faction, CompositionWeights c) =>
        Clamp01(species * c.Species + ideology * c.Ideology
                + ruler * c.Ruler + faction * c.Faction);

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
