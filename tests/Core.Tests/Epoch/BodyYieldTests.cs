using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyYieldTests
{
    private static StarSystem Giant(int size)
    {
        var sys = new StarSystem("T");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.GasGiant, Size = size } });
        sys.Stars.Add(s0);
        return sys;
    }

    private static StarSystem World(Biosphere bio, int hydro)
    {
        var sys = new StarSystem("T");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.RockyWorld, Size = 5,
                Biosphere = bio, Hydrographics = hydro } });
        sys.Stars.Add(s0);
        return sys;
    }

    [Fact]
    public void Skimmer_FatterGiant_YieldsMore_AcrossThePositiveBand()
    {
        double lean = BodySiting.RenewableYield(Giant(10), new BodyRef(0, 0),
            InfraTypeId.Skimmer);
        double fat = BodySiting.RenewableYield(Giant(14), new BodyRef(0, 0),
            InfraTypeId.Skimmer);
        Assert.Equal(0.5, lean, 9);              // any giant has mass: never 0
        Assert.Equal(1.0, fat, 9);
        Assert.True(fat > lean);
    }

    [Fact]
    public void Agri_LivingWateredWorld_OutyieldsBarrenDryRock()
    {
        double lush = BodySiting.RenewableYield(World(Biosphere.Flourishing, 70),
            new BodyRef(0, 0), InfraTypeId.AgriComplex);
        double barren = BodySiting.RenewableYield(World(Biosphere.Barren, 0),
            new BodyRef(0, 0), InfraTypeId.AgriComplex);
        Assert.True(lush > barren);
        Assert.Equal(0.0, barren, 9);            // NO floor: a barren, dry rock
                                                 // genuinely can't farm (L2)
        Assert.InRange(lush, 0.0, 1.0);
    }

    [Fact]
    public void RenewableGrade_RicherBody_BetterGrade()
    {
        double fatGrade = BodySiting.RenewableGrade(Giant(14), new BodyRef(0, 0),
            InfraTypeId.Skimmer);
        double leanGrade = BodySiting.RenewableGrade(Giant(10), new BodyRef(0, 0),
            InfraTypeId.Skimmer);
        Assert.True(fatGrade > leanGrade);
        Assert.InRange(fatGrade, 0.15, 0.85);    // Potentials.RawGrade shape
    }

    [Fact]
    public void MissingBody_YieldsZero()
    {
        Assert.Equal(0.0, BodySiting.RenewableYield(null, new BodyRef(0, 0),
            InfraTypeId.Skimmer), 9);
        Assert.Equal(0.0, BodySiting.RenewableYield(Giant(12), BodyRef.None,
            InfraTypeId.Skimmer), 9);
    }
}
