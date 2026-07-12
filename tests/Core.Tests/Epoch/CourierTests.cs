using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CE C9 (contract-economy spec §1): the courier contract —
/// move MY goods from A to B for a posted fee. Cargo escrows from the
/// poster's origin larder at post, the fee escrows from the poster's
/// ledger; acceptance dispatches a shipment over the route; delivery banks
/// the cargo in the poster's destination larder and pays the fee to the
/// fulfiller; expiry returns cargo and fee to the poster.</summary>
public class CourierTests
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
        var pb = new Port(1, a0.Id,
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
    public void Post_EscrowsCargoAndFee()
    {
        var (state, pa, _) = Fixture();
        pa.DepositStock(G, 40, 0.7);
        var pr = state.PolityOf(0);
        pr.Credits = 100;

        var c = CourierOps.Post(state, 0, 0, 1, new[] { (G, 25.0) }, 10,
            CourierPriority.Normal);

        Assert.NotNull(c);
        Assert.Equal(15.0, pa.StockQty[G], 6);       // cargo left the larder
        Assert.Equal(25.0, c!.Qty[G], 6);
        Assert.Equal(90.0, pr.Credits, 6);           // the fee is escrowed
        Assert.Equal(CourierStatus.Open, c.Status);
        Assert.Single(state.Couriers);
        Assert.Equal(1, state.NextCourierId);
    }

    [Fact]
    public void Accept_Dispatches_AndDeliveryPaysTheFee()
    {
        var (state, pa, pb) = Fixture();
        pa.DepositStock(G, 40, 0.7);
        state.PolityOf(0).Credits = 100;
        var c = CourierOps.Post(state, 0, 0, 1, new[] { (G, 25.0) }, 10,
            CourierPriority.Normal)!;
        double fulfillerBefore = state.LedgerOf(1).Credits;

        bool ok = CourierOps.Accept(state, c, fulfillerActorId: 1);

        Assert.True(ok);
        Assert.Equal(CourierStatus.InTransit, c.Status);
        var s = Assert.Single(state.Shipments);
        Assert.Equal(ShipmentChannel.Requisition, s.Channel);

        // four one-year steps sail it home
        for (int i = 0; i < 5; i++)
            ShipmentOps.Advance(state, new MarketStepScratch(state));
        Assert.Empty(state.Shipments);
        Assert.Equal(25.0, pb.StockQty[G], 6);       // the poster's larder
        Assert.Equal(fulfillerBefore + 10.0,
            state.LedgerOf(1).Credits, 6);           // the fee, at delivery
        Assert.Equal(CourierStatus.Delivered, c.Status);
        Assert.DoesNotContain(c, state.Couriers);    // retired from registry
    }

    [Fact]
    public void Expire_ReturnsCargoAndFee()
    {
        var (state, pa, _) = Fixture();
        pa.DepositStock(G, 40, 0.7);
        var pr = state.PolityOf(0);
        pr.Credits = 100;
        var c = CourierOps.Post(state, 0, 0, 1, new[] { (G, 25.0) }, 10,
            CourierPriority.Normal)!;
        state.WorldYear = c.ExpiryYear + 1;

        CourierOps.ExpireOpen(state);

        Assert.Equal(40.0, pa.StockQty[G], 6);       // cargo came home
        Assert.Equal(100.0, pr.Credits, 6);          // fee refunded
        Assert.Empty(state.Couriers);
    }

    [Fact]
    public void Couriers_RoundTrip_ByteIdentically()
    {
        var (state, pa, _) = Fixture();
        pa.DepositStock(G, 40, 0.7);
        state.PolityOf(0).Credits = 100;
        CourierOps.Post(state, 0, 0, 1, new[] { (G, 25.0) }, 10,
            CourierPriority.War);

        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new StringReader(text));

        var c = Assert.Single(loaded.Couriers);
        Assert.Equal(CourierPriority.War, c.Priority);
        Assert.Equal(25.0, c.Qty[G], 6);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }

    /// <summary>AcceptOpen picks the deepest first-leg carrier; with no
    /// hulls on the lane the contract stays open — no capacity, no haul.</summary>
    [Fact]
    public void AcceptOpen_PicksTheDeepestCarrier_OrWaits()
    {
        var (state, pa, _) = Fixture();
        pa.DepositStock(G, 40, 0.7);
        state.PolityOf(0).Credits = 100;
        var c = CourierOps.Post(state, 0, 0, 1, new[] { (G, 25.0) }, 10,
            CourierPriority.Normal)!;

        Assert.Equal(0, CourierOps.AcceptOpen(state));   // no hulls yet
        Assert.Equal(CourierStatus.Open, c.Status);

        EpochTestKit.PostFreight(state, 1, laneId: 0, hulls: 4);
        Assert.Equal(1, CourierOps.AcceptOpen(state));
        Assert.Equal(1, c.FulfillerActorId);
        Assert.Equal(CourierStatus.InTransit, c.Status);
    }
}
