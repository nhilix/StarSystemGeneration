using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The requisition channel as POSTED COURIER CONTRACTS (contract
/// economy, spec §3): when the plan schedules work, Allocation posts
/// couriers from the polity's own located stockpiles toward the site —
/// bypassing price (the state moving its own goods), never time, route, or
/// capacity — and the hauling costs a fee paid to whoever's hulls take the
/// job (the poster's own marine self-fulfills at cost). A remote site still
/// starves at the pace of its last delivery.</summary>
public class RequisitionTests
{
    /// <summary>One polity, a stocked home port and a bare frontier port
    /// 10 hexes down a live tier-2 lane; slow freight so transit is 5y
    /// against a 1y step; the polity's own freighters posted on the lane.</summary>
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
        EpochTestKit.PostFreight(state, actor.Id, laneId: 0, hulls: 4);
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
    public void Requisitions_PostCouriers_TowardTheStarvingSite()
    {
        var (state, home, frontier) = Fixture();
        home.DepositStock((int)GoodId.Alloys, 100, 0.6);
        var p = RemoteProject(state, frontier);
        var pr = state.PolityOf(home.OwnerActorId);

        int raised = ShipmentOps.RaiseRequisitions(state, pr);

        Assert.True(raised > 0, "the shortfall should post a courier");
        var c = Assert.Single(state.Couriers);
        Assert.Equal(home.Id, c.OriginPortId);
        Assert.Equal(frontier.Id, c.DestPortId);
        Assert.True(c.Qty[(int)GoodId.Alloys] > 0);
        Assert.True(home.StockQty[(int)GoodId.Alloys] < 100,
            "the cargo escrows out of the source larder at post");
        // no PRICE was paid — the state ships its own goods — but the
        // hauling fee is escrowed on the contract (conserved)
        Assert.Equal(1000.0, pr.Credits + c.FeeEscrow, 6);
        Assert.True(c.FeeEscrow > 0, "state hauling costs freight fees now");

        // the job board: the polity's own marine takes it at cost
        Assert.Equal(1, CourierOps.AcceptOpen(state));
        Assert.Equal(pr.ActorId, c.FulfillerActorId);
        var s = Assert.Single(state.Shipments);
        Assert.Equal(ShipmentChannel.Requisition, s.Channel);

        // a second raise does not double-order what is already sailing
        Assert.Equal(0, ShipmentOps.RaiseRequisitions(state, pr));
        _ = p;
    }

    /// <summary>Review fix 3: the quartermaster never ships into a larder
    /// the funder cannot draw from — a project site whose port fell to
    /// someone else gets the market channel only, not gifted stock.</summary>
    [Fact]
    public void Requisitions_SkipSites_TheFunderDoesNotOwn()
    {
        var (state, home, frontier) = Fixture();
        home.DepositStock((int)GoodId.Alloys, 100, 0.6);
        var p = RemoteProject(state, frontier);
        state.Actors[1].Entered = true;
        frontier.OwnerActorId = state.Actors[1].Id;   // captured mid-build

        int raised = ShipmentOps.RaiseRequisitions(state,
            state.PolityOf(home.OwnerActorId));

        Assert.Equal(0, raised);
        Assert.Empty(state.Couriers);
        _ = p;
    }

    /// <summary>The phase wiring: Allocation posts the standing orders and
    /// clears the job board mechanically, every step.</summary>
    [Fact]
    public void AllocationPhase_RaisesAndAcceptsTheRequisitions()
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
        CourierOps.AcceptOpen(state);
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
        CourierOps.AcceptOpen(state);
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
