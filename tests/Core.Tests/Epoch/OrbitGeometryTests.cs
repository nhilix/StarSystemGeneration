using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class OrbitGeometryTests
{
    private static StarSystem TwoStar()
    {
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Inner });
        s0.Slots.Add(new OrbitSlot { Index = 1, Band = OrbitBand.Habitable });
        s0.Slots.Add(new OrbitSlot { Index = 2, Band = OrbitBand.Outer });
        sys.Stars.Add(s0);
        var s1 = new Star();
        s1.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Inner });
        s1.Slots.Add(new OrbitSlot { Index = 1, Band = OrbitBand.Habitable });
        sys.Stars.Add(s1);
        return sys;
    }

    [Fact]
    public void SameStar_IsSlotIndexGap()
    {
        var sys = TwoStar();
        Assert.Equal(2, OrbitGeometry.OrbitDistance(
            sys, new BodyRef(0, 0), new BodyRef(0, 2), crossStarSteps: 8));
        Assert.Equal(0, OrbitGeometry.OrbitDistance(
            sys, new BodyRef(0, 1), new BodyRef(0, 1), crossStarSteps: 8));
    }

    [Fact]
    public void CrossStar_AddsConstantPlusInnerDistances()
    {
        var sys = TwoStar();
        // star0 slot2 (2 from inner 0) + const 8 + star1 slot1 (1 from inner 0)
        Assert.Equal(11, OrbitGeometry.OrbitDistance(
            sys, new BodyRef(0, 2), new BodyRef(1, 1), crossStarSteps: 8));
    }

    [Fact]
    public void NoneRef_IsZeroDistance()
    {
        var sys = TwoStar();
        Assert.Equal(0, OrbitGeometry.OrbitDistance(
            sys, BodyRef.None, new BodyRef(0, 2), crossStarSteps: 8));
    }

    [Fact]
    public void LocalHopYears_ScalesWithDistanceAndKnob()
    {
        var eco = new EconomyKnobs { LocalHopYearsPerOrbitStep = 0.05 };
        Assert.Equal(0.15, OrbitGeometry.LocalHopYears(3, eco), 9);
    }
}
