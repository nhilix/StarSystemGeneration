using System;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>Design-sheet derivation (fleets/ships-and-fleets.md §The design
/// sheet): pure functions from (chassis cell, species embodiment, doctrine
/// temperament, tech tier, component grade) to the ~18-stat sheet. Sheets
/// are never stored — designs carry the derivation inputs and recompute on
/// demand (P6: one source of truth).</summary>
public static class DesignMath
{
    /// <summary>Tech tier as a stat-quality term (mirrors the grade system's
    /// tech factor): the ladder is qualitative, reachable cells gate on
    /// <see cref="ShipCatalog.MinTechTier"/> separately.</summary>
    private static double TechFactor(int techTier) => techTier switch
    {
        <= 1 => 0.85,
        2 => 1.0,
        _ => 1.15,
    };

    /// <summary>The sheet for one design. Embodiment biases per the design
    /// doc (machine minds: crewless swarm bias; hives: living capital ships;
    /// lithics: dense armored slow hulls); temperament tilts combat toward
    /// militancy and cargo toward openness; grade multiplies per-stat through
    /// <see cref="ShipCatalog.GradeSensitivityOf"/>.</summary>
    public static DesignSheet Sheet(ShipRole role, ShipSize size,
                                    Embodiment embodiment, double militancy,
                                    double openness, int techTier, double grade)
    {
        if (!ShipCatalog.IsValid(role, size))
            throw new ArgumentException($"no {role}/{size} cell in the chassis grid");
        var stats = new double[ShipCatalog.StatCount];
        double bulk = ShipCatalog.SizeBulkFactor(size);
        double tech = TechFactor(techTier);
        for (int i = 0; i < stats.Length; i++)
        {
            var stat = (ShipStat)i;
            double v = ShipCatalog.BaseStat(role, stat);
            if (ShipCatalog.IsBulkScaled(stat)) v *= bulk;
            v *= stat switch
            {
                ShipStat.LaneSpeed or ShipStat.CombatManeuver =>
                    ShipCatalog.SizeSpeedFactor(size),
                ShipStat.Signature => ShipCatalog.SizeSignatureFactor(size),
                ShipStat.OffLaneEndurance => ShipCatalog.SizeEnduranceFactor(size),
                _ => 1.0,
            };
            v = ApplyEmbodiment(stat, v, embodiment);
            v = ApplyTemperament(stat, v, militancy, openness);
            // tech and grade are quality terms — they scale what the design
            // does, not what it physically holds
            if (stat != ShipStat.Cargo && stat != ShipStat.Berths
                && stat != ShipStat.Hangar && stat != ShipStat.Upkeep)
                v *= tech;
            v *= Math.Max(0.05,
                1.0 + ShipCatalog.GradeSensitivityOf(stat) * (grade - 0.5));
            stats[i] = v;
        }
        return new DesignSheet(stats);
    }

    /// <summary>Species embodiment bias — a fleet's composition reads as
    /// cultural history because its hulls are shaped like their builders.</summary>
    private static double ApplyEmbodiment(ShipStat stat, double v,
                                          Embodiment embodiment) => embodiment switch
    {
        Embodiment.Machine => stat switch
        {
            ShipStat.CrewDraw => v * 0.2,          // crewless swarm bias
            ShipStat.Automation => v + 0.5,
            ShipStat.Berths => v * 0.5,            // nobody rides in cabins
            ShipStat.Signature => v * 0.9,
            _ => v,
        },
        Embodiment.Hive => stat switch
        {
            ShipStat.Berths => v * 1.5,            // living capital ships
            ShipStat.Armor => v * 1.1,
            ShipStat.CrewDraw => v * 1.3,
            _ => v,
        },
        Embodiment.Lithic => stat switch
        {
            ShipStat.Armor => v * 1.4,             // dense armored slow hulls
            ShipStat.LaneSpeed => v * 0.85,
            ShipStat.CombatManeuver => v * 0.85,
            ShipStat.FuelEfficiency => v * 0.9,
            _ => v,
        },
        _ => v,
    };

    /// <summary>Culture/doctrine tilt: militancy leans the yard toward guns,
    /// openness toward holds (fleets doc: militancy → line, openness →
    /// freight). Neutral at 0.5.</summary>
    private static double ApplyTemperament(ShipStat stat, double v,
                                           double militancy, double openness) => stat switch
    {
        ShipStat.Strike or ShipStat.SustainedFire or ShipStat.Tracking
            or ShipStat.Screens or ShipStat.PointDefense =>
            v * (0.8 + 0.4 * militancy),
        ShipStat.Cargo => v * (0.9 + 0.2 * openness),
        _ => v,
    };

    /// <summary>Ship Components consumed to lay down one hull of this
    /// design (yard production, fleets doc §Production).</summary>
    public static double ComponentsPerHull(FleetKnobs knobs, ShipSize size) =>
        knobs.HullComponentsBase * ShipCatalog.SizeCostFactor(size);

    /// <summary>Armaments consumed per hull — warship roles only.</summary>
    public static double ArmamentsPerHull(FleetKnobs knobs, ShipRole role,
                                          ShipSize size) =>
        ShipCatalog.IsWarship(role)
            ? knobs.HullArmamentsBase * ShipCatalog.SizeCostFactor(size)
            : 0.0;

    /// <summary>The world-time a yard takes to build a batch of <paramref
    /// name="count"/> hulls of <paramref name="size"/> — the SINGLE source of
    /// truth the planner's cost estimate and the actual project both read, so a
    /// hull batch is scheduled against exactly the duration it will run for.
    ///
    /// A yard consumes materials at its throughput: building N hulls takes
    /// N / rate world-years (rate = <paramref name="yardTiers"/> ×
    /// YardHullsPerTierPerYear — a yard building 25 ships takes longer than 1),
    /// floored by the size cost (bigger hulls take longer to lay down; medium
    /// is the base). Making the duration count-aware is what makes hull
    /// production world-time-honest and TELESCOPE across tick resolutions
    /// (time-not-ticks, P7): per-year cost is then yard-rate-bounded, the same
    /// whether the planner buckets a step's matured slots into one coarse
    /// bundle or a fine clock spreads them into slivers — total cost
    /// (perYear × years = perHull × count) is conserved either way.
    ///
    /// A non-positive <paramref name="yardTiers"/> (an unknown/absent yard —
    /// a real hull groundbreak never has one, the yard-capacity gate blocks
    /// first) falls back to the size floor alone.</summary>
    public static double HullBatchYears(FleetKnobs knobs, ShipSize size,
                                        int count, int yardTiers)
    {
        double medium = ComponentsPerHull(knobs, ShipSize.Medium);
        double comp = ComponentsPerHull(knobs, size);
        double sizeFloor = Math.Max(1.0, knobs.HullBuildYearsBase * (comp / medium));
        double rate = yardTiers * knobs.YardHullsPerTierPerYear;
        if (rate <= 0) return sizeFloor;
        double throughputYears = Math.Max(1, count) / rate;
        return Math.Max(sizeFloor, throughputYears);
    }
}
