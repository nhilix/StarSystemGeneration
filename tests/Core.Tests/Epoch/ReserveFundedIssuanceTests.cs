using System.Linq;
using System.Reflection;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-2 task 5 — two-stage deficit funding. A negative treasury
/// is first covered from its currency's <see cref="Bank.Reserve"/> as a TRANSFER
/// (Reserve → Supply, conservation-neutral under Supply+Reserve, no
/// <see cref="Currency.CumulativeFiatIssued"/> growth), bounded per epoch by
/// <see cref="EconomyKnobs.IssuanceReserveRatio"/>; whatever stays negative falls
/// through to the unchanged bounded fiat backstop
/// (<c>IssueSovereignCredit</c>). The reserve draw is the regime-defining change:
/// a well-capitalized bank funds its polity's deficit without minting, so the
/// money supply no longer grows on every shortfall.</summary>
public class ReserveFundedIssuanceTests
{
    /// <summary>A single entered polity that has FOUNDED its own currency (and
    /// therefore has a parallel <see cref="Bank"/>), with the bank pre-loaded to
    /// <paramref name="reserve"/>. Mirrors AllocationMonetaryTests.Fixture but
    /// wires the currency so the reserve path is live.</summary>
    private static (SimState State, PolityRecord Pr, Bank Bank) Fixture(
        double credits, double receipts, double reserve)
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var pr = state.PolityOf(actor.Id);
        state.FoundCurrency(actor.Id);            // sets pr.CurrencyId, founds Bank
        var bank = state.BankOf(pr.CurrencyId);
        bank.Reserve = reserve;
        pr.Credits = credits;
        pr.Receipts = receipts;
        state.WorldYear = 100;
        return (state, pr, bank);
    }

    // FundDeficit is the private two-stage helper the issuance loop calls per
    // entered polity; reflection exercises it in isolation, free of the budget
    // churn the full phase would apply before the loop.
    private static void FundDeficit(SimState state, PolityRecord pr)
    {
        var m = typeof(AllocationPhase).GetMethod("FundDeficit",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        m.Invoke(null, new object[] { state, pr });
    }

    // ---- (a) well-capitalized bank: deficit funded from reserve, NO mint ----

    [Fact]
    public void FundDeficit_WellCapitalizedBank_CoversFromReserve_NoFiatMint()
    {
        var (state, pr, bank) = Fixture(credits: -50, receipts: 100, reserve: 1000);

        FundDeficit(state, pr);

        // draw = min(-(-50)=50, ratio*1000=500, 1000) = 50 — fully covers
        Assert.Equal(0.0, pr.Credits, 9);                 // topped up to zero
        Assert.Equal(950.0, bank.Reserve, 9);             // reserve fell by the draw
        Assert.Equal(50.0, bank.CumulativeReserveFunded, 9);
        // it is a TRANSFER, not a mint: no fiat counters move
        Assert.Equal(0.0, state.CumulativeFiatIssued, 9);
        Assert.Equal(0.0, state.CurrencyOf(pr.CurrencyId).CumulativeFiatIssued, 9);
        Assert.True(bank.Reserve >= 0.0, "reserve stays non-negative");
    }

    // conservation: the reserve draw leaves per-currency (Supply + Reserve)
    // unchanged, so the residual nets to zero with NO counter moving.
    [Fact]
    public void FundDeficit_ReserveDraw_KeepsPerCurrencyResidualZero()
    {
        var (state, pr, bank) = Fixture(credits: -50, receipts: 100, reserve: 1000);

        // baseline: circulating supply written, reserve captured
        SupplyOps.Recompute(state);
        state.Health.Rows.Add(MetricsOps.Snapshot(state));

        FundDeficit(state, pr);

        // post: Supply rose by the draw, Reserve fell by the draw
        SupplyOps.Recompute(state);
        var row = MetricsOps.Snapshot(state);
        var curRow = row.Currencies.First(r => r.CurrencyId == pr.CurrencyId);

        Assert.Equal(0.0, curRow.Residual, 9);            // Supply+Reserve conserved
        Assert.Equal(0.0, state.CumulativeFiatIssued, 9); // no mint entered the residual
    }

    // ---- (b) thin bank: reserve partially covers, fiat backstop finishes ----

    [Fact]
    public void FundDeficit_ThinBank_DrawsWhatItCan_ThenFiatBackstops_MayStayNegative()
    {
        var (state, pr, bank) = Fixture(credits: -1000, receipts: 100, reserve: 100);
        double fiatCap = state.Config.Economy.SovereignIssuanceRate * 100;

        FundDeficit(state, pr);

        // stage 1: draw = min(1000, ratio*100=50, 100) = 50 — reserve gives what
        // the per-epoch ratio allows, not the whole hole
        Assert.Equal(50.0, bank.CumulativeReserveFunded, 9);
        Assert.Equal(50.0, bank.Reserve, 9);
        Assert.True(bank.Reserve >= 0.0, "reserve stays non-negative");
        // stage 2: the -950 remainder draws the bounded fiat mint
        Assert.Equal(fiatCap, state.CumulativeFiatIssued, 6);
        Assert.Equal(fiatCap, state.CurrencyOf(pr.CurrencyId).CumulativeFiatIssued, 6);
        // NegativeTreasuries still breathes: neither the ratio-capped draw nor the
        // receipts-capped mint fully covers a -1000 hole, so the polity stays red
        Assert.True(pr.Credits < 0,
            $"a deep deficit remains negative after both stages ({pr.Credits:0.###})");
        Assert.Equal(-1000.0 + 50.0 + fiatCap, pr.Credits, 6);
    }

    // an EMPTY bank contributes nothing: funding is purely the fiat backstop,
    // byte-identical to the pre-task behavior.
    [Fact]
    public void FundDeficit_EmptyBank_FallsStraightThroughToFiat()
    {
        var (state, pr, bank) = Fixture(credits: -1000, receipts: 100, reserve: 0);
        double fiatCap = state.Config.Economy.SovereignIssuanceRate * 100;

        FundDeficit(state, pr);

        Assert.Equal(0.0, bank.CumulativeReserveFunded, 9);
        Assert.Equal(0.0, bank.Reserve, 9);
        Assert.Equal(fiatCap, state.CumulativeFiatIssued, 6);
        Assert.Equal(-1000.0 + fiatCap, pr.Credits, 6);
    }

    // ---- (c) the per-epoch ratio caps a single draw ----

    [Fact]
    public void FundDeficit_DeepDeficit_DrawCappedByRatio_NotWholeReserve()
    {
        var (state, pr, bank) = Fixture(credits: -100000, receipts: 0, reserve: 1000);
        double ratio = state.Config.Economy.IssuanceReserveRatio;

        FundDeficit(state, pr);

        // a bottomless deficit still draws only ratio*reserve in one epoch
        double expectedDraw = ratio * 1000.0;
        Assert.Equal(expectedDraw, bank.CumulativeReserveFunded, 9);
        Assert.Equal(1000.0 - expectedDraw, bank.Reserve, 9);
        Assert.True(bank.Reserve > 0.0,
            "the ratio cap leaves reserve for future epochs, never drains it in one");
        // receipts are zero, so the fiat cap is zero: no mint, and the polity
        // remains deeply negative (NegativeTreasuries breathes)
        Assert.Equal(0.0, state.CumulativeFiatIssued, 9);
        Assert.True(pr.Credits < 0);
    }

    // ---- combined residual: reserve draw + fiat backstop both net out ----

    [Fact]
    public void FundDeficit_ReserveDrawPlusFiatBackstop_PerCurrencyResidualZero()
    {
        var (state, pr, bank) = Fixture(credits: -1000, receipts: 100, reserve: 100);

        SupplyOps.Recompute(state);
        state.Health.Rows.Add(MetricsOps.Snapshot(state));

        FundDeficit(state, pr);

        SupplyOps.Recompute(state);
        var row = MetricsOps.Snapshot(state);
        var curRow = row.Currencies.First(r => r.CurrencyId == pr.CurrencyId);

        // draw moved Reserve→Supply (nets), fiat mint grew Supply AND
        // CumulativeFiatIssued (nets): the residual is zero either way
        Assert.Equal(0.0, curRow.Residual, 6);
        Assert.True(state.CumulativeFiatIssued > 0.0, "the backstop did mint");
    }
}
