using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 2: the once-per-epoch FX-rate recompute
/// (<see cref="FxOps.RecomputeRates"/>). Every currency's numeraire rate is a
/// money-per-output density over its own Supply and its issuing polity's
/// Receipts, receipts floored by Economy.FxReceiptsFloor and reactivity scaled
/// by Economy.FxSensitivity. The pass is a pure formula: the same state
/// recomputed twice is byte-identical, and a near-zero-receipts currency neither
/// divides by zero nor blows up.</summary>
public class FxRateTests
{
    private static SimState NewState(double fxSensitivity = 1.0,
                                     double fxReceiptsFloor = 1.0,
                                     double fxBackingSensitivity = 0.0)
    {
        var cfg = new EpochSimConfig();
        cfg.Economy.FxSensitivity = fxSensitivity;
        cfg.Economy.FxReceiptsFloor = fxReceiptsFloor;
        cfg.Economy.FxBackingSensitivity = fxBackingSensitivity;
        return new SimState(cfg, SkeletonBuilder.Build(new GalaxyConfig
        { MasterSeed = 1, GalaxyRadiusCells = 4 }));
    }

    // Founds the currency 1:1 with its bank, exactly as SimState.FoundCurrency
    // does in a live run — every currency in the registry has a bank, so the
    // FX pass can read BankOf(cur.Id) safely (slice BF design §5). The bank
    // starts empty (ClaimOnState = Reserve = 0), so the backing term is 0 and
    // the rate is unchanged unless a test seeds the claim book.
    private static Currency AddCurrency(SimState state, int id, double supply)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id) { Supply = supply };
        state.Currencies.Add(cur);
        state.Banks.Add(new Bank(id));
        return cur;
    }

    private static PolityRecord AddPolity(SimState state, int actorId,
                                          int currencyId, double receipts)
    {
        var pr = new PolityRecord(actorId, speciesId: 0)
        { CurrencyId = currencyId, Receipts = receipts };
        state.Polities.Add(pr);
        return pr;
    }

    // ---- the density formula: more supply-per-output => weaker currency ----

    [Fact]
    public void TwoCurrencies_DifferentSupplyAndReceipts_GetDifferentRates()
    {
        var state = NewState();
        var strong = AddCurrency(state, 0, supply: 100.0);
        var weak = AddCurrency(state, 1, supply: 400.0);
        AddPolity(state, actorId: 0, currencyId: 0, receipts: 100.0);  // density 1
        AddPolity(state, actorId: 1, currencyId: 1, receipts: 100.0);  // density 4

        FxOps.RecomputeRates(state);

        // density0 = 100/100 = 1 -> 1/(1+1) = 0.5 ; density1 = 400/100 = 4 -> 1/5 = 0.2
        Assert.Equal(0.5, strong.NumeraireRate, 9);
        Assert.Equal(0.2, weak.NumeraireRate, 9);
        Assert.True(strong.NumeraireRate > weak.NumeraireRate,
            "the currency with more supply per unit output is weaker");
    }

    [Fact]
    public void MoreRealOutput_StrengthensACurrency_AtEqualSupply()
    {
        var state = NewState();
        var poor = AddCurrency(state, 0, supply: 200.0);
        var rich = AddCurrency(state, 1, supply: 200.0);
        AddPolity(state, 0, 0, receipts: 100.0);   // density 2
        AddPolity(state, 1, 1, receipts: 400.0);   // density 0.5

        FxOps.RecomputeRates(state);

        Assert.True(rich.NumeraireRate > poor.NumeraireRate,
            "same supply, more output backing => stronger currency");
    }

    [Fact]
    public void ZeroSupplyCurrency_SitsAtParity()
    {
        var state = NewState();
        var fresh = AddCurrency(state, 0, supply: 0.0);
        AddPolity(state, 0, 0, receipts: 250.0);

        FxOps.RecomputeRates(state);

        Assert.Equal(1.0, fresh.NumeraireRate, 9);   // matches the founding rate
    }

    // ---- the receipts floor: near-zero output does not blow up or divide ----

    [Fact]
    public void ZeroReceipts_UsesTheFloor_NoDivideByZeroOrBlowUp()
    {
        var state = NewState(fxSensitivity: 1.0, fxReceiptsFloor: 1.0);
        var cur = AddCurrency(state, 0, supply: 50.0);
        AddPolity(state, 0, 0, receipts: 0.0);       // freshly split: no output yet

        FxOps.RecomputeRates(state);

        // density = 50 / max(0, 1) = 50 -> 1/(1+50) ; finite, positive, not NaN/Inf
        double expected = 1.0 / (1.0 + 50.0);
        Assert.Equal(expected, cur.NumeraireRate, 12);
        Assert.True(cur.NumeraireRate > 0.0);
        Assert.False(double.IsNaN(cur.NumeraireRate));
        Assert.False(double.IsInfinity(cur.NumeraireRate));
    }

    [Fact]
    public void CurrencyWithNoIssuingPolity_FloorsReceipts_StaysFinite()
    {
        // a Retired currency whose founder was absorbed: no live polity mints it,
        // so no receipts entry exists — it must still resolve, floored.
        var state = NewState();
        var orphan = AddCurrency(state, 0, supply: 10.0);
        // deliberately add no polity for currency 0

        FxOps.RecomputeRates(state);

        double expected = 1.0 / (1.0 + 10.0 / 1.0);
        Assert.Equal(expected, orphan.NumeraireRate, 12);
    }

    // ---- knob wiring ----

    [Fact]
    public void FxSensitivityZero_PinsEveryRateAtParity()
    {
        var state = NewState(fxSensitivity: 0.0);
        var a = AddCurrency(state, 0, supply: 100.0);
        var b = AddCurrency(state, 1, supply: 999.0);
        AddPolity(state, 0, 0, receipts: 1.0);
        AddPolity(state, 1, 1, receipts: 1.0);

        FxOps.RecomputeRates(state);

        Assert.Equal(1.0, a.NumeraireRate, 9);
        Assert.Equal(1.0, b.NumeraireRate, 9);
    }

    [Fact]
    public void HigherFxSensitivity_WidensTheSpreadFromParity()
    {
        Currency Recompute(double sensitivity)
        {
            var state = NewState(fxSensitivity: sensitivity);
            var cur = AddCurrency(state, 0, supply: 100.0);
            AddPolity(state, 0, 0, receipts: 100.0);   // density 1
            FxOps.RecomputeRates(state);
            return cur;
        }

        double gentle = Recompute(0.5).NumeraireRate;   // 1/1.5
        double sharp = Recompute(2.0).NumeraireRate;    // 1/3
        Assert.True(sharp < gentle,
            "a higher sensitivity reacts more sharply to the same density");
    }

    // ---- the FX backing term: unbacked claim weighs on the rate (slice BF §5) ----

    [Fact]
    public void UnbackedClaim_LowersTheRate_WhenBackingSensitivityIsPositive()
    {
        // Same state, same claim book, evaluated at backing sensitivity 0 vs > 0.
        // The bank holds a claim well above its reserve, so unbacked > 0 and the
        // term must bite: the >0 case gets a strictly LOWER rate than the 0 case.
        double RateAtBacking(double backing)
        {
            var state = NewState(fxSensitivity: 1.0, fxBackingSensitivity: backing);
            var cur = AddCurrency(state, 0, supply: 100.0);
            AddPolity(state, 0, 0, receipts: 100.0);
            var bank = state.BankOf(0);
            bank.ClaimOnState = 300.0;
            bank.Reserve = 50.0;                       // unbacked = 250
            FxOps.RecomputeRates(state);
            return cur.NumeraireRate;
        }

        double inert = RateAtBacking(0.0);             // effectiveMoney = Supply
        double biting = RateAtBacking(0.5);            // + 0.5 * 250 = 125 extra

        // inert: density 100/100 = 1 -> 1/(1+1) = 0.5 (the CU-2 value)
        Assert.Equal(0.5, inert, 12);
        // biting: effectiveMoney 225, density 2.25 -> 1/3.25
        Assert.Equal(1.0 / (1.0 + 2.25), biting, 12);
        Assert.True(biting < inert,
            "an unbacked claim book must weigh the currency down once the knob is on");
    }

    [Fact]
    public void FullyBackedBank_IsUnaffected_ByBackingSensitivity()
    {
        // Reserve >= ClaimOnState => unbacked clamps to 0, so even a large
        // backing sensitivity leaves the rate at its supply-only value.
        var state = NewState(fxSensitivity: 1.0, fxBackingSensitivity: 5.0);
        var cur = AddCurrency(state, 0, supply: 100.0);
        AddPolity(state, 0, 0, receipts: 100.0);
        var bank = state.BankOf(0);
        bank.ClaimOnState = 200.0;
        bank.Reserve = 200.0;                          // exactly backed -> unbacked 0

        FxOps.RecomputeRates(state);

        Assert.Equal(0.5, cur.NumeraireRate, 12);      // unchanged from supply alone
    }

    [Fact]
    public void EmptyClaimBook_AtDefaultKnob_IsByteIdenticalToSupplyOnlyRate()
    {
        // The default landing: FxBackingSensitivity = 0 and an empty claim book
        // both give effectiveMoney == Supply. Prove the injected term does not
        // perturb a single bit of the rate versus the pure supply/output form.
        var state = NewState(fxSensitivity: 1.0);      // backing 0 by default
        var cur = AddCurrency(state, 0, supply: 137.0);
        AddPolity(state, 0, 0, receipts: 91.0);

        FxOps.RecomputeRates(state);

        double expected = 1.0 / (1.0 + 1.0 * (137.0 / 91.0));
        Assert.Equal(
            System.BitConverter.DoubleToInt64Bits(expected),
            System.BitConverter.DoubleToInt64Bits(cur.NumeraireRate));
    }

    // ---- corporations refresh against the new table ----

    [Fact]
    public void RecomputeRates_RefreshesCorporationWalletTotals()
    {
        var state = NewState();
        AddCurrency(state, 0, supply: 100.0);
        AddPolity(state, 0, 0, receipts: 100.0);       // rate will become 0.5
        var corp = new Corporation(0, actorId: 100, name: "Corp", hostPolityId: 0,
            CorporateNiche.Freight, homePortId: 0, foundedYear: 0);
        state.Corporations.Add(corp);
        corp.Deposit(state, 40.0, 0);                  // banked at the founding rate 1.0
        Assert.Equal(40.0, corp.Credits, 9);

        FxOps.RecomputeRates(state);

        // rate dropped to 0.5, so the cached numeraire total must track it
        Assert.Equal(20.0, corp.Credits, 9);
    }

    // ---- determinism: the same state recomputed twice is byte-identical ----

    [Fact]
    public void SameState_RecomputedTwice_IsByteIdentical()
    {
        long[] Run()
        {
            var state = NewState();
            for (int i = 0; i < 4; i++)
            {
                AddCurrency(state, i, supply: 37.0 * (i + 1) + 5.0);
                AddPolity(state, i, i, receipts: 13.0 * (i + 2));
            }
            FxOps.RecomputeRates(state);
            var bits = new long[state.Currencies.Count];
            for (int i = 0; i < state.Currencies.Count; i++)
                bits[i] = System.BitConverter.DoubleToInt64Bits(
                    state.Currencies[i].NumeraireRate);
            return bits;
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void RecomputeRates_OnEmptyRegistry_IsANoOp()
    {
        var state = NewState();
        FxOps.RecomputeRates(state);   // no currencies, no polities, no corps
        Assert.Empty(state.Currencies);
    }
}
