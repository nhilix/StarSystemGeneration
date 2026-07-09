using System;
using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Substrate;

public class InfrastructureTests
{
    [Fact]
    public void Catalog_HasFifteenTypes_IdsStableAndUnique()
    {
        Assert.Equal(15, Infrastructure.All.Count);
        Assert.Equal(15, Infrastructure.All.Select(d => d.Id).Distinct().Count());
        // Frozen ids — closed, versioned vocabulary like the anchor types.
        Assert.Equal(0, (int)InfraTypeId.Port);
        Assert.Equal(1, (int)InfraTypeId.Mine);
        Assert.Equal(2, (int)InfraTypeId.Skimmer);
        Assert.Equal(3, (int)InfraTypeId.AgriComplex);
        Assert.Equal(4, (int)InfraTypeId.ExcavationSite);
        Assert.Equal(5, (int)InfraTypeId.Refinery);
        Assert.Equal(6, (int)InfraTypeId.Chemworks);
        Assert.Equal(7, (int)InfraTypeId.Fabricator);
        Assert.Equal(8, (int)InfraTypeId.ExoticsLab);
        Assert.Equal(9, (int)InfraTypeId.Foundry);
        Assert.Equal(10, (int)InfraTypeId.Shipyard);
        Assert.Equal(11, (int)InfraTypeId.Arsenal);
        Assert.Equal(12, (int)InfraTypeId.ComputeCore);
        Assert.Equal(13, (int)InfraTypeId.Depot);
        Assert.Equal(14, (int)InfraTypeId.Fortress);
        foreach (var def in Infrastructure.All)
            Assert.Same(def, Infrastructure.Get(def.Id));
    }

    [Fact]
    public void Families_MatchTheDesignTable()
    {
        Assert.Equal(1, Infrastructure.All.Count(d => d.Family == InfraFamily.Keystone));
        Assert.Equal(4, Infrastructure.All.Count(d => d.Family == InfraFamily.Extraction));
        Assert.Equal(4, Infrastructure.All.Count(d => d.Family == InfraFamily.Processing));
        Assert.Equal(4, Infrastructure.All.Count(d => d.Family == InfraFamily.Heavy));
        Assert.Equal(2, Infrastructure.All.Count(d => d.Family == InfraFamily.Support));
    }

    [Fact]
    public void BuildCostsAndUpkeep_AreRealConstructionGoods()
    {
        // "a build cost in real goods (Alloys, Machinery, Composites) plus
        //  construction time, an upkeep draw" (infrastructure.md)
        var constructionGoods = new[] { GoodId.Alloys, GoodId.Machinery, GoodId.Composites };
        foreach (var def in Infrastructure.All)
        {
            Assert.NotEmpty(def.BuildCost);
            foreach (var c in def.BuildCost)
            {
                Assert.Contains(c.Good, constructionGoods);
                Assert.True(c.Quantity > 0);
            }
            foreach (var u in def.UpkeepPerYear)
            {
                Assert.Contains(u.Good, constructionGoods);
                Assert.True(u.Quantity > 0);
            }
            Assert.True(def.ConstructionYears > 0);
        }
    }

    [Fact]
    public void Produces_MatchesFamilyTier()
    {
        foreach (var def in Infrastructure.All)
        {
            switch (def.Family)
            {
                case InfraFamily.Extraction:
                    Assert.NotEmpty(def.Produces);
                    Assert.All(def.Produces, g => Assert.Equal(GoodTier.Raw, Goods.Get(g).Tier));
                    break;
                case InfraFamily.Processing:
                    Assert.NotEmpty(def.Produces);
                    Assert.All(def.Produces, g => Assert.Equal(GoodTier.Processed, Goods.Get(g).Tier));
                    break;
                case InfraFamily.Heavy:
                    Assert.NotEmpty(def.Produces);
                    Assert.All(def.Produces, g => Assert.Equal(GoodTier.Capital, Goods.Get(g).Tier));
                    break;
                default:
                    // keystone/support provide capability, not goods
                    Assert.Empty(def.Produces);
                    break;
            }
        }
    }

    [Fact]
    public void EveryRecipeGood_HasAProducer_ExceptLuxuries()
    {
        // The design's facility table assigns no producer to Luxuries — the
        // corporate niche good (chartered production lands with Slice G).
        var produced = new HashSet<GoodId>(Infrastructure.All.SelectMany(d => d.Produces));
        foreach (var g in Goods.All.Where(g => g.Recipes.Count > 0 && g.Id != GoodId.Luxuries))
            Assert.Contains(g.Id, produced);
        Assert.DoesNotContain(GoodId.Luxuries, produced);
    }

    [Fact]
    public void Output_ScalesWithTier_TerrainGatesEverything()
    {
        var mine = Infrastructure.Get(InfraTypeId.Mine);
        double t1 = Production.Output(mine, tier: 1, terrain: 0.8, laborFactor: 1.0, machineryGrade: 0.5);
        double t2 = Production.Output(mine, tier: 2, terrain: 0.8, laborFactor: 1.0, machineryGrade: 0.5);
        double t3 = Production.Output(mine, tier: 3, terrain: 0.8, laborFactor: 1.0, machineryGrade: 0.5);
        Assert.True(t1 > 0);
        Assert.True(t2 > t1 && t3 > t2);
        Assert.Equal(0.0, Production.Output(mine, 3, terrain: 0.0, laborFactor: 1.0, machineryGrade: 0.5), 10);
    }

    [Fact]
    public void MachineryGrade_MultipliesProductivity()
    {
        var foundry = Infrastructure.Get(InfraTypeId.Foundry);
        double crude = Production.Output(foundry, 2, 1.0, 1.0, machineryGrade: 0.2);
        double fine = Production.Output(foundry, 2, 1.0, 1.0, machineryGrade: 0.9);
        Assert.True(fine > crude);
        // grade 0.5 machinery is the neutral baseline
        Assert.Equal(foundry.BaseOutputPerYear * Production.TierOutputFactor(2),
                     Production.Output(foundry, 2, 1.0, 1.0, 0.5), 10);
    }

    [Fact]
    public void LaborFactor_ClampsAtFullStaffing_AutomationSubstitutes()
    {
        Assert.Equal(1.0, Production.LaborFactor(10.0, 1.0, 0.0, required: 1.0), 10);
        Assert.Equal(0.5, Production.LaborFactor(0.5, 1.0, 0.0, required: 1.0), 10);
        // machine polities run thin-crewed industry: compute substitutes labor
        Assert.Equal(1.0, Production.LaborFactor(0.0, 1.0, 1.0, required: 1.0), 10);
        Assert.Equal(0.75, Production.LaborFactor(0.25, 1.0, 0.5, required: 1.0), 10);
        // affinity scales the population term only
        Assert.Equal(0.25, Production.LaborFactor(0.5, 0.5, 0.0, required: 1.0), 10);
    }

    [Fact]
    public void OrganicBaseline_SmallEnoughThatFacilitiesDominate()
    {
        double baseline = Production.OrganicBaseline(population: 1.0, biosphereRichness: 1.0);
        var agri = Infrastructure.Get(InfraTypeId.AgriComplex);
        double facility = Production.Output(agri, 1, terrain: 1.0, laborFactor: 1.0, machineryGrade: 0.5);
        Assert.True(baseline > 0);
        Assert.True(baseline < facility / 4,
            "unserviced systems are poor, not starving — but facilities always dominate");
        Assert.Equal(GradeBand.Crude, Grades.BandOf(Production.OrganicBaselineGrade));
        Assert.Equal(0.0, Production.OrganicBaseline(0.0, 1.0), 10);
    }

    [Fact]
    public void TierFactors_AreSuperlinear_CostAndOutput()
    {
        Assert.True(Production.TierOutputFactor(2) > 2 * Production.TierOutputFactor(1));
        Assert.True(Production.TierCostFactor(3) > Production.TierCostFactor(2));
        Assert.Equal(1.0, Production.TierOutputFactor(1), 10);
        Assert.Equal(1.0, Production.TierCostFactor(1), 10);
    }
}
