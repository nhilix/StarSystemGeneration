using StarGen.Core.Model;
using StarGen.Core.Naming;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class Generator
{
    /// <summary>Baseline stellar density (spec §4 stage 0). Tunable.</summary>
    public const double StellarDensity = 0.5;

    public static HexResult Generate(ulong masterSeed, HexCoordinate coord)
    {
        var ctx = new RollContext(masterSeed, coord);

        if (ctx.NextDouble(RollChannel.Presence) >= StellarDensity)
            return new HexResult(coord, null);

        var system = new StarSystem(Designation.For(coord));
        // PIPELINE (later tasks append stages here, in order):
        StarGenerator.Generate(ctx, system);
        BodyGenerator.Generate(ctx, system);
        // SocietyGenerator.Generate(ctx, system);
        // NameGenerator.AssignNames(ctx, system);
        // OverlayResolver.Resolve(ctx, system);
        return new HexResult(coord, system);
    }
}
