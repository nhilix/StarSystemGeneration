using System;
using StarGen.Core.Model;
using StarGen.Core.Tables;

namespace StarGen.Core.Content;

/// <summary>First-draft body content and cross-influence modifiers (spec §5).</summary>
public static class BodyTables
{
    // null = slot stays empty. Wreckage is overlay-only and never appears here.
    public static readonly WeightedTable<BodyKind?> Kind = new(
        ((BodyKind?)null, 25),
        (BodyKind.RockyWorld, 30),
        (BodyKind.IceWorld, 15),
        (BodyKind.GasGiant, 15),
        (BodyKind.PlanetoidBelt, 10));

    public static readonly WeightedTable<int> RockySize = new(
        (1, 5), (2, 8), (3, 10), (4, 12), (5, 12), (6, 10), (7, 8), (8, 5), (9, 3));

    public static readonly WeightedTable<int> GasGiantSize = new(
        (10, 3), (11, 5), (12, 5), (13, 3), (14, 2));

    public static readonly WeightedTable<Atmosphere> Atmo = new(
        (Atmosphere.None, 25), (Atmosphere.Trace, 15), (Atmosphere.Thin, 18),
        (Atmosphere.Breathable, 15), (Atmosphere.Dense, 12),
        (Atmosphere.Toxic, 10), (Atmosphere.Corrosive, 5));

    public static readonly WeightedTable<Biosphere> Bio = new(
        (Biosphere.Barren, 50), (Biosphere.Microbial, 30),
        (Biosphere.Flourishing, 15), (Biosphere.Sapient, 5));

    // Tuned against the per-system stats rollup: with ~8 bodies per system, even a
    // small per-body settled rate compounds — 12% settled bodies meant >50% of
    // systems were settled. ~2.8% base yields ~15% settled systems (frontier tone).
    public static readonly WeightedTable<Settlement> SettlementTable = new(
        (Settlement.None, 97.2), (Settlement.Outpost, 2),
        (Settlement.Colony, 0.6), (Settlement.MajorWorld, 0.2));

    public static Func<BodyKind?, double> KindModifier(OrbitBand band) => kind => (band, kind) switch
    {
        (OrbitBand.Inner, BodyKind.IceWorld) => 0.1,
        (OrbitBand.Inner, BodyKind.GasGiant) => 0.5,
        (OrbitBand.Habitable, BodyKind.RockyWorld) => 1.5,
        (OrbitBand.Habitable, BodyKind.IceWorld) => 0.5,
        (OrbitBand.Outer, BodyKind.IceWorld) => 2.0,
        (OrbitBand.Outer, BodyKind.GasGiant) => 1.5,
        (OrbitBand.Outer, BodyKind.RockyWorld) => 0.5,
        _ => 1.0,
    };

    public static Func<Atmosphere, double> AtmoModifier(int size, OrbitBand band) => atmo =>
    {
        double m = 1.0;
        if (size < 4) m *= atmo switch          // small worlds hold little air
        {
            Atmosphere.None => 3.0,
            Atmosphere.Trace => 2.0,
            Atmosphere.Breathable or Atmosphere.Dense => 0.2,
            _ => 1.0,
        };
        if (band != OrbitBand.Habitable && atmo == Atmosphere.Breathable) m *= 0.3;
        if (band == OrbitBand.Inner && (atmo == Atmosphere.Toxic || atmo == Atmosphere.Corrosive)) m *= 1.5;
        return m;
    };

    public static Func<Biosphere, double> BioModifier(Atmosphere atmo, OrbitBand band) => bio =>
    {
        if (bio == Biosphere.Barren) return 1.0;
        double m = 1.0;
        if (band != OrbitBand.Habitable) m *= bio switch
        {
            Biosphere.Microbial => 0.5,
            Biosphere.Flourishing => 0.2,
            _ => 0.05,                           // sapient off-band: vanishingly rare
        };
        if (atmo == Atmosphere.Breathable) m *= bio == Biosphere.Microbial ? 1.5 : 3.0;
        if (atmo == Atmosphere.None || atmo == Atmosphere.Corrosive) m *= 0.1;
        return m;
    };

    public static Func<Settlement, double> SettlementModifier(Biosphere bio, OrbitBand band) => s =>
    {
        if (s == Settlement.None) return 1.0;
        double m = 1.0;
        if (bio == Biosphere.Flourishing) m *= 2.0;   // people settle where it's pleasant
        if (band == OrbitBand.Habitable) m *= 1.5;
        if (band != OrbitBand.Habitable) m *= 0.5;    // barren off-band rocks rarely get settled
        return m;
    };
}
