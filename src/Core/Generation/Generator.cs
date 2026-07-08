using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using StarGen.Core.Overlays;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class Generator
{
    /// <summary>Flatspace stellar density (spec §4 stage 0 of the Phase 1 spec). Tunable.</summary>
    public const double StellarDensity = 0.5;

    /// <summary>Legacy Phase 1 signature — exactly flatspace (regional spec §8).</summary>
    public static HexResult Generate(ulong masterSeed, HexCoordinate coord) =>
        Generate(GalaxyContext.Flatspace(masterSeed), coord);

    public static HexResult Generate(GalaxyContext galaxy, HexCoordinate coord)
    {
        var ctx = new RollContext(galaxy.Config.MasterSeed, coord);

        double presenceThreshold = galaxy.IsFlatspace
            ? StellarDensity
            : DensityField.At(galaxy.Config, coord);
        if (ctx.NextDouble(RollChannel.Presence) >= presenceThreshold)
            return new HexResult(coord, null);

        var system = new StarSystem(Designation.For(coord));
        StarGenerator.Generate(ctx, system);
        BodyGenerator.Generate(ctx, system);
        SocietyGenerator.Generate(ctx, system);
        NameGenerator.AssignNames(ctx, system);
        OverlayResolver.Resolve(ctx, system);
        return new HexResult(coord, system);
    }
}
