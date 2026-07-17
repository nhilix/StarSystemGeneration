using System.Collections.Generic;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class OffLaneRouteTests
{
    private static SimState TwoLinkedPorts(out int a, out int b)
    {
        var (_, state) = EpochTestKit.Seeded();
        var a0 = state.Actors[0];
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new StarGen.Core.Model.HexCoordinate(a0.Seat.Q + 6, a0.Seat.R),
            tier: 2, foundedYear: 0);
        state.Ports.Add(pa); state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        a = 0; b = 1;
        return state;
    }

    [Fact]
    public void OffLaneRoute_IsASingleCrawlLeg()
    {
        var state = TwoLinkedPorts(out int a, out int b);
        var (laneIds, legYears) = ShipmentOps.OffLaneRoute(state, a, b);
        Assert.Empty(laneIds);
        Assert.Single(legYears);
        Assert.True(legYears[0] > 0);
    }

    [Fact]
    public void PlanBestRoute_TakesTheLane_WhenOpen()
    {
        var state = TwoLinkedPorts(out int a, out int b);
        var (laneIds, _) = ShipmentOps.PlanBestRoute(state, a, b, new HashSet<int>());
        Assert.NotEmpty(laneIds);        // the live lane
    }

    [Fact]
    public void PlanBestRoute_GoesOffLane_WhenTheLaneIsSevered()
    {
        var state = TwoLinkedPorts(out int a, out int b);
        var severed = new HashSet<int> { 0 };   // sever the only lane (id 0)
        var (laneIds, legYears) = ShipmentOps.PlanBestRoute(state, a, b, severed);
        Assert.Empty(laneIds);           // elected the off-lane alternative
        Assert.Single(legYears);
    }
}
