using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Generation;

public static class SocietyGenerator
{
    public static void Generate(RollContext ctx, StarSystem system)
    {
        for (int starIndex = 0; starIndex < system.Stars.Count; starIndex++)
        {
            var star = system.Stars[starIndex];
            foreach (var slot in star.Slots)
            {
                if (slot.Body == null) continue;
                int idx = starIndex * 100 + slot.Index;
                Attach(ctx, slot.Body, idx, 0);
                for (int s = 0; s < slot.Body.Satellites.Count; s++)
                    Attach(ctx, slot.Body.Satellites[s], idx, 1 + s);
            }
        }
    }

    private static void Attach(RollContext ctx, Body body, int idx, int sat)
    {
        if (!body.IsInhabited) return;
        if (body.Society != null) return;

        var (min, max) = body.Settlement switch
        {
            Settlement.Outpost => (1, 4),
            Settlement.Colony => (3, 7),
            Settlement.MajorWorld => (6, 10),
            _ => (4, 10), // native sapient, unsettled by others
        };

        int pop = ctx.NextInt(RollChannel.PopulationTier, min, max, idx, sat);
        body.Society = new Society
        {
            PopulationTier = pop,
            Government = SocietyTables.Government.Pick(ctx.NextDouble(RollChannel.Government, idx, sat)),
            Order = SocietyTables.Order.Pick(ctx.NextDouble(RollChannel.OrderTier, idx, sat)),
            Port = SocietyTables.Port.Pick(
                ctx.NextDouble(RollChannel.PortTier, idx, sat),
                SocietyTables.PortModifier(pop)),
        };
    }
}
