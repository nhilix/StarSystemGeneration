using System.Collections.Generic;

namespace StarGen.Core.Substrate;

/// <summary>Stable good ids (substrate/commodities.md) — the meaning of every
/// goods-keyed int dictionary in the controller contract (Epoch/Policies.cs).
/// Append-only; never renumber.</summary>
public enum GoodId
{
    // Raw — extracted/grown, terrain-derived
    Provisions = 0,
    Ore = 1,
    Volatiles = 2,
    Organics = 3,
    Exotics = 4,
    // Processed — one step; some directly consumable
    Alloys = 5,
    Fuel = 6,
    Composites = 7,
    ConsumerGoods = 8,
    Medicine = 9,
    Narcotics = 10,
    RefinedExotics = 11,
    // Capital — 2–3 processing steps, the most valuable
    Machinery = 12,
    ShipComponents = 13,
    Armaments = 14,
    Compute = 15,
    Luxuries = 16,
}

public enum GoodTier { Raw, Processed, Capital }

/// <summary>Standard = exotics-free (mass over quality); Advanced =
/// exotics-gated, higher grade base, tech-tier gated (commodities.md).</summary>
public enum RecipeKind { Standard, Advanced }

/// <summary>A (good, quantity) pair — recipe inputs, build costs, upkeep draws.</summary>
public sealed record GoodQuantity(GoodId Good, double Quantity);

/// <summary>Converts inputs (per output unit) into the output good. GradeBase
/// is the recipe term of the grade formula; MinTechTier gates which variants a
/// producer can run.</summary>
public sealed record Recipe(
    GoodId Output, RecipeKind Kind, IReadOnlyList<GoodQuantity> Inputs,
    double GradeBase, int MinTechTier);

public sealed record GoodDef(
    GoodId Id, string Name, GoodTier Tier, IReadOnlyList<Recipe> Recipes);

/// <summary>The 17-good vocabulary — closed, versioned, data as code
/// (substrate/commodities.md). Input quantities are per output unit;
/// alternate standard recipes reflect the design's "/" input choices.</summary>
public static class Goods
{
    private static Recipe R(GoodId output, RecipeKind kind, double gradeBase,
                            int minTechTier, params GoodQuantity[] inputs) =>
        new(output, kind, inputs, gradeBase, minTechTier);

    private static GoodQuantity Q(GoodId good, double qty) => new(good, qty);

    private static readonly GoodDef[] Table =
    {
        // -- Raw (terrain-derived; recipe-free — extraction owns them) --
        new(GoodId.Provisions, "Provisions", GoodTier.Raw, new Recipe[0]),
        new(GoodId.Ore,        "Ore",        GoodTier.Raw, new Recipe[0]),
        new(GoodId.Volatiles,  "Volatiles",  GoodTier.Raw, new Recipe[0]),
        new(GoodId.Organics,   "Organics",   GoodTier.Raw, new Recipe[0]),
        new(GoodId.Exotics,    "Exotics",    GoodTier.Raw, new Recipe[0]),

        // -- Processed --
        new(GoodId.Alloys, "Alloys", GoodTier.Processed, new[]
        {
            R(GoodId.Alloys, RecipeKind.Standard, 0.55, 1,
              Q(GoodId.Ore, 1.0), Q(GoodId.Volatiles, 0.2)),
        }),
        new(GoodId.Fuel, "Fuel", GoodTier.Processed, new[]
        {
            R(GoodId.Fuel, RecipeKind.Standard, 0.55, 1, Q(GoodId.Volatiles, 1.0)),
        }),
        new(GoodId.Composites, "Composites", GoodTier.Processed, new[]
        {
            R(GoodId.Composites, RecipeKind.Standard, 0.55, 1,
              Q(GoodId.Volatiles, 0.5), Q(GoodId.Organics, 0.5)),
        }),
        new(GoodId.ConsumerGoods, "Consumer Goods", GoodTier.Processed, new[]
        {
            R(GoodId.ConsumerGoods, RecipeKind.Standard, 0.50, 1,
              Q(GoodId.Organics, 0.5), Q(GoodId.Alloys, 0.5)),
            R(GoodId.ConsumerGoods, RecipeKind.Standard, 0.55, 1,
              Q(GoodId.Organics, 0.5), Q(GoodId.Composites, 0.5)),
        }),
        new(GoodId.Medicine, "Medicine", GoodTier.Processed, new[]
        {
            R(GoodId.Medicine, RecipeKind.Standard, 0.50, 1, Q(GoodId.Organics, 1.0)),
            R(GoodId.Medicine, RecipeKind.Advanced, 0.80, 2,
              Q(GoodId.Organics, 0.7), Q(GoodId.RefinedExotics, 0.2)),
        }),
        new(GoodId.Narcotics, "Narcotics", GoodTier.Processed, new[]
        {
            R(GoodId.Narcotics, RecipeKind.Standard, 0.50, 1, Q(GoodId.Organics, 1.0)),
            R(GoodId.Narcotics, RecipeKind.Standard, 0.50, 1, Q(GoodId.Composites, 1.0)),
        }),
        new(GoodId.RefinedExotics, "Refined Exotics", GoodTier.Processed, new[]
        {
            R(GoodId.RefinedExotics, RecipeKind.Standard, 0.70, 2, Q(GoodId.Exotics, 1.0)),
        }),

        // -- Capital --
        new(GoodId.Machinery, "Machinery", GoodTier.Capital, new[]
        {
            R(GoodId.Machinery, RecipeKind.Standard, 0.55, 2, Q(GoodId.Alloys, 1.0)),
            R(GoodId.Machinery, RecipeKind.Advanced, 0.80, 3,
              Q(GoodId.Alloys, 0.8), Q(GoodId.RefinedExotics, 0.2)),
        }),
        new(GoodId.ShipComponents, "Ship Components", GoodTier.Capital, new[]
        {
            R(GoodId.ShipComponents, RecipeKind.Standard, 0.55, 2,
              Q(GoodId.Alloys, 0.6), Q(GoodId.Machinery, 0.4)),
            R(GoodId.ShipComponents, RecipeKind.Advanced, 0.80, 3,
              Q(GoodId.Alloys, 0.5), Q(GoodId.Machinery, 0.35),
              Q(GoodId.RefinedExotics, 0.15)),
        }),
        new(GoodId.Armaments, "Armaments", GoodTier.Capital, new[]
        {
            R(GoodId.Armaments, RecipeKind.Standard, 0.55, 2, Q(GoodId.Alloys, 1.0)),
            R(GoodId.Armaments, RecipeKind.Advanced, 0.80, 3,
              Q(GoodId.Alloys, 0.8), Q(GoodId.RefinedExotics, 0.2)),
        }),
        new(GoodId.Compute, "Compute", GoodTier.Capital, new[]
        {
            R(GoodId.Compute, RecipeKind.Standard, 0.70, 3,
              Q(GoodId.RefinedExotics, 0.5), Q(GoodId.Composites, 0.5)),
        }),
        new(GoodId.Luxuries, "Luxuries", GoodTier.Capital, new[]
        {
            R(GoodId.Luxuries, RecipeKind.Standard, 0.65, 2,
              Q(GoodId.Organics, 0.6), Q(GoodId.Exotics, 0.4)),
        }),
    };

    public static IReadOnlyList<GoodDef> All => Table;

    public static GoodDef Get(GoodId id) => Table[(int)id];
}
