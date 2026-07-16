using System.Reflection;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 7 — Borrow/ServiceLoans across currencies. The
/// borrower-side debt ceiling sums open principals in a common (numeraire)
/// unit; lender ranking compares candidate wealth in numeraire, so a polity
/// whose own-currency balance is nominally smaller but numeraire-richer wins;
/// and a corporation lender fronts the principal from its real multi-currency
/// wallet (Withdraw) and banks repayments back into it (Deposit), so its
/// read-only <see cref="Corporation.Credits"/> stays exactly its wallet total
/// with no divergence — the transitional single-balance bridge is gone.</summary>
public class BorrowServiceCurrencyTests
{
    // Borrow is private and runs mid-phase; invoke it in isolation (mirrors
    // AllocationMonetaryTests) so the ranking/gate is exercised on a controlled
    // state that still holds its open loans.
    private static int InvokeBorrow(SimState state)
    {
        var m = typeof(AllocationPhase).GetMethod("Borrow",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (int)m.Invoke(null, new object[] { state })!;
    }

    private static Currency AddCurrency(SimState state, int id, double rate)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id)
        { NumeraireRate = rate };
        state.Currencies.Add(cur);
        state.Banks.Add(new Bank(id));
        return cur;
    }

    /// <summary>A minimal live-currency stage: three seeded polities entered,
    /// each with its own currency at a chosen rate. Returns the state.</summary>
    private static SimState ThreePolityStage(double r0, double r1, double r2)
    {
        var state = EpochTestKit.Seeded().State;
        AddCurrency(state, 0, r0);
        AddCurrency(state, 1, r1);
        AddCurrency(state, 2, r2);
        for (int i = 0; i < 3; i++)
        {
            state.Actors[i].Entered = true;
            state.PolityOf(i).CurrencyId = i;
        }
        state.WorldYear = 100;
        return state;
    }

    private static Corporation AddCorp(SimState state, int homePolity)
    {
        int actorId = state.Actors.Count;
        state.Actors.Add(new Actor(actorId, ActorKind.Corporation, "Bankco",
            state.Actors[homePolity].Seat, state.EpochIndex,
            new CorporateController(state.Config)) { Entered = true });
        var corp = new Corporation(state.Corporations.Count, actorId, "Bankco",
            homePolity, CorporateNiche.Freight, homePortId: 0, state.WorldYear);
        state.Corporations.Add(corp);
        return corp;
    }

    // ---- lender ranking is numeraire, not raw own-currency ----

    /// <summary>Two polity lenders in different currencies both qualify; the
    /// winner is the one richer in NUMERAIRE terms, even though its raw
    /// own-currency balance is the smaller number. RED on the pre-task ranking
    /// (which compared raw Credits and would pick the nominally-larger lender 2).</summary>
    [Fact]
    public void LenderRanking_PicksTheNumeraireRicher_AcrossCurrencies()
    {
        // lender 1: 100 of C1 @2.0 = 200 numeraire; lender 2: 150 of C2 @1.0 = 150
        var state = ThreePolityStage(r0: 1.0, r1: 2.0, r2: 1.0);
        state.PolityOf(0).Credits = -10;      // borrower: principal = 12
        state.PolityOf(1).Credits = 100;      // numeraire 200 (the richer)
        state.PolityOf(2).Credits = 150;      // numeraire 150 (nominally bigger)

        int issued = InvokeBorrow(state);

        Assert.Equal(1, issued);
        Assert.Single(state.Loans);
        Assert.Equal(1, state.Loans[0].LenderActorId);   // numeraire winner, not raw
    }

    /// <summary>The discriminator: with equal rates the same balances rank by
    /// the raw number, so lender 2 (150 &gt; 100) wins — proving the test above
    /// turned on the rate, not on actor order.</summary>
    [Fact]
    public void LenderRanking_WithEqualRates_PicksTheNominallyLarger()
    {
        var state = ThreePolityStage(r0: 1.0, r1: 1.0, r2: 1.0);
        state.PolityOf(0).Credits = -10;
        state.PolityOf(1).Credits = 100;
        state.PolityOf(2).Credits = 150;

        InvokeBorrow(state);

        Assert.Single(state.Loans);
        Assert.Equal(2, state.Loans[0].LenderActorId);
    }

    // ---- borrower-side debt ceiling gates on the summed principal ----

    /// <summary>A borrower already carrying open principal above its
    /// income-scaled ceiling is refused new credit; the sum is taken in a common
    /// numeraire unit across loans booked from different-currency lenders.</summary>
    [Fact]
    public void DebtCeiling_DeniesWhenSummedPrincipalExceedsIncome()
    {
        var state = ThreePolityStage(r0: 1.0, r1: 1.0, r2: 1.0);
        var pr = state.PolityOf(0);
        pr.Credits = -100;
        pr.LastIncomePerYear = 1.0;   // ceiling = 3.0 × 1 × 25 = 75 (numeraire)
        state.PolityOf(1).Credits = 100000;
        // two open loans booked from lenders in different currencies — total 90
        // exceeds the 75 ceiling
        state.Loans.Add(new Loan(0, lenderActorId: 1, borrowerActorId: 0,
            principal: 45, ratePerYear: 0.02, termYears: 125, issuedYear: 0));
        state.Loans.Add(new Loan(1, lenderActorId: 2, borrowerActorId: 0,
            principal: 45, ratePerYear: 0.02, termYears: 125, issuedYear: 0));

        int issued = InvokeBorrow(state);

        Assert.Equal(0, issued);
        Assert.Equal(2, state.Loans.Count);   // no third loan piled on
    }

    /// <summary>Same setup, debt under the ceiling: the gate opens and a loan
    /// issues — the discriminator proving the refusal above is the debt sum.</summary>
    [Fact]
    public void DebtCeiling_IssuesWhenSummedPrincipalIsUnderIncome()
    {
        var state = ThreePolityStage(r0: 1.0, r1: 1.0, r2: 1.0);
        var pr = state.PolityOf(0);
        pr.Credits = -100;
        pr.LastIncomePerYear = 10.0;   // ceiling = 3.0 × 10 × 25 = 750
        state.PolityOf(1).Credits = 100000;
        state.Loans.Add(new Loan(0, lenderActorId: 1, borrowerActorId: 0,
            principal: 45, ratePerYear: 0.02, termYears: 125, issuedYear: 0));

        int issued = InvokeBorrow(state);

        Assert.Equal(1, issued);
        Assert.Equal(2, state.Loans.Count);
    }

    // ---- a corp lender's balance never diverges from its real wallet ----

    /// <summary>A corporation lends from its real wallet (Withdraw) and receives
    /// repayments back into it (Deposit): across a full borrow→service cycle its
    /// read-only Credits equals the numeraire sum of Holdings at every step —
    /// there is no phantom single balance to diverge (task 7 removed the bridge),
    /// and the corp actually holds the borrower's currency after servicing.</summary>
    [Fact]
    public void CorpLender_BalanceTracksWallet_AcrossBorrowAndService()
    {
        var state = ThreePolityStage(r0: 1.0, r1: 1.0, r2: 1.0);
        // borrower polity 0 underwater; the only qualifying lender is a corp
        state.PolityOf(0).Credits = -50;
        state.PolityOf(1).Credits = 0;   // no polity lender qualifies
        state.PolityOf(2).Credits = 0;
        var corp = AddCorp(state, homePolity: 1);
        corp.Deposit(state, 1000, 1);            // wallet: 1000 of C1
        double walletBefore = WalletNumeraire(state, corp);
        Assert.Equal(walletBefore, corp.Credits, 9);

        int issued = InvokeBorrow(state);
        Assert.Equal(1, issued);
        Assert.Equal(corp.ActorId, state.Loans[0].LenderActorId);
        // the principal left the real wallet (Withdraw), not a phantom balance
        Assert.Equal(WalletNumeraire(state, corp), corp.Credits, 9);
        Assert.True(corp.Credits < walletBefore, "principal fronted from the wallet");

        // service the loan: the borrower repays in ITS currency (C0), which the
        // corp banks into a real C0 bucket — Credits still equals the wallet sum
        state.PolityOf(0).Credits = 500;          // solvent enough to service
        var m = typeof(AllocationPhase).GetMethod("ServiceLoans",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        m.Invoke(null, new object[] { state });

        Assert.Equal(WalletNumeraire(state, corp), corp.Credits, 9);
        Assert.True(corp.Holdings.ContainsKey(0),
            "the corp banked the borrower's own currency (C0) from the repayment");
    }

    private static double WalletNumeraire(SimState state, Corporation corp)
    {
        double sum = 0;
        foreach (var kv in corp.Holdings)
            sum += kv.Value * state.NumeraireRateOf(kv.Key);
        return sum;
    }

    // ---- Task 7c: true lender-currency denomination + FX risk ----

    private static void InvokeService(SimState state)
    {
        var m = typeof(AllocationPhase).GetMethod("ServiceLoans",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        m.Invoke(null, new object[] { state });
    }

    /// <summary>Borrower polity 0 (currency C0 @1.0) owing a loan denominated in
    /// lender polity 1's currency C1 (<paramref name="lenderRate"/>). The borrower
    /// is flush so a FULL payment runs; the lender starts empty to read its
    /// receipt cleanly. The loan's <see cref="Loan.Principal"/> is in C1.</summary>
    private static SimState TwoPolityLoanStage(
        double lenderRate, double principal, double borrowerCredits)
    {
        var state = EpochTestKit.Seeded().State;
        AddCurrency(state, 0, 1.0);         // borrower currency, fixed at parity
        AddCurrency(state, 1, lenderRate);  // lender currency
        for (int i = 0; i < 2; i++)
        {
            state.Actors[i].Entered = true;
            state.PolityOf(i).CurrencyId = i;
        }
        state.WorldYear = 100;
        state.PolityOf(0).Credits = borrowerCredits;   // borrower solvent
        state.PolityOf(1).Credits = 0;                  // lender receiver
        state.Loans.Add(new Loan(0, lenderActorId: 1, borrowerActorId: 0,
            principal: principal, ratePerYear: 0.02,
            termYears: 100, issuedYear: 100));
        return state;
    }

    /// <summary>The core FX-risk behavior: the loan is denominated in the
    /// LENDER's currency, so the amortization payment is a fixed lender-currency
    /// amount. When the lender's currency strengthens (rate 1.0 → 2.0), that same
    /// fixed payment costs the borrower TWICE as much of its own currency — while
    /// the lender still banks the identical lender-currency sum. This is the
    /// design's whole rationale (FX risk sits with the borrower), demonstrated
    /// end-to-end through ServiceLoans, not inferred from a bare conversion call.</summary>
    [Fact]
    public void ServiceLoans_RateDrift_CostsTheBorrowerMoreOfItsOwnCurrency()
    {
        var parity = TwoPolityLoanStage(lenderRate: 1.0, principal: 100,
            borrowerCredits: 1_000_000);
        double bBefore = parity.PolityOf(0).Credits;
        double lBefore = parity.PolityOf(1).Credits;
        InvokeService(parity);
        double costParity = bBefore - parity.PolityOf(0).Credits;      // C0 spent
        double lenderGotParity = parity.PolityOf(1).Credits - lBefore; // C1 banked

        var drift = TwoPolityLoanStage(lenderRate: 2.0, principal: 100,
            borrowerCredits: 1_000_000);
        double bBefore2 = drift.PolityOf(0).Credits;
        double lBefore2 = drift.PolityOf(1).Credits;
        InvokeService(drift);
        double costDrift = bBefore2 - drift.PolityOf(0).Credits;
        double lenderGotDrift = drift.PolityOf(1).Credits - lBefore2;

        Assert.True(costParity > 0, "the parity loan actually serviced");
        // the lender received the IDENTICAL lender-currency payment both times
        Assert.Equal(lenderGotParity, lenderGotDrift, 9);
        // but the borrower paid exactly twice as much of its OWN currency after
        // the lender's currency doubled in strength — FX risk on the borrower
        Assert.Equal(costParity * 2.0, costDrift, 6);
    }

    /// <summary>Cross-currency servicing conserves value: the borrower's
    /// own-currency outflow is booked as C0 converted-out, the lender's payment as
    /// C1 converted-in, and the numeraire value leaving equals the value entering —
    /// the conversion transfers, it does not mint or burn.</summary>
    [Fact]
    public void ServiceLoans_CrossCurrency_ConservesValue_AndBooksTheConversion()
    {
        var state = TwoPolityLoanStage(lenderRate: 2.0, principal: 100,
            borrowerCredits: 1_000_000);
        var c0 = state.CurrencyOf(0);
        var c1 = state.CurrencyOf(1);
        c0.CumulativeConvertedIn = c0.CumulativeConvertedOut = 0;
        c1.CumulativeConvertedIn = c1.CumulativeConvertedOut = 0;
        double bBefore = state.PolityOf(0).Credits;
        double lBefore = state.PolityOf(1).Credits;
        double reserve1Before = state.BankOf(1).Reserve;

        InvokeService(state);

        double borrowerPaidOwn = bBefore - state.PolityOf(0).Credits;  // C0 out
        double lenderGot = state.PolityOf(1).Credits - lBefore;         // C1 in
        // Withdraw (slice CU-2) grosses the payer up and books the FULL
        // grossed transfer (payment + skim) as the paired conversion counters,
        // then sequesters the skim into the destination currency's
        // Bank.Reserve before the lender's Deposit ever sees it — so the
        // lender's actual receipt is the counter net of that reserve delta
        // (MetricsOps.cs authoritative residual balances Supply + Reserve).
        double skim1 = state.BankOf(1).Reserve - reserve1Before;
        Assert.Equal(borrowerPaidOwn, c0.CumulativeConvertedOut, 9);
        Assert.Equal(lenderGot + skim1, c1.CumulativeConvertedIn, 9);
        // conservation: numeraire value out of C0 == numeraire value landed in
        // C1 plus the numeraire value of the skim sequestered into its reserve
        Assert.Equal(borrowerPaidOwn * c0.NumeraireRate,
                     (lenderGot + skim1) * c1.NumeraireRate, 9);
    }

    /// <summary>At issuance a cross-currency loan denominates its DEBT in the
    /// lender's currency, while the borrower's CASH lands in its own currency: a
    /// polity lender fronts the lender-currency principal via Withdraw (the fixed
    /// polity path, no longer currency-blind arithmetic), the borrower banks the
    /// full own-currency proceeds via Deposit, and the single cross-currency
    /// transfer is booked on the paired counters.</summary>
    [Fact]
    public void Borrow_PolityLender_DenominatesDebtInLenderCurrency_CashInBorrowerCurrency()
    {
        // borrower C0 @1, lender C1 @2 (lender currency the stronger)
        var state = ThreePolityStage(r0: 1.0, r1: 2.0, r2: 1.0);
        state.PolityOf(0).Credits = -100;     // borrowerAmount = 120 (C0)
        state.PolityOf(1).Credits = 100000;   // rich lender in C1
        state.PolityOf(2).Credits = 0;
        var c0 = state.CurrencyOf(0);
        var c1 = state.CurrencyOf(1);
        c0.CumulativeConvertedIn = c0.CumulativeConvertedOut = 0;
        c1.CumulativeConvertedIn = c1.CumulativeConvertedOut = 0;
        double lenderBefore = state.PolityOf(1).Credits;

        int issued = InvokeBorrow(state);

        Assert.Equal(1, issued);
        var loan = state.Loans[0];
        Assert.Equal(1, loan.LenderActorId);
        // 120 C0 -> C1 at 1/2 = 60: the DEBT is lender-denominated
        Assert.Equal(60.0, loan.Principal, 9);
        // the borrower's CASH rose by the 120 converted, net of the spread
        // Deposit (slice CU-2) skims off the top into Bank(C0).Reserve before
        // crediting — the counter below still books the FULL 120 (Deposit's
        // SettleConversion records the gross transfer; only the credited net
        // is smaller, per MetricsOps.cs's reserve-aware residual)
        double spread = state.Config.Economy.ConversionSpread;
        Assert.Equal(-100 + 120.0 * (1 - spread), state.PolityOf(0).Credits, 9);
        // the lender fronted 60 of its OWN currency (Withdraw, same currency)
        Assert.Equal(lenderBefore - 60.0, state.PolityOf(1).Credits, 9);
        // one conversion booked: C1 out 60, C0 in 120
        Assert.Equal(60.0, c1.CumulativeConvertedOut, 9);
        Assert.Equal(120.0, c0.CumulativeConvertedIn, 9);
    }
}
