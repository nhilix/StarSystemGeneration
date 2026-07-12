using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CE C1 (contract-economy spec §1): the MarketOrder record
/// with PHYSICAL escrow — a sell order holds the goods (qty + grade, like a
/// Shipment holds cargo), a buy order holds the credits; a fill moves
/// credits→seller and goods→caller atomically at maker price; cancel
/// releases the remainder to the caller. The escrow is already drawn — the
/// caller owns conservation up to the post (the ShipmentOps convention).</summary>
public class MarketOrderTests
{
    private static (SimState State, Port Port) Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.WorldYear = 100;
        return (state, port);
    }

    [Fact]
    public void PostSell_HoldsTheGoods()
    {
        var (state, port) = Fixture();
        int g = (int)GoodId.Alloys;

        var order = OrderOps.PostSell(state, ownerActorId: 0, portId: port.Id,
            good: g, qty: 25.0, grade: 0.7, ask: 3.0, expiryYear: 150);

        Assert.Equal(OrderSide.Sell, order.Side);
        Assert.Equal(0, order.Id);
        Assert.Equal(1, state.NextOrderId);
        Assert.Single(state.Orders);
        Assert.Equal(25.0, order.QtyRemaining, 6);
        Assert.Equal(0.7, order.Grade, 6);
        Assert.Equal(3.0, order.LimitPrice, 6);
        Assert.Equal(100, order.PostedYear);
        Assert.Equal(150, order.ExpiryYear);
        Assert.Equal(0.0, order.EscrowCredits, 6);   // sells hold goods only
    }

    [Fact]
    public void PostBuy_HoldsTheCredits()
    {
        var (state, port) = Fixture();
        int g = (int)GoodId.Alloys;

        var order = OrderOps.PostBuy(state, ownerActorId: 0, portId: port.Id,
            good: g, qty: 10.0, bid: 4.0, expiryYear: 150);

        Assert.Equal(OrderSide.Buy, order.Side);
        Assert.Equal(10.0, order.QtyRemaining, 6);
        Assert.Equal(4.0, order.LimitPrice, 6);
        Assert.Equal(40.0, order.EscrowCredits, 6);  // qty × bid, held whole
    }

    [Fact]
    public void Fill_TradesAtMakerPrice_CreditsToSeller_GoodsToCaller()
    {
        var (state, port) = Fixture();
        int g = (int)GoodId.Alloys;
        var seller = state.LedgerOf(0);
        double sellerBefore = seller.Credits;

        // the sell posted first is the maker: the trade prints at ITS ask
        var sell = OrderOps.PostSell(state, 0, port.Id, g,
            qty: 25.0, grade: 0.7, ask: 3.0, expiryYear: 150);
        var buy = OrderOps.PostBuy(state, 0, port.Id, g,
            qty: 10.0, bid: 4.0, expiryYear: 150);

        var (qty, grade, paid) = OrderOps.Fill(state, buy, sell);

        Assert.Equal(10.0, qty, 6);                  // min of the remainders
        Assert.Equal(0.7, grade, 6);
        Assert.Equal(30.0, paid, 6);
        Assert.Equal(15.0, sell.QtyRemaining, 6);
        Assert.Equal(0.0, buy.QtyRemaining, 6);
        // maker price 3.0 × 10 to the seller; the bid-limit surplus stays
        // escrowed until cancel (refunds go back where the escrow came from)
        Assert.Equal(sellerBefore + 30.0, seller.Credits, 6);
        Assert.Equal(10.0, buy.EscrowCredits, 6);
    }

    [Fact]
    public void Cancel_ReleasesTheRemainderToTheCaller()
    {
        var (state, port) = Fixture();
        int g = (int)GoodId.Alloys;
        var sell = OrderOps.PostSell(state, 0, port.Id, g,
            qty: 25.0, grade: 0.7, ask: 3.0, expiryYear: 150);
        var buy = OrderOps.PostBuy(state, 0, port.Id, g,
            qty: 10.0, bid: 4.0, expiryYear: 150);
        OrderOps.Fill(state, buy, sell);

        var (qty, grade) = OrderOps.CancelSell(state, sell);
        double credits = OrderOps.CancelBuy(state, buy);

        Assert.Equal(15.0, qty, 6);
        Assert.Equal(0.7, grade, 6);
        Assert.Equal(10.0, credits, 6);              // the bid-limit surplus
        Assert.Equal(0.0, sell.QtyRemaining, 6);
        Assert.Equal(0.0, buy.EscrowCredits, 6);
        // dead orders leave the registry; the id counter keeps identity
        Assert.Empty(state.Orders);
        Assert.Equal(2, state.NextOrderId);
    }

    [Fact]
    public void Artifact_RoundTrips_Orders_ByteIdentically()
    {
        var (state, port) = Fixture();
        int g = (int)GoodId.Alloys;
        OrderOps.PostSell(state, 0, port.Id, g,
            qty: 25.0, grade: 0.7, ask: 3.0, expiryYear: 150);
        OrderOps.PostBuy(state, 0, port.Id, g,
            qty: 10.0, bid: 4.0, expiryYear: 150);

        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new StringReader(text));

        Assert.Equal(2, loaded.Orders.Count);
        Assert.Equal(2, loaded.NextOrderId);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
