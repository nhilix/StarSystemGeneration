using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 1: the currency data model and the currency-aware
/// ledger interface. A corporation's Credits is the numeraire-converted total
/// of its multi-currency wallet; Withdraw spends the matching bucket first then
/// walks the others in ascending currency-id order, converting the shortfall,
/// and caps at what the wallet holds. A polity is single-currency and
/// auto-converts on deposit/withdraw, going negative rather than capping (the
/// existing insolvency convention).</summary>
public class CurrencyLedgerTests
{
    private static SimState NewState() =>
        new SimState(new EpochSimConfig(),
            SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = 1, GalaxyRadiusCells = 4 }));

    private static Currency AddCurrency(SimState state, int id, double rate)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id) { NumeraireRate = rate };
        state.Currencies.Add(cur);
        return cur;
    }

    private static Corporation NewCorp(int id = 0) =>
        new Corporation(id, actorId: 100 + id, name: $"Corp{id}", hostPolityId: 0,
            CorporateNiche.Freight, homePortId: 0, foundedYear: 0);

    // ---- Corporation.Credits: numeraire sum across the wallet ----

    [Fact]
    public void CorpCredits_IsNumeraireSum_AcrossCurrenciesAtDifferentRates()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);
        AddCurrency(state, 2, 0.5);
        var corp = NewCorp();

        corp.Deposit(state, 10.0, 0);   // 10 * 1.0 = 10
        corp.Deposit(state, 5.0, 1);    //  5 * 2.0 = 10
        corp.Deposit(state, 8.0, 2);    //  8 * 0.5 =  4

        Assert.Equal(24.0, corp.Credits, 9);
        Assert.Equal(10.0, corp.Holdings[0], 9);
        Assert.Equal(5.0, corp.Holdings[1], 9);
        Assert.Equal(8.0, corp.Holdings[2], 9);
    }

    [Fact]
    public void CorpCredits_TracksRateChanges_AfterRefresh()
    {
        var state = NewState();
        var cur = AddCurrency(state, 0, 1.0);
        var corp = NewCorp();
        corp.Deposit(state, 10.0, 0);
        Assert.Equal(10.0, corp.Credits, 9);

        cur.NumeraireRate = 3.0;
        corp.RefreshNumeraire(state);
        Assert.Equal(30.0, corp.Credits, 9);
    }

    // ---- Corporation.Withdraw: draw-down rule ----

    [Fact]
    public void CorpWithdraw_DrainsMatchingBucketFirst_LeavingOthersUntouched()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 1.0);
        var corp = NewCorp();
        corp.Deposit(state, 10.0, 0);
        corp.Deposit(state, 10.0, 1);

        double provided = corp.Withdraw(state, 6.0, 0);

        Assert.Equal(6.0, provided, 9);
        Assert.Equal(4.0, corp.Holdings[0], 9);   // matching bucket debited
        Assert.Equal(10.0, corp.Holdings[1], 9);  // other bucket untouched
    }

    [Fact]
    public void CorpWithdraw_FallsBackAscendingCurrencyId_ConvertingShortfall()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);   // 1 unit of cur1 is worth 2 of cur0
        AddCurrency(state, 2, 1.0);
        var corp = NewCorp();
        corp.Deposit(state, 1.0, 0);   // matching bucket, insufficient
        corp.Deposit(state, 4.0, 1);   // ascending fallback reaches this FIRST
        corp.Deposit(state, 3.0, 2);   // ...and must NOT touch this one

        // Need 5 of cur0: 1 comes from the cur0 bucket (drained), the remaining
        // 4 comes from cur1 (id 1 before id 2). 4 of cur0 = 2 of cur1.
        double provided = corp.Withdraw(state, 5.0, 0);

        Assert.Equal(5.0, provided, 9);
        Assert.False(corp.Holdings.ContainsKey(0));   // matching bucket drained + removed
        Assert.Equal(2.0, corp.Holdings[1], 9);       // 4 - 2 spent
        Assert.Equal(3.0, corp.Holdings[2], 9);       // higher id never reached
    }

    [Fact]
    public void CorpWithdraw_CapsAtTotalAvailable_AndDrainsTheWallet()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 1.0);
        var corp = NewCorp();
        corp.Deposit(state, 2.0, 0);
        corp.Deposit(state, 3.0, 1);   // total value in cur0 terms = 5

        double provided = corp.Withdraw(state, 10.0, 0);

        Assert.Equal(5.0, provided, 9);          // capped at what it held
        Assert.Empty(corp.Holdings);             // every bucket drained + removed
        Assert.Equal(0.0, corp.Credits, 9);
    }

    // ---- Corporation legacy-bridge setter (transitional; keeps un-migrated
    //      write-sites behaving until later slice tasks move them) ----

    [Fact]
    public void CorpCredits_LegacySetter_BehavesLikeTheOldFieldWhenWalletEmpty()
    {
        var corp = NewCorp();
        corp.Credits += 50.0;
        corp.Credits -= 20.0;
        Assert.Equal(30.0, corp.Credits, 9);
        Assert.Empty(corp.Holdings);
    }

    // ---- PolityRecord: single-currency auto-conversion ----

    [Fact]
    public void PolityDeposit_AutoConvertsForeignCurrency_IntoOwn()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);
        var pol = new PolityRecord(actorId: 0, speciesId: 0) { CurrencyId = 0, Credits = 100.0 };

        pol.Deposit(state, 10.0, fromCurrencyId: 1);   // 10 cur1 -> 20 cur0

        Assert.Equal(120.0, pol.Credits, 9);
    }

    [Fact]
    public void PolityWithdraw_ConvertsIntoTarget_AndReturnsTargetAmount()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 2.0);
        var pol = new PolityRecord(0, 0) { CurrencyId = 0, Credits = 100.0 };

        double provided = pol.Withdraw(state, 5.0, toCurrencyId: 1);   // 5 cur1 costs 10 cur0

        Assert.Equal(5.0, provided, 9);
        Assert.Equal(90.0, pol.Credits, 9);
    }

    [Fact]
    public void PolityWithdraw_GoesNegative_NoCap()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        var pol = new PolityRecord(0, 0) { CurrencyId = 0, Credits = 1.0 };

        double provided = pol.Withdraw(state, 5.0, toCurrencyId: 0);

        Assert.Equal(5.0, provided, 9);
        Assert.Equal(-4.0, pol.Credits, 9);   // insolvency, not a cap (Borrow answers it)
    }

    // ---- ConvertCurrency primitive ----

    [Fact]
    public void ConvertCurrency_UsesNumeraireRatio_AndIsIdentityForSameCurrency()
    {
        var state = NewState();
        AddCurrency(state, 0, 1.0);
        AddCurrency(state, 1, 4.0);
        Assert.Equal(40.0, state.ConvertCurrency(10.0, 1, 0), 9);   // 10 * 4 / 1
        Assert.Equal(2.5, state.ConvertCurrency(10.0, 0, 1), 9);    // 10 * 1 / 4
        Assert.Equal(7.0, state.ConvertCurrency(7.0, 1, 1), 9);     // identity
    }
}
