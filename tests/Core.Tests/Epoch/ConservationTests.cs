using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Per-currency money conservation (currency-and-FX design, slice
/// CU-1 task 9, deliverable 2). Each <see cref="Currency"/> grows only through
/// its own declared mints — sovereign and steady issuance INTO that currency —
/// while every cross-currency conversion is a transfer that nets out across the
/// CumulativeConvertedIn/Out pair. The old single lump supply number is gone as
/// an invariant: with real FX rates now live, summing native amounts across
/// currencies is not a conserved quantity. The invariant is N per-currency
/// residuals, each reading zero on a real history — the leak detector, now
/// strictly more precise. <see cref="MetricRow.ConservationResidual"/> is the
/// worst per-currency residual, so a single assertion still gates the galaxy.</summary>
public class ConservationTests
{
    // Task 8 (full cross-currency movement audit) routed every raw 1:1
    // cross-currency transfer through ConvertCurrency + RecordConversion — the
    // migration gradient path, the construction-wage channel, and the three
    // port-ownership-change seams (federation/absorption, war capture,
    // secession) — closing the pre-existing, lump-hidden leak class this
    // per-currency residual was the first tool precise enough to see (task 9
    // report, .superpowers/sdd/task-9-report.md). This test is that fix's
    // acceptance bar and now passes un-skipped at ME's validated tolerance.
    [Fact]
    public void EveryPerCurrencyResidualIsZeroAcrossAFullSeed42History()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);

        Assert.True(state.Health.Rows.Count >= 10, "history too short");
        for (int i = 1; i < state.Health.Rows.Count; i++)
        {
            var row = state.Health.Rows[i];
            foreach (var cur in row.Currencies)
            {
                // the tolerance ME validated (1.3e-9), scaled by the currency's
                // own supply so a large book isn't held to an absolute epsilon
                double scale = System.Math.Max(1.0, System.Math.Abs(cur.Supply));
                Assert.True(
                    System.Math.Abs(cur.Residual) <= 1.3e-9 * scale,
                    $"epoch {row.Epoch} currency {cur.CurrencyId}: residual "
                    + $"{cur.Residual:G6} on supply {cur.Supply:G6} — an unknown "
                    + "mint or leak in this currency");
            }
        }
    }

    // Same fix as above — the roll-up (worst per-currency residual) is zero once
    // every cross-currency transfer is recorded (task 8).
    [Fact]
    public void WorstResidualRollUpIsZeroAcrossAFullSeed42History()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);

        Assert.True(state.Health.Rows.Count >= 10, "history too short");
        for (int i = 1; i < state.Health.Rows.Count; i++)
        {
            var row = state.Health.Rows[i];
            double scale = 1.0;
            foreach (var cur in row.Currencies)
                scale = System.Math.Max(scale, System.Math.Abs(cur.Supply));
            Assert.True(
                System.Math.Abs(row.ConservationResidual) <= 1.3e-9 * scale,
                $"epoch {row.Epoch}: worst per-currency residual "
                + $"{row.ConservationResidual:G6} — an unknown mint or leak");
        }
    }

    [Fact]
    public void FirstRowResidualIsDefinedZero()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Step(state);
        // every currency is founded this first epoch — no baseline exists yet,
        // so each residual (and thus the roll-up) is defined 0
        Assert.Equal(0.0, state.Health.Rows[0].ConservationResidual, 9);
        foreach (var cur in state.Health.Rows[0].Currencies)
            Assert.Equal(0.0, cur.Residual, 9);
    }

    [Fact]
    public void EndowedEntriesCountsEmergenceEvents()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        engine.Step(state);
        engine.Step(state);
        int emerged = 0;
        foreach (var e in state.Log.Events)
            if (e.Type == WorldEventType.PolityEmerged) emerged++;
        Assert.Equal(emerged,
            state.Health.Rows[^1].EndowedEntries);
    }
}
