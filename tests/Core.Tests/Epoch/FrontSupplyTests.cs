using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CE / B3 (spec §4): the front is a demander — a mobilized
/// force fighting away from home draws upkeep from the nearest owned port
/// (its forward depot), not the home port; the quartermaster stocks the
/// depot; a cut supply line bleeds readiness legibly.</summary>
public class FrontSupplyTests
{
    /// <summary>A two-port realm crafted around actor 0: the capital at the
    /// seat and a forward port ten hexes out — the depot candidates.</summary>
    private static (SimState State, PolityRecord Pr, Port Home, Port Forward)
        TwoPortRealm()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        var home = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var fwd = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2,
            foundedYear: 0);
        state.Ports.Add(home);
        state.Ports.Add(fwd);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        return (state, state.PolityOf(a0.Id), home, fwd);
    }

    /// <summary>A war-stationed squadron at the given hex, flagged home to
    /// the capital — the deployed force whose draw should relocate.</summary>
    private static FleetRecord Deploy(SimState state, int actorId,
        HexCoordinate hex, int homePortId, int hulls = 4)
    {
        var design = DesignRegistry.Current(state, actorId,
                ShipRole.Escort, ShipSize.Light)
            ?? DesignRegistry.Register(state, actorId,
                ShipRole.Escort, ShipSize.Light, grade: 0.5);
        var fleet = new FleetRecord(state.Fleets.Count, actorId, hex)
        {
            Posture = FleetPosture.Blockade,
            TargetId = -1,
            HomePortId = homePortId,
        };
        fleet.AddHulls(design.Id, hulls, 0.5);
        state.Fleets.Add(fleet);
        state.PolityOf(actorId).HullsBuilt += hulls;
        return fleet;
    }

    [Fact]
    public void DeployedFleet_DrawsUpkeep_FromTheForwardDepot()
    {
        var (state, pr, home, fwd) = TwoPortRealm();
        // squadron on station two hexes past the forward port, home-flagged
        // to the capital
        var fleet = Deploy(state, pr.ActorId,
            new HexCoordinate(fwd.Hex.Q + 2, fwd.Hex.R), home.Id);
        // both books stocked: the draw's ADDRESS is what's under test
        EpochTestKit.Stock(state, home.Id, (int)GoodId.Fuel, 500, 0.5,
            ownerActorId: 1);
        EpochTestKit.Stock(state, home.Id, (int)GoodId.Armaments, 500, 0.5,
            ownerActorId: 1);
        EpochTestKit.Stock(state, fwd.Id, (int)GoodId.Fuel, 500, 0.5,
            ownerActorId: 1);
        EpochTestKit.Stock(state, fwd.Id, (int)GoodId.Armaments, 500, 0.5,
            ownerActorId: 1);
        pr.MilitaryPoints = 1000;
        double fwdFuel = BookOps.AskQty(state, fwd.Id, (int)GoodId.Fuel);
        double homeFuel = BookOps.AskQty(state, home.Id, (int)GoodId.Fuel);

        int lost = FleetOps.SupplyFleets(state, pr);

        Assert.Equal(0, lost);
        Assert.Equal(1.0, fleet.Readiness, 6);
        Assert.True(BookOps.AskQty(state, fwd.Id, (int)GoodId.Fuel) < fwdFuel,
            "a deployed fleet victuals at the forward depot");
        Assert.Equal(homeFuel,
            BookOps.AskQty(state, home.Id, (int)GoodId.Fuel), 6);
    }

    [Fact]
    public void Quartermaster_PostsWarCouriers_TowardTheDepot()
    {
        var (state, pr, home, fwd) = TwoPortRealm();
        Deploy(state, pr.ActorId,
            new HexCoordinate(fwd.Hex.Q + 2, fwd.Hex.R), home.Id);
        // rear stockpile deep, depot bare — the classic supply-line picture
        home.StockQty[(int)GoodId.Fuel] = 5000;
        home.StockQty[(int)GoodId.Armaments] = 5000;
        pr.Credits = 10000;

        int raised = ShipmentOps.StockDepots(state, pr);

        Assert.True(raised > 0, "the quartermaster should raise convoys");
        var c = Assert.Single(state.Couriers);
        Assert.Equal(CourierPriority.War, c.Priority);
        Assert.Equal(home.Id, c.OriginPortId);
        Assert.Equal(fwd.Id, c.DestPortId);
        Assert.True(c.Qty[(int)GoodId.Fuel] > 0, "convoys carry fuel");
        Assert.True(c.Qty[(int)GoodId.Armaments] > 0,
            "convoys carry armaments for warships");
    }

    [Fact]
    public void Quartermaster_HoldsFire_WhenTheDepotIsStocked()
    {
        var (state, pr, home, fwd) = TwoPortRealm();
        Deploy(state, pr.ActorId,
            new HexCoordinate(fwd.Hex.Q + 2, fwd.Hex.R), home.Id);
        home.StockQty[(int)GoodId.Fuel] = 5000;
        home.StockQty[(int)GoodId.Armaments] = 5000;
        fwd.StockQty[(int)GoodId.Fuel] = 5000;
        fwd.StockQty[(int)GoodId.Armaments] = 5000;
        pr.Credits = 10000;

        Assert.Equal(0, ShipmentOps.StockDepots(state, pr));
        Assert.Empty(state.Couriers);
    }

    /// <summary>The interdiction stage: a lane between actor 0's ports, an
    /// active war with actor 1, and actor 1's prize port for seized cargo.</summary>
    private static (SimState State, PolityRecord Pr, Port Home, Port Fwd,
                    Port Prize) ContestedLane()
    {
        var (state, pr, home, fwd) = TwoPortRealm();
        var a1 = state.Actors[1];
        a1.Entered = true;
        var prize = new Port(2, a1.Id,
            new HexCoordinate(home.Hex.Q + 5, home.Hex.R + 6), tier: 2,
            foundedYear: 0);
        state.Ports.Add(prize);
        state.Markets.Add(new Market(2, state.Config.Economy));
        EpochTestKit.AddLane(state, home.Id, fwd.Id);
        state.Wars.Add(new War(0, "The Supply War", a1.Id, pr.ActorId,
            CasusBelli.BorderIncident, -1, WarDemand.Reparations,
            state.WorldYear));
        // enemy squadron on station within reach of the lane's far end
        Deploy(state, a1.Id, new HexCoordinate(fwd.Hex.Q + 2, fwd.Hex.R),
               prize.Id);
        return (state, pr, home, fwd, prize);
    }

    [Fact]
    public void ContestedLeg_SeizesCargo_ToTheInterdictorsPort()
    {
        var (state, pr, home, fwd, prize) = ContestedLane();
        state.Config.War.InterdictionLossPerContestedYear = 1.0;  // certain

        var basket = new List<(int Good, double Qty, double Grade)>
            { ((int)GoodId.Provisions, 100.0, 0.5) };
        var s = ShipmentOps.Dispatch(state, pr.ActorId,
            ShipmentChannel.Requisition, home.Id, fwd.Id, basket);

        Assert.Null(s);                        // taken inside the sail
        Assert.Equal(0.0, fwd.StockQty[(int)GoodId.Provisions], 6);
        Assert.Equal(100.0, BookOps.AskQty(state, prize.Id,
            (int)GoodId.Provisions), 6);
        Assert.Contains(state.Staged,
            e => e.Type == WorldEventType.CargoSeized);
    }

    [Fact]
    public void Escorts_DampInterdiction_Deterministically()
    {
        var (state, pr, home, fwd, prize) = ContestedLane();
        state.Config.War.InterdictionLossPerContestedYear = 1.0;
        state.Config.War.EscortDampPerHull = 1e9;   // overwhelming screen
        // the convoy's own warships ride within reach of the same leg
        Deploy(state, pr.ActorId,
               new HexCoordinate(fwd.Hex.Q + 1, fwd.Hex.R), home.Id);

        var basket = new List<(int Good, double Qty, double Grade)>
            { ((int)GoodId.Provisions, 100.0, 0.5) };
        var s = ShipmentOps.Dispatch(state, pr.ActorId,
            ShipmentChannel.Requisition, home.Id, fwd.Id, basket);

        Assert.Equal(0.0, BookOps.AskQty(state, prize.Id,
            (int)GoodId.Provisions), 6);
        // the cargo made it through — delivered or still sailing
        Assert.True(s != null
            || fwd.StockQty[(int)GoodId.Provisions] > 0,
            "an escorted convoy should survive the contested leg");
    }

    [Fact]
    public void WarCouriers_OutrankCommerce_AtTheJobBoard()
    {
        var (state, pr, home, fwd) = TwoPortRealm();
        EpochTestKit.AddLane(state, home.Id, fwd.Id);
        EpochTestKit.PostFreight(state, pr.ActorId, 0, 4);
        // slow boats: the crossing outlasts the step, so both contracts
        // ride REGISTERED shipments and the dispatch order is observable
        state.Config.Economy.FreightHexesPerYearBase = 0.1;
        home.StockQty[(int)GoodId.Provisions] = 1000;
        home.StockQty[(int)GoodId.Armaments] = 1000;
        pr.Credits = 10000;
        // commerce posts first (lower contract id), the war order second
        var trade = CourierOps.Post(state, pr.ActorId, home.Id, fwd.Id,
            new[] { ((int)GoodId.Provisions, 100.0) }, fee: 10,
            CourierPriority.Normal);
        var war = CourierOps.Post(state, pr.ActorId, home.Id, fwd.Id,
            new[] { ((int)GoodId.Armaments, 100.0) }, fee: 10,
            CourierPriority.War);
        Assert.NotNull(trade);
        Assert.NotNull(war);

        CourierOps.AcceptOpen(state);

        // the quartermaster's convoy sailed FIRST: its shipment id is the
        // lower one even though its contract came second
        Assert.Equal(CourierStatus.InTransit, war!.Status);
        Assert.Equal(CourierStatus.InTransit, trade!.Status);
        Assert.True(war.ShipmentId < trade.ShipmentId,
            "War priority dispatches ahead of Normal");
    }

    [Fact]
    public void ReadinessStarves_OnACutSupplyLine()
    {
        var (state, pr, home, fwd) = TwoPortRealm();
        EpochTestKit.AddLane(state, home.Id, fwd.Id);
        EpochTestKit.PostFreight(state, pr.ActorId, 0, 4);
        var fleet = Deploy(state, pr.ActorId,
            new HexCoordinate(fwd.Hex.Q + 2, fwd.Hex.R), home.Id);
        // deep rear stores, bare depot, credits for fees — but the enemy
        // sits on the only lane: convoys stall, the depot never fills
        home.StockQty[(int)GoodId.Fuel] = 5000;
        home.StockQty[(int)GoodId.Armaments] = 5000;
        pr.Credits = 10000;
        pr.MilitaryPoints = 10000;
        var a1 = state.Actors[1];
        a1.Entered = true;
        EpochTestKit.BlockadePort(state, a1.Id, fwd.Id);

        double before = fleet.Readiness;
        for (int i = 0; i < 3; i++)
        {
            ShipmentOps.StockDepots(state, pr);
            CourierOps.AcceptOpen(state);
            var scratch = new MarketStepScratch(state);
            ShipmentOps.Advance(state, scratch);
            FleetOps.SupplyFleets(state, pr);
        }

        Assert.True(fleet.Readiness < before,
            "a cut supply line should bleed the front's readiness");
        Assert.Equal(0.0, fwd.StockQty[(int)GoodId.Fuel], 6);
    }

    [Fact]
    public void StationedFleet_StillDrawsItsHomePort()
    {
        var (state, pr, home, fwd) = TwoPortRealm();
        // a Reserve fleet docked at home never relocates its draw, however
        // the map is arranged
        var docked = FleetOps.HomeFleet(state, pr.ActorId, home);
        var design = DesignRegistry.Register(state, pr.ActorId,
            ShipRole.Freight, ShipSize.Medium, grade: 0.5);
        docked.AddHulls(design.Id, 4, 0.5);
        pr.HullsBuilt += 4;
        EpochTestKit.Stock(state, home.Id, (int)GoodId.Fuel, 500, 0.5,
            ownerActorId: 1);
        EpochTestKit.Stock(state, home.Id, (int)GoodId.ShipComponents, 500,
            0.5, ownerActorId: 1);
        pr.MilitaryPoints = 1000;
        double homeFuel = BookOps.AskQty(state, home.Id, (int)GoodId.Fuel);

        FleetOps.SupplyFleets(state, pr);

        Assert.True(BookOps.AskQty(state, home.Id, (int)GoodId.Fuel) < homeFuel,
            "a docked fleet victuals at home as ever");
    }
}
