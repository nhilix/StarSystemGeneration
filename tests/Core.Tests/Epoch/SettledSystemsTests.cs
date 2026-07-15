using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class SettledSystemsTests
{
    [Fact]
    public void Commit_IsIdempotent_ReturnsSameFrozenSystem()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        var first = SystemRegistry.Commit(state, hex);
        var second = SystemRegistry.Commit(state, hex);
        Assert.True(SystemRegistry.IsSettled(state, hex));
        Assert.Same(first, second);          // memoized, not regenerated
    }

    [Fact]
    public void Commit_MatchesFreshGeneratorOutput()
    {
        var (skeleton, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        var context = new StarGen.Core.Galaxy.GalaxyContext(skeleton.Config)
        { Skeleton = skeleton };
        var fresh = StarGen.Core.Generation.Generator.Generate(context, hex).System;
        var committed = SystemRegistry.Commit(state, hex);
        // deterministic pure function: same star count, same designation
        Assert.Equal(fresh?.Stars.Count ?? 0, committed?.Stars.Count ?? 0);
        Assert.Equal(fresh?.Designation, committed?.Designation);
    }

    [Fact]
    public void SettledSet_RoundTrips_AndReDerivesSystems()
    {
        var (_, state) = EpochTestKit.Seeded();
        var hex = state.Actors[0].Seat;
        SystemRegistry.Commit(state, hex);

        var text1 = ArtifactSerializer.ToText(state);
        var reloaded = ArtifactSerializer.Load(new StringReader(text1));
        var text2 = ArtifactSerializer.ToText(reloaded);

        Assert.Equal(text1, text2);
        Assert.True(SystemRegistry.IsSettled(reloaded, hex));
    }

    [Fact]
    public void SettledHexesMetric_CountsCommittedHexes()
    {
        var (_, state) = EpochTestKit.Seeded();
        Assert.NotNull(MetricRegistry.Find("Settlement.SettledHexes"));
        SystemRegistry.Commit(state, state.Actors[0].Seat);
        SystemRegistry.Commit(state,
            new StarGen.Core.Model.HexCoordinate(
                state.Actors[0].Seat.Q + 3, state.Actors[0].Seat.R));
        var row = MetricsOps.Snapshot(state);
        Assert.Equal(2, row.SettledHexes);
        Assert.Equal(2.0,
            MetricRegistry.Find("Settlement.SettledHexes")!.Get(row), 9);
    }

    [Fact]
    public void BodyStockRemainingMetric_SumsRemainingStock()
    {
        var (_, state) = EpochTestKit.Seeded();
        Assert.NotNull(MetricRegistry.Find("Extraction.BodyStockRemaining"));
        state.BodyResources[(state.Actors[0].Seat, new BodyRef(0, 0))]
            = new StarGen.Core.Substrate.Stock(
                StarGen.Core.Substrate.GoodId.Ore, 100.0, 0.5);
        state.BodyResources[(state.Actors[0].Seat, new BodyRef(0, 1))]
            = new StarGen.Core.Substrate.Stock(
                StarGen.Core.Substrate.GoodId.Ore, 25.0, 0.5);
        var row = MetricsOps.Snapshot(state);
        Assert.Equal(125.0, row.BodyStockRemaining, 6);
        Assert.Equal(125.0,
            MetricRegistry.Find("Extraction.BodyStockRemaining")!.Get(row), 6);
    }
}
