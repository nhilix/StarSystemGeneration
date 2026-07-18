using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-3 (currency-consolidation design §3): when a polity is
/// absorbed, <see cref="FederationOps.MergeInto"/> consolidates the absorbed
/// bank's balance sheet into the survivor's — the <b>reserve</b> (money) converts
/// AND records a conversion (§3b), the <b>claim</b> (memory) converts to reprice
/// but is NEVER recorded (§3c), and the cumulative counters stay on the drained
/// husk (§3d). The whole slice's correctness turns on that reserve-vs-claim
/// asymmetry.</summary>
public class CurrencyConsolidationTests
{
    private static SimState NewState() =>
        new SimState(new EpochSimConfig(),
            SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = 1, GalaxyRadiusCells = 4 }));

    private static Currency AddCurrency(SimState state, int id, double rate)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id)
        { NumeraireRate = rate };
        state.Currencies.Add(cur);
        state.Banks.Add(new Bank(id));
        return cur;
    }

    // ---- §3b: the reserve is money — converted AND recorded ----

    [Fact]
    public void MergeInto_TransfersReserve_ConvertedAndRecorded()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);   // absorbed currency
        AddCurrency(state, 1, 2.0);   // survivor currency
        var from = new PolityRecord(0, 0) { CurrencyId = 0 };
        var into = new PolityRecord(1, 0) { CurrencyId = 1 };
        state.Polities.Add(from);
        state.Polities.Add(into);
        // isolate the bank block: no treasury/pools cross the seam
        state.BankOf(0).Reserve = 600.0;
        state.BankOf(1).Reserve = 50.0;

        FederationOps.MergeInto(state, fromId: 0, intoId: 1);

        // 600 of cur0 lands as 600 * 1.0/2.0 = 300 of cur1 (never a raw carry)
        double landed = 600.0 * 1.0 / 2.0;
        Assert.Equal(0.0, state.BankOf(0).Reserve, 9);       // drained
        Assert.Equal(50.0 + landed, state.BankOf(1).Reserve, 9);
        // the reserve is MONEY, so the conversion is recorded on BOTH sides
        Assert.Equal(600.0, state.CurrencyOf(0).CumulativeConvertedOut, 9);
        Assert.Equal(landed, state.CurrencyOf(1).CumulativeConvertedIn, 9);
    }

    // ---- §3c: the claim is memory — converted (repriced) but NOT recorded ----

    [Fact]
    public void MergeInto_TransfersClaim_RepricedButNotRecorded()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);
        var from = new PolityRecord(0, 0) { CurrencyId = 0 };
        var into = new PolityRecord(1, 0) { CurrencyId = 1 };
        state.Polities.Add(from);
        state.Polities.Add(into);
        // THE distinguishing construction: reserve 0 / claim nonzero, so any
        // spurious claim-record shows up unambiguously in the convert counters
        // (a recorded reserve would be the ONLY thing that could move them, and
        // the reserve is zero here).
        state.BankOf(0).Reserve = 0.0;
        state.BankOf(0).ClaimOnState = 800.0;

        FederationOps.MergeInto(state, fromId: 0, intoId: 1);

        // the claim reprices into the survivor's currency: 800 * 1.0/2.0 = 400
        double landed = 800.0 * 1.0 / 2.0;
        Assert.Equal(0.0, state.BankOf(0).ClaimOnState, 9);  // drained
        Assert.Equal(landed, state.BankOf(1).ClaimOnState, 9);
        // NOT money → NOT recorded: the claim amount must NOT appear in EITHER
        // currency's converted counters (this is the slice's central point)
        Assert.Equal(0.0, state.CurrencyOf(0).CumulativeConvertedOut, 9);
        Assert.Equal(0.0, state.CurrencyOf(1).CumulativeConvertedIn, 9);
    }

    // ---- §4: per-currency conservation residual stays ~0 across the merge ----

    [Fact]
    public void MergeInto_BalanceSheetConsolidation_KeepsPerCurrencyResidualZero()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 3.0);
        var from = new PolityRecord(0, 0) { CurrencyId = 0 };
        var into = new PolityRecord(1, 0) { CurrencyId = 1 };
        state.Polities.Add(from);
        state.Polities.Add(into);
        // a bank carrying BOTH a reserve and a claim; treasury/pools left at 0 so
        // the only cross-seam motion is the bank block under test
        state.BankOf(0).Reserve = 420.0;
        state.BankOf(0).ClaimOnState = 900.0;

        // snapshot the residual inputs (MetricsOps identity, design §4) BEFORE
        var r0From = Residual(state, 0);
        var r0Into = Residual(state, 1);

        FederationOps.MergeInto(state, 0, 1);

        // the residual is the CHANGE in (Supply + Reserve) net of the recorded
        // transfers; both currencies must net to ~0 across the merge. A recorded
        // claim would inject a phantom leak here (ConvertedOut/In moves with no
        // matching Supply+Reserve motion); an unrecorded reserve would leak the
        // other way. Both being right is what keeps this at 0.
        Assert.Equal(r0From, Residual(state, 0), 9);
        Assert.Equal(r0Into, Residual(state, 1), 9);
    }

    /// <summary>The per-currency conservation identity (MetricsOps §4), read as a
    /// single scalar: <c>Supply + Reserve − CumulativeConvertedIn +
    /// CumulativeConvertedOut − issuance + CumulativeFiatRetired</c>. MergeInto's
    /// bank block must leave this invariant per currency (Supply unchanged in a
    /// bare state; Reserve and the convert counters move together).</summary>
    private static double Residual(SimState state, int currencyId)
    {
        var cur = state.CurrencyOf(currencyId);
        return cur.Supply + state.BankOf(currencyId).Reserve
            - cur.CumulativeConvertedIn + cur.CumulativeConvertedOut
            - cur.CumulativeFiatIssued - cur.CumulativeSteadyIssuance
            + cur.CumulativeFiatRetired;
    }

    // ---- §3d: the drained bank lingers with its cumulative counters intact ----

    [Fact]
    public void MergeInto_DrainedBankLingers_WithCumulativeCountersIntact()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 1.0);
        var from = new PolityRecord(0, 0) { CurrencyId = 0 };
        var into = new PolityRecord(1, 0) { CurrencyId = 1 };
        state.Polities.Add(from);
        state.Polities.Add(into);
        var fromBank = state.BankOf(0);
        fromBank.Reserve = 100.0;
        fromBank.ClaimOnState = 200.0;
        // historical levels of the ABSORBED polity's own activity — observability
        // only, they must NOT transfer to the survivor (§3d)
        fromBank.CumulativeSpreadIntake = 11.0;
        fromBank.CumulativeReserveFunded = 22.0;
        fromBank.CumulativeLentToState = 33.0;
        fromBank.CumulativeRetired = 44.0;
        int banksBefore = state.Banks.Count;

        FederationOps.MergeInto(state, 0, 1);

        // the husk stays in the dense registry, resolvable, live balances drained
        Assert.Equal(banksBefore, state.Banks.Count);
        var husk = state.BankOf(0);
        Assert.Equal(0.0, husk.Reserve, 9);
        Assert.Equal(0.0, husk.ClaimOnState, 9);
        // cumulative counters stay put on the husk — NOT attributed to the survivor
        Assert.Equal(11.0, husk.CumulativeSpreadIntake, 9);
        Assert.Equal(22.0, husk.CumulativeReserveFunded, 9);
        Assert.Equal(33.0, husk.CumulativeLentToState, 9);
        Assert.Equal(44.0, husk.CumulativeRetired, 9);
        // the survivor did NOT inherit the absorbed lending counter (§3d flagged
        // interaction: its ClaimOnState can now exceed its own CumulativeLentToState)
        Assert.Equal(0.0, state.BankOf(1).CumulativeLentToState, 9);
        Assert.Equal(200.0, state.BankOf(1).ClaimOnState, 9);
    }

    // ---- §5: union-genesis pools BOTH parents' reserves and claims ----

    [Fact]
    public void UnionGenesis_PoolsBothParentsReservesAndClaims()
    {
        var state = NewState();
        AddCurrency(state, 0, 2.0);   // parent A
        AddCurrency(state, 1, 4.0);   // parent B
        AddCurrency(state, 2, 1.0);   // the fresh union currency
        var a = new PolityRecord(0, 0) { CurrencyId = 0 };
        var b = new PolityRecord(1, 0) { CurrencyId = 1 };
        var union = new PolityRecord(2, 0) { CurrencyId = 2 };
        state.Polities.Add(a);
        state.Polities.Add(b);
        state.Polities.Add(union);
        state.BankOf(0).Reserve = 100.0;
        state.BankOf(0).ClaimOnState = 300.0;
        state.BankOf(1).Reserve = 200.0;
        state.BankOf(1).ClaimOnState = 800.0;

        // the union-genesis path: both parents merge into the fresh currency
        FederationOps.MergeInto(state, 0, 2);
        FederationOps.MergeInto(state, 1, 2);

        // union bank pools BOTH converted reserves: 100*2/1 + 200*4/1 = 1000
        double reserveA = 100.0 * 2.0 / 1.0;
        double reserveB = 200.0 * 4.0 / 1.0;
        Assert.Equal(reserveA + reserveB, state.BankOf(2).Reserve, 9);
        // and BOTH converted claims: 300*2/1 + 800*4/1 = 3800
        double claimA = 300.0 * 2.0 / 1.0;
        double claimB = 800.0 * 4.0 / 1.0;
        Assert.Equal(claimA + claimB, state.BankOf(2).ClaimOnState, 9);
        // both parents drained
        Assert.Equal(0.0, state.BankOf(0).Reserve, 9);
        Assert.Equal(0.0, state.BankOf(1).Reserve, 9);
        Assert.Equal(0.0, state.BankOf(0).ClaimOnState, 9);
        Assert.Equal(0.0, state.BankOf(1).ClaimOnState, 9);
    }

    // ---- §3a: guards — no crash, no-op ----

    [Fact]
    public void MergeInto_PreGenesisSentinel_SkipsBankBlock_NoCrash()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        // from is pre-genesis (currency -1): BankOf(-1) would THROW, so the guard
        // must return before touching the banks
        var from = new PolityRecord(0, 0) { CurrencyId = -1 };
        var into = new PolityRecord(1, 0) { CurrencyId = 0 };
        state.Polities.Add(from);
        state.Polities.Add(into);
        state.BankOf(0).Reserve = 77.0;

        FederationOps.MergeInto(state, 0, 1);   // must not throw

        Assert.Equal(77.0, state.BankOf(0).Reserve, 9);   // untouched
    }

    [Fact]
    public void MergeInto_SameCurrency_SkipsBankBlock_ReserveNotDoubledOrZeroed()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        // two polities sharing one currency — BankOf(0) is the SAME object for
        // both, so without the guard the block would read-then-zero it. The guard
        // leaves it exactly as-is.
        var from = new PolityRecord(0, 0) { CurrencyId = 0 };
        var into = new PolityRecord(1, 0) { CurrencyId = 0 };
        state.Polities.Add(from);
        state.Polities.Add(into);
        state.BankOf(0).Reserve = 500.0;
        state.BankOf(0).ClaimOnState = 250.0;

        FederationOps.MergeInto(state, 0, 1);

        Assert.Equal(500.0, state.BankOf(0).Reserve, 9);
        Assert.Equal(250.0, state.BankOf(0).ClaimOnState, 9);
    }
}
