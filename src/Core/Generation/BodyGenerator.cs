using System.Collections.Generic;
using StarGen.Core.Content;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class BodyGenerator
{
    public static void Generate(RollContext ctx, StarSystem system, RegionContext? region = null)
    {
        double settlementScale = region?.SettlementScale ?? 1.0;

        // Companion stars occupy a slot in the primary's own list (spec §5 "occupies");
        // those slots must stay empty of any independently-generated body.
        var companionSlots = new HashSet<int>();
        for (int i = 1; i < system.Stars.Count; i++)
            if (system.Stars[i].CompanionSlotIndex is int occupied)
                companionSlots.Add(occupied);

        for (int starIndex = 0; starIndex < system.Stars.Count; starIndex++)
        {
            var star = system.Stars[starIndex];
            foreach (var slot in star.Slots)
            {
                if (starIndex == 0 && companionSlots.Contains(slot.Index)) continue;

                // index/subIndex convention: body rolls use index = starIndex*100 + slotIndex.
                int idx = starIndex * 100 + slot.Index;
                var kindModifier = BodyTables.KindModifier(slot.Band);
                var kind = BodyTables.Kind.Pick(
                    ctx.NextDouble(RollChannel.BodyKind, idx),
                    k => kindModifier(k) * (region?.BeltModifier(k) ?? 1.0));
                if (kind == null) continue;
                slot.Body = GenerateBody(ctx, kind.Value, slot.Band, idx, 0, null, settlementScale);
                AddSatellites(ctx, slot.Body, slot.Band, idx, settlementScale);
            }
        }
    }

    /// <summary>
    /// Shared body pipeline. idx encodes star+slot; sat = 0 for planets,
    /// 1 + satelliteIndex for satellites (Task 9), keeping draws distinct.
    /// </summary>
    public static Body GenerateBody(RollContext ctx, BodyKind kind, OrbitBand band, int idx, int sat,
        int? presetSize = null, double settlementScale = 1.0)
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
                // presetSize (satellites): skip the size-table roll entirely and use the
                // already-determined satellite size so descriptors derive from the real size.
                body.Size = presetSize ?? BodyTables.RockySize.Pick(ctx.NextDouble(RollChannel.BodySize, idx, sat));
                body.Atmosphere = BodyTables.Atmo.Pick(
                    ctx.NextDouble(RollChannel.Atmosphere, idx, sat),
                    BodyTables.AtmoModifier(body.Size, band));
                body.Hydrographics = RollHydro(ctx, body, band, idx, sat);
                body.Biosphere = BodyTables.Bio.Pick(
                    ctx.NextDouble(RollChannel.Biosphere, idx, sat),
                    BodyTables.BioModifier(body.Atmosphere, band));
                break;
        }

        var settlementModifier = BodyTables.SettlementModifier(body.Biosphere, band);
        body.Settlement = BodyTables.SettlementTable.Pick(
            ctx.NextDouble(RollChannel.Settlement, idx, sat),
            st => settlementModifier(st) * (st == Settlement.None ? 1.0 : settlementScale));

        return body;
    }

    private static int RollHydro(RollContext ctx, Body body, OrbitBand band, int idx, int sat)
    {
        if (body.Atmosphere == Atmosphere.None || body.Atmosphere == Atmosphere.Trace) return 0;
        int hydro = ctx.NextInt(RollChannel.Hydrographics, 0, 101, idx, sat);
        return band == OrbitBand.Habitable ? hydro : hydro / 4;
    }

    private static void AddSatellites(RollContext ctx, Body parent, OrbitBand band, int idx, double settlementScale = 1.0)
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
            // Compute the capped satellite size first so it can be fed into GenerateBody
            // as a preset — atmosphere/biosphere modifiers must derive from the real size.
            int maxSize = parent.Kind == BodyKind.GasGiant ? 4 : parent.Size - 1;
            int satSize = 1 + ctx.NextInt(RollChannel.SatelliteSize, 0, maxSize, idx, s);
            // sat parameter = 1 + s so satellite draws never collide with the parent's (sat = 0).
            var sat = GenerateBody(ctx, kind, band, idx, 1 + s, satSize, settlementScale);
            sat.Satellites.Clear(); // guard: no satellites of satellites, ever
            parent.Satellites.Add(sat);
        }
    }
}
