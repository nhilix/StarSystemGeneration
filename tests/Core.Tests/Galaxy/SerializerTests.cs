using System;
using System.IO;
using System.Linq;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class SerializerTests
{
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = 8 });

    [Fact]
    public void SameConfig_ByteIdenticalSerialization()
    {
        Assert.Equal(SkeletonSerializer.ToText(Build()), SkeletonSerializer.ToText(Build()));
    }

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var original = Build();
        var loaded = SkeletonSerializer.Load(new StringReader(SkeletonSerializer.ToText(original)));
        Assert.Equal(SkeletonSerializer.ToText(original), SkeletonSerializer.ToText(loaded));
        Assert.Equal(original.Polities.Count, loaded.Polities.Count);
        Assert.Equal(original.Events.Count, loaded.Events.Count);
        Assert.Equal(original.Cells.Count, loaded.Cells.Count);
        Assert.Equal(original.Config.MasterSeed, loaded.Config.MasterSeed);
    }

    [Fact]
    public void SchemaVersionMismatch_Throws_NeverSilentlyRebuilds()
    {
        var text = SkeletonSerializer.ToText(Build());
        var tampered = text.Replace("STARGEN-SKELETON|4", "STARGEN-SKELETON|999");
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader(tampered)));
    }

    [Fact]
    public void Load_RecordBeforeConfig_Throws()
    {
        var text = "STARGEN-SKELETON|4\nANCHOR|0|0|1|0|0|-1\nEND\n";
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader(text)));
    }

    [Fact]
    public void Load_TruncatedCellLine_Throws()
    {
        var text = SkeletonSerializer.ToText(Build());
        var lines = text.Split('\n');
        var cellLineIndex = Array.FindIndex(lines, l => l.StartsWith("CELL|"));
        Assert.True(cellLineIndex >= 0, "fixture must contain a CELL line");
        var fields = lines[cellLineIndex].Split('|');
        lines[cellLineIndex] = string.Join("|", fields.Take(3));
        var tampered = string.Join("\n", lines);
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader(tampered)));
    }

    [Fact]
    public void Load_NonNumericSchemaVersion_Throws()
    {
        var text = "STARGEN-SKELETON|abc\nEND\n";
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader(text)));
    }

    [Fact]
    public void GoldenSnapshot_SmallGalaxyHeader()
    {
        // Golden guard against unintended drift (spec §10). If this fails because of an
        // INTENTIONAL generation change, update the literal and say so in the commit.
        var s = SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = 7, GalaxyRadiusCells = 3 });
        var lines = SkeletonSerializer.ToText(s).Split('\n');
        Assert.Equal("STARGEN-SKELETON|4", lines[0].TrimEnd('\r'));
        // Golden facts recorded at implementation time — fill the two literals with the
        // observed values on first run, then they are frozen. Re-frozen for the economy
        // slice (schema v4): Events.Count rose from 30 to 37 because the live economy
        // (war, famine, trade) now emits additional event types the seeding-era count
        // never saw; Polities.Count is unaffected. Re-frozen again for task 10's
        // invariant-suite tuning (ProvisionsPerPop 1.0->0.5): Events.Count fell from 37 to 34 (fewer famine events at this seed);
        // Polities.Count still unaffected.
        Assert.Equal(2, s.Polities.Count);
        Assert.Equal(34, s.Events.Count);
    }

    [Fact]
    public void RoundTrip_PreservesNewConfigFields()
    {
        var s = SkeletonBuilder.Build(new GalaxyConfig
        {
            MasterSeed = 11, GalaxyRadiusCells = 3,
            ArmStrength = 0.6, CoreRadius = 0.25, DiscFalloff = 0.7,
            MineralAnchorMultiplier = 2.0, PrecursorAnchorMultiplier = 0.5,
        });
        string text = SkeletonSerializer.ToText(s);
        var loaded = SkeletonSerializer.Load(new StringReader(text));
        Assert.Equal(0.6, loaded.Config.ArmStrength);
        Assert.Equal(0.25, loaded.Config.CoreRadius);
        Assert.Equal(0.7, loaded.Config.DiscFalloff);
        Assert.Equal(2.0, loaded.Config.MineralAnchorMultiplier);
        Assert.Equal(0.5, loaded.Config.PrecursorAnchorMultiplier);
        Assert.Equal(text, SkeletonSerializer.ToText(loaded));
    }

    [Fact]
    public void Load_RejectsSchemaV2()
    {
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader("STARGEN-SKELETON|2\nEND\n")));
    }

    [Fact]
    public void RoundTrip_PreservesEconomyState_AndWars()
    {
        var original = Build();
        // Ensure at least one war exists to round-trip even if this seed fought none.
        if (original.Wars.Count == 0)
        {
            var w = new War { Id = 0, AttackerId = 0, DefenderId = 1, StartEpoch = 3,
                Goal = WarGoal.Chokepoint, AttackerWeariness = 0.4, DefenderWeariness = 1.1,
                AttackerCellsLost = 1, DefenderCellsLost = 2, Ended = true,
                Outcome = WarOutcome.WhitePeace };
            w.GoalCells.Add(original.Cells[0].Coord);
            original.Wars.Add(w);
        }
        var loaded = SkeletonSerializer.Load(new StringReader(SkeletonSerializer.ToText(original)));
        Assert.Equal(SkeletonSerializer.ToText(original), SkeletonSerializer.ToText(loaded));
        Assert.Equal(original.Wars.Count, loaded.Wars.Count);
        for (int i = 0; i < original.Wars.Count; i++)
        {
            Assert.Equal(original.Wars[i].Goal, loaded.Wars[i].Goal);
            Assert.Equal(original.Wars[i].Outcome, loaded.Wars[i].Outcome);
            Assert.Equal(original.Wars[i].GoalCells, loaded.Wars[i].GoalCells);
            Assert.Equal(original.Wars[i].FrontCells, loaded.Wars[i].FrontCells);
        }
        for (int i = 0; i < original.Polities.Count; i++)
        {
            Assert.Equal(original.Polities[i].MilitaryStockpile, loaded.Polities[i].MilitaryStockpile);
            Assert.Equal(original.Polities[i].TechTier, loaded.Polities[i].TechTier);
            Assert.Equal(original.Polities[i].Wealth, loaded.Polities[i].Wealth);
        }
        for (int i = 0; i < original.Cells.Count; i++)
        {
            Assert.Equal(original.Cells[i].Population, loaded.Cells[i].Population);
            Assert.Equal(original.Cells[i].PopulationSpeciesId, loaded.Cells[i].PopulationSpeciesId);
            Assert.Equal(original.Cells[i].RouteThroughput, loaded.Cells[i].RouteThroughput);
        }
    }

    [Fact]
    public void RoundTrip_PreservesEconomyKnobs()
    {
        var s = SkeletonBuilder.Build(new GalaxyConfig
        {
            MasterSeed = 11, GalaxyRadiusCells = 3,
            WarWearinessRate = 0.2, StockpileDecayRate = 0.05,
            TechThresholdBase = 20.0, TradeIncomeWeight = 0.8, ProvisionsPerPop = 1.5,
        });
        var loaded = SkeletonSerializer.Load(new StringReader(SkeletonSerializer.ToText(s)));
        Assert.Equal(0.2, loaded.Config.WarWearinessRate);
        Assert.Equal(0.05, loaded.Config.StockpileDecayRate);
        Assert.Equal(20.0, loaded.Config.TechThresholdBase);
        Assert.Equal(0.8, loaded.Config.TradeIncomeWeight);
        Assert.Equal(1.5, loaded.Config.ProvisionsPerPop);
    }

    [Fact]
    public void Load_RejectsSchemaV3()
    {
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader("STARGEN-SKELETON|3\nEND\n")));
    }

    [Fact]
    public void RoundTrip_WarWithEmptyCellLists_UsesDashSentinel()
    {
        var s = SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = 7, GalaxyRadiusCells = 3 });
        s.Wars.Add(new War { Id = s.Wars.Count, AttackerId = 0, DefenderId = 1, StartEpoch = 1,
            Goal = WarGoal.Punitive, Ended = true, Outcome = WarOutcome.WhitePeace });
        string text = SkeletonSerializer.ToText(s);
        Assert.Contains("|-|-", text);   // empty goal + front lists serialize as '-' sentinels
        var loaded = SkeletonSerializer.Load(new StringReader(text));
        var war = loaded.Wars[^1];
        Assert.Empty(war.GoalCells);
        Assert.Empty(war.FrontCells);
    }
}
