using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class ActionPhaseTests
{
    private static GalaxySkeleton TwoPolities(double militancy0 = 0.9)
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; c.Metallicity = 0.3; }
        s.Species.Add(new SpeciesProfile { Id = 0, Name = "S0", Embodiment = Embodiment.TerranAnalog,
            Expansionism = 0.5, Cohesion = 0.5, Militancy = militancy0, Openness = 0.5, Industry = 0.5, Adaptability = 0.5 });
        s.Species.Add(new SpeciesProfile { Id = 1, Name = "S1", Embodiment = Embodiment.TerranAnalog,
            Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.1, Openness = 0.5, Industry = 0.5, Adaptability = 0.5 });
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = 0, CapitalR = 0, MilitaryStockpile = 5.0 });
        s.Polities.Add(new Polity { Id = 1, Name = "P1", SpeciesId = 1, CapitalQ = 1, CapitalR = 0, MilitaryStockpile = 5.0 });
        var c0 = s.CellAt(new HexCoordinate(0, 0));
        c0.OwnerPolityId = 0; c0.DevelopmentTier = 2; c0.Population = 2; c0.PopulationSpeciesId = 0;
        var c1 = s.CellAt(new HexCoordinate(1, 0));
        c1.OwnerPolityId = 1; c1.DevelopmentTier = 2; c1.Population = 2; c1.PopulationSpeciesId = 1;
        return s;
    }

    [Fact]
    public void Expansion_SpendsBudget_SeedsPopulationAndTier()
    {
        var s = TwoPolities();
        ActionPhase.Run(s, 0, new Dictionary<int, double> { [0] = 10.0, [1] = 0.0 });
        var claimed = s.Cells.Where(c => c.OwnerPolityId == 0).ToList();
        Assert.True(claimed.Count > 1, "expansion budget claims frontier cells");
        foreach (var c in claimed.Where(c => !(c.Q == 0 && c.R == 0)))
        {
            Assert.Equal(1, c.DevelopmentTier);
            Assert.Equal(0, c.PopulationSpeciesId);
            Assert.True(c.Population > 0);
        }
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.CellClaimed && e.ActorPolityId == 0);
    }

    [Fact]
    public void ZeroBudget_ClaimsNothing()
    {
        var s = TwoPolities();
        ActionPhase.Run(s, 0, new Dictionary<int, double> { [0] = 0.0, [1] = 0.0 });
        Assert.Equal(1, s.Cells.Count(c => c.OwnerPolityId == 0));
    }

    [Fact]
    public void HighMilitancy_EventuallyDeclaresWar_WithGoalAndContest()
    {
        var s = TwoPolities(militancy0: 0.95);
        s.Polities[0].OreBalance = -3.0;   // worst deficit → Ore goal
        for (int epoch = 0; epoch < 40 && s.Wars.Count == 0; epoch++)
            ActionPhase.Run(s, epoch, new Dictionary<int, double> { [0] = 0.0, [1] = 0.0 });
        Assert.NotEmpty(s.Wars);
        var war = s.Wars[0];
        Assert.Equal(0, war.AttackerId);
        Assert.Equal(1, war.DefenderId);
        Assert.Equal(WarGoal.Ore, war.Goal);
        Assert.InRange(war.GoalCells.Count, 1, 3);
        Assert.All(war.GoalCells, gc => Assert.True(s.CellAt(gc).Contested));
        Assert.Equal(war.GoalCells.Count, war.FrontCells.Count);
        var declared = s.Events.Single(e => e.Type == GalaxyEventType.WarStarted);
        Assert.Equal((int)WarGoal.Ore, declared.Detail);
    }

    [Fact]
    public void NoSecondWar_AgainstSameDefender()
    {
        var s = TwoPolities(militancy0: 0.95);
        for (int epoch = 0; epoch < 80; epoch++)
            ActionPhase.Run(s, epoch, new Dictionary<int, double> { [0] = 0.0, [1] = 0.0 });
        Assert.True(s.Wars.Count(w => !w.Ended) <= 1, "one live war per polity pair");
        Assert.Equal(s.Wars.Count,
            s.Events.Count(e => e.Type == GalaxyEventType.WarStarted));
    }

    [Fact]
    public void DepletedStockpile_PreventsDeclaration()
    {
        var s = TwoPolities(militancy0: 0.95);
        s.Polities[0].MilitaryStockpile = 0.0;
        for (int epoch = 0; epoch < 40; epoch++)
            ActionPhase.Run(s, epoch, new Dictionary<int, double> { [0] = 0.0, [1] = 0.0 });
        Assert.DoesNotContain(s.Wars, w => w.AttackerId == 0);
    }
}
