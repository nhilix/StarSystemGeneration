using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class DeterminismTests
{
    private static string RunAndRender(ulong seed)
    {
        var state = StubGenesis.Seed(new EpochSimConfig { MasterSeed = seed });
        new EpochEngine().Run(state);
        return SimTraceView.Render(state);
    }

    [Fact]
    public void SameConfig_ByteIdenticalTraceAndLog()
    {
        Assert.Equal(RunAndRender(42), RunAndRender(42));
    }

    [Fact]
    public void DifferentSeed_DivergentHistory()
    {
        Assert.NotEqual(RunAndRender(42), RunAndRender(43));
    }

    [Fact]
    public void Render_CoversTraceAndEveryLoggedEvent()
    {
        var state = StubGenesis.Seed(new EpochSimConfig { MasterSeed = 42 });
        new EpochEngine().Run(state);
        string text = SimTraceView.Render(state);
        foreach (var e in state.Log.Events)
            Assert.Contains($"y{e.WorldYear}", text);
        foreach (var a in state.Actors)
            Assert.Contains(a.Name, text);
        Assert.Contains("Perception", text);
        Assert.Contains("Chronicle", text);
    }
}
