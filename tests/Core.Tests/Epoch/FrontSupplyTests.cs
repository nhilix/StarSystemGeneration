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
