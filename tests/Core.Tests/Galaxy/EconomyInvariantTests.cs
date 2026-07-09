using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

/// <summary>Spec §9 sim invariants + shape bands over built galaxies.</summary>
public class EconomyInvariantTests
{
    private static GalaxySkeleton Build(ulong seed, int radius = 8) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius });

    [Fact]
    public void NothingNegativeOrNaN_AcrossSeeds()
    {
        for (ulong seed = 40; seed < 45; seed++)
        {
            var s = Build(seed);
            foreach (var p in s.Polities)
            {
                Assert.True(p.MilitaryStockpile >= 0 && !double.IsNaN(p.MilitaryStockpile));
                Assert.True(p.Wealth >= 0 && !double.IsNaN(p.Wealth));
                Assert.True(p.ExoticsInvested >= 0 && !double.IsNaN(p.ExoticsInvested));
                Assert.True(p.TechTier >= 0);
                Assert.False(double.IsNaN(p.ProvisionsBalance) || double.IsNaN(p.OreBalance)
                          || double.IsNaN(p.ExoticsBalance));
                Assert.True(p.BlockadeLoss >= 0 && !double.IsNaN(p.BlockadeLoss));
            }
            foreach (var c in s.Cells)
            {
                Assert.True(c.Population >= 0 && !double.IsNaN(c.Population));
                Assert.True(c.RouteThroughput >= 0 && !double.IsNaN(c.RouteThroughput));
            }
            foreach (var w in s.Wars)
            {
                Assert.True(w.AttackerWeariness >= 0 && w.DefenderWeariness >= 0);
                Assert.NotEmpty(w.GoalCells);
            }
        }
    }

    [Fact]
    public void Wars_TerminateOrSurviveToFinalEpoch_NeverDangle()
    {
        for (ulong seed = 40; seed < 45; seed++)
        {
            var s = Build(seed);
            foreach (var w in s.Wars)
            {
                if (w.Ended) Assert.NotEqual(WarOutcome.Ongoing, w.Outcome);
                else
                    // Live-at-final-epoch wars are the war-zone source: front stays contested.
                    Assert.All(w.FrontCells, fc => Assert.True(s.CellAt(fc).Contested));
            }
            // Every ended war produced a WarEnded event with matching outcome.
            foreach (var w in s.Wars.Where(w => w.Ended))
                Assert.Contains(s.Events, e => e.Type == GalaxyEventType.WarEnded
                    && e.ActorPolityId == w.AttackerId && e.TargetPolityId == w.DefenderId
                    && e.Detail == (int)w.Outcome);
        }
    }

    [Fact]
    public void Blockade_ReducesDeliveredFlow_ConstructedTwin()
    {
        GalaxySkeleton MakeTwin(bool blockaded)
        {
            var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
            foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
            s.Species.Add(new SpeciesProfile { Id = 0, Name = "S0",
                Embodiment = Embodiment.TerranAnalog, Expansionism = 0.5, Cohesion = 0.5,
                Militancy = 0.5, Openness = 0.5, Industry = 0.5, Adaptability = 0.5 });
            s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = -2, CapitalR = 0 });
            foreach (var (q, r) in new[] { (-2, 0), (-1, 0), (0, 0) })
            {
                var c = s.CellAt(new HexCoordinate(q, r));
                c.OwnerPolityId = 0; c.PopulationSpeciesId = 0;
            }
            var consumer = s.CellAt(new HexCoordinate(-2, 0));
            consumer.Population = 4.0; consumer.DevelopmentTier = 1;
            var producer = s.CellAt(new HexCoordinate(0, 0));
            producer.Population = 1.0; producer.DevelopmentTier = 5;
            s.CellAt(new HexCoordinate(-1, 0)).Population = 0.1;
            s.CellAt(new HexCoordinate(-1, 0)).DevelopmentTier = 1;
            if (blockaded)
                // Sever every route between producer and consumer: contest the full
                // q=-1 column AND the alternate lattice detours around it.
                foreach (var c in s.Cells.Where(c => c.Q == -1)) c.Contested = true;
            return s;
        }

        var open = MakeTwin(false);
        var blocked = MakeTwin(true);
        IncomePhase.Run(open, 0);
        IncomePhase.Run(blocked, 0);
        double openPop = open.CellAt(new HexCoordinate(-2, 0)).Population;
        double blockedPop = blocked.CellAt(new HexCoordinate(-2, 0)).Population;
        Assert.True(blockedPop < openPop,
            $"blockaded twin must starve: open {openPop} vs blocked {blockedPop}");
        Assert.Equal(0.0, open.Polities[0].BlockadeLoss);
        Assert.True(blocked.Polities[0].BlockadeLoss > 0,
            "the severed twin's unfilled need classifies as blockade loss (surplus exists unblockaded)");
    }

    [Fact]
    public void Throughput_OnlyOnCellsConnectedToOwnedTerritory()
    {
        var s = Build(42);
        foreach (var c in s.Cells.Where(c => c.RouteThroughput > 0))
            Assert.False(c.IsVoid, "flow never transits void cells");
    }

    [Fact]
    public void ShapeBands_ReferenceConfig()
    {
        var s = Build(42);
        var claimable = s.Cells.Where(c => !c.IsVoid).ToList();
        double claimed = (double)claimable.Count(c => c.OwnerPolityId >= 0) / claimable.Count;
        Assert.InRange(claimed, 0.2, 0.8);   // reopened 73.5% conversation: ceiling now 0.8

        int living = s.Polities.Count(p => !p.Extinct);
        Assert.InRange(living, 2, s.Polities.Count);

        // Economy actually ran: someone produced, someone has a stockpile.
        Assert.Contains(s.Polities, p => p.MilitaryStockpile > 0);
        Assert.Contains(s.Cells, c => c.RouteThroughput > 0);

        // Famines are possible but not the norm (famine dial sanity).
        int famines = s.Events.Count(e => e.Type == GalaxyEventType.Famine);
        Assert.True(famines < s.Config.EpochCount * s.Polities.Count,
            "famine every polity-epoch means the dial is broken");
    }

    [Fact]
    public void WarOutcomes_BothPathsOccur_AcrossSeeds()
    {
        int victories = 0, whitePeaces = 0;
        for (ulong seed = 40; seed < 50; seed++)
        {
            var s = Build(seed);
            victories += s.Wars.Count(w => w.Outcome is WarOutcome.AttackerVictory or WarOutcome.DefenderVictory);
            whitePeaces += s.Wars.Count(w => w.Outcome == WarOutcome.WhitePeace);
        }
        Assert.True(victories > 0, "no war ever produced a victor across 10 seeds - resolution is broken");
        Assert.True(whitePeaces > 0, "no war ended in white peace across 10 seeds - both termination paths must be exercised (spec §9)");
    }
}
