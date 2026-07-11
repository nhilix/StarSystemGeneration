using System.Collections.Generic;

namespace StarGen.Core.Substrate;

/// <summary>Stable facility-type ids (substrate/infrastructure.md) — a
/// closed, versioned vocabulary like the anchor types. Append-only; never
/// renumber.</summary>
public enum InfraTypeId
{
    Port = 0,            // keystone: outpost → starport → nexus
    Mine = 1,
    Skimmer = 2,
    AgriComplex = 3,
    ExcavationSite = 4,
    Refinery = 5,
    Chemworks = 6,
    Fabricator = 7,
    ExoticsLab = 8,
    Foundry = 9,
    Shipyard = 10,
    Arsenal = 11,
    ComputeCore = 12,
    Depot = 13,
    Fortress = 14,
    Gate = 15,           // lane terminus: paired port infrastructure
}

public enum InfraFamily { Keystone, Extraction, Processing, Heavy, Support }

/// <summary>One facility type. Costs and upkeep are tier-1 values in real
/// goods; scale by <see cref="Production.TierCostFactor"/>. BaseOutputPerYear
/// is base(type, tier 1) of the production formula; keystone/support types
/// provide capability, not goods, and produce nothing. LaborRequired is the
/// tier-1 workforce draw against domain population.</summary>
public sealed record InfraDef(
    InfraTypeId Id, string Name, InfraFamily Family,
    IReadOnlyList<GoodId> Produces,
    IReadOnlyList<GoodQuantity> BuildCost,
    double ConstructionYears,
    IReadOnlyList<GoodQuantity> UpkeepPerYear,
    double BaseOutputPerYear,
    double LaborRequired);

/// <summary>The 16-row facility catalog: the keystone port plus fifteen
/// buildable types in four families (substrate/infrastructure.md). Siting
/// rules live in <see cref="Siting"/>; the production formula in
/// <see cref="Production"/>.</summary>
public static class Infrastructure
{
    private static GoodQuantity Q(GoodId good, double qty) => new(good, qty);
    private static IReadOnlyList<GoodId> Makes(params GoodId[] goods) => goods;
    private static readonly GoodId[] None = new GoodId[0];

    private static readonly InfraDef[] Table =
    {
        new(InfraTypeId.Port, "Port", InfraFamily.Keystone, None,
            new[] { Q(GoodId.Alloys, 40), Q(GoodId.Machinery, 20), Q(GoodId.Composites, 20) },
            ConstructionYears: 4,
            new[] { Q(GoodId.Alloys, 0.5), Q(GoodId.Machinery, 0.25) },
            BaseOutputPerYear: 0, LaborRequired: 2.0),

        // -- Extraction --
        new(InfraTypeId.Mine, "Mine", InfraFamily.Extraction, Makes(GoodId.Ore),
            new[] { Q(GoodId.Alloys, 10), Q(GoodId.Machinery, 5) }, 2,
            new[] { Q(GoodId.Machinery, 0.1) }, 10, 1.0),
        new(InfraTypeId.Skimmer, "Skimmer", InfraFamily.Extraction, Makes(GoodId.Volatiles),
            new[] { Q(GoodId.Alloys, 8), Q(GoodId.Machinery, 6) }, 2,
            new[] { Q(GoodId.Machinery, 0.1) }, 10, 1.0),
        new(InfraTypeId.AgriComplex, "Agri-complex", InfraFamily.Extraction,
            Makes(GoodId.Provisions, GoodId.Organics),
            new[] { Q(GoodId.Alloys, 6), Q(GoodId.Machinery, 4), Q(GoodId.Composites, 2) }, 2,
            new[] { Q(GoodId.Machinery, 0.075) }, 12, 1.0),
        new(InfraTypeId.ExcavationSite, "Excavation site", InfraFamily.Extraction,
            Makes(GoodId.Exotics),
            new[] { Q(GoodId.Alloys, 8), Q(GoodId.Machinery, 8) }, 3,
            new[] { Q(GoodId.Machinery, 0.15) }, 2, 1.0),   // scarce by design

        // -- Processing --
        new(InfraTypeId.Refinery, "Refinery", InfraFamily.Processing,
            Makes(GoodId.Alloys, GoodId.Fuel),
            new[] { Q(GoodId.Alloys, 12), Q(GoodId.Machinery, 8) }, 2,
            new[] { Q(GoodId.Machinery, 0.125) }, 10, 1.0),
        new(InfraTypeId.Chemworks, "Chemworks", InfraFamily.Processing,
            Makes(GoodId.Composites, GoodId.Narcotics),
            new[] { Q(GoodId.Alloys, 10), Q(GoodId.Machinery, 8) }, 2,
            new[] { Q(GoodId.Machinery, 0.125) }, 10, 1.0),
        new(InfraTypeId.Fabricator, "Fabricator", InfraFamily.Processing,
            Makes(GoodId.ConsumerGoods, GoodId.Medicine),
            new[] { Q(GoodId.Alloys, 10), Q(GoodId.Machinery, 10) }, 2,
            new[] { Q(GoodId.Machinery, 0.125) }, 10, 1.2),
        new(InfraTypeId.ExoticsLab, "Exotics lab", InfraFamily.Processing,
            Makes(GoodId.RefinedExotics),
            new[] { Q(GoodId.Alloys, 10), Q(GoodId.Machinery, 12), Q(GoodId.Composites, 6) }, 3,
            new[] { Q(GoodId.Machinery, 0.2) }, 3, 1.0),

        // -- Heavy --
        new(InfraTypeId.Foundry, "Foundry", InfraFamily.Heavy, Makes(GoodId.Machinery),
            new[] { Q(GoodId.Alloys, 16), Q(GoodId.Machinery, 10) }, 3,
            new[] { Q(GoodId.Machinery, 0.15) }, 8, 1.5),
        new(InfraTypeId.Shipyard, "Shipyard", InfraFamily.Heavy, Makes(GoodId.ShipComponents),
            new[] { Q(GoodId.Alloys, 24), Q(GoodId.Machinery, 16), Q(GoodId.Composites, 8) }, 4,
            new[] { Q(GoodId.Machinery, 0.25) }, 6, 2.0),
        new(InfraTypeId.Arsenal, "Arsenal", InfraFamily.Heavy, Makes(GoodId.Armaments),
            new[] { Q(GoodId.Alloys, 14), Q(GoodId.Machinery, 10) }, 3,
            new[] { Q(GoodId.Machinery, 0.15) }, 8, 1.2),
        new(InfraTypeId.ComputeCore, "Compute core", InfraFamily.Heavy, Makes(GoodId.Compute),
            new[] { Q(GoodId.Alloys, 10), Q(GoodId.Machinery, 14), Q(GoodId.Composites, 10) }, 3,
            new[] { Q(GoodId.Machinery, 0.25) }, 4, 0.8),

        // -- Support --
        new(InfraTypeId.Depot, "Depot", InfraFamily.Support, None,
            new[] { Q(GoodId.Alloys, 8), Q(GoodId.Composites, 6) }, 1,
            new[] { Q(GoodId.Machinery, 0.05) }, 0, 0.5),
        new(InfraTypeId.Fortress, "Fortress", InfraFamily.Support, None,
            new[] { Q(GoodId.Alloys, 20), Q(GoodId.Machinery, 8), Q(GoodId.Composites, 6) }, 3,
            new[] { Q(GoodId.Machinery, 0.1), Q(GoodId.Alloys, 0.2) }, 0, 1.0),
        // gates carry no upkeep draw: a sealed mass-driver pair is passive
        // once linked — condition moves only by war damage and recovery
        // (upkeep's met-fraction rule would kill every frontier gate whose
        // market holds zero machinery, however small the draw)
        new(InfraTypeId.Gate, "Gate", InfraFamily.Support, None,
            new[] { Q(GoodId.Alloys, 4), Q(GoodId.Machinery, 3), Q(GoodId.Composites, 2) }, 3,
            new GoodQuantity[0], 0, 0.5),
    };

    public static IReadOnlyList<InfraDef> All => Table;

    public static InfraDef Get(InfraTypeId id) => Table[(int)id];
}
