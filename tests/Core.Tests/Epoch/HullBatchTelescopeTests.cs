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

    // ---- the GROUNDBREAK treasury gate telescopes too (fix wave) ----

    /// <summary>An isolated own port carrying exactly one commissioned tier-1
    /// yard, plus one HullBatch plan entry of <paramref name="count"/> hulls.
    /// Returns the batch's full administered value and the per-year draw the
    /// project will actually make, so a test can straddle the two.</summary>
    private static (SimState State, PolityRecord Polity, Port Port,
                    ShipDesign Design, double Value, double PerYear)
        YardPortWithPlan(int count)
    {
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        var pr = state.Polities[ProjectOpsTests.FirstEnteredPolity(state)];
        var actor = state.Actors[pr.ActorId];
        int home = ProjectOpsTests.OwnPort(state, pr.ActorId);
        var hex = new HexCoordinate(state.Ports[home].Hex.Q + 40,
                                    state.Ports[home].Hex.R);
        var yardPort = new Port(state.Ports.Count, pr.ActorId, hex,
                                tier: 1, state.WorldYear);
        state.Ports.Add(yardPort);
        state.Markets.Add(new Market(yardPort.Id, state.Config.Economy));
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)InfraTypeId.Shipyard, tier: 1, hex, pr.ActorId,
            state.WorldYear) { CommissionedYear = state.WorldYear });
        ShipDesign? design = null;
        foreach (var d in state.Designs)
            if (d.OwnerActorId == pr.ActorId) { design = d; break; }
        Assert.NotNull(design);
        actor.Policies = PolityPolicies.Default with
        {
            Plan = new StandingPlan(new[]
            {
                new PlanEntry(PlanEntryKind.HullBatch, ProjectPriority.Growth,
                    state.WorldYear, design!.Id, yardPort.Id,
                    new HexCoordinate(0, 0), count),
            }),
        };
        // the same value the gate administers, and the same duration the spawn
        // and the planner use (one tier-1 yard is attached)
        double comp = DesignMath.ComponentsPerHull(state.Config.Fleet, design.Size);
        double armaments = DesignMath.ArmamentsPerHull(state.Config.Fleet,
            design.Role, design.Size);
        double value = count * (
            comp * Market.InitialPrice(state.Config.Economy, GoodId.ShipComponents)
            + armaments * Market.InitialPrice(state.Config.Economy,
                GoodId.Armaments));
        double years = DesignMath.HullBatchYears(state.Config.Fleet, design.Size,
                                                 count, yardTiers: 1);
        // AllocationPhase tops the pools up from receipts (`MilitaryPoints +=
        // allocatable * budget.Military`) BEFORE Groundbreak runs, which would
        // swamp any treasury a test stages. Zero the receipts so `allocatable`
        // is 0 and the staged MilitaryPoints is what the gate actually sees.
        // (The pools only decay in DecayIdlePools, which runs AFTER Groundbreak.)
        pr.Receipts = 0;
        return (state, pr, yardPort, design, value, value / years);
    }

    private static int BatchesAt(SimState state, int portId)
    {
        int n = 0;
        foreach (var p in state.Projects)
            if (p.Kind == ProjectKind.HullBatch && p.PortId == portId) n++;
        return n;
    }

    /// <summary>The groundbreak treasury gate must bite on the PER-YEAR draw,
    /// not the lump: the project draws PerYearBasket = comp·count/years, so
    /// gating on the whole batch value made a coarse bundle need ~span× the
    /// treasury of a fine sliver AT THE SAME INSTANT — the coarse clock dropped
    /// bundles the fine clock built. A treasury that covers the per-year draw
    /// but NOT the lump must now break ground.</summary>
    [Fact]
    public void GroundbreakHullBatch_TreasuryGate_IsPerYear_NotLump()
    {
        var (state, pr, port, _, value, perYear) = YardPortWithPlan(count: 5);
        // comfortably above the per-year draw, far below the lump
        pr.MilitaryPoints = perYear * 1.5;
        Assert.True(pr.MilitaryPoints < value,
            "staging must straddle the gate: below the lump");

        new AllocationPhase().Run(state);

        Assert.Equal(1, BatchesAt(state, port.Id));
    }

    /// <summary>The gate still bites — a treasury below even the per-year draw
    /// breaks no ground. Without this the test above would pass on a gate that
    /// was simply deleted.</summary>
    [Fact]
    public void GroundbreakHullBatch_BelowThePerYearDraw_BreaksNoGround()
    {
        var (state, pr, port, _, _, perYear) = YardPortWithPlan(count: 5);
        pr.MilitaryPoints = perYear * 0.5;

        new AllocationPhase().Run(state);

        Assert.Equal(0, BatchesAt(state, port.Id));
    }

    /// <summary>Tick-invariance itself: a coarse bundle (count=5, years=25) and
    /// a fine sliver (count=1, years=5) face the SAME per-year bar at a 1-tier
    /// yard, so one treasury admits both.</summary>
    [Fact]
    public void GroundbreakHullBatch_BundleAndSliver_FaceTheSamePerYearBar()
    {
        var bundle = YardPortWithPlan(count: 5);
        var sliver = YardPortWithPlan(count: 1);
        // the bar is identical — that IS the telescoping property
        Assert.Equal(sliver.PerYear, bundle.PerYear, 6);
        // ...and the lumps are NOT (the pre-fix gate's 5x divergence)
        Assert.True(bundle.Value > sliver.Value * 4);

        double treasury = bundle.PerYear * 1.5;
        bundle.Polity.MilitaryPoints = treasury;
        sliver.Polity.MilitaryPoints = treasury;
        new AllocationPhase().Run(bundle.State);
        new AllocationPhase().Run(sliver.State);

        Assert.Equal(1, BatchesAt(bundle.State, bundle.Port.Id));
        Assert.Equal(1, BatchesAt(sliver.State, sliver.Port.Id));
    }
}
