using System.Collections.Generic;
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
        // Each companion must occupy a distinct slot ("occupies" — spec §5), so
        // a candidate that collides with an earlier companion's slot is resolved
        // deterministically without any additional RNG draws.
        var primary = system.Stars[0];
        var takenSlots = new HashSet<int>();
        for (int i = 1; i < system.Stars.Count; i++)
        {
            int half = primary.Slots.Count / 2;
            int candidate = ctx.NextInt(RollChannel.CompanionSlot, half,
                                        primary.Slots.Count, 0, i);
            int slot = ResolveFreeCompanionSlot(primary, takenSlots, candidate, half);
            takenSlots.Add(slot);
            system.Stars[i].CompanionSlotIndex = slot;
        }
    }

    /// <summary>
    /// Deterministically dedupes a companion slot candidate: first tries the
    /// candidate itself, then probes upward within the outer half
    /// [half, primary.Slots.Count), wrapping, then probes the full
    /// [0, primary.Slots.Count) range. If every existing slot is already taken
    /// (tiny primaries with more companions than slots), a new outer slot is
    /// appended to the primary and used.
    /// </summary>
    private static int ResolveFreeCompanionSlot(Star primary, HashSet<int> takenSlots, int candidate, int half)
    {
        int count = primary.Slots.Count;

        if (count > 0)
        {
            if (candidate >= 0 && candidate < count && !takenSlots.Contains(candidate))
                return candidate;

            int rangeLen = count - half;
            if (rangeLen > 0)
            {
                for (int step = 1; step < rangeLen; step++)
                {
                    int probe = half + (((candidate - half + step) % rangeLen + rangeLen) % rangeLen);
                    if (!takenSlots.Contains(probe)) return probe;
                }
            }

            for (int probe = 0; probe < count; probe++)
                if (!takenSlots.Contains(probe)) return probe;
        }

        int newIndex = primary.Slots.Count;
        primary.Slots.Add(new OrbitSlot { Index = newIndex, Band = OrbitBand.Outer });
        return newIndex;
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
