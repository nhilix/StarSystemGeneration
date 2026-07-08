using StarGen.Core.Galaxy;
using StarGen.Core.Rng;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class ValueNoiseTests
{
    [Fact]
    public void Sample_IsDeterministic()
    {
        var a = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 12.34, 56.78, 3, 0.05);
        var b = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 12.34, 56.78, 3, 0.05);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Sample_StaysInUnitInterval()
    {
        for (int i = 0; i < 2000; i++)
        {
            var v = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, i * 0.37, i * 0.91, 3, 0.05);
            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void Sample_VariesSpatially_AndIsContinuous()
    {
        var a = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 10.0, 10.0, 3, 0.05);
        var far = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 300.0, 470.0, 3, 0.05);
        var near = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 10.3, 10.0, 3, 0.05);
        Assert.NotEqual(a, far);
        Assert.True(System.Math.Abs(a - near) < 0.25, $"continuity: |{a}-{near}| too large");
    }

    [Fact]
    public void DifferentSeedsOrChannels_Differ()
    {
        var a = ValueNoise.Sample(7, RollChannel.NoiseDensityLattice, 40.0, 40.0, 3, 0.05);
        Assert.NotEqual(a, ValueNoise.Sample(8, RollChannel.NoiseDensityLattice, 40.0, 40.0, 3, 0.05));
        Assert.NotEqual(a, ValueNoise.Sample(7, RollChannel.NoiseStellarLattice, 40.0, 40.0, 3, 0.05));
    }

    [Fact]
    public void GalaxyConfig_Defaults()
    {
        var config = new GalaxyConfig { MasterSeed = 42 };
        Assert.Equal(10, config.SizeSectors);
        Assert.Equal(320, config.WidthHexes);
        Assert.Equal(400, config.HeightHexes);
        Assert.Equal(40, config.CellsX);
        Assert.Equal(40, config.CellsY);
        Assert.Equal(0.5, config.MeanDensityTarget);
        Assert.Equal(12, config.EpochCount);
    }
}
