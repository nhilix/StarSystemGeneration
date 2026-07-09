using System;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Substrate;

public class DemandProfileTests
{
    private static readonly PopulationBand[] Bands =
        (PopulationBand[])Enum.GetValues(typeof(PopulationBand));

    [Fact]
    public void EveryProfile_NormalizedAndCatalogClosed()
    {
        foreach (Embodiment e in Enum.GetValues(typeof(Embodiment)))
            foreach (var band in Bands)
            {
                var profile = DemandProfiles.Population(e, band);
                Assert.NotEmpty(profile);
                Assert.Equal(1.0, profile.Sum(p => p.Weight), 10);
                foreach (var (good, weight) in profile)
                {
                    Assert.True(Enum.IsDefined(typeof(GoodId), good));
                    Assert.True(weight > 0);
                }
            }
        foreach (var u in DemandProfiles.InstitutionalUseCases)
        {
            var profile = DemandProfiles.Institutional(u);
            Assert.NotEmpty(profile);
            Assert.Equal(1.0, profile.Sum(p => p.Weight), 10);
        }
    }

    [Fact]
    public void DefaultBands_MatchTheDesignTable()
    {
        // subsistence = Provisions (unmet means famine)
        var sub = DemandProfiles.Population(Embodiment.TerranAnalog, PopulationBand.Subsistence);
        Assert.Equal(GoodId.Provisions, Assert.Single(sub).Good);
        // SoL = Consumer Goods + Medicine
        var sol = DemandProfiles.Population(Embodiment.TerranAnalog, PopulationBand.StandardOfLiving);
        Assert.Contains(sol, p => p.Good == GoodId.ConsumerGoods);
        Assert.Contains(sol, p => p.Good == GoodId.Medicine);
        // luxury = Luxuries + Narcotics (elastic, prestige-driven)
        var lux = DemandProfiles.Population(Embodiment.TerranAnalog, PopulationBand.Luxury);
        Assert.Contains(lux, p => p.Good == GoodId.Luxuries);
        Assert.Contains(lux, p => p.Good == GoodId.Narcotics);
    }

    [Fact]
    public void MachinePopulations_ConsumeIndustryNotFoodOrMedicine()
    {
        var all = Bands.SelectMany(b => DemandProfiles.Population(Embodiment.Machine, b)).ToList();
        Assert.DoesNotContain(all, p => p.Good == GoodId.Provisions);
        Assert.DoesNotContain(all, p => p.Good == GoodId.Medicine);
        Assert.Contains(all, p => p.Good == GoodId.Fuel);
        Assert.Contains(all, p => p.Good == GoodId.Machinery);
        Assert.Contains(all, p => p.Good == GoodId.Compute);
    }

    [Fact]
    public void Lithics_EatLittle_DemandMoreMachinery()
    {
        double lithicFood = DemandProfiles.SubsistenceScale(Embodiment.Lithic);
        double terranFood = DemandProfiles.SubsistenceScale(Embodiment.TerranAnalog);
        Assert.True(lithicFood < terranFood);
        var lithicAll = Bands.SelectMany(b => DemandProfiles.Population(Embodiment.Lithic, b));
        Assert.Contains(lithicAll, p => p.Good == GoodId.Machinery);
    }

    [Fact]
    public void PriorityOrder_PopulationFirst_TechnologyLast()
    {
        var order = DemandProfiles.PriorityOrder;
        // population bands lead, in band order
        Assert.Equal(UseCase.Subsistence, order[0]);
        Assert.Equal(UseCase.StandardOfLiving, order[1]);
        Assert.Equal(UseCase.Luxury, order[2]);
        // then industry → movement → military → technology (commodities.md)
        int industry = order.ToList().IndexOf(UseCase.IndustryConstruction);
        int movement = order.ToList().IndexOf(UseCase.Movement);
        int military = order.ToList().IndexOf(UseCase.MilitaryUpkeep);
        int tech = order.ToList().IndexOf(UseCase.Technology);
        Assert.True(industry < movement && movement < military && military < tech);
        // every use-case appears exactly once
        var all = (UseCase[])Enum.GetValues(typeof(UseCase));
        Assert.Equal(all.OrderBy(u => u), order.OrderBy(u => u));
    }

    [Fact]
    public void InstitutionalProfiles_MatchTheDesignList()
    {
        Assert.Equal(GoodId.Fuel,
            Assert.Single(DemandProfiles.Institutional(UseCase.Movement)).Good);
        var milUp = DemandProfiles.Institutional(UseCase.MilitaryUpkeep);
        Assert.Contains(milUp, p => p.Good == GoodId.Armaments);
        Assert.Contains(milUp, p => p.Good == GoodId.Fuel);
        Assert.Contains(DemandProfiles.Institutional(UseCase.MilitaryConstruction),
            p => p.Good == GoodId.ShipComponents);
        var techP = DemandProfiles.Institutional(UseCase.Technology);
        Assert.Contains(techP, p => p.Good == GoodId.RefinedExotics);
        Assert.Contains(techP, p => p.Good == GoodId.Compute);
        var indC = DemandProfiles.Institutional(UseCase.IndustryConstruction);
        Assert.Contains(indC, p => p.Good == GoodId.Alloys);
        Assert.Contains(indC, p => p.Good == GoodId.Machinery);
    }
}
