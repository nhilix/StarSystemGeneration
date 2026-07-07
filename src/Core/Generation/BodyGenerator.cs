using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class BodyGenerator
{
    public static void Generate(RollContext ctx, StarSystem system)
    {
        for (int starIndex = 0; starIndex < system.Stars.Count; starIndex++)
        {
            var star = system.Stars[starIndex];
            foreach (var slot in star.Slots)
            {
                // index/subIndex convention: body rolls use index = starIndex*100 + slotIndex.
                int idx = starIndex * 100 + slot.Index;
                var kind = BodyTables.Kind.Pick(
                    ctx.NextDouble(RollChannel.BodyKind, idx),
                    BodyTables.KindModifier(slot.Band));
                if (kind == null) continue;
                slot.Body = GenerateBody(ctx, kind.Value, slot.Band, idx, 0);
                AddSatellites(ctx, slot.Body, slot.Band, idx);
            }
        }
    }

    /// <summary>
    /// Shared body pipeline. idx encodes star+slot; sat = 0 for planets,
    /// 1 + satelliteIndex for satellites (Task 9), keeping draws distinct.
    /// </summary>
    public static Body GenerateBody(RollContext ctx, BodyKind kind, OrbitBand band, int idx, int sat)
    {
        var body = new Body { Kind = kind };

        switch (kind)
        {
            case BodyKind.PlanetoidBelt:
                body.Size = 0;
                body.Atmosphere = Atmosphere.None;
                body.Biosphere = Biosphere.Barren;
                break;
            case BodyKind.GasGiant:
                body.Size = BodyTables.GasGiantSize.Pick(ctx.NextDouble(RollChannel.BodySize, idx, sat));
                body.Atmosphere = Atmosphere.Dense;
                body.Biosphere = Biosphere.Barren;
                break;
            default: // RockyWorld, IceWorld (Wreckage never reaches baseline)
                body.Size = BodyTables.RockySize.Pick(ctx.NextDouble(RollChannel.BodySize, idx, sat));
                body.Atmosphere = BodyTables.Atmo.Pick(
                    ctx.NextDouble(RollChannel.Atmosphere, idx, sat),
                    BodyTables.AtmoModifier(body.Size, band));
                body.Hydrographics = RollHydro(ctx, body, band, idx, sat);
                body.Biosphere = BodyTables.Bio.Pick(
                    ctx.NextDouble(RollChannel.Biosphere, idx, sat),
                    BodyTables.BioModifier(body.Atmosphere, band));
                break;
        }

        body.Settlement = BodyTables.SettlementTable.Pick(
            ctx.NextDouble(RollChannel.Settlement, idx, sat),
            BodyTables.SettlementModifier(body.Biosphere, band));

        return body;
    }

    private static int RollHydro(RollContext ctx, Body body, OrbitBand band, int idx, int sat)
    {
        if (body.Atmosphere == Atmosphere.None || body.Atmosphere == Atmosphere.Trace) return 0;
        int hydro = ctx.NextInt(RollChannel.Hydrographics, 0, 101, idx, sat);
        return band == OrbitBand.Habitable ? hydro : hydro / 4;
    }

    private static void AddSatellites(RollContext ctx, Body parent, OrbitBand band, int idx)
    {
        var countTable = parent.Kind switch
        {
            BodyKind.GasGiant => SatelliteTables.GasGiantCount,
            BodyKind.RockyWorld or BodyKind.IceWorld when parent.Size >= 4 => SatelliteTables.WorldCount,
            _ => null,
        };
        if (countTable == null) return;

        int count = countTable.Pick(ctx.NextDouble(RollChannel.SatelliteCount, idx));
        for (int s = 0; s < count; s++)
        {
            var kind = SatelliteTables.Kind.Pick(ctx.NextDouble(RollChannel.SatelliteKind, idx, s));
            // sat parameter = 1 + s so satellite draws never collide with the parent's (sat = 0).
            var sat = GenerateBody(ctx, kind, band, idx, 1 + s);
            int maxSize = parent.Kind == BodyKind.GasGiant ? 4 : parent.Size - 1;
            sat.Size = 1 + ctx.NextInt(RollChannel.SatelliteSize, 0, maxSize, idx, s);
            sat.Satellites.Clear(); // guard: no satellites of satellites, ever
            parent.Satellites.Add(sat);
        }
    }
}
