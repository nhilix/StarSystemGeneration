using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class ResolutionPhaseTests
{
    /// <summary>Two polities, one declared war over one goal cell owned by P1.</summary>
    private static GalaxySkeleton AtWarFixture(double attackerStock = 20.0, double defenderStock = 1.0)
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        for (int i = 0; i < 2; i++)
            s.Species.Add(new SpeciesProfile { Id = i, Name = $"S{i}", Embodiment = Embodiment.TerranAnalog,
                Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5, Openness = 0.5, Industry = 0.5, Adaptability = 0.5 });
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = -1, CapitalR = 0, MilitaryStockpile = attackerStock });
        s.Polities.Add(new Polity { Id = 1, Name = "P1", SpeciesId = 1, CapitalQ = 1, CapitalR = 0, MilitaryStockpile = defenderStock });
        var a = s.CellAt(new HexCoordinate(-1, 0));
        a.OwnerPolityId = 0; a.DevelopmentTier = 3; a.Population = 2; a.PopulationSpeciesId = 0;
        var goal = s.CellAt(new HexCoordinate(1, 0));
        goal.OwnerPolityId = 1; goal.DevelopmentTier = 2; goal.Population = 1; goal.PopulationSpeciesId = 1;
        var cap1 = s.CellAt(new HexCoordinate(2, 0));
        cap1.OwnerPolityId = 1; cap1.DevelopmentTier = 3; cap1.Population = 2; cap1.PopulationSpeciesId = 1;
        s.Polities[1].CapitalQ = 2; s.Polities[1].CapitalR = 0;
        var war = new War { Id = 0, AttackerId = 0, DefenderId = 1, StartEpoch = 0, Goal = WarGoal.Punitive };
        war.GoalCells.Add(goal.Coord);
        war.FrontCells.Add(goal.Coord);
        goal.Contested = true;
        s.Wars.Add(war);
        return s;
    }

    [Fact]
    public void EveryWar_Terminates()
    {
        var s = AtWarFixture(attackerStock: 5.0, defenderStock: 5.0);
        for (int epoch = 0; epoch < 100 && !s.Wars[0].Ended; epoch++)
            ResolutionPhase.Run(s, epoch);
        Assert.True(s.Wars[0].Ended, "weariness accrues monotonically; wars must end");
        Assert.NotEqual(WarOutcome.Ongoing, s.Wars[0].Outcome);
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.WarEnded
            && e.Detail == (int)s.Wars[0].Outcome);
    }

    [Fact]
    public void OverwhelmingAttacker_WinsAndAnnexesGoal()
    {
        var s = AtWarFixture(attackerStock: 100.0, defenderStock: 0.05);
        for (int epoch = 0; epoch < 100 && !s.Wars[0].Ended; epoch++)
            ResolutionPhase.Run(s, epoch);
        Assert.Equal(WarOutcome.AttackerVictory, s.Wars[0].Outcome);
        Assert.Equal(0, s.CellAt(new HexCoordinate(1, 0)).OwnerPolityId);
        Assert.False(s.CellAt(new HexCoordinate(1, 0)).Contested, "fronts demilitarize at termination");
    }

    [Fact]
    public void ContestedCells_GetWarScarred()
    {
        var s = AtWarFixture();
        ResolutionPhase.Run(s, 0);
        Assert.True(s.CellAt(new HexCoordinate(1, 0)).WarScarred);
    }

    [Fact]
    public void Weariness_AccruesMonotonically_WhileLive()
    {
        var s = AtWarFixture(attackerStock: 50.0, defenderStock: 50.0);
        double last = 0;
        for (int epoch = 0; epoch < 10 && !s.Wars[0].Ended; epoch++)
        {
            ResolutionPhase.Run(s, epoch);
            Assert.True(s.Wars[0].AttackerWeariness >= last);
            last = s.Wars[0].AttackerWeariness;
        }
        Assert.True(last > 0);
    }

    [Fact]
    public void LosingLastCell_MarksExtinct_AndVictorHoldsEverything()
    {
        var s = AtWarFixture(attackerStock: 100.0, defenderStock: 0.05);
        // Make the goal cell P1's ONLY cell → its loss is extinction.
        var cap1 = s.CellAt(new HexCoordinate(2, 0));
        cap1.OwnerPolityId = -1; cap1.DevelopmentTier = 0; cap1.Population = 0; cap1.PopulationSpeciesId = -1;
        s.Polities[1].CapitalQ = 1; s.Polities[1].CapitalR = 0;
        for (int epoch = 0; epoch < 100 && !s.Wars[0].Ended; epoch++)
            ResolutionPhase.Run(s, epoch);
        Assert.True(s.Polities[1].Extinct);
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.PolityExtinct && e.TargetPolityId == 1);
        Assert.Contains(s.Polities, p => p.Id == 1);   // retained, flagged
    }

    [Fact]
    public void StockpilesAttrit_WhileFighting()
    {
        var s = AtWarFixture(attackerStock: 50.0, defenderStock: 50.0);
        ResolutionPhase.Run(s, 0);
        Assert.True(s.Polities[0].MilitaryStockpile < 50.0);
        Assert.True(s.Polities[1].MilitaryStockpile < 50.0);
        Assert.True(s.Polities[0].MilitaryStockpile >= 0);
    }
}
