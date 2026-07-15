using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The freight line's founding act (lane-economics spec §4):
/// corp-owned gate pairs across profitable, non-hostile borders — no
/// treaty required.</summary>
public class CorpGateLaneTests
{
    [Fact]
    public void FreightCorp_BridgesANonHostileBorder()
    {
        var (state, corp, _, _) = CrossBorderOpportunity();
        int lanesBefore = state.Lanes.Count;

        CorporationOps.Operate(state);

        // groundbreaking: the lane row and both gates exist now, but the
        // gates are uncommissioned — the pair runs in world-time (Task 9)
        Assert.Equal(lanesBefore + 1, state.Lanes.Count);
        var lane = state.Lanes[state.Lanes.Count - 1];
        Assert.Equal(corp.ActorId, state.Facilities[lane.GateAId].OwnerActorId);
        Assert.Equal(corp.ActorId, state.Facilities[lane.GateBId].OwnerActorId);
        Assert.False(LaneMath.IsLive(state, lane));   // half-built: no lane yet

        // the pair feeds from its laydown yard (a corp on host ports owns
        // no larder) and streams wages from corp credits over the build
        // years — fill the yard with the tier-scaled pair basket
        var pairProject = state.Projects[state.Projects.Count - 1];
        foreach (var q in Infrastructure.Get(InfraTypeId.Gate).BuildCost)
        {
            pairProject.DeliveredQty[(int)q.Good] += q.Quantity * 40;
            pairProject.DeliveredGrade[(int)q.Good] = 0.6;
        }
        ProjectOps.AdvanceAll(state);
        Assert.True(LaneMath.IsLive(state, lane), "the pair opens on commission");
    }

    [Fact]
    public void FreightCorp_WillNotBridgeAHostileBorder()
    {
        var (state, _, homePort, foreignPort) = CrossBorderOpportunity();
        var rel = state.RelationOf(
            state.Ports[homePort].OwnerActorId,
            state.Ports[foreignPort].OwnerActorId);
        rel!.Tension = 1.0;                       // over the ceiling
        int lanesBefore = state.Lanes.Count;

        CorporationOps.Operate(state);

        Assert.Equal(lanesBefore, state.Lanes.Count);
    }

    [Fact]
    public void FreightCorp_StopsAtItsGateLaneCap()
    {
        var (state, corp, _, _) = CrossBorderOpportunity();
        state.Config.Corporate.MaxGateLanes = 0;
        int lanesBefore = state.Lanes.Count;

        CorporationOps.Operate(state);

        Assert.Equal(lanesBefore, state.Lanes.Count);
    }

    /// <summary>Two entered polities, one port each 10 hexes apart, a fat
    /// provisions gradient toward the foreign port, both markets stocked
    /// with the gate basket, a funded freight corp chartered at the first —
    /// and a standing low-tension relation between the hosts.</summary>
    private static (SimState, Corporation, int, int) CrossBorderOpportunity()
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
        var rel = new PolityRelation(a0.Id, a1.Id, state.WorldYear);
        rel.Warmth = 0.5;
        rel.Tension = 0.1;
        state.Relations.Add(rel);

        // the price gap that makes the pair carry profit
        var mA = state.Markets[0];
        var mB = state.Markets[1];
        EpochTestKit.Stock(state, 0, (int)GoodId.Provisions, 200, 0.6);
        mB.Price[(int)GoodId.Provisions] = mA.Price[(int)GoodId.Provisions] * 5;
        // both ends can physically supply a tier-1 gate basket
        foreach (var q in Infrastructure.Get(InfraTypeId.Gate).BuildCost)
        {
            EpochTestKit.Stock(state, 0, (int)q.Good, q.Quantity * 5, 0.6);
            EpochTestKit.Stock(state, 1, (int)q.Good, q.Quantity * 5, 0.6);
        }

        int actorId = state.Actors.Count;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation,
            "Test Line", pa.Hex, state.EpochIndex, new CorporateController(state.Config))
        { Entered = true });
        var corp = new Corporation(state.Corporations.Count, actorId,
            "Test Line", a0.Id, CorporateNiche.Freight, pa.Id,
            state.WorldYear);
        state.Corporations.Add(corp);
        corp.Deposit(state, 10_000, 0);   // wallet is the corp's whole balance now
        return (state, corp, pa.Id, pb.Id);
    }
}
