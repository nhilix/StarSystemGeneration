using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>CSV rendering of the health series — the sweep runner's file
/// substance (sim-health spec §4). Invariant culture, registry-driven
/// columns, byte-identical for the same history.</summary>
public class MetricCsvTests
{
    [Fact]
    public void MetricsCsv_HeaderIsTheRegistry_OneRowPerEpoch()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        engine.Step(state);
        engine.Step(state);
        string csv = MetricCsv.RenderMetrics(state.Health);
        var lines = csv.TrimEnd('\n').Split('\n');
        Assert.Equal(3, lines.Length);   // header + 2 epochs
        string expectedHeader = "epoch,world_year";
        foreach (var m in MetricRegistry.All)
            expectedHeader += "," + m.Name;
        Assert.Equal(expectedHeader, lines[0]);
        Assert.StartsWith("0,", lines[1]);
        Assert.StartsWith("1,", lines[2]);
    }

    [Fact]
    public void PhasesCsv_OneRowPerPhasePerEpoch()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Step(state);
        string csv = MetricCsv.RenderPhases(state.Health);
        var lines = csv.TrimEnd('\n').Split('\n');
        Assert.Equal(8, lines.Length);   // header + 7 phases
        Assert.Equal("epoch,phase,polity_credits,polity_pools,corp_credits,"
            + "segment_wealth,faction_wealth,order_escrow,courier_escrow,"
            + "expedition_purses,loan_principal,supply", lines[0]);
        Assert.StartsWith("0,Perception,", lines[1]);
        Assert.StartsWith("0,Chronicle,", lines[7]);
    }

    [Fact]
    public void PolitiesCsv_HasTheNarrowTable()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Step(state);
        string csv = MetricCsv.RenderPolities(state.Health);
        var lines = csv.TrimEnd('\n').Split('\n');
        Assert.Equal("epoch,actor_id,credits,pools,population,mean_sol,"
            + "legitimacy", lines[0]);
        Assert.Equal(state.Health.PolityRows.Count, lines.Length - 1);
    }

    [Fact]
    public void RenderingIsByteIdenticalAcrossIdenticalRuns()
    {
        var (_, a) = EpochTestKit.Seeded();
        var (_, b) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        for (int i = 0; i < 3; i++) { engine.Step(a); engine.Step(b); }
        Assert.Equal(MetricCsv.RenderMetrics(a.Health),
                     MetricCsv.RenderMetrics(b.Health));
        Assert.Equal(MetricCsv.RenderPhases(a.Health),
                     MetricCsv.RenderPhases(b.Health));
        Assert.Equal(MetricCsv.RenderPolities(a.Health),
                     MetricCsv.RenderPolities(b.Health));
    }
}
