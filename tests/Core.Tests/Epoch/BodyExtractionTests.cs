using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyExtractionTests
{
    private static StarSystem TwoRockyWorlds()
    {
        // Rocky worlds carry real Size variance (RockySize table 1-9), so a
        // mine on a fat rock out-yields one on a lean rock.
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.RockyWorld, Size = 6 } });
        s0.Slots.Add(new OrbitSlot { Index = 1, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.RockyWorld, Size = 1 } });
        sys.Stars.Add(s0);
        return sys;
    }

    [Fact]
    public void RicherBody_YieldsAHigherModifier()
    {
        var sys = TwoRockyWorlds();
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
        var sys = TwoRockyWorlds();
        Assert.Equal(1.0, BodySiting.RichnessModifier(sys, BodyRef.None,
            InfraTypeId.Mine), 9);
        Assert.Equal(1.0, BodySiting.RichnessModifier(null, new BodyRef(0, 0),
            InfraTypeId.Mine), 9);
    }

    [Fact]
    public void MineOnABelt_IsNeutralOne_NoPerBodySignal()
    {
        // BodyGenerator sets PlanetoidBelt.Size = 0 always — there is no
        // per-body richness signal for belts, so the modifier is an honest
        // 1.0, not the misleading Size/6 floor of 0.5 the old formula gave.
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.PlanetoidBelt, Size = 0 } });
        sys.Stars.Add(s0);
        Assert.Equal(1.0, BodySiting.RichnessModifier(sys, new BodyRef(0, 0),
            InfraTypeId.Mine), 9);
    }

    [Fact]
    public void ExcavationOnWreckage_IsNeutralOne_NoPerBodySignal()
    {
        // OverlayCatalog sets Wreckage.Size = 0 always — same honest neutral.
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Outer,
            Body = new Body { Kind = BodyKind.Wreckage, Size = 0 } });
        sys.Stars.Add(s0);
        Assert.Equal(1.0, BodySiting.RichnessModifier(sys, new BodyRef(0, 0),
            InfraTypeId.ExcavationSite), 9);
    }

    [Fact]
    public void Skimmer_MapsTheGiantSizeRange_LinearlyAcrossTheBand()
    {
        // GasGiantSize table spans 10-14; the old Size/6 clamp saturated
        // every giant to 1.5 with zero variance. Normalizing against the
        // real range restores proportional variance across the band.
        static StarSystem Giant(int size)
        {
            var sys = new StarSystem("TEST");
            var s0 = new Star();
            s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Outer,
                Body = new Body { Kind = BodyKind.GasGiant, Size = size } });
            sys.Stars.Add(s0);
            return sys;
        }

        double lean = BodySiting.RichnessModifier(Giant(10),
            new BodyRef(0, 0), InfraTypeId.Skimmer);
        double mid = BodySiting.RichnessModifier(Giant(12),
            new BodyRef(0, 0), InfraTypeId.Skimmer);
        double fat = BodySiting.RichnessModifier(Giant(14),
            new BodyRef(0, 0), InfraTypeId.Skimmer);

        Assert.Equal(0.5, lean, 9);        // Size 10 -> floor
        Assert.Equal(1.0, mid, 9);         // Size 12 -> midpoint
        Assert.Equal(1.5, fat, 9);         // Size 14 -> ceiling
        Assert.True(fat > mid && mid > lean);
    }

    [Fact]
    public void NonExtractionType_IsNeutralOne_EvenOnARichBody()
    {
        // Richness is an extraction-grade signal only; a Refinery sited at a
        // fat, lush body must NOT inherit a ±50% swing from the port-body
        // fallback (Fix 3: the leak MarketEngine.SupplyLands used to carry).
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.RockyWorld, Size = 9,
                Biosphere = Biosphere.Sapient } });
        sys.Stars.Add(s0);
        Assert.Equal(1.0, BodySiting.RichnessModifier(sys, new BodyRef(0, 0),
            InfraTypeId.Refinery), 9);
    }

    [Fact]
    public void SapientBiosphere_AgriComplex_ExceedsNeutral()
    {
        var sys = new StarSystem("TEST");
        var s0 = new Star();
        s0.Slots.Add(new OrbitSlot { Index = 0, Band = OrbitBand.Habitable,
            Body = new Body { Kind = BodyKind.RockyWorld, Size = 6,
                Biosphere = Biosphere.Sapient } });
        sys.Stars.Add(s0);

        double modifier = BodySiting.RichnessModifier(sys, new BodyRef(0, 0),
            InfraTypeId.AgriComplex);

        // Biosphere.Sapient == 3, the max of the 0-3 range, so with a
        // divisor of 3.0 this should saturate at the top of the band
        // rather than the old bug's cap at 1.0 (neutral).
        Assert.Equal(1.5, modifier, 9);
        Assert.True(modifier > 1.0);
    }
}
