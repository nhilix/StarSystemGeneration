using StarGen.Core.Rng;
using Xunit;

namespace StarGen.Core.Tests.Rng;

public class StableHashTests
{
    [Fact]
    public void SameInputs_SameOutput() =>
        Assert.Equal(StableHash.Mix(1, 2, 3, 4), StableHash.Mix(1, 2, 3, 4));

    [Fact]
    public void AnySingleInputChange_ChangesOutput()
    {
        var baseline = StableHash.Mix(1, 2, 3, 4);
        Assert.NotEqual(baseline, StableHash.Mix(9, 2, 3, 4));
        Assert.NotEqual(baseline, StableHash.Mix(1, 9, 3, 4));
        Assert.NotEqual(baseline, StableHash.Mix(1, 2, 9, 4));
        Assert.NotEqual(baseline, StableHash.Mix(1, 2, 3, 9));
    }

    [Fact]
    public void ZeroInputs_DoNotCollapse() =>
        Assert.NotEqual(StableHash.Mix(0, 0, 0, 0), StableHash.Mix(0, 0, 0, 1));
}
