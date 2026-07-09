using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Shared seeding helper: the real genesis pipeline over a small
/// galaxy — natural raster passes, then polity seeding from homeworld anchors.</summary>
public static class EpochTestKit
{
    public static (GalaxySkeleton Skeleton, SimState State) Seeded(
        ulong seed = 42, int radiusCells = 8)
    {
        var gc = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radiusCells };
        var skeleton = SkeletonBuilder.BuildNatural(gc);
        var state = EpochGenesis.Seed(skeleton, new EpochSimConfig { MasterSeed = seed });
        return (skeleton, state);
    }
}
