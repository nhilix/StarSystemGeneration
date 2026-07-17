using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Task 9 (slice L2): a hull batch's build DURATION is yard-
/// throughput-bounded — max(size floor, Count / yardRate) — so its per-year
/// materials draw is the SAME whether the planner buckets a step's matured
/// slots into one coarse bundle or a fine clock spreads them into slivers.
/// This is what makes hull commissioning telescope across tick resolutions
/// (time-not-ticks, P7): a coarse bundle of Count=rate·span over duration=span
/// costs the same per year — and builds the same total — as a fine sliver.
/// The planner's affordability ESTIMATE (Planner.CostOf) and the actual
/// project (ProjectOps.SpawnHullBatch) must agree on that duration, or the
/// schedule packs against a cost the spawn doesn't match.</summary>
public class HullBatchTelescopeTests
{
    /// <summary>One entered polity with an own port and its starter designs
    /// (medium freight exists so comp/medium == 1, the clean base case).</summary>
    private static (SimState State, PolityRecord Polity, Port Port) SteppedOwnPort()
    {
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Step(state);
        foreach (var a in state.Actors)
            if (a.Entered)
                foreach (var p in state.Ports)
                    if (p.OwnerActorId == a.Id)
                        return (state, state.PolityOf(a.Id), p);
        throw new Xunit.Sdk.XunitException("no polity entered after one epoch");
    }

    // ---- per-year cost is yard-throughput-bounded, batch-size-independent ----
    [Fact]
    public void HullBatch_PerYearDraw_IsYardRateBounded_AndTotalConserves()
    {
        var (state, pr, port) = SteppedOwnPort();
        var design = DesignRegistry.Current(state, pr.ActorId,
            ShipRole.Freight, ShipSize.Medium)!;
        const int yardTiers = 1;                 // rate = 1 * 0.2 = 0.2 hulls/yr
        double rate = yardTiers * state.Config.Fleet.YardHullsPerTierPerYear;
        double comp = DesignMath.ComponentsPerHull(state.Config.Fleet,
                                                   ShipSize.Medium);

        // a coarse bundle (25 years' worth of slots at once) and a fine sliver
        var bundle = ProjectOps.SpawnHullBatch(state, pr.ActorId, port.Id,
            design, count: 5, ProjectPriority.Growth, 0, yardTiers);
        var sliver = ProjectOps.SpawnHullBatch(state, pr.ActorId, port.Id,
            design, count: 1, ProjectPriority.Growth, 0, yardTiers);

        // the components drawn per world-year are IDENTICAL — bucketing slots
        // does not change the yard's throughput
        Assert.Equal(sliver.PerYearBasket[(int)GoodId.ShipComponents],
                     bundle.PerYearBasket[(int)GoodId.ShipComponents], 9);
        // and equal to the yard rate's worth of components
        Assert.Equal(comp * rate,
                     bundle.PerYearBasket[(int)GoodId.ShipComponents], 9);

        // duration telescopes: 5 hulls take 5/rate = 25y, 1 hull 1/rate = 5y
        Assert.Equal(5 / rate, bundle.YearsRequired, 6);
        Assert.Equal(1 / rate, sliver.YearsRequired, 6);

        // total is conserved — perYear * years == comp * count (only the
        // spread over world-time changed, never the sum)
        Assert.Equal(comp * 5,
            bundle.PerYearBasket[(int)GoodId.ShipComponents] * bundle.YearsRequired, 6);
        Assert.Equal(comp * 1,
            sliver.PerYearBasket[(int)GoodId.ShipComponents] * sliver.YearsRequired, 6);
    }

    // ---- the size floor still wins for a tiny batch on a fat yard ----
    [Fact]
    public void HullBatch_SizeFloor_StillDominatesWhenYardIsFast()
    {
        var (state, pr, port) = SteppedOwnPort();
        var design = DesignRegistry.Current(state, pr.ActorId,
            ShipRole.Freight, ShipSize.Medium)!;
        double floor = System.Math.Max(1.0, state.Config.Fleet.HullBuildYearsBase);
        // 10 yard tiers -> rate 2.0; one hull would take 0.5y throughput, below
        // the 1.5y size floor, so the floor governs
        var one = ProjectOps.SpawnHullBatch(state, pr.ActorId, port.Id,
            design, count: 1, ProjectPriority.Growth, 0, yardTiers: 10);
        Assert.Equal(floor, one.YearsRequired, 6);
    }

    // ---- Planner.CostOf agrees with the actual spawn on duration AND cost ----
    [Fact]
    public void PlannerCostOf_HullBatch_MatchesSpawn_DurationAndPerYearCost()
    {
        var (state, pr, port) = SteppedOwnPort();
        var design = DesignRegistry.Current(state, pr.ActorId,
            ShipRole.Freight, ShipSize.Medium)!;
        const int yardTiers = 1;
        const int count = 5;

        var proj = ProjectOps.SpawnHullBatch(state, pr.ActorId, port.Id,
            design, count, ProjectPriority.Growth, 0, yardTiers);

        var view = new PerceptionView(pr.ActorId, state.WorldYear, new int[0],
            ownDesigns: new[] { new DesignBrief(design.Id, design.Role,
                                                design.Size, design.Mark) },
            ownPorts: new[] { new PortBrief(port.Id, port.Tier, yardTiers) });
        var entry = new PlanEntry(PlanEntryKind.HullBatch, ProjectPriority.Growth,
            state.WorldYear, design.Id, port.Id, new HexCoordinate(0, 0), count);
        var (costPerYear, duration) = Planner.CostOf(entry, view, state.Config);

        Assert.Equal(proj.YearsRequired, duration, 6);

        // the estimate's per-year cost equals the project's real per-year cost
        // valued at founding prices (goods basket + wages)
        double actualPerYear = proj.WagesPerYear;
        for (int g = 0; g < proj.PerYearBasket.Length; g++)
            actualPerYear += proj.PerYearBasket[g]
                * Market.InitialPrice(state.Config.Economy, (GoodId)g);
        Assert.Equal(actualPerYear, costPerYear, 6);
    }
}
