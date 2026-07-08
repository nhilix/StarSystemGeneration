using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class DensityFieldTests
{
    private static GalaxyConfig Config(ulong seed = 42) => new() { MasterSeed = seed };

    [Fact]
    public void At_IsDeterministic_AndBounded()
    {
        var config = Config();
        foreach (var hex in HexGrid.Spiral(new HexCoordinate(0, 0), 40).Where((_, i) => i % 7 == 0))
        {
            var v = DensityField.At(config, hex);
            Assert.Equal(v, DensityField.At(config, hex));
            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void OutsideGalaxy_IsZero_AndNotInGalaxy()
    {
        var config = Config();   // radius 21 cells -> rim well inside |q| ~ 250
        var far = new HexCoordinate(400, 0);
        Assert.False(DensityField.InGalaxy(config, far));
        Assert.Equal(0.0, DensityField.At(config, far));
        Assert.True(DensityField.InGalaxy(config, new HexCoordinate(0, 0)));
    }

    [Fact]
    public void Core_IsDenserThanMidDisc()
    {
        var config = Config();
        double Avg(HexCoordinate center) =>
            HexGrid.Spiral(center, 6).Average(h => DensityField.At(config, h));
        // mid-disc reference: a hex roughly 60% of the way to the rim along +q
        int midQ = (int)(0.6 * 2.0 / 3.0 * DensityField.WorldRimRadius(config));
        double coreAvg = Avg(new HexCoordinate(0, 0));
        double midAvg = Avg(new HexCoordinate(midQ, -midQ / 2));
        Assert.True(coreAvg > midAvg, $"core {coreAvg:F3} should exceed mid-disc {midAvg:F3}");
    }

    [Fact]
    public void MeanInsideDisc_NearTarget()
    {
        var config = Config();
        double rim = DensityField.WorldRimRadius(config);
        double sum = 0; int count = 0;
        foreach (var hex in HexGrid.Spiral(new HexCoordinate(0, 0), 230).Where((_, i) => i % 16 == 0))
        {
            var (wx, wy) = HexGrid.HexToWorld(hex);
            if (System.Math.Sqrt(wx * wx + wy * wy) > 0.9 * rim) continue;
            if (!DensityField.InGalaxy(config, hex)) continue;
            sum += DensityField.At(config, hex);
            count++;
        }
        Assert.True(count > 3000, $"sample too small: {count}");
        Assert.InRange(sum / count, config.MeanDensityTarget - 0.12, config.MeanDensityTarget + 0.12);
    }
}
