using System.Linq;
using System.Reflection;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice BF task 4 (bank-flow design §3) — the fiat backstop becomes a
/// BANK operation. The money creation is unchanged in every respect (same
/// trigger, same receipts cap, same magnitude, same
/// <see cref="Currency.CumulativeFiatIssued"/> counter); what is new is that the
/// currency's <see cref="Bank"/> now books a matching claim against the state.
/// These tests pin BOTH halves: the claim appears, and the mint does not move.</summary>
public class SovereignLendingTests
{
    /// <summary>An entered polity that has founded its own currency (and so has a
    /// parallel <see cref="Bank"/>). Mirrors ReserveFundedIssuanceTests.Fixture.</summary>
    private static (SimState State, PolityRecord Pr, Bank Bank) Fixture(
        double credits, double receipts, double reserve = 0)
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var pr = state.PolityOf(actor.Id);
        state.FoundCurrency(actor.Id);
        var bank = state.BankOf(pr.CurrencyId);
        bank.Reserve = reserve;
        pr.Credits = credits;
        pr.Receipts = receipts;
        state.WorldYear = 100;
        return (state, pr, bank);
    }

    /// <summary>A pre-genesis polity: entered, in deficit, but with NO currency
    /// (<c>CurrencyId &lt; 0</c>) and therefore no bank at all.</summary>
    private static (SimState State, PolityRecord Pr) PreGenesisFixture(
        double credits, double receipts)
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var pr = state.PolityOf(actor.Id);
        pr.Credits = credits;
        pr.Receipts = receipts;
        state.WorldYear = 100;
        return (state, pr);
    }

    private static void FundDeficit(SimState state, PolityRecord pr)
    {
        var m = typeof(AllocationPhase).GetMethod("FundDeficit",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        m.Invoke(null, new object[] { state, pr });
    }

    // ---- the claim is booked, and it matches the mint exactly ----

    [Fact]
    public void FiatBackstop_BooksAMatchingClaimOnTheBank()
    {
        var (state, pr, bank) = Fixture(credits: -1000, receipts: 100);
        double fiatCap = state.Config.Economy.SovereignIssuanceRate * 100;

        FundDeficit(state, pr);

        // the mint is UNCHANGED: same cap, same magnitude, same counters
        Assert.Equal(fiatCap, state.CumulativeFiatIssued, 6);
        Assert.Equal(fiatCap, state.CurrencyOf(pr.CurrencyId).CumulativeFiatIssued, 6);
        Assert.Equal(-1000.0 + fiatCap, pr.Credits, 6);
        // NEW: the bank now holds a claim of exactly the minted amount
        Assert.Equal(fiatCap, bank.ClaimOnState, 6);
        Assert.Equal(fiatCap, bank.CumulativeLentToState, 6);
        // nothing retired: task 4 only lends
        Assert.Equal(0.0, bank.CumulativeRetired, 9);
        Assert.Equal(0.0, state.CurrencyOf(pr.CurrencyId).CumulativeFiatRetired, 9);
    }

    // the claim is a LEVEL: repeated lending accumulates, one epoch's mint at a time
    [Fact]
    public void RepeatedLending_AccumulatesTheClaim()
    {
        var (state, pr, bank) = Fixture(credits: -1000, receipts: 100);
        double fiatCap = state.Config.Economy.SovereignIssuanceRate * 100;

        FundDeficit(state, pr);
        pr.Credits = -1000;                       // next epoch, deficit again
        FundDeficit(state, pr);

        Assert.Equal(2 * fiatCap, bank.ClaimOnState, 6);
        Assert.Equal(2 * fiatCap, bank.CumulativeLentToState, 6);
        Assert.Equal(2 * fiatCap, state.CurrencyOf(pr.CurrencyId).CumulativeFiatIssued, 6);
    }

    // ---- the cap and the floor are untouched: no reserve gate ----

    [Fact]
    public void LendingIsNeverGatedOnReserve_AnEmptyBankStillLendsInFull()
    {
        var (state, pr, bank) = Fixture(credits: -1000, receipts: 100, reserve: 0);
        double fiatCap = state.Config.Economy.SovereignIssuanceRate * 100;

        FundDeficit(state, pr);

        // an empty reserve is NOT a constraint — the backstop is absolute (ME's
        // spiral cure depends on this floor); the claim rides the full mint
        Assert.Equal(fiatCap, state.CumulativeFiatIssued, 6);
        Assert.Equal(fiatCap, bank.ClaimOnState, 6);
        Assert.Equal(0.0, bank.Reserve, 9);
    }

    // stage 1 (the reserve TRANSFER) books no claim — only the stage-2 mint does
    [Fact]
    public void ReserveFundedDeficit_BooksNoClaim()
    {
        var (state, pr, bank) = Fixture(credits: -50, receipts: 100, reserve: 1000);

        FundDeficit(state, pr);

        Assert.Equal(50.0, bank.CumulativeReserveFunded, 9);   // stage 1 covered it
        Assert.Equal(0.0, state.CumulativeFiatIssued, 9);      // no mint ran
        Assert.Equal(0.0, bank.ClaimOnState, 9);               // so no claim booked
        Assert.Equal(0.0, bank.CumulativeLentToState, 9);
    }

    // a zero-receipts polity has a zero cap: no mint, no claim
    [Fact]
    public void NoMint_BooksNoClaim()
    {
        var (state, pr, bank) = Fixture(credits: -1000, receipts: 0);

        FundDeficit(state, pr);

        Assert.Equal(0.0, state.CumulativeFiatIssued, 9);
        Assert.Equal(0.0, bank.ClaimOnState, 9);
        Assert.Equal(0.0, bank.CumulativeLentToState, 9);
    }

    // ---- pre-genesis: no currency, no bank, no claim — and no crash ----

    [Fact]
    public void PreGenesisPolity_StillMints_WithNoBankAndNoClaim()
    {
        var (state, pr) = PreGenesisFixture(credits: -1000, receipts: 100);
        Assert.True(pr.CurrencyId < 0, "fixture precondition: no currency founded");
        double fiatCap = state.Config.Economy.SovereignIssuanceRate * 100;

        FundDeficit(state, pr);                    // must not throw

        // byte-identical to the pre-task path: the galaxy-wide mint still runs
        Assert.Equal(fiatCap, state.CumulativeFiatIssued, 6);
        Assert.Equal(-1000.0 + fiatCap, pr.Credits, 6);
        // and no bank anywhere booked anything
        Assert.All(state.Banks, b => Assert.Equal(0.0, b.ClaimOnState, 9));
        Assert.All(state.Banks, b => Assert.Equal(0.0, b.CumulativeLentToState, 9));
    }

    // ---- conservation: the claim is NOT money and does not enter the residual ----

    [Fact]
    public void Lending_KeepsPerCurrencyResidualZero_ClaimIsNotMoney()
    {
        var (state, pr, bank) = Fixture(credits: -1000, receipts: 100);

        SupplyOps.Recompute(state);
        state.Health.Rows.Add(MetricsOps.Snapshot(state));

        FundDeficit(state, pr);

        SupplyOps.Recompute(state);
        var row = MetricsOps.Snapshot(state);
        var curRow = row.Currencies.First(r => r.CurrencyId == pr.CurrencyId);

        // Supply and CumulativeFiatIssued both moved by m; ClaimOnState appears
        // nowhere in the identity, so the residual is untouched by it
        Assert.Equal(0.0, curRow.Residual, 6);
        Assert.True(bank.ClaimOnState > 0.0, "the claim was booked");
    }
}
