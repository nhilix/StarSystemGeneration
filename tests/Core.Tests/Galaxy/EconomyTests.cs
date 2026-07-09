using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class EconomyTests
{
    private static SpeciesProfile Species(Embodiment e = Embodiment.TerranAnalog) => new()
    {
        Id = 0, Embodiment = e, Expansionism = 0.5, Cohesion = 0.5,
        Militancy = 0.5, Openness = 0.5, Industry = 0.5, Adaptability = 0.5,
    };

    private static RegionCell Cell() => new()
    {
        Q = 0, R = 0, MeanDensity = 0.5, Lean = StellarLean.Balanced,
        Metallicity = 0.5, DevelopmentTier = 2, Population = 1.0,
    };

    [Fact]
    public void Provisions_AreEmbodimentRelative()
    {
        var bright = Cell(); bright.Lean = StellarLean.YoungBright;
        var dim = Cell(); dim.Lean = StellarLean.OldDim;
        Assert.True(Economy.ProvisionsPotential(Species(Embodiment.Aquatic), bright)
                  > Economy.ProvisionsPotential(Species(Embodiment.Aquatic), dim));
        Assert.True(Economy.ProvisionsPotential(Species(Embodiment.Cryophilic), dim)
                  > Economy.ProvisionsPotential(Species(Embodiment.Cryophilic), bright));
    }

    [Fact]
    public void MineralAnchor_DominatesOreProduction()
    {
        var plain = Cell();
        var anchored = Cell();
        anchored.Anchors.Add(new Anchor { Type = AnchorType.MineralRich, Hex = anchored.Coord });
        Assert.True(Economy.OrePotential(anchored) > Economy.OrePotential(plain) + 1.0);
    }

    [Fact]
    public void PrecursorSite_DominatesExotics()
    {
        var plain = Cell();
        var site = Cell();
        site.Anchors.Add(new Anchor { Type = AnchorType.PrecursorSite, Hex = site.Coord });
        Assert.True(Economy.ExoticsPotential(site) > 10 * Economy.ExoticsPotential(plain));
    }

    [Fact]
    public void LithicsAndMachines_BarelyConsumeProvisions()
    {
        var config = new GalaxyConfig();
        var cell = Cell();
        double terran = Economy.Consumed(Commodity.Provisions, config, Species(), cell);
        double lithic = Economy.Consumed(Commodity.Provisions, config, Species(Embodiment.Lithic), cell);
        double machine = Economy.Consumed(Commodity.Provisions, config, Species(Embodiment.Machine), cell);
        Assert.True(lithic < terran * 0.3);
        Assert.True(machine < terran * 0.2);
    }

    [Fact]
    public void SystemValue_RewardsThroughputAndChokepoints()
    {
        var plain = Cell();
        var busy = Cell(); busy.RouteThroughput = 4.0;
        var choke = Cell(); choke.IsChokepoint = true;
        Assert.True(Economy.SystemValue(Species(), busy) > Economy.SystemValue(Species(), plain));
        Assert.True(Economy.SystemValue(Species(), choke) > Economy.SystemValue(Species(), plain));
    }

    [Fact]
    public void TechLadder_IsGeometric()
    {
        var config = new GalaxyConfig { TechThresholdBase = 10.0 };
        Assert.Equal(10.0, Economy.TechThreshold(config, 0));
        Assert.Equal(30.0, Economy.TechThreshold(config, 1));
        Assert.Equal(90.0, Economy.TechThreshold(config, 2));
    }

    [Fact]
    public void DevCeiling_StartsAtStageOneCap_AndIsTechScaled()
    {
        Assert.Equal(5, Economy.DevCeiling(0));   // stage-1 flat cap preserved at tier 0
        Assert.Equal(7, Economy.DevCeiling(2));
        Assert.Equal(9, Economy.DevCeiling(20));  // absolute cap 9 (single map glyph)
    }

    [Fact]
    public void WarStrength_ScalesWithTechAndMilitancy()
    {
        double baseline = Economy.WarStrength(10, 0, 0.5);
        Assert.True(Economy.WarStrength(10, 2, 0.5) > baseline);
        Assert.True(Economy.WarStrength(10, 0, 0.9) > baseline);
        Assert.Equal(0.0, Economy.WarStrength(0, 5, 1.0));
    }

    [Fact]
    public void Production_IsNonNegative_EverywhereReasonable()
    {
        foreach (var lean in new[] { StellarLean.Balanced, StellarLean.YoungBright,
                                     StellarLean.OldDim, StellarLean.RemnantGraveyard })
            foreach (var e in new[] { Embodiment.TerranAnalog, Embodiment.Aquatic,
                                      Embodiment.Cryophilic, Embodiment.Lithic,
                                      Embodiment.Hive, Embodiment.Machine })
            {
                var cell = Cell(); cell.Lean = lean;
                foreach (Commodity good in System.Enum.GetValues(typeof(Commodity)))
                    Assert.True(Economy.Produced(good, Species(e), cell) >= 0);
            }
    }
}
