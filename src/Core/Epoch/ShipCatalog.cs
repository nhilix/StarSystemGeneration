using System;

namespace StarGen.Core.Epoch;

/// <summary>Chassis-grid roles (fleets/ships-and-fleets.md). Stable ids —
/// append-only, never renumber. Special (precursor and unique hulls) is
/// above-grid and not a buildable catalog row; it lands with archaeology.</summary>
public enum ShipRole
{
    Freight = 0,
    Escort = 1,
    Line = 2,
    Carrier = 3,
    Scout = 4,
    Colony = 5,
}

/// <summary>Chassis-grid sizes. Stable ids — append-only.</summary>
public enum ShipSize { Light = 0, Medium = 1, Heavy = 2, Capital = 3 }

/// <summary>The design sheet's stat axes, in the design doc's four blocks
/// (combat · mobility · capacity · operations). Stable ids — the sheet
/// array is indexed by these.</summary>
public enum ShipStat
{
    // Combat
    Strike = 0,
    SustainedFire = 1,
    Tracking = 2,
    Armor = 3,
    Screens = 4,
    PointDefense = 5,
    // Mobility
    LaneSpeed = 6,
    CombatManeuver = 7,
    OffLaneEndurance = 8,
    FuelEfficiency = 9,
    // Capacity
    Cargo = 10,
    Hangar = 11,
    Berths = 12,
    // Operations
    Sensors = 13,
    Signature = 14,
    CrewDraw = 15,
    Automation = 16,
    Upkeep = 17,
}

/// <summary>One design's ~18-stat sheet — layer 1 of the two-layer stat
/// model. Immutable; built by <see cref="DesignMath.Sheet"/>, consumed by
/// fleet aggregation (layer 2) and, at play clock, as the ship the player
/// flies (P7: one source of truth, two samplings).</summary>
public readonly struct DesignSheet
{
    private readonly double[] _stats;

    public DesignSheet(double[] stats)
    {
        if (stats.Length != ShipCatalog.StatCount)
            throw new ArgumentException($"sheet needs {ShipCatalog.StatCount} stats");
        _stats = stats;
    }

    public double this[ShipStat stat] => _stats[(int)stat];
}

/// <summary>The chassis grid as data-as-code (fleets/ships-and-fleets.md):
/// cell names, validity, per-role Medium-baseline stat sheets, and the
/// per-size scaling vectors. Structural constants like the goods and
/// infrastructure catalogs — calibration dials live in
/// <see cref="FleetKnobs"/> instead (TUNING.md carries both).</summary>
public static class ShipCatalog
{
    public const int RoleCount = 6;
    public const int SizeCount = 4;
    public const int StatCount = 18;

    /// <summary>Grid cell names, role-major. null marks the grid's "—"
    /// cells — no such hull exists.</summary>
    private static readonly string?[,] CellNames =
    {
        // Light            Medium        Heavy               Capital
        { "courier-trader", "hauler",     "bulk freighter",   "super-freighter" },
        { "corvette",       "frigate",    "cruiser",          null },
        { "attack craft",   "destroyer",  "battlecruiser",    "dreadnought" },
        { null,             "tender",     "fleet carrier",    "swarm-mother" },
        { "scout",          "surveyor",   "expedition ship",  null },
        { null,             "pioneer",    "colony ship",      "seed-ark" },
    };

    /// <summary>Per-role stat baselines at Medium size, ShipStat order.
    /// Rows distinguish per the design table: line = armor+sustained,
    /// escort = tracking+PD, freight = cargo, scout = sensors+endurance,
    /// colony = berths.</summary>
    private static readonly double[][] RoleBase =
    {
        //           Strk  Sust  Trck  Armr  Scrn  PtDf  LSpd  Mnvr  Endu  FEff  Crgo  Hngr  Brth  Sens  Sig   Crew  Auto  Upkp
        /*Freight*/ new[]{ 0.5,  0.5,  1.0,  2.0,  1.0,  1.0,  1.0,  0.5,  4.0,  1.2, 10.0,  0.0,  1.0,  1.0,  1.2,  1.0,  0.3,  0.5 },
        /*Escort */ new[]{ 3.0,  3.0,  4.0,  3.0,  3.0,  4.0,  1.2,  1.2,  6.0,  1.0,  1.0,  0.0,  0.5,  3.0,  0.9,  1.5,  0.3,  1.0 },
        /*Line   */ new[]{ 5.0,  6.0,  2.5,  6.0,  4.0,  2.0,  1.0,  0.9,  5.0,  0.8,  0.5,  0.0,  0.5,  2.0,  1.3,  2.0,  0.3,  1.6 },
        /*Carrier*/ new[]{ 1.0,  2.0,  2.0,  4.0,  3.0,  3.0,  0.9,  0.6,  7.0,  0.9,  3.0,  8.0,  2.0,  3.0,  1.5,  2.5,  0.3,  1.8 },
        /*Scout  */ new[]{ 0.8,  0.8,  2.0,  1.0,  1.0,  1.0,  1.4,  1.3, 10.0,  1.4,  1.0,  0.0,  0.5,  6.0,  0.6,  0.8,  0.4,  0.7 },
        /*Colony */ new[]{ 0.3,  0.3,  0.5,  2.0,  1.0,  1.0,  0.8,  0.4,  9.0,  0.9,  6.0,  1.0,  8.0,  2.0,  1.5,  2.0,  0.3,  1.2 },
    };

    /// <summary>Which stats scale with hull bulk (mass-driven: firepower,
    /// protection, holds, crews, running costs), ShipStat order.</summary>
    private static readonly bool[] BulkScaled =
    {
        true, true, false, true, true, true,      // combat (tracking is quality)
        false, false, false, false,               // mobility scales by its own vector
        true, true, true,                         // capacity
        false, false, true, false, true,          // ops: crew and upkeep grow
    };

    /// <summary>Per-stat component-grade sensitivity: grade multiplies
    /// through the design's emphasis — precision systems (tracking, PD,
    /// sensors) gain most; sheer volume (cargo, berths) barely; negative
    /// values shrink with grade (finer hulls run quieter with fewer hands).</summary>
    private static readonly double[] GradeSensitivity =
    {
        1.2, 1.2, 1.4, 1.0, 1.2, 1.4,
        0.4, 0.6, 0.6, 0.6,
        0.3, 0.3, 0.2,
        1.4, -0.6, -0.4, 0.6, 0.0,
    };

    /// <summary>Bulk multiplier per size — superlinear so capitals matter.</summary>
    public static double SizeBulkFactor(ShipSize size) => size switch
    {
        ShipSize.Light => 0.5,
        ShipSize.Medium => 1.0,
        ShipSize.Heavy => 2.2,
        _ => 5.0,
    };

    /// <summary>Speed multiplier per size (LaneSpeed, CombatManeuver) —
    /// small hulls dart, capitals lumber.</summary>
    public static double SizeSpeedFactor(ShipSize size) => size switch
    {
        ShipSize.Light => 1.3,
        ShipSize.Medium => 1.0,
        ShipSize.Heavy => 0.8,
        _ => 0.6,
    };

    /// <summary>Signature multiplier per size — big hulls are loud.</summary>
    public static double SizeSignatureFactor(ShipSize size) => size switch
    {
        ShipSize.Light => 0.5,
        ShipSize.Medium => 1.0,
        ShipSize.Heavy => 1.6,
        _ => 2.5,
    };

    /// <summary>Off-lane endurance multiplier per size — larger hulls carry
    /// deeper stores (role dominates; this is a nudge).</summary>
    public static double SizeEnduranceFactor(ShipSize size) => size switch
    {
        ShipSize.Light => 0.8,
        ShipSize.Medium => 1.0,
        ShipSize.Heavy => 1.2,
        _ => 1.4,
    };

    /// <summary>Build-cost multiplier per size, applied to the hull cost
    /// knobs (FleetKnobs.HullComponentsBase / HullArmamentsBase).</summary>
    public static double SizeCostFactor(ShipSize size) => size switch
    {
        ShipSize.Light => 0.5,
        ShipSize.Medium => 1.0,
        ShipSize.Heavy => 2.5,
        _ => 6.0,
    };

    /// <summary>Producer tech tier required to lay down this size — tech
    /// unlocks grid regions (fleets/ships-and-fleets.md).</summary>
    public static int MinTechTier(ShipSize size) => size switch
    {
        ShipSize.Light => 1,
        ShipSize.Medium => 1,
        ShipSize.Heavy => 2,
        _ => 3,
    };

    /// <summary>Warship roles draw Armaments at the yard and armaments-
    /// flavored upkeep; the rest are civilian hulls.</summary>
    public static bool IsWarship(ShipRole role) =>
        role == ShipRole.Escort || role == ShipRole.Line || role == ShipRole.Carrier;

    /// <summary>True iff the grid has a hull at this cell.</summary>
    public static bool IsValid(ShipRole role, ShipSize size) =>
        CellNames[(int)role, (int)size] != null;

    /// <summary>The grid cell's hull name ("hauler", "corvette", …) — the
    /// lineage's inherited name root. Throws on "—" cells.</summary>
    public static string CellName(ShipRole role, ShipSize size) =>
        CellNames[(int)role, (int)size]
        ?? throw new ArgumentException($"no {role}/{size} cell in the chassis grid");

    /// <summary>Role baseline stat (Medium size, neutral everything).</summary>
    public static double BaseStat(ShipRole role, ShipStat stat) =>
        RoleBase[(int)role][(int)stat];

    internal static bool IsBulkScaled(ShipStat stat) => BulkScaled[(int)stat];

    internal static double GradeSensitivityOf(ShipStat stat) =>
        GradeSensitivity[(int)stat];
}
