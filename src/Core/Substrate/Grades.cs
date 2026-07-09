using System;

namespace StarGen.Core.Substrate;

/// <summary>Every consumption channel that draws goods — population bands
/// first, then institutions, in the design's budget-priority order
/// (commodities.md demand model). Grade sensitivity is keyed per use-case.</summary>
public enum UseCase
{
    Subsistence = 0,
    StandardOfLiving = 1,
    Luxury = 2,
    IndustryConstruction = 3,
    IndustryUpkeep = 4,
    Movement = 5,
    MilitaryConstruction = 6,
    MilitaryUpkeep = 7,
    Technology = 8,
}

/// <summary>Display bands over the continuous grade scalar — map, chronicle,
/// and shops speak one language (commodities.md).</summary>
public enum GradeBand { Crude, Standard, Fine, Advanced, Masterwork, PrecursorGrade }

/// <summary>The Grade system: one scalar in [0,1] carried wherever stocks
/// live. Origin in terrain, ceiling in tech, effect through
/// <c>Effective(useCase)</c> (substrate/commodities.md).</summary>
public static class Grades
{
    /// <summary>Grades at or above this band read as precursor work — above
    /// any current-era tech ceiling, mechanically why ruins are prizes.</summary>
    public const double PrecursorFloor = 0.92;

    /// <summary>The era's grade ceiling per producer tech tier (1–3): the tech
    /// ladder is qualitative, not just multiplicative.</summary>
    public static double TechCeiling(int techTier) => techTier switch
    {
        <= 1 => 0.55,
        2 => 0.75,
        _ => 0.90,
    };

    public static GradeBand BandOf(double grade) => grade switch
    {
        < 0.25 => GradeBand.Crude,
        < 0.45 => GradeBand.Standard,
        < 0.65 => GradeBand.Fine,
        < 0.80 => GradeBand.Advanced,
        < PrecursorFloor => GradeBand.Masterwork,
        _ => GradeBand.PrecursorGrade,
    };

    /// <summary>How strongly grade converts to effect per use-case. Calories
    /// are calories; prestige and war reward quality.</summary>
    private static double Sensitivity(UseCase useCase) => useCase switch
    {
        UseCase.Subsistence => 0.4,
        UseCase.StandardOfLiving => 1.0,
        UseCase.Luxury => 1.6,
        UseCase.IndustryConstruction => 1.0,
        UseCase.IndustryUpkeep => 1.0,
        UseCase.Movement => 0.6,
        UseCase.MilitaryConstruction => 1.2,
        UseCase.MilitaryUpkeep => 1.2,
        UseCase.Technology => 1.4,
        _ => 1.0,
    };

    /// <summary>Effect per unit at this grade — 1.0 at grade 0.5, so quantity
    /// and quality trade off around the standard-issue midpoint.</summary>
    public static double Multiplier(UseCase useCase, double grade) =>
        1.0 + Sensitivity(useCase) * (grade - 0.5);

    private static double FacilityTierFactor(int facilityTier) => facilityTier switch
    {
        <= 1 => 0.8,
        2 => 1.0,
        _ => 1.2,
    };

    /// <summary>Producer tech tier as a quality term below the ceiling — the
    /// fourth factor of the design's grade formula.</summary>
    private static double TechTierFactor(int techTier) => techTier switch
    {
        <= 1 => 0.85,
        2 => 1.0,
        _ => 1.1,
    };

    /// <summary>Output grade of a production run: recipe base × input-grade
    /// blend × facility tier × producer tech tier, capped by the producer's
    /// tech ceiling (commodities.md grade formula).</summary>
    public static double Output(Recipe recipe, double meanInputGrade,
                                int facilityTier, int techTier)
    {
        double raw = recipe.GradeBase
                     * (0.5 + meanInputGrade)
                     * FacilityTierFactor(facilityTier)
                     * TechTierFactor(techTier);
        return Math.Min(TechCeiling(techTier), Math.Max(0.0, raw));
    }
}

/// <summary>A stock of one good: (quantity, grade). Stocks mix by
/// quantity-weighted mean grade; consumption draws at the mean.</summary>
public readonly struct Stock
{
    public GoodId Good { get; }
    public double Quantity { get; }
    public double Grade { get; }

    public Stock(GoodId good, double quantity, double grade)
    {
        Good = good; Quantity = quantity; Grade = grade;
    }

    /// <summary>Effective units for a use-case — the one interface every
    /// consumer reads: quantity × grade multiplier.</summary>
    public double Effective(UseCase useCase) =>
        Quantity * Grades.Multiplier(useCase, Grade);

    public static Stock Blend(Stock a, Stock b)
    {
        if (a.Good != b.Good)
            throw new ArgumentException($"Cannot blend {a.Good} with {b.Good}.");
        double qty = a.Quantity + b.Quantity;
        if (qty <= 0) return new Stock(a.Good, 0, 0);
        if (a.Quantity <= 0) return b;
        if (b.Quantity <= 0) return a;
        double grade = (a.Quantity * a.Grade + b.Quantity * b.Grade) / qty;
        return new Stock(a.Good, qty, grade);
    }

    public override string ToString() =>
        FormattableString.Invariant($"{Good} x{Quantity:0.##} @{Grade:0.00} ({Grades.BandOf(Grade)})");
}
