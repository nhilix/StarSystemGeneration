using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The engine feeds the in-memory health series every step: a
/// money row after every phase (the phase-attribution instrument), the
/// full metric row and per-polity rows after Chronicle. Never serialized —
/// a loaded artifact starts empty and accumulates as it steps.</summary>
public class HealthSeriesTests
{
    [Fact]
    public void SteppingAccumulatesMoneyRowsPerPhaseAndMetricRowsPerEpoch()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        engine.Step(state);
        engine.Step(state);

        Assert.Equal(14, state.Health.MoneyRows.Count);   // 7 phases × 2
        Assert.Equal("Perception", state.Health.MoneyRows[0].Phase);
        Assert.Equal("Chronicle", state.Health.MoneyRows[6].Phase);
        Assert.Equal(0, state.Health.MoneyRows[6].Epoch);
        Assert.Equal(1, state.Health.MoneyRows[7].Epoch);

        Assert.Equal(2, state.Health.Rows.Count);
        Assert.Equal(0, state.Health.Rows[0].Epoch);
        Assert.Equal(1, state.Health.Rows[1].Epoch);
    }

    [Fact]
    public void PolityRowsLandOncePerEpochPerEnteredPolity()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Step(state);
        foreach (var r in state.Health.PolityRows)
            Assert.Equal(0, r.Epoch);
        // count matches the live-polity count the epoch row saw
        Assert.Equal(state.Health.Rows[0].LivePolities,
                     state.Health.PolityRows.Count);
    }

    [Fact]
    public void TheSeriesIsNotSerialized()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Step(state);
        Assert.NotEmpty(state.Health.Rows);
        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        Assert.Empty(loaded.Health.Rows);
        Assert.Empty(loaded.Health.MoneyRows);
    }
}
