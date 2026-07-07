using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class StarGenerator
{
    public static void Generate(RollContext ctx, StarSystem system)
    {
        system.Arrangement = StarTypes.Arrangement.Pick(ctx.NextDouble(RollChannel.StarArrangement));
        int starCount = system.Arrangement switch
        {
            StarArrangement.Single => 1,
            StarArrangement.Binary => 2,
            _ => 3,
        };

        for (int i = 0; i < starCount; i++)
        {
            var def = StarTypes.Table.Pick(ctx.NextDouble(RollChannel.StarType, 0, i));
            var star = new Star
            {
                TypeId = def.Id,
                TypeName = def.DisplayName,
                Age = StarTypes.Age.Pick(ctx.NextDouble(RollChannel.StarAge, 0, i)),
            };

            // Companions carry a small close-in slot set; primaries a full one.
            int slotCount = i == 0
                ? ctx.NextInt(RollChannel.SlotCount, def.MinSlots, def.MaxSlots + 1, 0, i)
                : ctx.NextInt(RollChannel.SlotCount, 0, 4, 0, i);

            for (int s = 0; s < slotCount; s++)
                star.Slots.Add(new OrbitSlot { Index = s, Band = BandFor(def, s) });

            system.Stars.Add(star);
        }

        // Place companions in the outer half of the primary's slots (spec §5).
        var primary = system.Stars[0];
        for (int i = 1; i < system.Stars.Count; i++)
        {
            int half = primary.Slots.Count / 2;
            int slot = ctx.NextInt(RollChannel.CompanionSlot, half,
                                   primary.Slots.Count, 0, i);
            system.Stars[i].CompanionSlotIndex = slot;
        }
    }

    private static OrbitBand BandFor(StarTypeDef def, int slotIndex)
    {
        if (def.HabStart < 0 || slotIndex < def.HabStart) return
            def.HabStart < 0 && slotIndex >= 0 ? OrbitBand.Outer   // no-band stars: all outer
            : OrbitBand.Inner;
        if (slotIndex <= def.HabEnd) return OrbitBand.Habitable;
        return OrbitBand.Outer;
    }
}
