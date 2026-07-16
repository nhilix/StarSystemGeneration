using System;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-2 task 8 fix 2: a CORPORATION funder provides its treasury
/// in the build-port currency via <see cref="Corporation.Withdraw"/>, which
/// sources the matched (provide-currency) bucket at par but yields only
/// <c>value/(1+spread)</c> from every OTHER bucket (the gross-up skim to the
/// destination reserve). Valuing the affordability headroom at the raw numeraire
/// wallet total (<c>corp.Credits</c>) over-states what a currency-FRAGMENTED corp
/// can actually provide, letting the wage/escrow bound commit more than the
/// wallet delivers while the recipients are credited the full requested amount —
/// minting the <c>spread × non-matched-mass</c> difference. This is the exact
/// case the multi-seed sweep does not hit (live corp wallets are
/// home-currency-dominated). The fix makes <c>TreasuryAvailable</c>
/// provision-aware; here the fragmented corp pays wages at the bound and money is
/// conserved (no mint).</summary>
public class CorpFunderProvisionTests
{
    private static Currency AddCurrency(SimState state, int id, double rate)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id)
        { NumeraireRate = rate };
        state.Currencies.Add(cur);
        state.Banks.Add(new Bank(id));
        return cur;
    }

    // Entered polity 0 owns build port 0 in its own currency C0 (numeraire 1:1);
    // C1 is a foreign currency, one unit worth two of C0.
    private static (SimState state, int hostActor, int portId) HostWithPort()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        a0.Entered = true;
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);
        state.PolityOf(a0.Id).CurrencyId = 0;
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.WorldYear = 100;
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

    // A corp wallet split 10 of C0 (the matched build-port currency, at par) and
    // 5 of C1 (foreign, numeraire 10). Raw numeraire total (pre-fix headroom) is
    // 20; the provision-aware headroom discounts the foreign bucket by (1+spread),
    // since Withdraw yields only 10/(1+spread) of C0 from it. A wage bill far
    // larger than the treasury drives fraction = treasury/wages, so the paid
    // amount is exactly the treasury bound.
    private static (SimState state, Corporation corp, PopulationSegment seg,
        double provisionAware, double rawCredits) BoundFixture()
    {
        var (state, host, _) = HostWithPort();
        var corp = AddCorp(state, host, CorporateNiche.Extraction, homePortId: 0);
        corp.Deposit(state, 10.0, 0);   // matched (build-port) bucket C0
        corp.Deposit(state, 5.0, 1);    // foreign bucket C1 (numeraire 10)
        var seg = new PopulationSegment(0, 0, state.PolityOf(host).SpeciesId,
            state.PolityOf(host).SpeciesId, 5.0) { Wealth = 0.0 };
        state.Segments.Add(seg);
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise,
            ownerActorId: corp.ActorId, funderActorId: corp.ActorId, portId: 0,
            state.Ports[0].Hex, yearsRequired: 25.0, ProjectPriority.Core,
            planOrder: 0);
        p.WagesPerYear = 100.0;         // no goods (all-zero basket)
        double spread = state.Config.Economy.ConversionSpread;
        double provisionAware = 10.0 + 10.0 / (1.0 + spread);
        return (state, corp, seg, provisionAware, rawCredits: 20.0);
    }

    /// <summary>At the bound the fragmented corp pays the PROVISION-AWARE amount
    /// (matched bucket at par, foreign bucket discounted by the spread), not the
    /// raw numeraire wallet total the pre-fix headroom used.</summary>
    [Fact]
    public void FragmentedCorpFunder_AtTheBound_PaysProvisionAwareAmount()
    {
        var (state, corp, seg, provisionAware, rawCredits) = BoundFixture();
        Assert.Equal(rawCredits, corp.Credits, 9);   // pre-fix bound would pay this

        ProjectOps.AdvanceAll(state);

        // the workers were credited exactly what the wallet could actually
        // provide — strictly below the raw-Credits total the old bound allowed
        Assert.Equal(provisionAware, seg.Wealth, 6);
        Assert.True(seg.Wealth < rawCredits - 1e-6,
            $"paid {seg.Wealth:G9} — the foreign bucket was not discounted");
    }

    /// <summary>At the affordability bound money is conserved: what the wallet
    /// actually lost equals what the workers received PLUS what the destination
    /// Bank skimmed into its reserve — no mint. Pre-fix, the raw-Credits bound let
    /// the corp commit spread × (foreign mass) more than Withdraw delivered, so
    /// the workers' full-amount credit minted that difference.</summary>
    [Fact]
    public void FragmentedCorpFunder_AtTheBound_PaysWages_MoneyConserved()
    {
        var (state, corp, seg, provisionAware, _) = BoundFixture();
        double corpBefore = corp.Credits;                 // numeraire (C0 rate 1)

        ProjectOps.AdvanceAll(state);

        double corpLoss = corpBefore - corp.Credits;
        double reserves = state.BankOf(0).Reserve + state.BankOf(1).Reserve;
        Assert.Equal(provisionAware, seg.Wealth, 6);
        // conservation: wallet loss == worker credit + reserve skim (no mint)
        Assert.Equal(corpLoss, seg.Wealth + reserves, 6);
        // and the reserve skim is the genuine cross-bucket spread, not zero
        Assert.True(reserves > 0, "the C1->C0 sourcing skim never reached a reserve");
    }
}
