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
                                     double fxReceiptsFloor = 1.0)
    {
        var cfg = new EpochSimConfig();
        cfg.Economy.FxSensitivity = fxSensitivity;
        cfg.Economy.FxReceiptsFloor = fxReceiptsFloor;
        return new SimState(cfg, SkeletonBuilder.Build(new GalaxyConfig
        { MasterSeed = 1, GalaxyRadiusCells = 4 }));
    }

    private static Currency AddCurrency(SimState state, int id, double supply)
    {
        var cur = new Currency(id, $"C{id}", foundingPolityId: id) { Supply = supply };
        state.Currencies.Add(cur);
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
