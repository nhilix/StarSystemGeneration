using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyExtractionTests
{
    private static StarSystem TwoBelts()
    {
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 6 } });
        s0.Slots.Add(new OrbitSlot { Index = 1, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 1 } });
        sys.Stars.Add(s0);
        return sys;
    }

    [Fact]
    public void RicherBody_YieldsAHigherModifier()
    {
        var sys = TwoBelts();
        double rich = BodySiting.RichnessModifier(sys, new BodyRef(0, 0),
            InfraTypeId.Mine);
        double poor = BodySiting.RichnessModifier(sys, new BodyRef(0, 1),
            InfraTypeId.Mine);
        Assert.True(rich > poor);
        Assert.InRange(rich, 0.5, 1.5);
        Assert.InRange(poor, 0.5, 1.5);
    }

    [Fact]
    public void NoneBody_IsNeutralOne()
    {
        var sys = TwoBelts();
        Assert.Equal(1.0, BodySiting.RichnessModifier(sys, BodyRef.None,
            InfraTypeId.Mine), 9);
        Assert.Equal(1.0, BodySiting.RichnessModifier(null, new BodyRef(0, 0),
            InfraTypeId.Mine), 9);
    }
}
