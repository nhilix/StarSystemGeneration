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
        var region = RegionContext.For(galaxy, coord);

        bool anchored = region?.AnchorAt != null;
        if (!anchored)
        {
            // a context without a skeleton has no nature to read — flatspace
            // (mirrors RegionContext.For returning null)
            double presenceThreshold = galaxy.IsFlatspace || galaxy.Skeleton == null
                ? StellarDensity
                : DensityField.At(galaxy.Skeleton, coord);
            if (ctx.NextDouble(RollChannel.Presence) >= presenceThreshold)
                return new HexResult(coord, null);
        }

        var system = new StarSystem(Designation.For(coord));
        StarGenerator.Generate(ctx, system, region);
        BodyGenerator.Generate(ctx, system, region);
        SocietyGenerator.Generate(ctx, system);
        NameGenerator.AssignNames(ctx, system);
        if (anchored) ApplyPreCommitment(ctx, system, region!.AnchorAt!);
        OverlayResolver.Resolve(ctx, system, anchored);
        return new HexResult(coord, system);
    }

    private static void ApplyPreCommitment(RollContext ctx, StarSystem system, Anchor anchor)
    {
        switch (anchor.Type)
        {
            case AnchorType.MineralRich:
                system.Tags.Add("mineral-rich");
                break;
            case AnchorType.PrecursorSite:
                system.Tags.Add("precursor site");
                break;
            case AnchorType.Homeworld:
                system.Tags.Add("homeworld");
                var world = BestWorld(system);
                if (world == null)
                {
                    // All slots rolled empty — synthesize the homeworld body deterministically.
                    // The primary always has >= 1 slot (every star type's MinSlots >= 1), and an
                    // empty slot never consumed its body-descriptor draws, so GenerateBody's
                    // rolls at (idx = slot.Index, sat = 0) are fresh and deterministic.
                    var slot = system.Stars[0].Slots[0];
                    slot.Body = BodyGenerator.GenerateBody(ctx, BodyKind.RockyWorld, slot.Band, slot.Index, 0);
                    world = slot.Body;
                }
                world.Biosphere = Biosphere.Sapient;
                world.Settlement = Settlement.MajorWorld;
                world.Society = null;                       // re-attach with forced facts
                SocietyGenerator.Generate(ctx, system);     // fills only missing societies
                NameGenerator.AssignNames(ctx, system);     // fills only missing names
                break;
        }
    }

    private static Body? BestWorld(StarSystem system)
    {
        Body? best = null;
        foreach (var star in system.Stars)
            foreach (var slot in star.Slots)
            {
                var b = slot.Body;
                if (b == null) continue;
                if (b.Kind != BodyKind.RockyWorld && b.Kind != BodyKind.IceWorld)
                {
                    if (best == null) best = b;   // fallback: any body at all
                    continue;
                }
                if (best == null || best.Kind == BodyKind.GasGiant
                    || best.Kind == BodyKind.PlanetoidBelt || b.Size > best.Size)
                    best = b;
            }
        return best;
    }
}
