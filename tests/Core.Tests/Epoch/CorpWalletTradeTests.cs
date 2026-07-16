using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 4: the remaining corporation trade sites move money
/// through the currency-aware wallet. Facility input costs and dividends draw
/// the wallet down (converting from other buckets when the paid currency is
/// short); the cartel skim banks into the home market's currency; a seizure
/// sweeps the whole multi-currency wallet into the taking polity, converting
/// each bucket. Every site rides <c>CreditLocal</c>/<c>DebitLocal</c>, so it is
/// byte-identical in the dormant single-currency world and correct once genesis
/// mints currencies.</summary>
public class CorpWalletTradeTests
{
    private static Currency AddCurrency(SimState state, int id, double rate)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id)
        { NumeraireRate = rate };
        state.Currencies.Add(cur);
        state.Banks.Add(new Bank(id));
        return cur;
    }

    /// <summary>An entered polity 0 with its own currency, one owned port and
    /// market — the minimal live-currency stage for a corp to operate on.</summary>
    private static (SimState state, int hostActor, int portId) HostWithPort()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        AddCurrency(state, 0, 1.0);   // the host/port currency (numeraire 1:1)
        AddCurrency(state, 1, 2.0);   // a foreign currency, 1 unit worth 2 of C0
        state.PolityOf(a0.Id).CurrencyId = 0;
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        return (state, a0.Id, port.Id);
    }

    private static Corporation AddCorp(SimState state, int hostPolityId,
        CorporateNiche niche, int homePortId)
    {
        int actorId = state.Actors.Count;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation, "Testco",
            state.Ports[homePortId].Hex, state.EpochIndex,
            new CorporateController(state.Config)) { Entered = true });
        var corp = new Corporation(state.Corporations.Count, actorId, "Testco",
            hostPolityId, niche, homePortId, state.WorldYear);
        state.Corporations.Add(corp);
        return corp;
    }

    // ---- dividend / lobby: paid in the host currency, drawn down ----

    [Fact]
    public void Dividend_PaidInHostCurrency_DrawsDownAForeignHolding()
    {
        var (state, host, _) = HostWithPort();
        var corp = AddCorp(state, host, CorporateNiche.Extraction, homePortId: 0);
        corp.Deposit(state, 100.0, 1);   // holds ONLY C1 (numeraire 200)
        corp.Receipts = 50.0;            // there is a payout to distribute
        double creditsBefore = corp.Credits;

        CorporationOps.Operate(state);

        // the dividend + lobby were paid to the host's corporate faction in C0,
        // which the wallet did not hold — so the payment drew C1 down, converting
        var elites = state.Factions.Single(f => f.PolityId == host
            && f.Basis == FactionBasis.Corporate);
        Assert.True(elites.Wealth > 0, "host elites were never paid");
        Assert.False(corp.Holdings.ContainsKey(0),
            "the corp paid C0 out; it should never hold a C0 bucket");
        Assert.True(corp.Holdings[1] < 100.0, "the C1 holding was not drawn down");
        Assert.True(state.CurrencyOf(1).CumulativeConvertedOut > 0,
            "no C1->C0 conversion was recorded for the draw-down");
        // conservation: what the wallet lost (numeraire, C0 rate 1) is exactly
        // what the elites gained (their wealth is C0-denominated)
        Assert.Equal(creditsBefore - corp.Credits, elites.Wealth, 6);
        Assert.True(corp.Active);
    }

    // ---- cartel skim: banks into the home market's currency bucket ----

    [Fact]
    public void CartelSkim_BanksIntoTheHomeMarketCurrency()
    {
        var (state, host, _) = HostWithPort();
        var corp = AddCorp(state, hostPolityId: -1, CorporateNiche.Cartel,
                           homePortId: 0);
        corp.Deposit(state, 100.0, 1);   // a pre-existing foreign holding
        // a live black-book margin at the home market
        var market = state.Markets[0];
        market.BlackBookDemand[(int)GoodId.Narcotics] = 10.0;
        market.BlackBookPrice[(int)GoodId.Narcotics] = 5.0;   // value = 50
        // wealthy buyers at the home port for the skim to bite into
        state.Segments.Add(new PopulationSegment(0, 0, 0, 0, 3.0) { Wealth = 1000.0 });

        CorporationOps.Operate(state);

        // the skim landed in the home market's currency (C0), NOT the C1 bucket
        Assert.True(corp.Holdings.TryGetValue(0, out double c0) && c0 > 0,
            "the skim did not bank into the home-market currency");
        Assert.Equal(100.0, corp.Holdings[1], 9);   // the foreign bucket is untouched
        Assert.Equal(c0, corp.Receipts, 9);         // receipts mirror the bank
        Assert.True(state.Segments[0].Wealth < 1000.0, "buyer wealth was not skimmed");
    }

    // ---- facility input cost: paid in the port currency, drawn down ----

    [Fact]
    public void FacilityUpkeep_PaidInPortCurrency_DrawsDownAForeignHolding()
    {
        var (state, host, _) = HostWithPort();
        var corp = AddCorp(state, host, CorporateNiche.Extraction, homePortId: 0);
        corp.Deposit(state, 1000.0, 1);   // holds ONLY C1
        corp.Receipts = 0.0;              // no dividend this step — isolate upkeep
        // a corp-owned, commissioned Mine at the home port (upkeep = Machinery)
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)InfraTypeId.Mine, tier: 1, state.Ports[0].Hex,
            corp.ActorId, state.WorldYear));
        // the machinery its upkeep buys off the book
        EpochTestKit.Stock(state, 0, (int)GoodId.Machinery, 100.0, 0.5);

        CorporationOps.Operate(state);

        // upkeep cost was in the port's currency (C0), which the wallet lacked —
        // so it drew C1 down and recorded the conversion
        Assert.False(corp.Holdings.ContainsKey(0),
            "the corp paid C0 upkeep out; it should hold no C0 bucket");
        Assert.True(corp.Holdings[1] < 1000.0, "the C1 holding was not drawn down");
        Assert.True(state.CurrencyOf(1).CumulativeConvertedOut > 0
            && state.CurrencyOf(0).CumulativeConvertedIn > 0,
            "no C1->C0 conversion was recorded for the upkeep draw-down");
        Assert.True(corp.Active);
    }

    /// <summary>Task 6b: when the corp's wallet cannot cover facility upkeep, it
    /// pays exactly what it holds (no overdraft) and the upkeep sellers are
    /// credited only that capped amount — never the full requested basket. The
    /// pre-fix bug lifted the whole basket at <c>budget: double.MaxValue</c>,
    /// settling the sellers in full, then discarded the corp's capped debit —
    /// minting the shortfall. Conservation is checked in numeraire terms across
    /// every ledger the upkeep can touch.</summary>
    [Fact]
    public void FacilityUpkeep_CappedByWallet_CreditsSellersOnlyWhatWasPaid()
    {
        var (state, host, _) = HostWithPort();
        var corp = AddCorp(state, host, CorporateNiche.Extraction, homePortId: 0);
        corp.Deposit(state, 3.0, 0);      // a tiny C0 wallet the upkeep will exhaust
        corp.Receipts = 0.0;              // no dividend this step — isolate upkeep
        // a tier-1 Mine whose upkeep basket costs far more than the 3 C0 wallet
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)InfraTypeId.Mine, tier: 1, state.Ports[0].Hex,
            corp.ActorId, state.WorldYear));
        // a resident segment so the sellers' wage split has somewhere to land
        state.Segments.Add(new PopulationSegment(0, 0,
            state.PolityOf(host).SpeciesId, state.PolityOf(host).SpeciesId, 3.0)
        { Wealth = 500 });
        // plenty of dear machinery for the upkeep to (try to) buy off the book
        EpochTestKit.Stock(state, 0, (int)GoodId.Machinery, 1000.0, 0.5, ask: 5.0);

        double before = NumeraireTotal(state);
        CorporationOps.Operate(state);
        double after = NumeraireTotal(state);

        // the cap bit: the wallet is drained, not overdrafted
        Assert.True(corp.Credits <= 0.5,
            "upkeep should have drained the corp's tiny wallet");
        Assert.True(corp.Credits >= -1e-9, "the corp must never overdraft");
        Assert.True(corp.Active, "a drained-but-solvent corp is not dissolved");
        // and no money was minted settling the upkeep sellers beyond the cap
        Assert.Equal(before, after, System.Math.Max(1.0, before) * 1e-9);
    }

    /// <summary>Numeraire value of every ledger the interior step can move money
    /// between — polities (own-currency face × rate), their segments (local
    /// currency), corporations (already numeraire), and open-order escrow.</summary>
    private static double NumeraireTotal(SimState state)
    {
        double Rate(int curId) =>
            curId < 0 ? 1.0 : state.CurrencyOf(curId).NumeraireRate;
        double total = 0;
        foreach (var p in state.Polities)
            total += (p.Credits + p.DevelopmentPoints + p.MilitaryPoints
                      + p.ExpansionPoints + p.ReservePoints) * Rate(p.CurrencyId);
        foreach (var s in state.Segments)
            total += s.Wealth * Rate(state.LocalCurrencyOf(s.PortId));
        foreach (var c in state.Corporations)
            total += c.Credits;   // Credits is the numeraire-valued wallet
        foreach (var o in state.Orders)
            total += o.EscrowCredits * Rate(state.LocalCurrencyOf(o.PortId));
        return total;
    }

    // ---- seizure: the whole multi-currency wallet sweeps into the polity ----

    [Fact]
    public void Nationalize_SweepsTheWholeWallet_ConvertingEachBucket()
    {
        var (state, host, _) = HostWithPort();
        var corp = AddCorp(state, host, CorporateNiche.Freight, homePortId: 0);
        corp.Deposit(state, 100.0, 0);   // 100 C0 (numeraire 100)
        corp.Deposit(state, 5.0, 1);     //   5 C1 (numeraire 10)
        var pr = state.PolityOf(host);
        double before = pr.Credits;

        Assert.True(CorporationOps.Nationalize(state, host, corp.Id));

        // both buckets seized: the C0 bucket at par, the C1 bucket converted
        Assert.Equal(before + 110.0, pr.Credits, 6);
        Assert.Empty(corp.Holdings);
        Assert.Equal(0.0, corp.Credits, 9);
        Assert.False(corp.Active);
        Assert.Equal(5.0, state.CurrencyOf(1).CumulativeConvertedOut, 9);
        Assert.Equal(10.0, state.CurrencyOf(0).CumulativeConvertedIn, 9);
    }
}
