using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 Task 3: cross-currency behaviour on the order-book path
/// (design "Conversion mechanics" §1). A fill deducts tax and wages in the
/// port's LOCAL currency first (the sovereign and local segments are local by
/// construction) and converts only the seller's NET remainder into the seller's
/// own currency; a cancelled buy converts its local-currency escrow back into the
/// buyer's currency at the current rate; freight converts a foreign trader's
/// payment out of its own ledger before paying the source book. Every conversion
/// nets to zero in numeraire terms and is booked on the paired
/// <see cref="Currency.CumulativeConvertedIn"/>/<c>Out</c> counters.</summary>
public class OrderBookCurrencyTests
{
    private const int G = (int)GoodId.Alloys;

    private static Currency AddCurrency(SimState state, int id, double rate)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id)
        { NumeraireRate = rate };
        state.Currencies.Add(cur);
        state.Banks.Add(new Bank(id));
        return cur;
    }

    /// <summary>Port 0 owned by polity 0 (currency 0, tax 0.1) with a resident
    /// segment for payroll; polity 1 (currency 1) is the foreign seller. cur1 is
    /// worth twice cur0 (rate 2.0 vs 1.0), so a cur0 amount converts into HALF as
    /// much cur1.</summary>
    private static SimState CrossCurrencyFixture()
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
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);
        state.PolityOf(0).CurrencyId = 0;
        state.PolityOf(1).CurrencyId = 1;
        state.WorldYear = 100;
        return state;
    }

    // ---- Fill/SettleSale: local tax+wages first, seller net converts ----

    [Fact]
    public void CrossCurrencyFill_TaxAndWagesStayLocal_SellerNetConverts()
    {
        var state = CrossCurrencyFixture();
        double laborShare = state.Config.Economy.LaborShare;
        var sovereign = state.PolityOf(0);     // currency 0, local
        var seller = state.PolityOf(1);        // currency 1, foreign
        double sovBefore = sovereign.Credits;
        double sellerBefore = seller.Credits;
        double segBefore = state.Segments[0].Wealth;

        // maker is the resting sell: prints at 3.0, not the 4.0 bid
        OrderOps.PostSell(state, 1, 0, G, qty: 25.0, grade: 0.7,
            ask: 3.0, expiryYear: 150);
        OrderOps.PostBuy(state, 0, 0, G, qty: 10.0, bid: 4.0, expiryYear: 150);

        OrderOps.MatchPort(state, portId: 0);

        // paid 30 in LOCAL currency 0: tax 3 (local), wages of the 27 net (local),
        // seller's net remainder converts cur0 -> cur1 at rate 1/2
        double tax = 30.0 * 0.1;
        double wages = (30.0 - tax) * laborShare;
        double netLocal = 30.0 - tax - wages;
        double netSeller = netLocal * 1.0 / 2.0;   // cur0 -> cur1, gross of the skim
        // SettleSale credits the seller via CreditLocal -> Deposit (slice
        // CU-2), which skims the spread off the top into Bank(cur1).Reserve
        // before crediting the NET — the paired counter below still books
        // the full gross conversion.
        double netSellerCredited =
            netSeller * (1 - state.Config.Economy.ConversionSpread);

        Assert.Equal(sovBefore + tax, sovereign.Credits, 9);       // local, no FX
        Assert.Equal(segBefore + wages, state.Segments[0].Wealth, 9); // local, no FX
        Assert.Equal(sellerBefore + netSellerCredited, seller.Credits, 9);  // converted
        // the conversion is booked as a transfer, not a mint: cur0 out, cur1 in
        Assert.Equal(netLocal, state.CurrencyOf(0).CumulativeConvertedOut, 9);
        Assert.Equal(netSeller, state.CurrencyOf(1).CumulativeConvertedIn, 9);
        Assert.Equal(0.0, state.CurrencyOf(1).CumulativeConvertedOut, 9);
        Assert.Equal(0.0, state.CurrencyOf(0).CumulativeConvertedIn, 9);
    }

    [Fact]
    public void CrossCurrencyFill_ConservesValue_InNumeraireTerms()
    {
        var state = CrossCurrencyFixture();
        var sell = OrderOps.PostSell(state, 1, 0, G, qty: 25.0, grade: 0.7,
            ask: 3.0, expiryYear: 150);
        var buy = OrderOps.PostBuy(state, 0, 0, G, qty: 10.0, bid: 4.0,
            expiryYear: 150);

        double before = NumeraireHeld(state, buy, sell);
        OrderOps.MatchPort(state, portId: 0);
        double after = NumeraireHeld(state, buy, sell);

        // nothing is created or destroyed — the escrow moved to the sovereign,
        // the segments, and the seller (converted), all conserving in numeraire
        Assert.Equal(before, after, 9);
        // and the paired counters net to zero in numeraire (a transfer, not a mint)
        double outN = state.CurrencyOf(0).CumulativeConvertedOut
                      * state.CurrencyOf(0).NumeraireRate;
        double inN = state.CurrencyOf(1).CumulativeConvertedIn
                     * state.CurrencyOf(1).NumeraireRate;
        Assert.Equal(outN, inN, 9);
    }

    /// <summary>Numeraire value of every place the fill's money can live: the
    /// buy escrow (currency 0), the sovereign and its segments (currency 0), and
    /// the foreign seller (currency 1).</summary>
    private static double NumeraireHeld(SimState state, MarketOrder buy,
                                        MarketOrder sell)
    {
        double r0 = state.CurrencyOf(0).NumeraireRate;
        double r1 = state.CurrencyOf(1).NumeraireRate;
        // the seller's net-conversion spread is sequestered OUT of circulation
        // into Bank.Reserve (MetricsOps.cs authoritative residual balances
        // Supply + Reserve) — omitting it here reads as a false leak exactly
        // equal to the skim, in numeraire terms
        return buy.EscrowCredits * r0
             + state.PolityOf(0).Credits * r0
             + state.Segments[0].Wealth * r0
             + state.PolityOf(1).Credits * r1
             + state.BankOf(0).Reserve * r0
             + state.BankOf(1).Reserve * r1;
    }

    // ---- CancelBuy/ExpireOrders: refund back into the buyer's currency ----

    [Fact]
    public void CancelledBuy_RefundsIntoBuyerCurrency_AtCurrentRate()
    {
        var state = CrossCurrencyFixture();
        var buyer = state.PolityOf(1);         // currency 1, foreign to port 0
        double buyerBefore = buyer.Credits;

        // a foreign buyer's bid: escrow rides the book in LOCAL currency 0
        var buy = OrderOps.PostBuy(state, 1, 0, G, qty: 10.0, bid: 4.0,
            expiryYear: 99);                    // already lapsed at year 100
        double escrowLocal = buy.EscrowCredits; // 40, currency 0

        int expired = OrderOps.ExpireOrders(state);

        Assert.Equal(1, expired);
        // refund converts cur0 -> cur1 at the current rate (1/2): 40 -> 20 gross.
        // CreditLocal routes through Deposit (slice CU-2), which skims the
        // spread off the top into Bank(cur1).Reserve before crediting the
        // NET — the paired counter still books the full gross conversion.
        double refundSeller = escrowLocal * 1.0 / 2.0;
        double refundNet = refundSeller * (1 - state.Config.Economy.ConversionSpread);
        Assert.Equal(buyerBefore + refundNet, buyer.Credits, 9);
        Assert.Equal(escrowLocal, state.CurrencyOf(0).CumulativeConvertedOut, 9);
        Assert.Equal(refundSeller, state.CurrencyOf(1).CumulativeConvertedIn, 9);
    }

    // ---- LiftAsks: a foreign buyer converts its payment before paying ----

    [Fact]
    public void LiftAsks_ForeignBuyerPays_ConvertsIntoLocal_SellerNetConverts()
    {
        var state = CrossCurrencyFixture();
        double laborShare = state.Config.Economy.LaborShare;
        var seller = state.PolityOf(1);        // ask owner, currency 1
        // a LOCAL ask so the seller keeps the whole gross local (no seller FX) —
        // isolate the BUYER-side conversion. Owner 0 sells at its own port.
        OrderOps.PostSell(state, 0, 0, G, qty: 10.0, grade: 0.5, ask: 3.0,
            expiryYear: 150);
        var localSeller = state.PolityOf(0);
        double localSellerBefore = localSeller.Credits;

        // the foreign trader (polity 1) lifts 5 units; cost = 15 in LOCAL cur0
        var (drawn, _, cost) = BookOps.LiftAsks(state, 0, G, qty: 5.0,
            budget: double.MaxValue);
        double buyerBefore = state.PolityOf(1).Credits;
        state.DebitLocal(1, cost, state.LocalCurrencyOf(0));

        Assert.Equal(5.0, drawn, 9);
        Assert.Equal(15.0, cost, 9);           // 5 * 3.0, in cur0
        // the buyer paid 15 cur0 by converting from cur1: 15 cur0 = 7.5 cur1.
        // DebitLocal routes through Withdraw (slice CU-2), which grosses the
        // PAYER up by the conversion spread on top of the requested amount
        // (the skim lands in Bank(cur0).Reserve, the payee stays whole) — so
        // both the payer's actual cost and the booked cur0-in counter are the
        // GROSSED amount, not the bare `cost`.
        double spread = state.Config.Economy.ConversionSpread;
        double grossTo = cost * (1 + spread);  // cur0, gross of the skim
        double ownCost = grossTo * 1.0 / 2.0;  // cur0 (grossed) -> cur1
        Assert.Equal(buyerBefore - ownCost, state.PolityOf(1).Credits, 9);
        Assert.Equal(ownCost, state.CurrencyOf(1).CumulativeConvertedOut, 9);
        Assert.Equal(grossTo, state.CurrencyOf(0).CumulativeConvertedIn, 9);
        // the local seller kept its gross-minus-local-split, all in cur0
        double tax = 15.0 * 0.1;
        double wages = (15.0 - tax) * laborShare;
        // seller 0 IS the sovereign here, so it books both its net AND the tax
        Assert.Equal(localSellerBefore + (15.0 - wages), localSeller.Credits, 9);
    }

    // ---- MoveFreight: a foreign hauler converts its lane spend ----

    [Fact]
    public void MoveFreight_ForeignTrader_ConvertsPayment_BeforeItLeavesLedger()
    {
        // Two ports, two owners; the marine posted by polity 0 hauls goods from
        // port 1 (owned by polity 1) back to its own port 0 — so it BUYS at a
        // foreign market and must convert its own currency to pay.
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        var a1 = state.Actors[1];
        a0.Entered = true;
        a1.Entered = true;
        foreach (var sp in state.Skeleton.Species)
            sp.Embodiment = Embodiment.TerranAnalog;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var hexB = new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R);
        var pb = new Port(1, a1.Id, hexB, tier: 2, foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        int s0 = state.PolityOf(0).SpeciesId;
        int s1 = state.PolityOf(1).SpeciesId;
        state.Segments.Add(new PopulationSegment(0, 0, s0, s0, 3.0) { Wealth = 500 });
        state.Segments.Add(new PopulationSegment(1, 1, s1, s1, 3.0) { Wealth = 500 });
        AddCurrency(state, 0, 1.0);            // trader (polity 0) currency
        AddCurrency(state, 1, 2.0);            // source-port currency (foreign)
        state.PolityOf(0).CurrencyId = 0;
        state.PolityOf(1).CurrencyId = 1;
        state.PolityOf(0).Credits = 5000;      // the marine's working capital
        state.WorldYear = 100;
        EpochTestKit.PostFreight(state, a0.Id, laneId: 0, hulls: 6);

        int g = (int)GoodId.Provisions;
        // stock the FOREIGN port (1) and pull toward home (0): goods flow 1 -> 0
        EpochTestKit.Stock(state, 1, g, 1000, 0.6);
        var owner0 = state.PolityOf(0);
        double bid = state.Markets[1].Price[g] * 4;
        owner0.Credits -= 500 * bid;           // escrow the resting bid at port 0
        OrderOps.PostBuy(state, 0, 0, g, 500, bid, state.WorldYear + 1000);
        double traderBefore = owner0.Credits;

        MarketEngine.MoveFreight(state, new MarketStepScratch(state));

        // goods crossed to the home port's book
        Assert.True(BookOps.AskQty(state, 0, g) > 0,
            "the hauler should move goods from the foreign source");
        // the trader paid out of its own cur0 ledger, converting from the cur1
        // it owed the source book — booked as cur0-out / cur1-in
        Assert.True(owner0.Credits < traderBefore, "the trader fronted the run");
        double spentOwn = traderBefore - owner0.Credits;
        Assert.True(state.CurrencyOf(0).CumulativeConvertedOut > 0,
            "the trader converted its own currency out");
        Assert.True(state.CurrencyOf(1).CumulativeConvertedIn > 0,
            "the source-market currency received the converted payment");
        // every credit the trader spent was a cross-currency conversion out of cur0
        Assert.Equal(spentOwn, state.CurrencyOf(0).CumulativeConvertedOut, 6);
        // and the pair reconciles through the rate: cur0-out = cur1-in * r1/r0
        Assert.Equal(state.CurrencyOf(0).CumulativeConvertedOut,
            state.CurrencyOf(1).CumulativeConvertedIn * 2.0 / 1.0, 6);
    }
}
