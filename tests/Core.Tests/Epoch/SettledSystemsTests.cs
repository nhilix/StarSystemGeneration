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
}
