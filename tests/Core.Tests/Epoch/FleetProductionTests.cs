using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice E task 3: yard production and the starter fleet — the
/// military treasury flows, yards convert components (+armaments) into
/// hulls per the standing priorities, and the hull ledger conserves.</summary>
public class FleetProductionTests
{
    /// <summary>A stepped state with one entered polity, plus handles.</summary>
    private static (SimState State, PolityRecord Polity, Port Port) Entered()
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

    [Fact]
    public void Entry_SeedsTheStarterFleet()
    {
        var (state, pr, port) = Entered();
        FleetRecord? home = null;
        foreach (var f in state.Fleets)
            if (f.OwnerActorId == pr.ActorId && f.HomePortId == port.Id)
                home = f;
        Assert.NotNull(home);
        Assert.Equal(FleetPosture.Reserve, home!.Posture);
        int freight = 0, colony = 0;
        foreach (var g in home.Hulls)
        {
            var role = state.Designs[g.DesignId].Role;
            if (role == ShipRole.Freight) freight += g.Count;
            if (role == ShipRole.Colony) colony += g.Count;
        }
        Assert.Equal(state.Config.Fleet.StarterFreightHulls, freight);
        Assert.Equal(state.Config.Fleet.StarterColonyHulls, colony);
        // the ledger opened with the furniture
        Assert.Equal(home.TotalHulls, pr.HullsBuilt);
        Assert.True(FleetOps.ColonyHullsInReserve(state, pr.ActorId) > 0);
    }

    /// <summary>Hull batches replace instant yard laydown (Task 8): a batch
    /// commissions its whole count at completion, drawing components (+
    /// armaments for warships) and treasury over its build span. Batch
    /// sizes here stand in for what the standing plan's D'Hondt over
    /// ShipbuildingPriorities would apportion — freight gets more hulls
    /// than escorts under a freight-favoring weight split.</summary>
    [Fact]
    public void HullBatches_CompleteAndCommission_PriorityWeightedCounts()
    {
        var (state, pr, port) = Entered();
        // yard feedstock lives in the site larder now (the shelf is gone)
        port.DepositStock((int)GoodId.ShipComponents, 600, 0.55);
        port.DepositStock((int)GoodId.Armaments, 300, 0.5);
        pr.MilitaryPoints = 5000;
        var freight = DesignRegistry.Current(state, pr.ActorId,
            ShipRole.Freight, ShipSize.Medium)!;
        var escort = DesignRegistry.Current(state, pr.ActorId,
            ShipRole.Escort, ShipSize.Light);

        double componentsBefore = port.StockQty[(int)GoodId.ShipComponents];
        double treasuryBefore = pr.MilitaryPoints;
        int builtBefore = pr.HullsBuilt;
        int escortHullsBefore = 0;
        foreach (var g in FleetOps.HomeFleet(state, pr.ActorId, port).Hulls)
            if (escort != null && state.Designs[g.DesignId].Role == ShipRole.Escort)
                escortHullsBefore += g.Count;

        var freightBatch = ProjectOps.SpawnHullBatch(state, pr.ActorId,
            port.Id, freight, count: 4, ProjectPriority.Growth, 0);
        Project? escortBatch = escort == null ? null
            : ProjectOps.SpawnHullBatch(state, pr.ActorId, port.Id, escort,
                count: 2, ProjectPriority.Growth, 1);

        ProjectOps.AdvanceAll(state);          // both batches' spans are
                                                // well under one epoch
        Assert.True(freightBatch.Completed);
        if (escortBatch != null) Assert.True(escortBatch.Completed);

        int expectedBuilt = builtBefore + 4 + (escortBatch != null ? 2 : 0);
        Assert.Equal(expectedBuilt, pr.HullsBuilt);
        Assert.True(port.StockQty[(int)GoodId.ShipComponents] < componentsBefore);
        Assert.True(pr.MilitaryPoints < treasuryBefore);

        // hulls joined the home reserve; freight out-built the escorts 2:1
        var home = FleetOps.HomeFleet(state, pr.ActorId, port);
        int freightHulls = 0, escortHulls = 0;
        foreach (var g in home.Hulls)
        {
            var d = state.Designs[g.DesignId];
            if (d.Role == ShipRole.Freight && d.Size == ShipSize.Medium)
                freightHulls += g.Count;
            if (escort != null && d.Role == ShipRole.Escort) escortHulls += g.Count;
        }
        Assert.Equal(state.Config.Fleet.StarterFreightHulls + 4, freightHulls);
        if (escortBatch != null)
            Assert.Equal(escortHullsBefore + 2, escortHulls);
    }

    /// <summary>A batch short on components delivers only the fed fraction
    /// of its build span, same as any other project (spec §1) — it does not
    /// commission partial hulls. A broke treasury halts progress entirely
    /// (wages gate the fraction too); refunding it lets the remainder
    /// deliver and the batch commissions in full.</summary>
    [Fact]
    public void HullBatch_StarvesOnShortStock_AndZeroTreasuryHaltsProgress()
    {
        var (state, pr, port) = Entered();
        double perHull = DesignMath.ComponentsPerHull(state.Config.Fleet,
                                                       ShipSize.Medium);
        var freight = DesignRegistry.Current(state, pr.ActorId,
            ShipRole.Freight, ShipSize.Medium)!;
        // stock for exactly one hull of a two-hull batch
        port.DepositStock((int)GoodId.ShipComponents, perHull, 0.5);
        pr.MilitaryPoints = 10_000;
        var p = ProjectOps.SpawnHullBatch(state, pr.ActorId, port.Id,
            freight, count: 2, ProjectPriority.Growth, 0);
        ProjectOps.AdvanceAll(state);
        Assert.False(p.Completed);
        Assert.Equal(0.5, p.YearsDelivered / p.YearsRequired, 3);
        Assert.Equal(0.0, port.StockQty[(int)GoodId.ShipComponents], 6);

        // fully stocked but broke: wages can't be paid, so nothing draws
        port.DepositStock((int)GoodId.ShipComponents, perHull * 2, 0.5);
        pr.MilitaryPoints = 0;
        double deliveredBefore = p.YearsDelivered;
        ProjectOps.AdvanceAll(state);
        Assert.Equal(deliveredBefore, p.YearsDelivered, 6);
        Assert.False(p.Completed);

        // treasury refunded: the remaining years deliver and it commissions
        pr.MilitaryPoints = 10_000;
        int builtBefore = pr.HullsBuilt;
        ProjectOps.AdvanceAll(state);
        Assert.True(p.Completed);
        Assert.Equal(builtBefore + 2, pr.HullsBuilt);
    }

    [Fact]
    public void HullLedger_Conserves_OverAWholeHistory()
    {
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Run(state);
        foreach (var pr in state.Polities)
        {
            int active = 0;
            foreach (var f in state.Fleets)
                if (f.OwnerActorId == pr.ActorId) active += f.TotalHulls;
            Assert.Equal(pr.HullsBuilt, active + pr.HullsWrecked + pr.HullsScrapped);
        }
    }

    [Fact]
    public void Navy_Serializes_WithTheFleetsLayer()
    {
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Run(state);
        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("\nNAVY|", text);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        foreach (var pr in state.Polities)
        {
            var lp = loaded.PolityOf(pr.ActorId);
            Assert.Equal(pr.MilitaryPoints, lp.MilitaryPoints);
            Assert.Equal(pr.HullsBuilt, lp.HullsBuilt);
        }
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
