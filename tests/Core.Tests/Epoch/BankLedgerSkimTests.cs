using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-2 task 4a: the LEDGER-MEDIATED exchange sites
/// (<see cref="PolityRecord.Deposit"/>/<see cref="PolityRecord.Withdraw"/> and
/// the internal draw-down conversions in <see cref="Corporation.Withdraw"/>) now
/// route their real cross-currency conversion through
/// <see cref="SimState.SettleConversion"/>, which skims the spread into the
/// DESTINATION currency's <see cref="Bank.Reserve"/> and returns the net. The
/// skim is charged exactly once per real conversion, by the destination Bank,
/// deducted from what the destination holder receives/banks. Same-currency legs
/// never skim.</summary>
public class BankLedgerSkimTests
{
    private static SimState NewState() =>
        new SimState(new EpochSimConfig(),
            SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = 1, GalaxyRadiusCells = 4 }));

    private static PolityRecord AddPolity(SimState state, int id)
    {
        state.Actors.Add(new Actor(id, ActorKind.Polity, $"P{id}",
            new HexCoordinate(id, id), entryYear: 0,
            new GenesisController(state.Config)) { Entered = true });
        var pr = new PolityRecord(id, 0);
        state.Polities.Add(pr);
        return pr;
    }

    // FoundCurrency assigns the currency to the polity AND founds the parallel
    // Bank (SimState.FoundCurrency) — the live post-genesis shape every real
    // conversion site now relies on.
    private static Currency FoundCurrency(SimState state, int polityId, double rate)
    {
        var cur = state.FoundCurrency(polityId);
        cur.NumeraireRate = rate;
        return cur;
    }

    private static Corporation AddCorp(SimState state, int id = 0) =>
        new Corporation(id, actorId: 100 + id, name: $"Corp{id}", hostPolityId: 0,
            CorporateNiche.Freight, homePortId: 0, foundedYear: 0);

    private static double Spread(SimState state) =>
        state.Config.Economy.ConversionSpread;

    // ---- PolityRecord.Deposit: a foreign receipt banks NET, own Bank keeps skim ----

    [Fact]
    public void PolityDeposit_ForeignCurrency_BanksNet_OwnBankKeepsSkim()
    {
        var state = NewState();
        var p0 = AddPolity(state, 0);
        AddPolity(state, 1);
        var own = FoundCurrency(state, 0, 1.0);   // this polity's currency
        var foreign = FoundCurrency(state, 1, 2.0);
        p0.Credits = 100.0;

        double gross = state.ConvertCurrency(10.0, foreign.Id, own.Id); // 20 own
        double skim = gross * Spread(state);                            // 20 * 0.005

        double banked = p0.Deposit(state, 10.0, foreign.Id);

        // the treasury credits and RETURNS the net (the Receipts mirror is honest)
        Assert.Equal(gross - skim, banked, 12);
        Assert.Equal(100.0 + (gross - skim), p0.Credits, 12);
        // the skim sits in THIS polity's own-currency Bank reserve
        Assert.Equal(skim, state.BankOf(own.Id).Reserve, 12);
        Assert.Equal(0.0, state.BankOf(foreign.Id).Reserve, 12);
        // the counters still record the FULL amounts (spread is reserve-side)
        Assert.Equal(10.0, foreign.CumulativeConvertedOut, 12);
        Assert.Equal(gross, own.CumulativeConvertedIn, 12);
    }

    [Fact]
    public void PolityDeposit_SameCurrency_BanksFull_NoSkim()
    {
        var state = NewState();
        var p0 = AddPolity(state, 0);
        var own = FoundCurrency(state, 0, 1.0);
        p0.Credits = 100.0;

        double banked = p0.Deposit(state, 30.0, own.Id);

        Assert.Equal(30.0, banked, 12);
        Assert.Equal(130.0, p0.Credits, 12);
        Assert.Equal(0.0, state.BankOf(own.Id).Reserve, 12);
    }

    // ---- PolityRecord.Withdraw: providing a foreign currency delivers NET ----

    [Fact]
    public void PolityWithdraw_ForeignCurrency_ProvidesFull_PayerBearsSkim_PayeeBankKeepsSkim()
    {
        var state = NewState();
        var p0 = AddPolity(state, 0);
        AddPolity(state, 1);
        var own = FoundCurrency(state, 0, 1.0);
        var payee = FoundCurrency(state, 1, 2.0);
        p0.Credits = 100.0;

        // provide the full 5 of the payee currency: the payer sources 5 + skim of
        // the payee currency (5*2 own-cost for the payee, plus the skim grossed on
        // top), so the payee stays whole and the payer bears the spread.
        double skim = 5.0 * Spread(state);                 // 5 * 0.005, in payee units
        double grossTo = 5.0 + skim;                       // total payee currency sourced
        double ownCostGross = state.ConvertCurrency(grossTo, payee.Id, own.Id); // *2

        double provided = p0.Withdraw(state, 5.0, payee.Id);

        // the payee is paid IN FULL — the return is the requested amount
        Assert.Equal(5.0, provided, 12);
        // the payer bears the grossed cost (amount + skim, converted)
        Assert.Equal(100.0 - ownCostGross, p0.Credits, 12);
        // the skim lands in the PAYEE currency's Bank, not the polity's own
        Assert.Equal(skim, state.BankOf(payee.Id).Reserve, 12);
        Assert.Equal(0.0, state.BankOf(own.Id).Reserve, 12);
        // the FULL grossed transfer is booked: own currency out, payee currency in
        Assert.Equal(ownCostGross, own.CumulativeConvertedOut, 12);
        Assert.Equal(grossTo, payee.CumulativeConvertedIn, 12);
    }

    [Fact]
    public void PolityWithdraw_SameCurrency_ProvidesFull_NoSkim()
    {
        var state = NewState();
        var p0 = AddPolity(state, 0);
        var own = FoundCurrency(state, 0, 1.0);
        p0.Credits = 100.0;

        double provided = p0.Withdraw(state, 40.0, own.Id);

        Assert.Equal(40.0, provided, 12);
        Assert.Equal(60.0, p0.Credits, 12);
        Assert.Equal(0.0, state.BankOf(own.Id).Reserve, 12);
    }

    // ---- Corporation.Withdraw: each cross-currency bucket conversion skims once ----

    [Fact]
    public void CorpWithdraw_MatchingBucket_NoConversion_NoSkim()
    {
        var state = NewState();
        AddPolity(state, 0);
        AddPolity(state, 1);
        var c0 = FoundCurrency(state, 0, 1.0);
        FoundCurrency(state, 1, 1.0);
        var corp = AddCorp(state);
        corp.Deposit(state, 10.0, c0.Id);
        corp.Deposit(state, 10.0, 1);

        double provided = corp.Withdraw(state, 6.0, c0.Id);

        // the matching bucket covers it with no conversion — no skim anywhere
        Assert.Equal(6.0, provided, 12);
        Assert.Equal(4.0, corp.Holdings[c0.Id], 12);
        Assert.Equal(10.0, corp.Holdings[1], 12);
        Assert.Equal(0.0, state.BankOf(c0.Id).Reserve, 12);
    }

    [Fact]
    public void CorpWithdraw_WholeBucketConsumed_GrossUpSplitsPayeeAndSkim()
    {
        var state = NewState();
        AddPolity(state, 0);
        AddPolity(state, 1);
        var c0 = FoundCurrency(state, 0, 1.0);   // pay-out currency
        var c1 = FoundCurrency(state, 1, 2.0);   // 1 C1 worth 2 C0
        var corp = AddCorp(state);
        corp.Deposit(state, 3.0, c1.Id);   // ONLY C1: worth 6 in C0

        // ask for 6 of C0 but the wallet holds only 6 C0-worth: the whole C1 bucket
        // converts to valueInTo=6, which the gross-up splits into the payee
        // contribution (6/(1+spread)) and the skim on top — so the corp provides
        // less than the full 6 (capped by the wallet, since delivering 6 net would
        // need 6*(1+spread) of C0-worth it does not hold).
        double gross = state.ConvertCurrency(3.0, c1.Id, c0.Id);   // 6
        double pcontrib = gross / (1.0 + Spread(state));
        double skim = gross - pcontrib;

        double provided = corp.Withdraw(state, 6.0, c0.Id);

        Assert.Equal(pcontrib, provided, 12);
        Assert.False(corp.Holdings.ContainsKey(c1.Id));   // whole bucket drained
        Assert.Equal(skim, state.BankOf(c0.Id).Reserve, 12);
        // the FULL bucket drain is booked exactly: 3 C1 out, 6 C0 in
        Assert.Equal(3.0, c1.CumulativeConvertedOut, 12);
        Assert.Equal(gross, c0.CumulativeConvertedIn, 12);
    }

    [Fact]
    public void CorpWithdraw_PartialBucket_ProvidesFull_SkimsOnce_LeavesHigherIdUntouched()
    {
        var state = NewState();
        AddPolity(state, 0);
        AddPolity(state, 1);
        AddPolity(state, 2);
        var c0 = FoundCurrency(state, 0, 1.0);
        var c1 = FoundCurrency(state, 1, 2.0);   // 1 C1 worth 2 C0
        var c2 = FoundCurrency(state, 2, 1.0);
        var corp = AddCorp(state);
        corp.Deposit(state, 1.0, c0.Id);   // matching, insufficient
        corp.Deposit(state, 4.0, c1.Id);   // ascending fallback reaches this FIRST
        corp.Deposit(state, 3.0, c2.Id);   // ...and must NOT be touched

        // need 5 of C0: 1 from the C0 bucket, the remaining 4 from C1 (partial).
        // Gross-up: the payee gets the full 4, plus skim=4*spread grossed on top;
        // the C1 bucket has ample room so it is NOT fully drained and the C2 bucket
        // is never reached (no second skim). grossTo=4+skim of C0 = (4+skim)/2 C1.
        double skim = 4.0 * Spread(state);
        double grossTo = 4.0 + skim;
        double spendC1 = state.ConvertCurrency(grossTo, c0.Id, c1.Id);   // /2

        double provided = corp.Withdraw(state, 5.0, c0.Id);

        Assert.Equal(5.0, provided, 12);                     // payee paid in full
        Assert.False(corp.Holdings.ContainsKey(c0.Id));      // matching drained
        Assert.Equal(4.0 - spendC1, corp.Holdings[c1.Id], 12);
        Assert.Equal(3.0, corp.Holdings[c2.Id], 12);         // higher id NEVER reached
        Assert.Equal(skim, state.BankOf(c0.Id).Reserve, 12);
        // exactly one conversion recorded (C1 out spendC1, C0 in grossTo) — the C2
        // bucket did not convert, so no second skim
        Assert.Equal(spendC1, c1.CumulativeConvertedOut, 12);
        Assert.Equal(grossTo, c0.CumulativeConvertedIn, 12);
        Assert.Equal(0.0, c2.CumulativeConvertedOut, 12);
    }
}
