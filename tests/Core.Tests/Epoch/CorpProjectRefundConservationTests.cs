using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 7b — the currency-activation Markets settlement leak.
/// A corporate-funded project escrows its per-year basket bid from the wallet
/// (<c>ProjectOps.SpendTreasury</c> → <c>DebitLocal</c>); the unfilled escrow
/// refunds at the step's end (<c>RefundTreasury</c> → <c>SpendTreasury(-amount)</c>).
/// Once genesis mints real currencies (task 6), that refund routed a NEGATIVE
/// amount through <c>Corporation.Withdraw</c>, whose no-overdraft guard
/// (<c>amount &lt;= 0 → return 0</c>) silently swallowed it — destroying the
/// escrowed credits and leaking the money supply (the −196.967 seed-42 epoch-37
/// residual). A polity funder was never hit (its pool does <c>-= -amount</c>).
/// The fix credits the corp wallet back symmetrically.</summary>
public class CorpProjectRefundConservationTests
{
    /// <summary>An entered polity 0 with its own real currency, one owned port
    /// and market — the minimal post-genesis stage (mirrors CorpWalletTradeTests).</summary>
    private static (SimState state, int hostActor, int portId) HostWithPort()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        state.Currencies.Add(new Currency(0, "C0", foundingPolityId: 0));
        state.PolityOf(a0.Id).CurrencyId = 0;   // NumeraireRate defaults to 1.0
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        return (state, a0.Id, port.Id);
    }

    private static Corporation AddCorp(SimState state, int hostPolityId, int homePortId)
    {
        int actorId = state.Actors.Count;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation, "Testco",
            state.Ports[homePortId].Hex, state.EpochIndex,
            new CorporateController(state.Config)) { Entered = true });
        var corp = new Corporation(state.Corporations.Count, actorId, "Testco",
            hostPolityId, CorporateNiche.Fabrication, homePortId, state.WorldYear);
        state.Corporations.Add(corp);
        return corp;
    }

    private static double SupplyTotal(SimState state)
    {
        double total = 0;
        foreach (var p in state.Polities)
            total += p.Credits + p.ExpansionPoints + p.DevelopmentPoints
                     + p.MilitaryPoints + p.ReservePoints;
        foreach (var c in state.Corporations) total += c.Credits;
        foreach (var s in state.Segments) total += s.Wealth;
        foreach (var o in state.Orders) total += o.EscrowCredits;
        return total;
    }

    [Fact]
    public void CorpProjectBid_FullyRefunded_ReturnsTheEscrowToTheWallet()
    {
        var (state, host, portId) = HostWithPort();
        var corp = AddCorp(state, host, homePortId: portId);
        corp.Deposit(state, 10_000.0, 0);   // a healthy C0 wallet

        // a corp-funded, in-flight civil work at the port, wanting Provisions —
        // NOT a colony expedition (which carries cargo, posts no demand)
        var project = new Project(id: 0, ProjectKind.FacilityConstruction,
            ownerActorId: corp.ActorId, funderActorId: corp.ActorId,
            portId: portId, state.Ports[portId].Hex,
            yearsRequired: 10.0, startedYear: (int)state.WorldYear);
        project.PerYearBasket[(int)GoodId.Provisions] = 10.0;
        state.Projects.Add(project);

        double supplyBefore = SupplyTotal(state);
        double walletBefore = corp.Credits;

        var scratch = new MarketStepScratch(state);
        MarketEngine.PostProjectBids(state, scratch);

        // the escrow left the wallet and now rides the order book
        Assert.True(corp.Credits < walletBefore,
            "the project bid never escrowed from the corp wallet");
        double escrowed = 0;
        foreach (var o in state.Orders) escrowed += o.EscrowCredits;
        Assert.True(escrowed > 0, "no escrow posted");
        Assert.Equal(supplyBefore, SupplyTotal(state), 6);   // escrow is conserved money

        // no asks exist, so the bid cannot fill — the whole escrow refunds
        MarketEngine.MatchAndClear(state, scratch);

        // the refund landed back in the wallet: no leak (pre-fix, the negative
        // Withdraw no-op swallowed it and the wallet stayed drained)
        Assert.Equal(walletBefore, corp.Credits, 6);
        Assert.Equal(supplyBefore, SupplyTotal(state), 6);
        foreach (var o in state.Orders)
            Assert.True(o.EscrowCredits <= 1e-9, "escrow lingered after refund");
    }
}
