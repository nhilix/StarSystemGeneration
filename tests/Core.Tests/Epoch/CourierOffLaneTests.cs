using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice L2 Task 7 (locality spec §5): a courier's lane goes
/// severed underneath it — a rival Blockade fleet parked on the dest
/// port — and there is no posted carrier fleet on that lane either, so
/// the lane-fulfiller branch of AcceptOpen finds nothing. The board must
/// route through ShipmentOps.PlanBestRoute so the severed lane yields the
/// off-lane alternative and the poster self-fulfills, instead of the
/// contract stalling Open forever (the old PlanRoute path: a route with
/// hulls-but-no-carrier is indistinguishable from a route with no lane at
/// all until PlanBestRoute elects around the severed first leg).</summary>
public class CourierOffLaneTests
{
    private const int G = (int)GoodId.Provisions;

    private static (SimState State, Port A, Port B) Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        var a1 = state.Actors[1];
        a0.Entered = true;
        a1.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2,
            foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);          // a lane, no posted fleet
        state.WorldYear = 100;
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;
        return (state, pa, pb);
    }

    /// <summary>The crux: BlockadePort at the dest port must actually land
    /// lane 0 in FleetOps.SeveredLaneIds — otherwise the rest of the test
    /// is vacuous (it would pass for the wrong reason: no carrier posted,
    /// same as the un-severed case).</summary>
    [Fact]
    public void BlockadePort_SeversTheLaneBetweenTheTwoPorts()
    {
        var (state, _, pb) = Fixture();
        var rival = state.Actors[1];

        EpochTestKit.BlockadePort(state, rival.Id, pb.Id);

        var severed = FleetOps.SeveredLaneIds(state);
        Assert.Contains(0, severed);
    }

    [Fact]
    public void SeveredCourier_IsAccepted_OffLane_NotStalled()
    {
        var (state, pa, pb) = Fixture();
        pa.DepositStock(G, 50.0, 0.5);               // the poster's larder
        state.PolityOf(0).Credits = 100;

        var c = CourierOps.Post(state, 0, 0, 1, new[] { (G, 20.0) }, 1.0,
            CourierPriority.Normal);
        Assert.NotNull(c);

        // sever the only lane (no posted carrier fleet exists on it either
        // — the lane-fulfiller branch was already going to find nothing),
        // then run the board
        var rival = state.Actors[1];
        EpochTestKit.BlockadePort(state, rival.Id, pb.Id);
        Assert.Contains(0, FleetOps.SeveredLaneIds(state));   // sanity re-check

        int accepted = CourierOps.AcceptOpen(state);

        Assert.True(accepted >= 1,
            "a severed-lane courier must be accepted off-lane, not stalled Open");
        Assert.NotEqual(CourierStatus.Open, c!.Status);
        // self-fulfilled: the poster's own actor took the off-lane haul
        Assert.Equal(0, c.FulfillerActorId);
    }
}
