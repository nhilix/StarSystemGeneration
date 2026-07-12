using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The four fee-table rows of lane-economics spec §4: the
/// destination-side gate's owner decides what a crossing costs.</summary>
public class LaneFeeTests
{
    private const int Good = 0;
    private const double DstPrice = 10.0;

    [Fact]
    public void ShipperOwnedGate_CrossesFree()
    {
        var (state, lane, dstPort, shipper) = CrossBorderLane();
        state.Facilities[DstGate(lane, dstPort)].OwnerActorId = shipper;
        double fee = LaneFees.CrossingFeePerUnit(state, lane, dstPort, Good,
            DstPrice, shipper, out int to);
        Assert.Equal(0.0, fee);
        Assert.Equal(-1, to);
    }

    [Fact]
    public void CorpOwnedGate_TollsEveryoneElse()
    {
        var (state, lane, dstPort, shipper) = CrossBorderLane();
        int corpActor = AddCorpActor(state);
        state.Facilities[DstGate(lane, dstPort)].OwnerActorId = corpActor;
        double fee = LaneFees.CrossingFeePerUnit(state, lane, dstPort, Good,
            DstPrice, shipper, out int to);
        Assert.Equal(state.Config.Economy.GateTollRate * DstPrice, fee, 10);
        Assert.Equal(corpActor, to);
    }

    [Fact]
    public void PolityGate_SamePolityFreight_Free()
    {
        var (state, lane, dstPort, _) = CrossBorderLane();
        int gateOwner = state.Facilities[DstGate(lane, dstPort)].OwnerActorId;
        SetTariff(state, gateOwner, Good, 0.2);   // schedule exists, unused
        double fee = LaneFees.CrossingFeePerUnit(state, lane, dstPort, Good,
            DstPrice, gateOwner, out int to);
        Assert.Equal(0.0, fee);
        Assert.Equal(-1, to);
    }

    [Fact]
    public void PolityGate_ForeignFreight_PaysCustomsToTheGateOwner()
    {
        var (state, lane, dstPort, shipper) = CrossBorderLane();
        int gateOwner = state.Facilities[DstGate(lane, dstPort)].OwnerActorId;
        SetTariff(state, gateOwner, Good, 0.2);
        double fee = LaneFees.CrossingFeePerUnit(state, lane, dstPort, Good,
            DstPrice, shipper, out int to);
        Assert.Equal(0.2 * DstPrice
            * RelationsOps.TariffFactor(state, shipper, gateOwner), fee, 10);
        Assert.Equal(gateOwner, to);
    }

    [Fact]
    public void PolityGate_ForeignFreight_NoSchedule_CrossesFree()
    {
        var (state, lane, dstPort, shipper) = CrossBorderLane();
        double fee = LaneFees.CrossingFeePerUnit(state, lane, dstPort, Good,
            DstPrice, shipper, out int to);
        Assert.Equal(0.0, fee);
        Assert.Equal(-1, to);
    }

    private static int DstGate(Lane lane, int dstPortId) =>
        lane.PortAId == dstPortId ? lane.GateAId : lane.GateBId;

    /// <summary>Two entered polities, one port each, linked by a lane whose
    /// gates each belong to their port's polity. Returns (state, lane,
    /// dstPortId — the second polity's, shipperActorId — the first polity,
    /// foreign at dst).</summary>
    private static (SimState, Lane, int, int) CrossBorderLane()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        var a1 = state.Actors[1];
        a0.Entered = true;
        a1.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a1.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2, 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        var lane = EpochTestKit.AddLane(state, 0, 1);
        return (state, lane, pb.Id, a0.Id);
    }

    private static int AddCorpActor(SimState state)
    {
        int actorId = state.Actors.Count;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation,
            "Test Line", state.Ports[0].Hex, state.EpochIndex,
            new CorporateController(state.Config))
        { Entered = true });
        state.Corporations.Add(new Corporation(state.Corporations.Count,
            actorId, "Test Line", state.Ports[0].OwnerActorId,
            CorporateNiche.Freight, 0, state.WorldYear));
        return actorId;
    }

    private static void SetTariff(SimState state, int actorId, int good,
                                  double rate)
    {
        var policies = state.Actors[actorId].Policies as PolityPolicies
                       ?? PolityPolicies.Default;
        var schedule = new Dictionary<int, double>();
        foreach (var kv in policies.TariffSchedule) schedule[kv.Key] = kv.Value;
        schedule[good] = rate;
        state.Actors[actorId].Policies =
            policies with { TariffSchedule = schedule };
    }
}
