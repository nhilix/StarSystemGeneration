using System;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-2 task 4b: the market/order/project conversion sites.
/// A foreign build port makes a project's bid escrow, its refund, and its
/// construction wages cross a currency boundary. Direction-specific incidence:
/// the ESCROW POST is a payment that parks endCur money, so it grosses up the
/// funder and keeps the escrow whole (destination Bank keeps the skim); the
/// REFUND un-posts the funder's own unfilled escrow (exempt — one-skim
/// round-trip); the WAGES arrive into the workers' own port currency, so the
/// workers bank NET (reduce-recipient, like a seller's net) and the build-port
/// Bank keeps the skim.</summary>
public class BankProjectSkimTests
{
    private static Currency AddCurrency(SimState state, int id, double rate)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id)
        { NumeraireRate = rate };
        state.Currencies.Add(cur);
        state.Banks.Add(new Bank(id));
        return cur;
    }

    // funder = polity 0 (currency 0, rate 1); build port 0 owned by polity 1
    // (currency 1, rate 2 — one unit of C1 is worth two of C0).
    private static SimState Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        state.Actors[0].Entered = true;
        state.Actors[1].Entered = true;
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);
        state.PolityOf(0).CurrencyId = 0;
        state.PolityOf(1).CurrencyId = 1;
        state.Ports.Add(new Port(0, ownerActorId: 1, state.Actors[1].Seat,
            tier: 2, foundedYear: 0));
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.WorldYear = 100;
        return state;
    }

    private static Project GoodsProject(SimState state)
    {
        state.Markets[0].Price[(int)GoodId.Alloys] = 10.0;
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, ownerActorId: 0,
            funderActorId: 0, portId: 0, state.Ports[0].Hex,
            yearsRequired: 25.0, ProjectPriority.Core, planOrder: 0);
        p.TargetId = 0;
        p.PerYearBasket[(int)GoodId.Alloys] = 1.0;
        p.WagesPerYear = 0.0;
        return p;
    }

    // ---- (1) escrow post: gross up the funder, park the WHOLE escrow ----

    [Fact]
    public void EscrowPost_ForeignBuildPort_GrossesUpFunder_ParksWholeEscrow_ReserveKeepsSkim()
    {
        var state = Fixture();
        var funder = state.PolityOf(0);
        funder.DevelopmentPoints = 100_000.0;
        GoodsProject(state);
        double spread = state.Config.Economy.ConversionSpread;
        double bid = Math.Max(state.Config.Economy.PriceFloor,
            10.0 * state.Config.Economy.ProjectBidPremium);
        double before = funder.DevelopmentPoints;

        var scratch = new MarketStepScratch(state);
        MarketEngine.PostProjectBids(state, scratch);

        Assert.Single(scratch.ProjectBids);
        var order = scratch.ProjectBids[0].Order;
        double escrow = order.EscrowCredits;                // parked, currency 1
        // the escrow is WHOLE (qty*bid) — nothing skimmed off it, so fills and
        // refunds stay honourable
        Assert.Equal(order.QtyRemaining * bid, escrow, 6);
        double skim = escrow * spread;
        double cost = (escrow + skim) * 2.0;                // grossEscrow, C1 -> C0

        // the funder bore the grossed cost (escrow + skim, converted)
        Assert.Equal(before - cost, funder.DevelopmentPoints, 4);
        // the build-port (destination) Bank keeps the skim; the funder's own does not
        Assert.Equal(skim, state.BankOf(1).Reserve, 6);
        Assert.Equal(0.0, state.BankOf(0).Reserve, 6);
        Assert.Equal(cost, state.CurrencyOf(0).CumulativeConvertedOut, 4);
        Assert.Equal(escrow + skim, state.CurrencyOf(1).CumulativeConvertedIn, 4);
    }

    // ---- (2) refund: exempt un-posting, full principal back, no second skim ----

    [Fact]
    public void ProjectBidRefund_ExemptUnPosting_ReturnsFullPrincipal_NoSecondSkim()
    {
        var state = Fixture();
        var funder = state.PolityOf(0);
        funder.DevelopmentPoints = 100_000.0;
        GoodsProject(state);

        var scratch = new MarketStepScratch(state);
        MarketEngine.PostProjectBids(state, scratch);
        var order = scratch.ProjectBids[0].Order;
        double escrow = order.EscrowCredits;
        double reserveAfterPost = state.BankOf(1).Reserve;   // = post skim
        double devAfterPost = funder.DevelopmentPoints;

        // no sells at the port => every project bid is unfilled and retires
        MarketEngine.MatchAndClear(state, scratch);

        // the funder gets the FULL principal back (escrow converted, no skim) —
        // the round-trip is taxed once (at post), not twice
        double back = escrow * 2.0;                          // C1 -> C0 at 2/1
        Assert.Equal(devAfterPost + back, funder.DevelopmentPoints, 4);
        // the refund did NOT touch the reserve (no second skim)
        Assert.Equal(reserveAfterPost, state.BankOf(1).Reserve, 6);
        Assert.Equal(0.0, order.EscrowCredits, 6);           // escrow fully returned
    }

    // ---- (3) wages: workers bank NET into their own port currency ----

    [Fact]
    public void ConstructionWages_ForeignBuildPort_WorkersBankNet_BuildPortBankKeepsSkim()
    {
        var state = Fixture();
        var funder = state.PolityOf(0);
        funder.DevelopmentPoints = 100_000.0;
        // workers resident at the foreign build port (their wealth resolves to C1)
        var seg = new PopulationSegment(0, 0, state.PolityOf(1).SpeciesId,
            state.PolityOf(1).SpeciesId, 5.0) { Wealth = 0.0 };
        state.Segments.Add(seg);
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, ownerActorId: 0,
            funderActorId: 0, portId: 0, state.Ports[0].Hex,
            yearsRequired: 25.0, ProjectPriority.Core, planOrder: 0);
        p.TargetId = 0;
        p.WagesPerYear = 4.0;                        // no goods (all-zero basket)
        int years = state.Config.Sim.YearsPerEpoch;  // 25
        double spent = p.WagesPerYear * years;       // funder currency (C0)
        double credit = spent * 1.0 / 2.0;           // C0 -> C1 (rate 1/2)
        double skim = credit * state.Config.Economy.ConversionSpread;
        double before = funder.DevelopmentPoints;

        ProjectOps.AdvanceAll(state);

        // the funder's outlay is the FULL spent (fixed, debited by SpendTreasury)
        Assert.Equal(before - spent, funder.DevelopmentPoints, 5);
        // the workers bank the NET; the build-port Bank keeps the skim
        Assert.Equal(credit - skim, seg.Wealth, 6);
        Assert.Equal(skim, state.BankOf(1).Reserve, 6);
        Assert.Equal(0.0, state.BankOf(0).Reserve, 6);
        Assert.Equal(spent, state.CurrencyOf(0).CumulativeConvertedOut, 6);
        Assert.Equal(credit, state.CurrencyOf(1).CumulativeConvertedIn, 6);
    }
}
