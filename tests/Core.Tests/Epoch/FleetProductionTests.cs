using System.Collections.Generic;
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

    [Fact]
    public void Yard_ConvertsComponentsIntoHulls_ByPriorities()
    {
        var (state, pr, port) = Entered();
        var market = state.Markets[port.Id];
        // a tier-2 yard, banked components + armaments, a funded treasury
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)InfraTypeId.Shipyard, tier: 2, port.Hex, pr.ActorId,
            builtYear: -100));
        market.Deposit((int)GoodId.ShipComponents, 60, 0.55);
        market.Deposit((int)GoodId.Armaments, 30, 0.5);
        pr.MilitaryPoints = 500;
        var freight = DesignRegistry.Current(state, pr.ActorId,
            ShipRole.Freight, ShipSize.Medium)!;
        var escort = DesignRegistry.Current(state, pr.ActorId,
            ShipRole.Escort, ShipSize.Light);
        var weights = new Dictionary<int, double> { [freight.Id] = 1.0 };
        if (escort != null) weights[escort.Id] = 0.5;
        state.Actors[pr.ActorId].Policies =
            (PolityPolicies.Default with { ShipbuildingPriorities = weights });

        double componentsBefore = market.Inventory[(int)GoodId.ShipComponents];
        double treasuryBefore = pr.MilitaryPoints;
        int builtBefore = pr.HullsBuilt;
        var ownPorts = new List<Port> { port };
        int laid = FleetOps.BuildFleets(state, pr, ownPorts);

        // tier 2 × 0.2/tier-year × 25y = 10 slots, components permitting
        Assert.True(laid > 0, "the yard laid nothing down");
        Assert.Equal(builtBefore + laid, pr.HullsBuilt);
        Assert.True(market.Inventory[(int)GoodId.ShipComponents] < componentsBefore);
        Assert.True(pr.MilitaryPoints < treasuryBefore);
        // hulls joined the home reserve; freight out-built the escorts ~2:1
        var home = FleetOps.HomeFleet(state, pr.ActorId, port);
        int freightHulls = 0, escortHulls = 0;
        foreach (var g in home.Hulls)
        {
            var d = state.Designs[g.DesignId];
            if (d.Role == ShipRole.Freight && d.Size == ShipSize.Medium)
                freightHulls += g.Count;
            if (escort != null && d.Role == ShipRole.Escort) escortHulls += g.Count;
        }
        Assert.True(freightHulls > state.Config.Fleet.StarterFreightHulls);
        if (escort != null && laid >= 3)
            Assert.True(freightHulls - state.Config.Fleet.StarterFreightHulls
                        > escortHulls,
                "priorities should favor freight over escorts");
    }

    [Fact]
    public void Yard_StopsAtEmptyStock_AndEmptyTreasury()
    {
        var (state, pr, port) = Entered();
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)InfraTypeId.Shipyard, tier: 2, port.Hex, pr.ActorId, -100));
        var market = state.Markets[port.Id];
        // components for barely two medium hulls
        double perHull = DesignMath.ComponentsPerHull(state.Config.Fleet,
                                                      ShipSize.Medium);
        market.Deposit((int)GoodId.ShipComponents, perHull * 2 + 0.1, 0.5);
        pr.MilitaryPoints = 10_000;
        int laid = FleetOps.BuildFleets(state, pr, new List<Port> { port });
        Assert.Equal(2, laid);

        // broke treasury: nothing moves however full the shelves
        market.Deposit((int)GoodId.ShipComponents, 100, 0.5);
        pr.MilitaryPoints = 0;
        Assert.Equal(0, FleetOps.BuildFleets(state, pr, new List<Port> { port }));
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
