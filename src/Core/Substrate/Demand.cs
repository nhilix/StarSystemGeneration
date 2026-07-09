using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Substrate;

/// <summary>The three population demand bands, in budget-priority order:
/// unmet subsistence means famine; SoL feeds growth, legitimacy, and
/// migration pull; luxury is elastic and prestige-driven.</summary>
public enum PopulationBand { Subsistence = 0, StandardOfLiving = 1, Luxury = 2 }

/// <summary>Per-good legality under one polity's law code, plus tariff.
/// Jurisdiction-relative by design: prohibition converts demand into
/// black-market demand, it never deletes it (commodities.md legality).</summary>
public sealed record GoodLegality(LegalityLevel Level, double Tariff)
{
    public static GoodLegality Default { get; } = new(LegalityLevel.Legal, 0.0);
}

/// <summary>Demand profiles — relative draw weights per population segment
/// (embodiment-modulated) and per institutional use-case. Weights are
/// normalized shares; absolute per-capita rates are economy-config knobs
/// applied by the market layer (Slice D).</summary>
public static class DemandProfiles
{
    /// <summary>Allocation order when budgets are tight: population bands,
    /// then industry, movement, military, technology (commodities.md).</summary>
    public static readonly IReadOnlyList<UseCase> PriorityOrder = new[]
    {
        UseCase.Subsistence, UseCase.StandardOfLiving, UseCase.Luxury,
        UseCase.IndustryConstruction, UseCase.IndustryUpkeep,
        UseCase.Movement,
        UseCase.MilitaryConstruction, UseCase.MilitaryUpkeep,
        UseCase.Technology,
    };

    public static readonly IReadOnlyList<UseCase> InstitutionalUseCases = new[]
    {
        UseCase.IndustryConstruction, UseCase.IndustryUpkeep, UseCase.Movement,
        UseCase.MilitaryConstruction, UseCase.MilitaryUpkeep, UseCase.Technology,
    };

    private static (GoodId Good, double Weight)[] P(params (GoodId, double)[] entries)
    {
        double total = 0;
        foreach (var (_, w) in entries) total += w;
        var result = new (GoodId, double)[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            result[i] = (entries[i].Item1, entries[i].Item2 / total);
        return result;
    }

    private static readonly (GoodId Good, double Weight)[] DefaultSubsistence =
        P((GoodId.Provisions, 1.0));
    private static readonly (GoodId Good, double Weight)[] DefaultSoL =
        P((GoodId.ConsumerGoods, 0.7), (GoodId.Medicine, 0.3));
    private static readonly (GoodId Good, double Weight)[] DefaultLuxury =
        P((GoodId.Luxuries, 0.6), (GoodId.Narcotics, 0.4));

    // Lithics eat little (see SubsistenceScale) but demand more machinery.
    private static readonly (GoodId Good, double Weight)[] LithicSoL =
        P((GoodId.ConsumerGoods, 0.4), (GoodId.Medicine, 0.2), (GoodId.Machinery, 0.4));

    // Machine populations consume Fuel, Machinery, and Compute instead of
    // Provisions and Medicine — their birth rate is fab capacity.
    private static readonly (GoodId Good, double Weight)[] MachineSubsistence =
        P((GoodId.Fuel, 0.7), (GoodId.Machinery, 0.3));
    private static readonly (GoodId Good, double Weight)[] MachineSoL =
        P((GoodId.Compute, 0.6), (GoodId.Machinery, 0.2), (GoodId.ConsumerGoods, 0.2));
    private static readonly (GoodId Good, double Weight)[] MachineLuxury =
        P((GoodId.Luxuries, 0.7), (GoodId.Compute, 0.3));

    /// <summary>Relative draw weights for one population band of one
    /// embodiment. Weights sum to 1.</summary>
    public static IReadOnlyList<(GoodId Good, double Weight)> Population(
        Embodiment embodiment, PopulationBand band) => embodiment switch
    {
        Embodiment.Machine => band switch
        {
            PopulationBand.Subsistence => MachineSubsistence,
            PopulationBand.StandardOfLiving => MachineSoL,
            _ => MachineLuxury,
        },
        Embodiment.Lithic => band switch
        {
            PopulationBand.Subsistence => DefaultSubsistence,
            PopulationBand.StandardOfLiving => LithicSoL,
            _ => DefaultLuxury,
        },
        _ => band switch
        {
            PopulationBand.Subsistence => DefaultSubsistence,
            PopulationBand.StandardOfLiving => DefaultSoL,
            _ => DefaultLuxury,
        },
    };

    /// <summary>Scale on the subsistence band's absolute draw — how much a
    /// body of this embodiment eats. Lithics eat little; machine subsistence
    /// is fuel and parts, not food.</summary>
    public static double SubsistenceScale(Embodiment embodiment) => embodiment switch
    {
        Embodiment.Lithic => 0.4,
        Embodiment.Machine => 0.8,
        Embodiment.Hive => 1.2,
        _ => 1.0,
    };

    private static readonly (GoodId Good, double Weight)[] IndustryConstructionP =
        P((GoodId.Alloys, 0.5), (GoodId.Machinery, 0.3), (GoodId.Composites, 0.2));
    private static readonly (GoodId Good, double Weight)[] IndustryUpkeepP =
        P((GoodId.Alloys, 0.3), (GoodId.Composites, 0.3), (GoodId.Machinery, 0.4));
    private static readonly (GoodId Good, double Weight)[] MovementP =
        P((GoodId.Fuel, 1.0));
    private static readonly (GoodId Good, double Weight)[] MilitaryConstructionP =
        P((GoodId.ShipComponents, 1.0));
    private static readonly (GoodId Good, double Weight)[] MilitaryUpkeepP =
        P((GoodId.Armaments, 0.6), (GoodId.Fuel, 0.4));
    private static readonly (GoodId Good, double Weight)[] TechnologyP =
        P((GoodId.RefinedExotics, 0.7), (GoodId.Compute, 0.3));

    /// <summary>Relative draw weights for an institutional use-case.
    /// Population bands live in <see cref="Population"/>.</summary>
    public static IReadOnlyList<(GoodId Good, double Weight)> Institutional(
        UseCase useCase) => useCase switch
    {
        UseCase.IndustryConstruction => IndustryConstructionP,
        UseCase.IndustryUpkeep => IndustryUpkeepP,
        UseCase.Movement => MovementP,
        UseCase.MilitaryConstruction => MilitaryConstructionP,
        UseCase.MilitaryUpkeep => MilitaryUpkeepP,
        UseCase.Technology => TechnologyP,
        _ => throw new ArgumentException(
            $"{useCase} is a population band, not an institutional use-case."),
    };
}
