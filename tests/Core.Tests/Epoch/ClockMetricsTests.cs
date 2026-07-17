using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>The metrics the clock-invariance sweep reads (slice MC). The
/// nominal/real distinction is the sweep's entire analytical frame, so both
/// sides of it must be first-class registry metrics rather than something a
/// throwaway harness sums its own way: receipts (nominal, a per-epoch FLOW
/// that MarketsPhase zeroes every step) and ports (real, a level).
///
/// <para>Note genesis seeds NO ports — polities enter over the emergence
/// window — so every test here steps until the economy is live. Asserting
/// against a bare genesis state passes trivially (0 == 0) and measures
/// nothing.</para></summary>
public class ClockMetricsTests
{
    /// <summary>Step until the economy is actually trading, or give up.</summary>
    private static SimState Live()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        for (int i = 0; i < 40 && state.Markets.Count == 0; i++)
            engine.Step(state);
        Assert.True(state.Markets.Count > 0, "no market emerged to measure");
        return state;
    }

    [Fact]
    public void PolityReceipts_IsRegistered_AndSumsThisEpochsReceipts()
    {
        var state = Live();
        new EpochEngine().Step(state);
        double expected = 0;
        foreach (var pr in state.Polities) expected += pr.Receipts;
        Assert.Equal(expected,
            MetricRegistry.Find("Economy.PolityReceipts")!
                .Get(MetricsOps.Snapshot(state)), 9);
    }

    [Fact]
    public void CorpReceipts_IsRegistered_AndSumsThisEpochsCorpReceipts()
    {
        var state = Live();
        new EpochEngine().Step(state);
        double expected = 0;
        foreach (var c in state.Corporations) expected += c.Receipts;
        Assert.Equal(expected,
            MetricRegistry.Find("Economy.CorpReceipts")!
                .Get(MetricsOps.Snapshot(state)), 9);
    }

    /// <summary>Receipts are a per-epoch flow, NOT a running total — the sweep
    /// integrates the column over rows. If this ever became cumulative the
    /// sweep's Σ would silently square.</summary>
    [Fact]
    public void PolityReceipts_IsAFlow_ZeroedEachEpoch()
    {
        var state = Live();
        state.Polities[0].Receipts = 1e9;
        new EpochEngine().Step(state);
        Assert.True(
            MetricRegistry.Find("Economy.PolityReceipts")!
                .Get(MetricsOps.Snapshot(state)) < 1e9,
            "receipts must be zeroed each epoch, not carried");
    }

    [Fact]
    public void Ports_IsRegistered_AndCountsTheRealEconomysPorts()
    {
        var state = Live();
        Assert.Equal(state.Ports.Count,
            MetricRegistry.Find("Settlement.Ports")!
                .Get(MetricsOps.Snapshot(state)), 9);
        Assert.True(state.Ports.Count > 0);
    }
}
