using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CE C3 (contract-economy spec §2): the book-step policy
/// primitives. Supply posts into the owner's ONE resting sell per (port,
/// good) — quoted at the reference price × markup, later output blends in;
/// resting quotes re-anchor to the market each step (discovery lives in
/// the reference's imbalance drift, MatchAndClear); LiftAsks buys
/// cheapest-first with the same tax + wages settlement matching uses.</summary>
public class BookStepTests
{
    private const int G = (int)GoodId.Alloys;

    private static SimState Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        var a1 = state.Actors[1];
        a0.Entered = true;
        a1.Entered = true;
        state.Ports.Add(new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0));
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Segments.Add(new PopulationSegment(0, 0,
            state.PolityOf(0).SpeciesId, state.PolityOf(0).SpeciesId, 1.0));
        state.WorldYear = 100;
        return state;
    }

    [Fact]
    public void PostSupply_QuotesAtReferenceMarkup_ThenBlends()
    {
        var state = Fixture();
        var eco = state.Config.Economy;
        double reference = state.Markets[0].Price[G];

        var order = BookOps.PostSupply(state, portId: 0, ownerActorId: 1,
            good: G, qty: 10.0, grade: 0.8);

        Assert.Equal(reference * eco.AskMarkupOnPost, order.LimitPrice, 6);
        Assert.Equal(10.0, order.QtyRemaining, 6);

        // more output from the same owner at the same port blends into the
        // SAME resting order — one quote per (owner, port, good)
        var again = BookOps.PostSupply(state, 0, 1, G, qty: 10.0, grade: 0.4);
        Assert.Same(order, again);
        Assert.Equal(20.0, order.QtyRemaining, 6);
        Assert.Equal(0.6, order.Grade, 6);
        Assert.Single(state.Orders);
    }

    [Fact]
    public void RepriceAsks_ReanchorsRestingQuotes_ToTheReference()
    {
        var state = Fixture();
        var eco = state.Config.Economy;
        var order = BookOps.PostSupply(state, 0, 1, G, 10.0, 0.5);
        // the market moved (imbalance drift): resting quotes follow it
        state.Markets[0].Price[G] *= 2.0;

        BookOps.RepriceAsks(state);

        Assert.Equal(state.Markets[0].Price[G] * eco.AskMarkupOnPost,
                     order.LimitPrice, 6);
    }

    [Fact]
    public void LiftAsks_BuysCheapestFirst_SettlingTaxAndWages()
    {
        var state = Fixture();
        state.Actors[0].Policies = PolityPolicies.Default
            with { TaxRate = 0.1 };
        double laborShare = state.Config.Economy.LaborShare;
        var seller = state.LedgerOf(1);
        double sellerBefore = seller.Credits;
        double segBefore = state.Segments[0].Wealth;

        OrderOps.PostSell(state, 1, 0, G, qty: 5.0, grade: 0.9, ask: 2.0,
            expiryYear: 150);
        OrderOps.PostSell(state, 1, 0, G, qty: 20.0, grade: 0.5, ask: 4.0,
            expiryYear: 150);

        var (drawn, grade, cost) = BookOps.LiftAsks(state, portId: 0,
            good: G, qty: 10.0, budget: double.MaxValue);

        // 5 @ 2.0 then 5 @ 4.0 = 30 paid; blended grade 0.7
        Assert.Equal(10.0, drawn, 6);
        Assert.Equal(0.7, grade, 6);
        Assert.Equal(30.0, cost, 6);
        double tax = 30.0 * 0.1;
        double wages = (30.0 - tax) * laborShare;
        Assert.Equal(sellerBefore + 30.0 - tax - wages, seller.Credits, 6);
        Assert.Equal(segBefore + wages, state.Segments[0].Wealth, 6);
        // the budget caps the lift: nothing else affordable was taken
        var (drawn2, _, cost2) = BookOps.LiftAsks(state, 0, G,
            qty: 10.0, budget: 4.0);
        Assert.Equal(1.0, drawn2, 6);
        Assert.Equal(4.0, cost2, 6);
    }

}
