using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Money conservation (sim-health spec §2): the supply grows only
/// through the three declared mints — the entry endowment, bounded reactive
/// sovereign issuance (monetary-equilibrium design §5), and the always-on
/// steady issuance channel (Part B) — and every other flow is a move between
/// holder classes. The residual metric is the leak detector; this test is the
/// contract that it reads zero (against the widened formula, now netting all
/// three mints) on a real history — the steady mint fires every epoch, so this
/// would go red if the residual formula failed to net it.</summary>
public class ConservationTests
{
    [Fact]
    public void ResidualIsZeroAcrossAFullSeed42History()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);

        Assert.True(state.Health.Rows.Count >= 10, "history too short");
        for (int i = 1; i < state.Health.Rows.Count; i++)
        {
            var row = state.Health.Rows[i];
            double scale = System.Math.Max(1.0,
                System.Math.Abs(row.Money.Supply));
            Assert.True(
                System.Math.Abs(row.ConservationResidual) <= 1e-6 * scale,
                $"epoch {row.Epoch}: residual {row.ConservationResidual:G6} "
                + $"on supply {row.Money.Supply:G6} — an unknown mint or leak");
        }
    }

    [Fact]
    public void FirstRowResidualIsDefinedZero()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Step(state);
        Assert.Equal(0.0, state.Health.Rows[0].ConservationResidual, 9);
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
