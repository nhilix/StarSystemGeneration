using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyStockConfigTests
{
    [Fact]
    public void BothKnobs_AreRegisteredAndRoundTrip()
    {
        var eco = new EconomyKnobs
        {
            BodyStockOreScale = 5000.0,
            BodyStockVarianceSpread = 0.4,
        };
        Assert.Equal(5000.0, eco.BodyStockOreScale, 6);
        Assert.Equal(0.4, eco.BodyStockVarianceSpread, 6);

        bool ore = false, spread = false;
        foreach (var k in KnobRegistry.All)
        {
            if (k.Name == "Economy.BodyStockOreScale") ore = true;
            if (k.Name == "Economy.BodyStockVarianceSpread") spread = true;
        }
        Assert.True(ore, "Economy.BodyStockOreScale must be registered");
        Assert.True(spread, "Economy.BodyStockVarianceSpread must be registered");
    }
}
