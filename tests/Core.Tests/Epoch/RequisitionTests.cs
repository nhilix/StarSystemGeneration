using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Stage 2 (spec §4b, the requisition channel): when the plan
/// schedules work, Allocation raises shipping orders from the polity's own
/// located stockpiles toward the site — bypassing price (the state moving
/// its own goods), never bypassing time, route, or capacity. A remote site
/// starves at the pace of its last delivery.</summary>
public class RequisitionTests
{
    /// <summary>One polity, a stocked home port and a bare frontier port
    /// 10 hexes down a live tier-2 lane; slow freight so transit is 5y
    /// against a 1y step.</summary>
    private static (SimState State, Port Home, Port Frontier) Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var home = new Port(0, actor.Id, actor.Seat, tier: 2, foundedYear: 0);
        var frontier = new Port(1, actor.Id,
            new HexCoordinate(actor.Seat.Q + 10, actor.Seat.R), tier: 1,
            foundedYear: 0);
        state.Ports.Add(home);
        state.Ports.Add(frontier);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        int species = state.PolityOf(actor.Id).SpeciesId;
        state.Segments.Add(new PopulationSegment(0, 0, species, species, 3.0));
        state.PolityOf(actor.Id).Credits = 1000;
        state.WorldYear = 100;
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;   // 5y transit
        return (state, home, frontier);
    }

    private static Project RemoteProject(SimState state, Port frontier)
    {
        var pr = state.PolityOf(frontier.OwnerActorId);
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, frontier.Id, frontier.Hex, yearsRequired: 10.0,
            ProjectPriority.Core, planOrder: 0);
        p.TargetId = frontier.Id;
        p.PerYearBasket[(int)GoodId.Alloys] = 1.0;
        p.WagesPerYear = 0.0;
        return p;
    }

    [Fact]
    public void Requisitions_ShipStockTowardTheStarvingSite()
    {
        var (state, home, frontier) = Fixture();
        home.DepositStock((int)GoodId.Alloys, 100, 0.6);
        var p = RemoteProject(state, frontier);

        int raised = ShipmentOps.RaiseRequisitions(state,
            state.PolityOf(home.OwnerActorId));

        Assert.True(raised > 0, "the shortfall should raise a shipping order");
        var s = Assert.Single(state.Shipments);
        Assert.Equal(ShipmentChannel.Requisition, s.Channel);
        Assert.Equal(home.Id, s.OriginPortId);
        Assert.Equal(frontier.Id, s.DestPortId);
        Assert.True(s.Qty[(int)GoodId.Alloys] > 0);
        Assert.True(home.StockQty[(int)GoodId.Alloys] < 100,
            "the order draws the source larder at departure");
        // no price was paid: the state moved its own goods
        Assert.Equal(1000.0, state.PolityOf(home.OwnerActorId).Credits, 6);
        // and a second raise does not double-order what is already sailing
        Assert.Equal(0, ShipmentOps.RaiseRequisitions(state,
            state.PolityOf(home.OwnerActorId)));
        _ = p;
    }

    /// <summary>The phase wiring: Allocation raises the standing orders
    /// mechanically, every step, before it advances the works.</summary>
    [Fact]
    public void AllocationPhase_RaisesTheRequisitions()
    {
        var (state, home, frontier) = Fixture();
        home.DepositStock((int)GoodId.Alloys, 100, 0.6);
        RemoteProject(state, frontier);

        new AllocationPhase().Run(state);

        Assert.Contains(state.Shipments,
            s => s.Channel == ShipmentChannel.Requisition
                 && s.DestPortId == frontier.Id);
    }

    /// <summary>THE stage-2 taste test in miniature: the remote site lives
    /// delivery to delivery; severing the lane starves it at the pace of
    /// the last one.</summary>
    [Fact]
    public void RemoteSite_StarvesAtThePaceOfItsLastDelivery()
    {
        var (state, home, frontier) = Fixture();
        home.DepositStock((int)GoodId.Alloys, 100, 0.6);
        var p = RemoteProject(state, frontier);
        var pr = state.PolityOf(home.OwnerActorId);

        // the order sails; until anything lands, the site starves
        ShipmentOps.RaiseRequisitions(state, pr);
        for (int i = 0; i < 4; i++)
        {
            ProjectOps.AdvanceAll(state);
            Assert.Equal(0.0, p.LastFedFraction, 6);
            ShipmentOps.Advance(state, new MarketStepScratch(state));
        }
        // delivery landed in the frontier larder: the site eats this step
        Assert.True(frontier.StockQty[(int)GoodId.Alloys] > 0);
        ProjectOps.AdvanceAll(state);
        Assert.Equal(1.0, p.LastFedFraction, 6);

        // now sever the supply line mid-flight of the next order: the site
        // lives off what its larder still holds, then starves
        ShipmentOps.RaiseRequisitions(state, pr);
        state.Lanes[0].QuarantinedUntil = 100000;
        for (int i = 0; i < 8; i++)
        {
            ShipmentOps.Advance(state, new MarketStepScratch(state));
            ProjectOps.AdvanceAll(state);
        }
        Assert.Equal(0.0, p.LastFedFraction, 6);
        Assert.True(p.YearsDelivered < p.YearsRequired,
            "a severed lane must starve the site once the larder empties");
    }
}
