using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class StubGenesisTests
{
    private static EpochSimConfig Config(ulong seed = 42) =>
        new EpochSimConfig { MasterSeed = seed };

    [Fact]
    public void Seed_CreatesConfiguredPolityCount_WithSequentialIds()
    {
        var state = StubGenesis.Seed(Config());
        Assert.Equal(Config().Genesis.StubPolityCount, state.Actors.Count);
        Assert.Equal(Enumerable.Range(0, state.Actors.Count),
                     state.Actors.Select(a => a.Id));
        Assert.All(state.Actors, a => Assert.Equal(ActorKind.Polity, a.Kind));
    }

    [Fact]
    public void Seed_StartsAtYearZero_NothingEntered_EmptyLog()
    {
        var state = StubGenesis.Seed(Config());
        Assert.Equal(0, state.EpochIndex);
        Assert.Equal(0, state.WorldYear);
        Assert.All(state.Actors, a => Assert.False(a.Entered));
        Assert.Empty(state.Log.Events);
    }

    [Fact]
    public void EntryEpochs_AreStaggered_InsideTheEmergenceWindow()
    {
        var config = Config();
        var state = StubGenesis.Seed(config);
        int windowEpochs = config.Genesis.EmergenceWindowYears / config.Sim.YearsPerEpoch;
        Assert.All(state.Actors, a =>
        {
            Assert.InRange(a.EntryEpoch, 0, windowEpochs);
        });
        // staggered, not simultaneous (asymmetric emergence, frame/time.md)
        Assert.True(state.Actors.Select(a => a.EntryEpoch).Distinct().Count() > 1);
    }

    [Fact]
    public void Actors_HaveNames_AndControllers()
    {
        var state = StubGenesis.Seed(Config());
        Assert.All(state.Actors, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
            Assert.IsType<TrivialController>(a.Controller);
        });
    }

    [Fact]
    public void Seed_IsDeterministic_AndSeedSensitive()
    {
        var a = StubGenesis.Seed(Config(7));
        var b = StubGenesis.Seed(Config(7));
        var c = StubGenesis.Seed(Config(8));
        Assert.Equal(a.Actors.Select(x => (x.Name, x.Seat, x.EntryEpoch)),
                     b.Actors.Select(x => (x.Name, x.Seat, x.EntryEpoch)));
        Assert.NotEqual(a.Actors.Select(x => (x.Name, x.Seat, x.EntryEpoch)),
                        c.Actors.Select(x => (x.Name, x.Seat, x.EntryEpoch)));
    }
}
