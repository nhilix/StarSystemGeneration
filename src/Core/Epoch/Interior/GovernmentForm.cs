using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>The eight government forms (polity/factions-and-government.md
/// §Government forms). Values are STABLE registry ids — never renumber.</summary>
public enum GovernmentFormId
{
    Autocracy = 0,
    Collective = 1,
    Assembly = 2,
    Syndicate = 3,
    Theocracy = 4,
    HiveUnity = 5,
    MachineConsensus = 6,
    StewardDynasty = 7,
}

/// <summary>How the seat refills when its occupant dies (characters.md
/// §Lifespan and succession). Stable ids.</summary>
public enum SuccessionRule
{
    Dynastic = 0,        // heir of the line; contested when none stands
    Committee = 1,       // the collective selects from within
    Election = 2,        // popular cycles; lurches with the electorate
    Boardroom = 3,       // wealth picks; magnates ascend
    Doctrinal = 4,       // the faith names its own
    Continuity = 5,      // the hive IS the character; personality drifts
    NoneForked = 6,      // machine minds fork or deprecate, never inherit
    RareDesignation = 7, // century-reigns; a successor named once an age
}

/// <summary>The temperament-composition weights a form sets
/// (§The temperament composition): species disposition × official ideology ×
/// ruler personality × faction pressure. Sum to 1.</summary>
public sealed record CompositionWeights(
    double Species, double Ideology, double Ruler, double Faction);

/// <summary>One government form: where it sits in ideology space (or which
/// embodiment claims it), how succession works, how it takes pressure, and
/// what makes it legitimate. Catalog data-as-code — TUNING.md carries a
/// structural note, not per-value knobs.</summary>
public sealed record GovernmentFormDef(
    GovernmentFormId Id,
    string Name,
    /// <summary>Ideology-space seat: target per axis (null = species-gated).</summary>
    IReadOnlyList<double>? SeatIdeology,
    /// <summary>Per-axis relevance of the seat (unweighted axes don't count).</summary>
    IReadOnlyList<double>? SeatWeights,
    /// <summary>Embodiment that claims this form outright (hive/machine/lithic).</summary>
    Embodiment? SpeciesGate,
    SuccessionRule Succession,
    /// <summary>[0,1] resistance of official ideology to popular drift.</summary>
    double PolicyInertia,
    /// <summary>[0,1] how much faction pressure the form absorbs before
    /// grievance accrues (assemblies bend, autocracies suppress).</summary>
    double FactionTolerance,
    /// <summary>Cohesion never falls below this (hive unity).</summary>
    double CohesionFloor,
    /// <summary>Form multiplier on the ruler-prestige legitimacy term.</summary>
    double LegitimacyRulerWeight,
    /// <summary>Form multiplier on the prosperity legitimacy term.</summary>
    double LegitimacyProsperityWeight,
    /// <summary>Form multiplier on the ideology-alignment legitimacy term
    /// (theocracies: doctrine IS legitimacy).</summary>
    double LegitimacyIdeologyWeight,
    CompositionWeights Composition);

/// <summary>The closed form catalog, seated in ideology space × species, and
/// the species→ideology entry tilt. Ideology axes per
/// <see cref="IdeologyAxis"/>: 0 = first-named pole.</summary>
public static class GovernmentForms
{
    private static readonly GovernmentFormDef[] Catalog =
    {
        new(GovernmentFormId.Autocracy, "Autocracy",
            SeatIdeology: new[] { 0.0, 1.0, 0.5, 0.5 },   // Authority + Individual
            SeatWeights: new[] { 1.0, 1.0, 0.0, 0.0 },
            SpeciesGate: null, SuccessionRule.Dynastic,
            PolicyInertia: 0.7, FactionTolerance: 0.2, CohesionFloor: 0.0,
            LegitimacyRulerWeight: 2.0, LegitimacyProsperityWeight: 0.8,
            LegitimacyIdeologyWeight: 0.7,
            new CompositionWeights(0.20, 0.15, 0.50, 0.15)),
        new(GovernmentFormId.Collective, "Collective",
            SeatIdeology: new[] { 0.0, 0.0, 0.5, 0.5 },   // Authority + Communal
            SeatWeights: new[] { 1.0, 1.0, 0.0, 0.0 },
            SpeciesGate: null, SuccessionRule.Committee,
            PolicyInertia: 0.85, FactionTolerance: 0.4, CohesionFloor: 0.0,
            LegitimacyRulerWeight: 0.5, LegitimacyProsperityWeight: 1.3,
            LegitimacyIdeologyWeight: 1.0,
            new CompositionWeights(0.30, 0.40, 0.10, 0.20)),
        new(GovernmentFormId.Assembly, "Assembly",
            SeatIdeology: new[] { 1.0, 0.5, 0.0, 0.5 },   // Autonomy + Open
            SeatWeights: new[] { 1.0, 0.0, 1.0, 0.0 },
            SpeciesGate: null, SuccessionRule.Election,
            PolicyInertia: 0.3, FactionTolerance: 0.8, CohesionFloor: 0.0,
            LegitimacyRulerWeight: 0.6, LegitimacyProsperityWeight: 1.2,
            LegitimacyIdeologyWeight: 1.2,
            new CompositionWeights(0.15, 0.55, 0.10, 0.20)),
        new(GovernmentFormId.Syndicate, "Syndicate",
            SeatIdeology: new[] { 0.5, 1.0, 0.5, 1.0 },   // Material + Individual
            SeatWeights: new[] { 0.0, 1.0, 0.0, 1.0 },
            SpeciesGate: null, SuccessionRule.Boardroom,
            PolicyInertia: 0.5, FactionTolerance: 0.6, CohesionFloor: 0.0,
            LegitimacyRulerWeight: 0.7, LegitimacyProsperityWeight: 2.0,
            LegitimacyIdeologyWeight: 0.5,
            new CompositionWeights(0.15, 0.30, 0.20, 0.35)),
        new(GovernmentFormId.Theocracy, "Theocracy",
            SeatIdeology: new[] { 0.0, 0.5, 0.5, 0.0 },   // Sacral + Authority
            SeatWeights: new[] { 1.0, 0.0, 0.0, 1.0 },
            SpeciesGate: null, SuccessionRule.Doctrinal,
            PolicyInertia: 0.9, FactionTolerance: 0.25, CohesionFloor: 0.0,
            LegitimacyRulerWeight: 1.2, LegitimacyProsperityWeight: 0.5,
            LegitimacyIdeologyWeight: 2.0,
            new CompositionWeights(0.20, 0.40, 0.25, 0.15)),
        new(GovernmentFormId.HiveUnity, "Hive Unity",
            SeatIdeology: null, SeatWeights: null,
            SpeciesGate: Embodiment.Hive, SuccessionRule.Continuity,
            PolicyInertia: 0.95, FactionTolerance: 0.05, CohesionFloor: 0.7,
            LegitimacyRulerWeight: 0.3, LegitimacyProsperityWeight: 1.0,
            LegitimacyIdeologyWeight: 0.3,
            new CompositionWeights(0.80, 0.10, 0.05, 0.05)),
        new(GovernmentFormId.MachineConsensus, "Machine Consensus",
            SeatIdeology: null, SeatWeights: null,
            SpeciesGate: Embodiment.Machine, SuccessionRule.NoneForked,
            PolicyInertia: 0.98, FactionTolerance: 0.1, CohesionFloor: 0.5,
            LegitimacyRulerWeight: 0.2, LegitimacyProsperityWeight: 1.0,
            LegitimacyIdeologyWeight: 0.8,
            new CompositionWeights(0.70, 0.25, 0.00, 0.05)),
        new(GovernmentFormId.StewardDynasty, "Steward Dynasty",
            SeatIdeology: null, SeatWeights: null,
            SpeciesGate: Embodiment.Lithic, SuccessionRule.RareDesignation,
            PolicyInertia: 0.95, FactionTolerance: 0.3, CohesionFloor: 0.0,
            LegitimacyRulerWeight: 1.8, LegitimacyProsperityWeight: 0.7,
            LegitimacyIdeologyWeight: 0.8,
            new CompositionWeights(0.35, 0.15, 0.40, 0.10)),
    };

    public static IReadOnlyList<GovernmentFormDef> All => Catalog;

    public static GovernmentFormDef Get(GovernmentFormId id) => Catalog[(int)id];

    /// <summary>The species→ideology entry tilt: where a society of this
    /// disposition starts in ideology space. Structural mapping (catalog
    /// data), applied to the founding population and the official line —
    /// popular and official ideology agree at birth, then drift apart with
    /// lived conditions.</summary>
    public static double[] SpeciesIdeologyTilt(SpeciesProfile sp) => new[]
    {
        // cohesive, militant species lean Authority; open ones Autonomy
        Clamp01(0.5 - 0.3 * (sp.Cohesion - 0.5) - 0.3 * (sp.Militancy - 0.5)
                    + 0.2 * (sp.Openness - 0.5)),
        // expansionists individualize; cohesion binds communal
        Clamp01(0.5 + 0.3 * (sp.Expansionism - 0.5) - 0.4 * (sp.Cohesion - 0.5)),
        // openness is the axis, directly
        Clamp01(1.0 - sp.Openness),
        // industrial, open species read the world materially
        Clamp01(0.5 + 0.3 * (sp.Industry - 0.5) + 0.2 * (sp.Openness - 0.5)),
    };

    /// <summary>Seat a polity: embodiment-gated forms claim their species
    /// outright; everyone else takes the nearest ideology seat (weighted
    /// mean axis distance; ties to the lower form id).</summary>
    public static GovernmentFormId SeatFor(SpeciesProfile species,
                                           IReadOnlyList<double> ideology)
    {
        foreach (var def in Catalog)
            if (def.SpeciesGate != null && def.SpeciesGate == species.Embodiment)
                return def.Id;
        GovernmentFormId best = GovernmentFormId.Autocracy;
        double bestDist = double.MaxValue;
        foreach (var def in Catalog)
        {
            if (def.SeatIdeology == null) continue;
            double sum = 0, weight = 0;
            for (int ax = 0; ax < 4; ax++)
            {
                sum += def.SeatWeights![ax]
                       * Math.Abs(ideology[ax] - def.SeatIdeology[ax]);
                weight += def.SeatWeights[ax];
            }
            double dist = sum / weight;
            if (dist < bestDist) { bestDist = dist; best = def.Id; }
        }
        return best;
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
