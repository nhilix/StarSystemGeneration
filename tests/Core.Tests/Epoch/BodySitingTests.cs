using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using System.Collections.Generic;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodySitingTests
{
    private static StarSystem WithBelts()
    {
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Inner,
            Body = new Body { Kind = BodyKind.RockyWorld, Size = 3 } });
        s0.Slots.Add(new OrbitSlot { Index = 1, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 5 } });
        s0.Slots.Add(new OrbitSlot { Index = 2, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 4 } });
        sys.Stars.Add(s0);
        return sys;
    }

    [Fact]
    public void SecondMine_PicksADifferentBelt_WhenTheFirstIsClaimed()
    {
        var sys = WithBelts();
        var port = BodySiting.PortBody(sys);
        var first = BodySiting.Assign(sys, InfraTypeId.Mine, port,
            new List<BodyRef>());
        Assert.Equal(new BodyRef(0, 1), first);      // first belt in slot order
        var second = BodySiting.Assign(sys, InfraTypeId.Mine, port,
            new List<BodyRef> { first });
        Assert.Equal(new BodyRef(0, 2), second);     // the OTHER belt
        Assert.NotEqual(first, second);
    }

    private static StarSystem WithNoExtractionSubstrate()
    {
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.GasGiant, Size = 5 } });
        sys.Stars.Add(s0);
        return sys;
    }

    [Fact]
    public void Mine_WithNoBeltOrRock_IsNone_NotThePortBody()
    {
        var sys = WithNoExtractionSubstrate();   // a gas giant only
        var port = BodySiting.PortBody(sys);
        Assert.False(port.IsNone);               // a port body exists...
        var first = BodySiting.Assign(sys, InfraTypeId.Mine, port,
            new List<BodyRef>());
        Assert.True(first.IsNone);               // ...but a mine won't ride it
    }

    [Fact]
    public void Skimmer_WithNoGasGiant_IsNone()
    {
        var sys = WithBelts();                   // belts + a rocky world, no giant
        var port = BodySiting.PortBody(sys);
        Assert.True(BodySiting.Assign(sys, InfraTypeId.Skimmer, port,
            new List<BodyRef>()).IsNone);
    }

    [Fact]
    public void NonExtraction_RidesThePortBody()
    {
        var sys = WithBelts();
        var port = BodySiting.PortBody(sys);
        Assert.Equal(port, BodySiting.Assign(sys, InfraTypeId.Refinery, port,
            new List<BodyRef>()));
    }

    [Fact]
    public void NullSystem_IsNone()
    {
        Assert.True(BodySiting.Assign(null, InfraTypeId.Mine, BodyRef.None,
            new List<BodyRef>()).IsNone);
    }
}
