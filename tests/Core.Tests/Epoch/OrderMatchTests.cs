using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CE C2 (contract-economy spec §2 step 3): per-(port, good)
/// matching crosses the book while best bid ≥ best ask at MAKER price with
/// (price, order id) priority; each fill skims transaction tax to the port's
/// sovereign and pays the labor share of the seller's net to the local
/// segments; the goods return as fills for the caller to route. Pure
/// ordered math — no rolls.</summary>
public class OrderMatchTests
{
    private const int G = (int)GoodId.Alloys;

    /// <summary>Port 0 owned by polity 0 (tax rate 0.1), one segment for
    /// the payroll, polity 1 entered as the seller.</summary>
    private static SimState Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        var a1 = state.Actors[1];
        a0.Entered = true;
        a1.Entered = true;
        a0.Policies = PolityPolicies.Default with { TaxRate = 0.1 };
        state.Ports.Add(new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0));
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Segments.Add(new PopulationSegment(0, 0,
            state.PolityOf(0).SpeciesId, state.PolityOf(0).SpeciesId, 1.0));
        state.WorldYear = 100;
        return state;
    }

    [Fact]
    public void MatchPort_Crosses_AtMakerPrice_TaxAndWagesConserved()
    {
        var state = Fixture();
        double laborShare = state.Config.Economy.LaborShare;
        var seller = state.LedgerOf(1);
        var sovereign = state.PolityOf(0);
        double sellerBefore = seller.Credits;
        double sovereignBefore = sovereign.Credits;
        double segBefore = state.Segments[0].Wealth;

        // the resting sell is the maker: the trade prints at 3.0, not 4.0
        OrderOps.PostSell(state, 1, 0, G, qty: 25.0, grade: 0.7,
            ask: 3.0, expiryYear: 150);
        var buy = OrderOps.PostBuy(state, 0, 0, G, qty: 10.0, bid: 4.0,
            expiryYear: 150);

        var fills = OrderOps.MatchPort(state, portId: 0);

        Assert.Single(fills);
        Assert.Same(buy, fills[0].Buy);
        Assert.Equal(10.0, fills[0].Qty, 6);
        Assert.Equal(0.7, fills[0].Grade, 6);
        // paid 30 at maker price: tax 3.0 to the sovereign, labor share of
        // the 27 net to the segment, the rest to the seller
        double tax = 30.0 * 0.1;
        double wages = (30.0 - tax) * laborShare;
        Assert.Equal(sovereignBefore + tax, sovereign.Credits, 6);
        Assert.Equal(segBefore + wages, state.Segments[0].Wealth, 6);
        Assert.Equal(sellerBefore + 30.0 - tax - wages, seller.Credits, 6);
        // the buy is spent whole but its bid-limit surplus stays escrowed
        Assert.Equal(10.0, buy.EscrowCredits, 6);
    }

    [Fact]
    public void MatchPort_PriceTimePriority_CheapestAsk_ThenEarlierId()
    {
        var state = Fixture();
        var cheapLate = OrderOps.PostSell(state, 1, 0, G, 5.0, 0.5,
            ask: 2.0, expiryYear: 150);
        var dearEarly = OrderOps.PostSell(state, 1, 0, G, 5.0, 0.5,
            ask: 3.0, expiryYear: 150);
        var cheapTwin = OrderOps.PostSell(state, 1, 0, G, 5.0, 0.5,
            ask: 2.0, expiryYear: 150);
        OrderOps.PostBuy(state, 0, 0, G, qty: 12.0, bid: 3.5,
            expiryYear: 150);

        var fills = OrderOps.MatchPort(state, portId: 0);

        // cheapest ask first; between the 2.0 twins the earlier id rests
        // first; the dear order fills last and only partially
        Assert.Equal(3, fills.Count);
        Assert.Equal(0.0, cheapLate.QtyRemaining, 6);
        Assert.Equal(0.0, cheapTwin.QtyRemaining, 6);
        Assert.Equal(3.0, dearEarly.QtyRemaining, 6);
    }

    [Fact]
    public void MatchPort_NoCross_NoTrade()
    {
        var state = Fixture();
        OrderOps.PostSell(state, 1, 0, G, 5.0, 0.5, ask: 4.0,
            expiryYear: 150);
        OrderOps.PostBuy(state, 0, 0, G, qty: 5.0, bid: 3.0,
            expiryYear: 150);

        var fills = OrderOps.MatchPort(state, portId: 0);

        Assert.Empty(fills);
        Assert.Equal(2, state.Orders.Count);
    }
}
