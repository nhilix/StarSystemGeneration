using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>AC2.5: the courier job board card — `econtracts` parity
/// (Repl.RenderContracts). Open + in-transit contracts, registry order
/// (P6): route (ports + owner names), cargo, fee, priority (WAR flagged),
/// poster, and fulfiller once accepted.</summary>
public class ContractsPanelTests
{
    private const int G = (int)GoodId.Alloys;

    private static (SimState State, Port A, Port B) Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        var a1 = state.Actors[1];
        a0.Entered = true;
        a1.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a1.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2,
            foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        state.WorldYear = 100;
        state.Config.Sim.YearsPerEpoch = 1;
        state.Config.Economy.FreightHexesPerYearBase = 1.0;   // 5y transit
        return (state, pa, pb);
    }

    [Fact]
    public void AnOpenContractCarriesRouteCargoFeeAndPoster()
    {
        var (state, pa, pb) = Fixture();
        pa.DepositStock(G, 40, 0.7);
        state.PolityOf(0).Credits = 100;
        CourierOps.Post(state, 0, 0, 1, new[] { (G, 25.0) }, 10,
            CourierPriority.Normal);

        var row = Assert.Single(
            ContractsPanel.Rows(new AtlasReadModel(state),
                EyeContext.God(state.WorldYear)));

        Assert.Equal(0, row.OriginPortId);
        Assert.Equal(1, row.DestPortId);
        Assert.Equal(state.Actors[0].Name, row.OriginPortOwnerName);
        Assert.Equal(state.Actors[1].Name, row.DestPortOwnerName);
        var line = Assert.Single(row.Cargo);
        Assert.Equal(GoodId.Alloys, line.Good);
        Assert.Equal(25.0, line.Qty);
        Assert.Equal(0.7, line.Grade);
        Assert.Equal(10.0, row.FeeEscrow);
        Assert.Equal(CourierPriority.Normal, row.Priority);
        Assert.Equal(CourierStatus.Open, row.Status);
        Assert.Equal(0, row.PosterActorId);
        Assert.Equal(state.Actors[0].Name, row.PosterName);
        Assert.Equal(-1, row.FulfillerActorId);
        Assert.Null(row.FulfillerName);
    }

    [Fact]
    public void WarPriorityIsCarriedDistinctlyFromNormal()
    {
        var (state, pa, _) = Fixture();
        pa.DepositStock(G, 40, 0.7);
        state.PolityOf(0).Credits = 100;
        CourierOps.Post(state, 0, 0, 1, new[] { (G, 25.0) }, 10,
            CourierPriority.War);

        var row = Assert.Single(
            ContractsPanel.Rows(new AtlasReadModel(state),
                EyeContext.God(state.WorldYear)));
        Assert.Equal(CourierPriority.War, row.Priority);
    }

    [Fact]
    public void AnAcceptedContractShowsTheFulfiller()
    {
        var (state, pa, _) = Fixture();
        pa.DepositStock(G, 40, 0.7);
        state.PolityOf(0).Credits = 100;
        var c = CourierOps.Post(state, 0, 0, 1, new[] { (G, 25.0) }, 10,
            CourierPriority.Normal)!;

        CourierOps.Accept(state, c, fulfillerActorId: 1);

        var row = Assert.Single(
            ContractsPanel.Rows(new AtlasReadModel(state),
                EyeContext.God(state.WorldYear)));
        Assert.Equal(CourierStatus.InTransit, row.Status);
        Assert.Equal(1, row.FulfillerActorId);
        Assert.Equal(state.Actors[1].Name, row.FulfillerName);
    }

    [Fact]
    public void RowsComeInRegistryOrder_AndAPosterFilterNarrowsThem()
    {
        var (state, pa, _) = Fixture();
        pa.DepositStock(G, 80, 0.7);
        state.PolityOf(0).Credits = 100;
        state.PolityOf(1).Credits = 100;
        var pb = state.Ports[1];
        pb.DepositStock(G, 40, 0.5);
        var c0 = CourierOps.Post(state, 0, 0, 1, new[] { (G, 10.0) }, 5,
            CourierPriority.Normal)!;
        var c1 = CourierOps.Post(state, 1, 1, 0, new[] { (G, 5.0) }, 5,
            CourierPriority.War)!;

        var all = ContractsPanel.Rows(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear));
        Assert.Equal(2, all.Count);
        Assert.Equal(c0.Id, all[0].Id);
        Assert.Equal(c1.Id, all[1].Id);

        var filtered = ContractsPanel.Rows(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear), posterFilter: 1);
        var row = Assert.Single(filtered);
        Assert.Equal(c1.Id, row.Id);
    }

    [Fact]
    public void NoContractsRendersAnEmptyBoard()
    {
        var (state, _, _) = Fixture();
        Assert.Empty(ContractsPanel.Rows(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear)));
    }
}
