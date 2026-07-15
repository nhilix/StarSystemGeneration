using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice CU-1 task 9, deliverable 1: the end-of-epoch pass that WRITES
/// <see cref="Currency.Supply"/> back onto the live record, so the next epoch's
/// <see cref="FxOps.RecomputeRates"/> reads a fresh, diverging value. Before
/// this pass existed, Supply was permanently 0 and every NumeraireRate was
/// pinned at exactly 1.0 (identity conversions everywhere). These tests are the
/// acceptance bar: rates must genuinely diverge in an ordinary full-history run,
/// not just in a hand-constructed unit test.</summary>
public class CurrencySupplyTests
{
    [Fact]
    public void SupplyIsWrittenNonZeroAcrossAFullRun()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);

        Assert.NotEmpty(state.Currencies);
        // at least one living currency carries a real, positive supply
        bool anyPositive = false;
        foreach (var cur in state.Currencies)
            if (cur.Supply > 0) { anyPositive = true; break; }
        Assert.True(anyPositive,
            "no currency ever had its Supply written — the FX pass stays dormant");
    }

    [Fact]
    public void NumeraireRateDivergesFromOneInARealRun()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);

        // the acceptance bar: rates actually move off 1.0 through a real
        // gameplay path, and different currencies land at different rates
        int offParity = 0;
        var distinct = new HashSet<double>();
        foreach (var cur in state.Currencies)
        {
            distinct.Add(cur.NumeraireRate);
            if (Math.Abs(cur.NumeraireRate - 1.0) > 1e-9) offParity++;
        }
        Assert.True(offParity > 0,
            "every NumeraireRate is still 1.0 — Supply never reached FxOps");
        Assert.True(distinct.Count > 1,
            "all currencies share one rate — no genuine FX divergence");
    }

    [Fact]
    public void SupplyChangesEpochToEpochAsMoneyMovesAndMints()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        // warm up so currencies exist and the economy is live
        for (int i = 0; i < 6; i++) engine.Step(state);
        Assert.NotEmpty(state.Currencies);

        var before = new Dictionary<int, double>();
        foreach (var cur in state.Currencies) before[cur.Id] = cur.Supply;

        for (int i = 0; i < 4; i++) engine.Step(state);

        bool anyChanged = false;
        foreach (var cur in state.Currencies)
            if (!before.TryGetValue(cur.Id, out double was)
                || Math.Abs(cur.Supply - was) > 1e-9)
            { anyChanged = true; break; }
        Assert.True(anyChanged,
            "Supply is frozen across epochs — mints and trade aren't reaching it");
    }
}
