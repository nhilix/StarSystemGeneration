using StarGen.Core.Model;
using StarGen.Core.Rng;
using Xunit;

namespace StarGen.Core.Tests.Rng;

public class RollContextTests
{
    private static RollContext Ctx(ulong seed = 42, int x = 3, int y = 7) =>
        new(seed, new HexCoordinate(x, y));

    [Fact]
    public void NextDouble_IsDeterministic_AndOrderIndependent()
    {
        var a = Ctx().NextDouble(RollChannel.Presence);
        Ctx().NextDouble(RollChannel.BodyKind, 5); // unrelated draw in between
        var b = Ctx().NextDouble(RollChannel.Presence);
        Assert.Equal(a, b);
    }

    [Fact]
    public void NextDouble_IsInUnitInterval()
    {
        for (int i = 0; i < 1000; i++)
        {
            var v = Ctx().NextDouble(RollChannel.BodyKind, i);
            Assert.InRange(v, 0.0, 0.9999999999999999);
        }
    }

    [Fact]
    public void DifferentChannelIndexSubIndexSeedCoord_AllDiffer()
    {
        var baseline = Ctx().NextDouble(RollChannel.BodyKind, 1, 1);
        Assert.NotEqual(baseline, Ctx().NextDouble(RollChannel.BodySize, 1, 1));
        Assert.NotEqual(baseline, Ctx().NextDouble(RollChannel.BodyKind, 2, 1));
        Assert.NotEqual(baseline, Ctx().NextDouble(RollChannel.BodyKind, 1, 2));
        Assert.NotEqual(baseline, Ctx(seed: 43).NextDouble(RollChannel.BodyKind, 1, 1));
        Assert.NotEqual(baseline, Ctx(x: 4).NextDouble(RollChannel.BodyKind, 1, 1));
    }

    [Fact]
    public void NextInt_StaysInRange()
    {
        for (int i = 0; i < 1000; i++)
        {
            var v = Ctx().NextInt(RollChannel.SlotCount, 3, 9, i);
            Assert.InRange(v, 3, 8);
        }
    }
}
