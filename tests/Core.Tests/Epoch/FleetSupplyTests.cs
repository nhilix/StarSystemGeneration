using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice E task 5: movement and supply — upkeep draws from home
/// markets, readiness drift, attrition into wreckage (event 401), physical
/// freight fuel, endurance floors, and the colony convoy (event 402).</summary>
public class FleetSupplyTests
{
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
    public void SuppliedFleet_HoldsReadiness_AndPaysTheMarket()
    {
        var (state, pr, port) = Entered();
        var market = state.Markets[port.Id];
        market.Deposit((int)GoodId.Fuel, 500, 0.5);
        market.Deposit((int)GoodId.Armaments, 500, 0.5);
        market.Deposit((int)GoodId.ShipComponents, 500, 0.5);   // civilian spares
        pr.MilitaryPoints = 1000;
        var home = FleetOps.HomeFleet(state, pr.ActorId, port);
        Assert.True(home.TotalHulls > 0);

        double treasury = pr.MilitaryPoints;
        double fuel = market.Inventory[(int)GoodId.Fuel];
        int lost = FleetOps.SupplyFleets(state, pr);

        Assert.Equal(0, lost);
        Assert.Equal(1.0, home.Readiness, 6);
        Assert.True(market.Inventory[(int)GoodId.Fuel] < fuel,
            "supply should physically draw fuel");
        Assert.True(pr.MilitaryPoints < treasury,
            "upkeep is bought from the treasury");
    }

    /// <summary>Stage 2 (spec §4b): the quartermaster's stores are the HOME
    /// PORT's stockpile — a bare market with a stocked larder still feeds
    /// the fleet, and the draw is local, never a polity pool.</summary>
    [Fact]
    public void FleetSupply_FallsBackOnTheHomePortStockpile()
    {
        var (state, pr, port) = Entered();
        pr.MilitaryPoints = 0;                 // can't buy from the market
        port.StockQty[(int)GoodId.Fuel] = 500;
        port.StockQty[(int)GoodId.Armaments] = 500;
        port.StockQty[(int)GoodId.ShipComponents] = 500;
        var home = FleetOps.HomeFleet(state, pr.ActorId, port);
        Assert.True(home.TotalHulls > 0);

        int lost = FleetOps.SupplyFleets(state, pr);

        Assert.Equal(0, lost);
        Assert.Equal(1.0, home.Readiness, 6);
        Assert.True(port.StockQty[(int)GoodId.Fuel] < 500,
            "supply should draw the home port's stock");
    }

    [Fact]
    public void UnsuppliedFleet_LosesReadiness_ThenHulls_IntoWreckage()
    {
        var (state, pr, port) = Entered();
        // bone-dry market, empty treasury: nothing to draw
        pr.MilitaryPoints = 0;
        var home = FleetOps.HomeFleet(state, pr.ActorId, port);
        int before = home.TotalHulls;
        Assert.True(before > 0);

        int totalLost = 0;
        for (int i = 0; i < 6 && home.TotalHulls > 0; i++)
            totalLost += FleetOps.SupplyFleets(state, pr);

        Assert.True(home.Readiness < state.Config.Fleet.AttritionReadinessFloor,
            "starved readiness should fall through the attrition floor");
        Assert.True(totalLost > 0, "unsupplied fleets lose hulls");
        Assert.NotEmpty(state.Wreckage);
        Assert.Equal(port.Hex, state.Wreckage[0].Hex);   // wrecks at real hexes
        Assert.Equal(pr.HullsWrecked, totalLost);
        // the ledger still conserves
        int active = 0;
        foreach (var f in state.Fleets)
            if (f.OwnerActorId == pr.ActorId) active += f.TotalHulls;
        Assert.Equal(pr.HullsBuilt, active + pr.HullsWrecked + pr.HullsScrapped);
        // and the chronicle carries the losses
        bool attrition = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.FleetAttrition) attrition = true;
        Assert.True(attrition);
    }

    [Fact]
    public void Founding_ConsumesAColonyHull_AndDispatchesAConvoy()
    {
        var (state, pr, port) = Entered();
        pr.ExpansionPoints = state.Config.Expansion.ColonyCost * 2;
        int hullsBefore = FleetOps.ColonyHullsInReserve(state, pr.ActorId);
        Assert.True(hullsBefore > 0, "starter fleet should carry a colony hull");

        // a valid nearby target: reuse the colony-candidate machinery
        var candidates = ColonyValuation.CandidatesFor(state, pr.ActorId);
        Assert.NotEmpty(candidates);
        var act = new FoundColonyAct(pr.ActorId, candidates[0].Target);
        state.Decisions.Add(new ActorDecision(pr.ActorId,
            new ControllerDecision(PolityPolicies.Default, new Act[] { act })));

        int portsBefore = state.Ports.Count;
        new ResolutionPhase().Run(state);

        // dispatch takes the hull out of the reserve and chronicles the
        // convoy, but the crossing runs in world-time now (Task 9): no port
        // and no scrap until the expedition arrives
        Assert.Equal(portsBefore, state.Ports.Count);
        Assert.Equal(hullsBefore - 1, FleetOps.ColonyHullsInReserve(state, pr.ActorId));
        bool convoy = false;
        foreach (var e in state.Staged)
            if (e.Type == WorldEventType.ConvoyDispatched) convoy = true;
        Assert.True(convoy, "the founding should chronicle its convoy");

        // fly the expedition (empty basket — it always advances) to arrival
        ProjectOps.AdvanceAll(state);
        Assert.Equal(portsBefore + 1, state.Ports.Count);
        Assert.Equal(1, pr.HullsScrapped);   // the colony ship became the colony
        // the convoy's fleet docks as the colony's reserve
        var newPort = state.Ports[portsBefore];
        FleetRecord? docked = null;
        foreach (var f in state.Fleets)
            if (f.HomePortId == newPort.Id && f.Posture == FleetPosture.Reserve)
                docked = f;
        Assert.NotNull(docked);
        Assert.Equal(newPort.Hex, docked!.Hex);
    }

    [Fact]
    public void Founding_WithoutAColonyHull_Fails()
    {
        var (state, pr, port) = Entered();
        pr.ExpansionPoints = state.Config.Expansion.ColonyCost * 2;
        // strip every colony hull
        foreach (var fleet in state.Fleets)
            for (int i = fleet.Hulls.Count - 1; i >= 0; i--)
                if (state.Designs[fleet.Hulls[i].DesignId].Role == ShipRole.Colony)
                {
                    pr.HullsScrapped += fleet.Hulls[i].Count;
                    fleet.Hulls.RemoveAt(i);
                }
        var candidates = ColonyValuation.CandidatesFor(state, pr.ActorId);
        Assert.NotEmpty(candidates);
        state.Decisions.Add(new ActorDecision(pr.ActorId,
            new ControllerDecision(PolityPolicies.Default, new Act[]
            { new FoundColonyAct(pr.ActorId, candidates[0].Target) })));

        int portsBefore = state.Ports.Count;
        double expansion = pr.ExpansionPoints;
        new ResolutionPhase().Run(state);

        Assert.Equal(portsBefore, state.Ports.Count);   // no hull, no colony
        Assert.Equal(expansion, pr.ExpansionPoints);    // losers aren't charged
    }

    [Fact]
    public void Controller_WaitsForAColonyHull()
    {
        var config = new EpochSimConfig();
        var candidates = new[] { new ColonyCandidate(new HexCoordinate(3, 3), 1.0) };
        var noHull = new PerceptionView(0, 0, new int[0],
            expansionPoints: 1000, colonyCandidates: candidates,
            colonyHullsAvailable: 0);
        Assert.Empty(new GenesisController(config).Decide(noHull).Acts);
        var hull = new PerceptionView(0, 0, new int[0],
            expansionPoints: 1000, colonyCandidates: candidates,
            colonyHullsAvailable: 1);
        Assert.NotEmpty(new GenesisController(config).Decide(hull).Acts);
    }

    [Fact]
    public void FreightFuel_IsBurnedPhysically()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2, foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        EpochTestKit.PostFreight(state, a0.Id, 0, 6);
        var mA = state.Markets[0];
        mA.Deposit((int)GoodId.Provisions, 1000, 0.6);
        mA.Deposit((int)GoodId.Fuel, 100, 0.5);
        state.Markets[1].Price[(int)GoodId.Provisions] =
            mA.Price[(int)GoodId.Provisions] * 4;
        var scratch = new MarketStepScratch(state);
        scratch.Demand[1][(int)GoodId.Provisions] = 500;

        MarketEngine.MoveFreight(state, scratch);

        Assert.True(state.Markets[1].Inventory[(int)GoodId.Provisions] > 0);
        Assert.True(mA.Inventory[(int)GoodId.Fuel] < 100,
            "shipments should burn fuel out of the source market");
    }

    [Fact]
    public void Wreckage_Serializes_WithTheFleetsLayer()
    {
        var (state, pr, port) = Entered();
        var home = FleetOps.HomeFleet(state, pr.ActorId, port);
        FleetOps.Wreck(state, home, 2);
        new ChroniclePhase().Run(state);   // flush staged events
        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("\nWRECK|0|", text);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        Assert.Equal(state.Wreckage.Count, loaded.Wreckage.Count);
        Assert.Equal(state.Wreckage[0].Hex, loaded.Wreckage[0].Hex);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
