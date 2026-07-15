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
}
