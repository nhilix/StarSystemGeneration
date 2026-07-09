using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class AllocationPhaseTests
{
    private static GalaxySkeleton Fixture(double militancy = 0.5, double industry = 0.5)
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        s.Species.Add(new SpeciesProfile
        {
            Id = 0, Name = "S0", Embodiment = Embodiment.TerranAnalog,
            Expansionism = 0.5, Cohesion = 0.5, Militancy = militancy,
            Openness = 0.5, Industry = industry, Adaptability = 0.5,
        });
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = 0, CapitalR = 0 });
        foreach (var (q, r) in new[] { (0, 0), (1, 0), (0, 1) })
        {
            var c = s.CellAt(new HexCoordinate(q, r));
            c.OwnerPolityId = 0; c.DevelopmentTier = 3; c.Population = 2.0; c.PopulationSpeciesId = 0;
        }
        return s;
    }

    [Fact]
    public void Run_ProducesNonNegativeBudgets_AndGrowsStockpile()
    {
        var s = Fixture();
        var budgets = AllocationPhase.Run(s, 0);
        Assert.True(budgets[0] >= 0);
        Assert.True(s.Polities[0].MilitaryStockpile > 0, "military spend grows the stockpile");
        Assert.True(s.Polities[0].Wealth >= 0);
    }

    [Fact]
    public void Stockpile_DecaysWithoutSpending()
    {
        var s = Fixture();
        var p = s.Polities[0];
        p.MilitaryStockpile = 100.0;
        foreach (var c in s.Cells) c.OwnerPolityId = -1;   // no income at all
        AllocationPhase.Run(s, 0);
        Assert.True(p.MilitaryStockpile < 100.0, "stockpile decays");
        Assert.True(p.MilitaryStockpile >= 0);
    }

    [Fact]
    public void DevelopmentSpending_RaisesTiers_UpToCeiling()
    {
        var s = Fixture(industry: 0.9);
        int before = s.Cells.Where(c => c.OwnerPolityId == 0).Sum(c => c.DevelopmentTier);
        for (int e = 0; e < 30; e++) { s.Polities[0].OreBalance = 1.0; AllocationPhase.Run(s, e); }
        int after = s.Cells.Where(c => c.OwnerPolityId == 0).Sum(c => c.DevelopmentTier);
        Assert.True(after > before, "development budget raises tiers");
        Assert.All(s.Cells.Where(c => c.OwnerPolityId == 0),
            c => Assert.True(c.DevelopmentTier <= Economy.DevCeiling(s.Polities[0].TechTier)));
    }

    [Fact]
    public void OreDeficit_StallsDevelopment()
    {
        var s = Fixture(industry: 0.9);
        s.Polities[0].OreBalance = -5.0;
        int before = s.Cells.Where(c => c.OwnerPolityId == 0).Sum(c => c.DevelopmentTier);
        AllocationPhase.Run(s, 0);
        int after = s.Cells.Where(c => c.OwnerPolityId == 0).Sum(c => c.DevelopmentTier);
        Assert.Equal(before, after);
    }

    [Fact]
    public void ExoticsSurplus_CrossesTechThresholds_AndLogsEvent()
    {
        var s = Fixture(industry: 0.9);
        var p = s.Polities[0];
        p.ExoticsBalance = 100.0;   // >> TechThresholdBase 5
        AllocationPhase.Run(s, 0);
        Assert.True(p.TechTier >= 1, "big exotics surplus crosses tier 1");
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.TechAdvance && e.ActorPolityId == 0);
        Assert.Equal(p.TechTier, s.Events.Last(e => e.Type == GalaxyEventType.TechAdvance).Detail);
    }

    [Fact]
    public void WarFooting_PaysUpkeep_AndShiftsBudgetTowardMilitary()
    {
        var sPeace = Fixture();
        var sWar = Fixture();
        sWar.Wars.Add(new War { Id = 0, AttackerId = 0, DefenderId = 99 });
        sPeace.Polities[0].MilitaryStockpile = sWar.Polities[0].MilitaryStockpile = 50.0;
        var peaceBudgets = AllocationPhase.Run(sPeace, 0);
        var warBudgets = AllocationPhase.Run(sWar, 0);
        // Upkeep shrinks the pool and doubled militancy weight shifts shares:
        // the at-war polity expands less and stockpiles more than its peaceful twin.
        Assert.True(warBudgets[0] < peaceBudgets[0],
            "war upkeep + military shift shrink the expansion budget");
        Assert.True(sWar.Polities[0].MilitaryStockpile > sPeace.Polities[0].MilitaryStockpile,
            "doubled militancy weight grows the at-war stockpile faster");
    }
}
