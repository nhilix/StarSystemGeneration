using System;
using System.Linq;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Substrate;

public class GradeTests
{
    [Fact]
    public void Blend_IsQuantityWeightedMeanGrade()
    {
        var blended = Stock.Blend(
            new Stock(GoodId.Ore, 10, 0.8),
            new Stock(GoodId.Ore, 30, 0.4));
        Assert.Equal(GoodId.Ore, blended.Good);
        Assert.Equal(40, blended.Quantity, 10);
        Assert.Equal(0.5, blended.Grade, 10);
    }

    [Fact]
    public void Blend_EmptySideIsIdentity()
    {
        var s = new Stock(GoodId.Fuel, 12, 0.7);
        Assert.Equal(s, Stock.Blend(s, new Stock(GoodId.Fuel, 0, 0.2)));
        Assert.Equal(s, Stock.Blend(new Stock(GoodId.Fuel, 0, 0.9), s));
    }

    [Fact]
    public void Blend_DifferentGoods_Throws()
    {
        Assert.Throws<ArgumentException>(() => Stock.Blend(
            new Stock(GoodId.Ore, 1, 0.5), new Stock(GoodId.Fuel, 1, 0.5)));
    }

    [Fact]
    public void Effective_AtMidGrade_IsQuantity_ForEveryUseCase()
    {
        foreach (UseCase u in Enum.GetValues(typeof(UseCase)))
            Assert.Equal(100.0, new Stock(GoodId.Armaments, 100, 0.5).Effective(u), 10);
    }

    [Fact]
    public void Multiplier_MonotoneInGrade_AndElasticGoodsMoreSensitive()
    {
        foreach (UseCase u in Enum.GetValues(typeof(UseCase)))
        {
            Assert.True(Grades.Multiplier(u, 0.9) > Grades.Multiplier(u, 0.1));
            Assert.True(Grades.Multiplier(u, 0.0) > 0);
        }
        // prestige demand rewards grade more than subsistence calories do
        double luxurySpread = Grades.Multiplier(UseCase.Luxury, 0.9)
                            - Grades.Multiplier(UseCase.Luxury, 0.1);
        double subsistenceSpread = Grades.Multiplier(UseCase.Subsistence, 0.9)
                                 - Grades.Multiplier(UseCase.Subsistence, 0.1);
        Assert.True(luxurySpread > subsistenceSpread);
    }

    [Fact]
    public void TechCeiling_CapsOutputGrade()
    {
        var adv = Goods.Get(GoodId.Machinery).Recipes.First(r => r.Kind == RecipeKind.Advanced);
        // masterwork inputs, top facility, top tech — still capped at the era ceiling
        double g = Grades.Output(adv, meanInputGrade: 0.95, facilityTier: 3, techTier: 3);
        Assert.Equal(Grades.TechCeiling(3), g, 10);
        // and the ceiling ladder is strict
        Assert.True(Grades.TechCeiling(1) < Grades.TechCeiling(2));
        Assert.True(Grades.TechCeiling(2) < Grades.TechCeiling(3));
    }

    [Fact]
    public void PrecursorGrades_SitAboveEveryCurrentEraCeiling()
    {
        for (int tech = 1; tech <= 3; tech++)
            Assert.True(Grades.PrecursorFloor > Grades.TechCeiling(tech));
        Assert.Equal(GradeBand.PrecursorGrade, Grades.BandOf(Grades.PrecursorFloor));
        Assert.Equal(GradeBand.PrecursorGrade, Grades.BandOf(1.0));
    }

    [Fact]
    public void AdvancedRecipe_OutgradesStandard_SameInputsFacilityTech()
    {
        var m = Goods.Get(GoodId.Machinery);
        var std = m.Recipes.First(r => r.Kind == RecipeKind.Standard);
        var adv = m.Recipes.First(r => r.Kind == RecipeKind.Advanced);
        double gStd = Grades.Output(std, 0.5, facilityTier: 2, techTier: 3);
        double gAdv = Grades.Output(adv, 0.5, facilityTier: 2, techTier: 3);
        Assert.True(gAdv > gStd);
    }

    [Fact]
    public void OutputGrade_ImprovesWithInputsAndFacility()
    {
        var std = Goods.Get(GoodId.Alloys).Recipes.First();
        Assert.True(Grades.Output(std, 0.8, 1, 2) > Grades.Output(std, 0.2, 1, 2));
        Assert.True(Grades.Output(std, 0.5, 3, 2) > Grades.Output(std, 0.5, 1, 2));
    }

    [Fact]
    public void TechTier_MultipliesGradeBelowTheCeiling_NotOnlyCapsIt()
    {
        // the design formula's fourth factor: same recipe, inputs, and
        // facility — a higher-tech producer turns out better goods
        var std = Goods.Get(GoodId.Machinery).Recipes.First(r => r.Kind == RecipeKind.Standard);
        double t2 = Grades.Output(std, 0.5, facilityTier: 1, techTier: 2);
        double t3 = Grades.Output(std, 0.5, facilityTier: 1, techTier: 3);
        Assert.True(t3 > t2);
        Assert.True(t3 < Grades.TechCeiling(3), "the comparison must happen below the ceiling");
    }

    [Fact]
    public void Bands_CoverTheUnitInterval_InDisplayOrder()
    {
        Assert.Equal(GradeBand.Crude, Grades.BandOf(0.0));
        Assert.Equal(GradeBand.Standard, Grades.BandOf(0.3));
        Assert.Equal(GradeBand.Fine, Grades.BandOf(0.5));
        Assert.Equal(GradeBand.Advanced, Grades.BandOf(0.7));
        Assert.Equal(GradeBand.Masterwork, Grades.BandOf(0.85));
        Assert.Equal(GradeBand.PrecursorGrade, Grades.BandOf(0.95));
        // monotone: band never decreases as grade rises
        var prev = GradeBand.Crude;
        for (double g = 0; g <= 1.0001; g += 0.01)
        {
            var band = Grades.BandOf(Math.Min(g, 1.0));
            Assert.True(band >= prev);
            prev = band;
        }
    }
}
