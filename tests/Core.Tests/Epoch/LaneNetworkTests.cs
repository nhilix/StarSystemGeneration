using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class LaneNetworkTests
{
    // Chain A-B-C on a line: the network path A→C equals the direct
    // distance, so a direct A-C lane is redundant (detour rule).
    [Fact]
    public void DirectLane_RedundantWhileTheChainIsShortEnough()
    {
        var state = ThreePortChain(out int a, out int b, out int c);
        EpochTestKit.AddLane(state, a, b);
        EpochTestKit.AddLane(state, b, c);
        Assert.False(LaneNetwork.DirectLaneEligible(state, a, c));
    }

    [Fact]
    public void DirectLane_EligibleWhenNoPathExists()
    {
        var state = ThreePortChain(out int a, out _, out int c);
        Assert.True(LaneNetwork.DirectLaneEligible(state, a, c));
    }

    [Fact]
    public void DirectLane_EligibleWhenTheDetourIsTooLong()
    {
        // B far off the A-C axis: the network path doubles the crossing
        var state = ThreePortChain(out int a, out int b, out int c,
                                   bendDr: 12);
        EpochTestKit.AddLane(state, a, b);
        EpochTestKit.AddLane(state, b, c);
        Assert.True(LaneNetwork.DirectLaneEligible(state, a, c));
    }

    [Fact]
    public void SaturatedChain_EarnsTheExpressBypass()
    {
        var state = ThreePortChain(out int a, out int b, out int c);
        var ab = EpochTestKit.AddLane(state, a, b);
        var bc = EpochTestKit.AddLane(state, b, c);
        int need = state.Config.Expansion.SaturatedEpochsForExpress
                   * state.Config.Sim.GenerationYears;
        ab.SaturatedYears = need;
        bc.SaturatedYears = need;
        Assert.True(LaneNetwork.DirectLaneEligible(state, a, c));
        bc.SaturatedYears = need - 1;       // one cool link blocks the waiver
        Assert.False(LaneNetwork.DirectLaneEligible(state, a, c));
    }

    [Fact]
    public void ShortestPath_PicksTheChain_AndReportsItsLanes()
    {
        var state = ThreePortChain(out int a, out int b, out int c);
        var ab = EpochTestKit.AddLane(state, a, b);
        var bc = EpochTestKit.AddLane(state, b, c);
        var (hexes, laneIds) = LaneNetwork.ShortestPath(state, a, c);
        Assert.Equal(12, hexes);
        Assert.Equal(new[] { ab.Id, bc.Id }, laneIds);
    }

    [Fact]
    public void DeadLanes_DropOutOfTheNetwork()
    {
        var state = ThreePortChain(out int a, out int b, out int c);
        var ab = EpochTestKit.AddLane(state, a, b);
        EpochTestKit.AddLane(state, b, c);
        state.Facilities[ab.GateAId].Condition = 0.0;   // gate down
        var (hexes, _) = LaneNetwork.ShortestPath(state, a, c);
        Assert.Equal(-1, hexes);
    }

    [Fact]
    public void GateSlots_CapByPortTier()
    {
        var state = ThreePortChain(out int a, out int b, out int c);
        var port = state.Ports[a];
        port.Tier = 1;                       // 1 × GateSlotsPerPortTier = 2
        EpochTestKit.AddLane(state, a, b);
        Assert.True(LaneNetwork.HasFreeGateSlot(state, port));
        EpochTestKit.AddLane(state, a, c);
        Assert.False(LaneNetwork.HasFreeGateSlot(state, port));
    }

    /// <summary>A seeded state with three same-owner ports on a line: the
    /// homeworld plus two manufactured ports 6 and 12 hexes out. A nonzero
    /// bend pushes B off the axis to lengthen the chain.</summary>
    private static SimState ThreePortChain(out int a, out int b, out int c,
                                           int bendDr = 0)
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Actors[0].Entered = true;
        var home = new Port(state.Ports.Count, state.Actors[0].Id,
                            state.Actors[0].Seat, tier: 3, state.WorldYear);
        state.Ports.Add(home);
        state.Markets.Add(new Market(home.Id, state.Config.Economy));
        a = home.Id;
        b = AddPort(state, home, dq: 6, dr: bendDr);
        c = AddPort(state, home, dq: 12, dr: 0);
        return state;
    }

    private static int AddPort(SimState state, Port home, int dq, int dr)
    {
        var hex = new HexCoordinate(home.Hex.Q + dq, home.Hex.R + dr);
        var port = new Port(state.Ports.Count, home.OwnerActorId, hex,
                            tier: 3, state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, state.Config.Economy));
        return port.Id;
    }
}
