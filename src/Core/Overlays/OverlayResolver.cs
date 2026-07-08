using System.Linq;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using StarGen.Core.Rng;
using StarGen.Core.Tables;

namespace StarGen.Core.Overlays;

public static class OverlayResolver
{
    /// <summary>Chance any overlay applies to a system (spec §6). Tunable.</summary>
    public const double GlobalOverlayChance = 0.05;

    public static void Resolve(RollContext ctx, StarSystem system, bool anchored = false)
    {
        if (anchored) return;   // anchored systems: no random overlay pile-up (spec §8)

        // Step 1: does any overlay apply at all?
        if (ctx.NextDouble(RollChannel.OverlayChance) >= GlobalOverlayChance) return;

        // Step 2: weighted pick among eligible only; none eligible -> no overlay, no retry.
        var eligible = OverlayCatalog.All.Where(o => o.IsEligible(system))
            .Select(o => (o, o.Weight)).ToArray();
        if (eligible.Length == 0) return;

        var overlay = new WeightedTable<OverlayDefinition>(eligible)
            .Pick(ctx.NextDouble(RollChannel.OverlayPick));
        overlay.Apply(ctx, system);
        system.OverlayId = overlay.Id;
        NameGenerator.EnsureNamed(ctx, system); // notable systems have names (spec §7)
    }
}
