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
        for (int i = 0; i < 500; i++)
        {
            var hex = new HexCoordinate((i * 13) % config.WidthHexes, (i * 29) % config.HeightHexes);
            var v = DensityField.At(config, hex);
            Assert.Equal(v, DensityField.At(config, hex));
            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void BeyondRim_IsZero()
    {
        var config = Config();
        Assert.Equal(0.0, DensityField.At(config, new HexCoordinate(0, 0)));
        Assert.Equal(0.0, DensityField.At(config, new HexCoordinate(config.WidthHexes - 1, 0)));
    }

    [Fact]
    public void Core_IsDenserThanMidDisc()
    {
        var config = Config();
        var center = new HexCoordinate(config.WidthHexes / 2, config.HeightHexes / 2);
        double Avg(HexCoordinate c, int radius)
        {
            double sum = 0; int n = 0;
            for (int dx = -radius; dx <= radius; dx += 2)
                for (int dy = -radius; dy <= radius; dy += 2)
                { sum += DensityField.At(config, new HexCoordinate(c.X + dx, c.Y + dy)); n++; }
            return sum / n;
        }
        double coreAvg = Avg(center, 8);
        double midAvg = Avg(new HexCoordinate(center.X + config.WidthHexes / 3, center.Y), 8);
        Assert.True(coreAvg > midAvg, $"core {coreAvg:F3} should exceed mid-disc {midAvg:F3}");
    }

    [Fact]
    public void MeanInsideDisc_NearTarget()
    {
        var config = Config();
        double sum = 0; int count = 0;
        for (int x = 0; x < config.WidthHexes; x += 4)
            for (int y = 0; y < config.HeightHexes; y += 4)
            {
                double nx = (x - config.WidthHexes / 2.0) / (config.WidthHexes / 2.0);
                double ny = (y - config.HeightHexes / 2.0) / (config.HeightHexes / 2.0);
                if (nx * nx + ny * ny > 0.81) continue;   // inside the disc only
                sum += DensityField.At(config, new HexCoordinate(x, y));
                count++;
            }
        Assert.InRange(sum / count, config.MeanDensityTarget - 0.12, config.MeanDensityTarget + 0.12);
    }
}
