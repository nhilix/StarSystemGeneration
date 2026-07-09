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

    /// <summary>Three polities, two wars, id-ordered so polity A (id 1) is resolved
    /// as war 0's defender before war 1's attacker in the same Run() call: B (id 0)
    /// overwhelmingly attacks A over A's one and only cell (war 0, id 0); A, still
    /// alive at the start of the epoch, is separately the attacker against C (id 2,
    /// overwhelmingly strong) over one of C's cells (war 1, id 1). When A loses its
    /// last cell mid-epoch in war 0's battle it goes extinct; war 1 must then also
    /// terminate that same epoch (ghost-attacker regression, I-1) instead of letting
    /// the now-extinct A keep contesting and re-acquiring territory in C's war.</summary>
    private static GalaxySkeleton GhostAttackerFixture()
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        for (int i = 0; i < 3; i++)
            s.Species.Add(new SpeciesProfile { Id = i, Name = $"S{i}", Embodiment = Embodiment.TerranAnalog,
                Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5, Openness = 0.5, Industry = 0.5, Adaptability = 0.5 });

        // B (id 0): overwhelming attacker in war 0.
        s.Polities.Add(new Polity { Id = 0, Name = "B", SpeciesId = 0, CapitalQ = -3, CapitalR = 0, MilitaryStockpile = 100.0 });
        // A (id 1): defender in war 0 (owns ONLY the war-0 goal cell, so losing it is
        // extinction); attacker in war 1.
        s.Polities.Add(new Polity { Id = 1, Name = "A", SpeciesId = 1, CapitalQ = 1, CapitalR = 0, MilitaryStockpile = 1.0 });
        // C (id 2): overwhelming defender in war 1.
        s.Polities.Add(new Polity { Id = 2, Name = "C", SpeciesId = 2, CapitalQ = 3, CapitalR = -1, MilitaryStockpile = 1000.0 });

        var bCap = s.CellAt(new HexCoordinate(-3, 0));
        bCap.OwnerPolityId = 0; bCap.DevelopmentTier = 3; bCap.Population = 2; bCap.PopulationSpeciesId = 0;

        var aOnly = s.CellAt(new HexCoordinate(1, 0));
        aOnly.OwnerPolityId = 1; aOnly.DevelopmentTier = 2; aOnly.Population = 1; aOnly.PopulationSpeciesId = 1;
        s.Polities[1].CapitalQ = 1; s.Polities[1].CapitalR = 0;

        var cCap = s.CellAt(new HexCoordinate(3, -1));
        cCap.OwnerPolityId = 2; cCap.DevelopmentTier = 3; cCap.Population = 2; cCap.PopulationSpeciesId = 2;
        var cGoal = s.CellAt(new HexCoordinate(3, 0));
        cGoal.OwnerPolityId = 2; cGoal.DevelopmentTier = 2; cGoal.Population = 1; cGoal.PopulationSpeciesId = 2;

        var war0 = new War { Id = 0, AttackerId = 0, DefenderId = 1, StartEpoch = 0, Goal = WarGoal.Punitive };
        war0.GoalCells.Add(aOnly.Coord);
        war0.FrontCells.Add(aOnly.Coord);
        aOnly.Contested = true;
        s.Wars.Add(war0);

        var war1 = new War { Id = 1, AttackerId = 1, DefenderId = 2, StartEpoch = 0, Goal = WarGoal.Punitive };
        war1.GoalCells.Add(cGoal.Coord);
        war1.FrontCells.Add(cGoal.Coord);
        cGoal.Contested = true;
        s.Wars.Add(war1);

        return s;
    }

    [Fact]
    public void ExtinctAttacker_CannotKeepFightingOrAnnexTerritory()
    {
        var s = GhostAttackerFixture();
        for (int epoch = 0; epoch < 100 && !s.Wars[0].Ended; epoch++)
            ResolutionPhase.Run(s, epoch);

        Assert.True(s.Wars[0].Ended, "war 0 (B attacks A) must terminate");
        Assert.True(s.Polities[1].Extinct, "A must go extinct losing its only cell");
        Assert.True(s.Wars[1].Ended, "war 1 (A attacks C) must terminate the same epoch A goes extinct");
        Assert.NotEqual(WarOutcome.AttackerVictory, s.Wars[1].Outcome);
        Assert.DoesNotContain(s.Cells, c => c.OwnerPolityId == 1);
    }

    [Fact]
    public void BlockadeStrain_CountsAsHardship_ForWeariness()
    {
        var strained = AtWarFixture(attackerStock: 50.0, defenderStock: 50.0);
        var relaxed = AtWarFixture(attackerStock: 50.0, defenderStock: 50.0);
        strained.Polities[0].BlockadeLoss = Economy.TradeBlockedFloor + 1.0;
        ResolutionPhase.Run(strained, 0);
        ResolutionPhase.Run(relaxed, 0);
        // Identical seeds → identical battle rolls; only the 1.5× hardship multiplier differs.
        Assert.Equal(relaxed.Wars[0].AttackerWeariness * 1.5, strained.Wars[0].AttackerWeariness, 10);
        Assert.Equal(relaxed.Wars[0].DefenderWeariness, strained.Wars[0].DefenderWeariness, 10);
    }
}
