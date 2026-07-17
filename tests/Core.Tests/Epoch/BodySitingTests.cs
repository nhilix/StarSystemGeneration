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

    private static StarSystem WithOneBody(Body body)
    {
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Habitable,
            Body = body });
        sys.Stars.Add(s0);
        return sys;
    }

    [Fact]
    public void RenewableYield_AgriComplex_IsZeroAtAFullyBarrenDryBody()
    {
        // a barren, waterless rock genuinely can't farm — no renewable floor
        var sys = WithOneBody(new Body { Kind = BodyKind.RockyWorld,
            Biosphere = Biosphere.Barren, Hydrographics = 0 });
        double yield = BodySiting.RenewableYield(sys, new BodyRef(0, 0),
                                                 InfraTypeId.AgriComplex);
        Assert.Equal(0.0, yield, 9);
    }

    [Fact]
    public void RenewableYield_AgriComplex_ApproachesOne_AtARichWateredGarden()
    {
        var sys = WithOneBody(new Body { Kind = BodyKind.RockyWorld,
            Biosphere = Biosphere.Sapient, Hydrographics = 100 });
        double yield = BodySiting.RenewableYield(sys, new BodyRef(0, 0),
                                                 InfraTypeId.AgriComplex);
        Assert.Equal(1.0, yield, 6);              // 0.7*1 + 0.3*1
    }

    [Fact]
    public void RenewableYield_Skimmer_KeepsItsMassFloor_AtALeanGiant()
    {
        // deliberate asymmetry: a gas giant always has mass to skim (floor 0.5)
        var sys = WithOneBody(new Body { Kind = BodyKind.GasGiant, Size = 10 });
        double yield = BodySiting.RenewableYield(sys, new BodyRef(0, 0),
                                                 InfraTypeId.Skimmer);
        Assert.Equal(0.5, yield, 9);
    }

    [Fact]
    public void CompetesForBody_SameResourceClass_Competes_CrossClass_DoesNot()
    {
        // depletable vs depletable share one per-body stock — must exclude
        Assert.True(BodySiting.CompetesForBody(InfraTypeId.Mine, InfraTypeId.Mine));
        Assert.True(BodySiting.CompetesForBody(InfraTypeId.Mine,
                                               InfraTypeId.ExcavationSite));
        Assert.True(BodySiting.CompetesForBody(InfraTypeId.ExcavationSite,
                                               InfraTypeId.ExcavationSite));
        // same-type renewables still exclude (the two-mines fix, generalized)
        Assert.True(BodySiting.CompetesForBody(InfraTypeId.Skimmer,
                                               InfraTypeId.Skimmer));
        Assert.True(BodySiting.CompetesForBody(InfraTypeId.AgriComplex,
                                               InfraTypeId.AgriComplex));
        // both non-extraction ride the port body — keep excluding
        Assert.True(BodySiting.CompetesForBody(InfraTypeId.Refinery,
                                               InfraTypeId.Shipyard));
        // cross-class: different resources on one body coexist
        Assert.False(BodySiting.CompetesForBody(InfraTypeId.Mine,
                                                InfraTypeId.AgriComplex));
        Assert.False(BodySiting.CompetesForBody(InfraTypeId.Skimmer,
                                                InfraTypeId.Mine));
        Assert.False(BodySiting.CompetesForBody(InfraTypeId.Skimmer,
                                                InfraTypeId.AgriComplex));
        Assert.False(BodySiting.CompetesForBody(InfraTypeId.ExcavationSite,
                                                InfraTypeId.AgriComplex));
    }
}
