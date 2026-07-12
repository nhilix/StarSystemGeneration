using System.Collections.Generic;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The metric registry is the single index of every macro
/// observation (sim-health spec §1): name → doc → accessor over a
/// MetricRow. It drives the sweep CSV columns, the REPL `ehealth` readout,
/// and docs/SIMHEALTH.md — a metric must never exist outside it.</summary>
public class MetricRegistryTests
{
    [Fact]
    public void Names_AreUnique_Sorted_AndDocumented()
    {
        string? previous = null;
        var seen = new HashSet<string>();
        foreach (var m in MetricRegistry.All)
        {
            Assert.True(seen.Add(m.Name), $"duplicate metric '{m.Name}'");
            Assert.False(string.IsNullOrWhiteSpace(m.Doc),
                $"metric '{m.Name}' lacks documentation");
            Assert.Contains(".", m.Name);   // Family.Name convention
            if (previous != null)
                Assert.True(string.CompareOrdinal(previous, m.Name) < 0,
                    $"registry must be name-sorted: '{previous}' >= '{m.Name}'");
            previous = m.Name;
        }
    }

    [Fact]
    public void EveryMetric_ReadsFiniteFromARealSnapshot()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Step(state);
        var row = MetricsOps.Snapshot(state);
        foreach (var m in MetricRegistry.All)
            Assert.True(double.IsFinite(m.Get(row)),
                $"metric '{m.Name}' not finite on a real snapshot");
    }

    [Fact]
    public void TheMoneyVocabulary_IsCovered()
    {
        Assert.NotNull(MetricRegistry.Find("Money.Supply"));
        Assert.NotNull(MetricRegistry.Find("Money.PolityCredits"));
        Assert.NotNull(MetricRegistry.Find("Money.OrderEscrow"));
        Assert.NotNull(MetricRegistry.Find("Money.LoanPrincipal"));
        Assert.NotNull(MetricRegistry.Find("Polity.NegativeTreasuries"));
        Assert.Null(MetricRegistry.Find("Money.Nonsense"));
    }

    [Fact]
    public void Accessors_ReadTheRowTheyAreNamedFor()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Polities[0].Credits += 123.0;
        var row = MetricsOps.Snapshot(state);
        Assert.Equal(row.Money.PolityCredits,
            MetricRegistry.Find("Money.PolityCredits")!.Get(row), 9);
        Assert.Equal(row.Money.Supply,
            MetricRegistry.Find("Money.Supply")!.Get(row), 9);
        Assert.Equal(row.Population,
            MetricRegistry.Find("Segment.Population")!.Get(row), 9);
    }
}
