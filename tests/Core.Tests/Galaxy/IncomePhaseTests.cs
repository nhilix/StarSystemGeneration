using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class IncomePhaseTests
{
    /// <summary>Two-polity fixture on a blank radius-3 lattice: P0 (terran) owns a
    /// 3-cell west chain, P1 owns one east cell. Densities/metallicity hand-set.</summary>
    private static GalaxySkeleton Fixture()
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; c.Metallicity = 0.3; }
        for (int i = 0; i < 2; i++)
        {
            s.Species.Add(new SpeciesProfile
            {
                Id = i, Name = $"S{i}", Embodiment = Embodiment.TerranAnalog,
                Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5,
                Openness = 0.5, Industry = 0.5, Adaptability = 0.5,
            });
        }
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = -2, CapitalR = 0 });
        s.Polities.Add(new Polity { Id = 1, Name = "P1", SpeciesId = 1, CapitalQ = 2, CapitalR = 0 });
        foreach (var (q, r) in new[] { (-2, 0), (-1, 0), (0, 0) })
        {
            var c = s.CellAt(new HexCoordinate(q, r));
            c.OwnerPolityId = 0; c.DevelopmentTier = 2; c.Population = 1.0; c.PopulationSpeciesId = 0;
        }
        var e = s.CellAt(new HexCoordinate(2, 0));
        e.OwnerPolityId = 1; e.DevelopmentTier = 2; e.Population = 1.0; e.PopulationSpeciesId = 1;
        return s;
    }

    [Fact]
    public void Run_SetsBalances_AndNothingGoesNegativeOrNaN()
    {
        var s = Fixture();
        IncomePhase.Run(s, 0);
        foreach (var p in s.Polities)
        {
            Assert.False(double.IsNaN(p.ProvisionsBalance) || double.IsNaN(p.OreBalance)
                      || double.IsNaN(p.ExoticsBalance) || double.IsNaN(p.Wealth));
            Assert.True(p.Wealth >= 0);
        }
        foreach (var c in s.Cells)
        {
            Assert.True(c.Population >= 0);
            Assert.True(c.RouteThroughput >= 0);
            Assert.False(double.IsNaN(c.Population) || double.IsNaN(c.RouteThroughput));
        }
    }

    [Fact]
    public void SurplusRoutesToDeficit_AndAccumulatesThroughput()
    {
        var s = Fixture();
        // Make the P0 capital a heavy consumer (big population), its chain-end a producer.
        var capital = s.CellAt(new HexCoordinate(-2, 0));
        capital.Population = 4.0; capital.DevelopmentTier = 1;
        var farm = s.CellAt(new HexCoordinate(0, 0));
        farm.DevelopmentTier = 5; farm.Population = 1.0;
        IncomePhase.Run(s, 0);
        var middle = s.CellAt(new HexCoordinate(-1, 0));
        Assert.True(middle.RouteThroughput > 0, "flow from farm to capital transits the middle cell");
    }

    [Fact]
    public void UnfedCells_Famine_ShrinksPopulation_AndLogsEvent()
    {
        var s = Fixture();
        // Starve P1: huge population, no development to feed it, no trade partner
        // adjacency (P0 is 2+ cells away and produces little surplus).
        var e = s.CellAt(new HexCoordinate(2, 0));
        e.Population = 10.0; e.DevelopmentTier = 1;
        double before = e.Population;
        IncomePhase.Run(s, 3);
        Assert.True(e.Population < before, "famine shrinks population");
        Assert.Contains(s.Events, ev => ev.Type == GalaxyEventType.Famine && ev.ActorPolityId == 1);
    }

    [Fact]
    public void FedCells_GrowTowardDevelopmentCap()
    {
        var s = Fixture();
        var c = s.CellAt(new HexCoordinate(0, 0));
        c.DevelopmentTier = 4; c.Population = 0.5;
        IncomePhase.Run(s, 0);
        Assert.True(c.Population > 0.5, "fed cells grow");
        for (int i = 0; i < 50; i++) IncomePhase.Run(s, i);
        Assert.True(c.Population <= 1 + c.DevelopmentTier + 1e-9, "population caps at 1 + dev tier");
    }

    [Fact]
    public void Throughput_IsSnapshot_ResetEachRun()
    {
        var s = Fixture();
        var capital = s.CellAt(new HexCoordinate(-2, 0));
        capital.Population = 4.0; capital.DevelopmentTier = 1;
        s.CellAt(new HexCoordinate(0, 0)).DevelopmentTier = 5;
        IncomePhase.Run(s, 0);
        double t1 = s.CellAt(new HexCoordinate(-1, 0)).RouteThroughput;
        IncomePhase.Run(s, 1);
        double t2 = s.CellAt(new HexCoordinate(-1, 0)).RouteThroughput;
        Assert.True(t2 <= t1 * 2 + 1.0, "throughput must not accumulate across epochs unboundedly");
    }

    [Fact]
    public void CrossPolityTrade_MatchedSurplus_EnrichesBothSides()
    {
        var s = Fixture();
        // Relocate P1 adjacent to P0's chain end so the polities share a border.
        var old = s.CellAt(new HexCoordinate(2, 0));
        old.OwnerPolityId = -1; old.DevelopmentTier = 0; old.Population = 0; old.PopulationSpeciesId = -1;
        var adj = s.CellAt(new HexCoordinate(1, 0));
        adj.OwnerPolityId = 1; adj.DevelopmentTier = 1; adj.Population = 3.0; adj.PopulationSpeciesId = 1;
        s.Polities[1].CapitalQ = 1; s.Polities[1].CapitalR = 0;
        // P0 runs a provisions surplus (one high-dev cell); P1 runs a deficit (pop 3, dev 1).
        s.CellAt(new HexCoordinate(0, 0)).DevelopmentTier = 5;
        IncomePhase.Run(s, 0);
        Assert.True(s.Polities[0].Wealth > 0, "exporter gains trade wealth");
        Assert.Equal(s.Polities[0].Wealth, s.Polities[1].Wealth, 10);   // symmetric gain
    }

    [Fact]
    public void CrossPolityTrade_SuppressedByWar()
    {
        var s = Fixture();
        var old = s.CellAt(new HexCoordinate(2, 0));
        old.OwnerPolityId = -1; old.DevelopmentTier = 0; old.Population = 0; old.PopulationSpeciesId = -1;
        var adj = s.CellAt(new HexCoordinate(1, 0));
        adj.OwnerPolityId = 1; adj.DevelopmentTier = 1; adj.Population = 3.0; adj.PopulationSpeciesId = 1;
        s.Polities[1].CapitalQ = 1; s.Polities[1].CapitalR = 0;
        s.CellAt(new HexCoordinate(0, 0)).DevelopmentTier = 5;
        s.Wars.Add(new War { Id = 0, AttackerId = 0, DefenderId = 1 });
        IncomePhase.Run(s, 0);
        Assert.Equal(0.0, s.Polities[0].Wealth);
        Assert.Equal(0.0, s.Polities[1].Wealth);
    }

    /// <summary>Neutral-polity corridor severed by third-party contested cells (the
    /// parent spec §5 canonical scenario): P0 fights no war, yet its producer→consumer
    /// route is blockaded. Strain must accrue and, above the floor, fire TradeBlocked.
    /// Arithmetic (defaults: ProvisionsPerPop 0.5, density 0.5, terran affinity 1.0):
    /// consumer (dev 1, pop 8) nets 0.5 − 4.0 = −3.5; producer (dev 5, pop 1) nets
    /// 2.5 − 0.5 = +2.0, reachable only through the contested q=−1 column → the whole
    /// unfilled 3.5 classifies as blockade loss (a surplus IS reachable unblockaded).</summary>
    private static GalaxySkeleton SeveredNeutralFixture(double consumerPop)
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        s.Species.Add(new SpeciesProfile
        {
            Id = 0, Name = "S0", Embodiment = Embodiment.TerranAnalog,
            Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5,
            Openness = 0.5, Industry = 0.5, Adaptability = 0.5,
        });
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = -2, CapitalR = 0 });
        foreach (var (q, r) in new[] { (-2, 0), (0, 0) })
        {
            var c = s.CellAt(new HexCoordinate(q, r));
            c.OwnerPolityId = 0; c.PopulationSpeciesId = 0;
        }
        var consumer = s.CellAt(new HexCoordinate(-2, 0));
        consumer.Population = consumerPop; consumer.DevelopmentTier = 1;
        var producer = s.CellAt(new HexCoordinate(0, 0));
        producer.Population = 1.0; producer.DevelopmentTier = 5;
        // Sever the corridor with third-party contest (P0 is at war with nobody):
        // the full q=−1 column cuts the radius-3 disc in two.
        foreach (var c in s.Cells.Where(c => c.Q == -1)) c.Contested = true;
        return s;
    }

    [Fact]
    public void NeutralPolity_SeveredByThirdPartyContest_AccruesStrain_AndFiresTradeBlocked()
    {
        var s = SeveredNeutralFixture(consumerPop: 8.0);
        IncomePhase.Run(s, 0);
        Assert.True(s.Polities[0].BlockadeLoss > Economy.TradeBlockedFloor,
            $"blockade loss {s.Polities[0].BlockadeLoss} must exceed the event floor");
        Assert.Contains(s.Events, e => e.Type == GalaxyEventType.TradeBlocked && e.ActorPolityId == 0);
    }

    [Fact]
    public void WarringPolity_WithNoSurplusAnywhere_AccruesNoStrain_NoEvent()
    {
        var s = SeveredNeutralFixture(consumerPop: 8.0);
        // Remove the producer's output entirely and put P0 at war: scarcity while at
        // war must NOT read as blockade (the old HasLiveWar-gated false positive).
        var producer = s.CellAt(new HexCoordinate(0, 0));
        producer.DevelopmentTier = 0; producer.Population = 0.0;
        s.Polities.Add(new Polity { Id = 1, Name = "P1", SpeciesId = 0, CapitalQ = 3, CapitalR = 0 });
        var enemyCell = s.CellAt(new HexCoordinate(3, 0));
        enemyCell.OwnerPolityId = 1; enemyCell.DevelopmentTier = 1;
        enemyCell.Population = 0.5; enemyCell.PopulationSpeciesId = 0;
        var war = new War { Id = 0, AttackerId = 0, DefenderId = 1, StartEpoch = 0 };
        war.GoalCells.Add(enemyCell.Coord);
        war.FrontCells.Add(enemyCell.Coord);
        s.Wars.Add(war);
        IncomePhase.Run(s, 0);
        Assert.Equal(0.0, s.Polities[0].BlockadeLoss);
        Assert.DoesNotContain(s.Events, e => e.Type == GalaxyEventType.TradeBlocked);
    }

    /// <summary>Cross-polity classification: two non-belligerents with complementary
    /// provisions positions whose capital-capital path is severed by third-party
    /// contest. Arithmetic: P0 (0,0) dev 5 pop 1 nets +2.0 (its (1,0) dev 1 pop 1 cell
    /// nets 0); P1 (3,0) dev 1 pop 4 nets −1.5 ((2,0) dev 0 pop 0 nets 0) →
    /// give = 1.5, blocked by the contested q=1 column → both sides accrue 1.5
    /// (below the 2.0 event floor: strain state without the event).</summary>
    [Fact]
    public void CrossPolityTrade_BlockedPath_AccruesStrainOnBothPartners()
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        foreach (var c in s.Cells) { c.MeanDensity = 0.5; c.IsVoid = false; }
        for (int i = 0; i < 2; i++)
            s.Species.Add(new SpeciesProfile
            {
                Id = i, Name = $"S{i}", Embodiment = Embodiment.TerranAnalog,
                Expansionism = 0.5, Cohesion = 0.5, Militancy = 0.5,
                Openness = 0.5, Industry = 0.5, Adaptability = 0.5,
            });
        s.Polities.Add(new Polity { Id = 0, Name = "P0", SpeciesId = 0, CapitalQ = 0, CapitalR = 0 });
        s.Polities.Add(new Polity { Id = 1, Name = "P1", SpeciesId = 1, CapitalQ = 3, CapitalR = 0 });
        var p0Cap = s.CellAt(new HexCoordinate(0, 0));
        p0Cap.OwnerPolityId = 0; p0Cap.DevelopmentTier = 5; p0Cap.Population = 1.0; p0Cap.PopulationSpeciesId = 0;
        var p0Edge = s.CellAt(new HexCoordinate(1, 0));
        p0Edge.OwnerPolityId = 0; p0Edge.DevelopmentTier = 1; p0Edge.Population = 1.0; p0Edge.PopulationSpeciesId = 0;
        var p1Edge = s.CellAt(new HexCoordinate(2, 0));
        p1Edge.OwnerPolityId = 1; p1Edge.DevelopmentTier = 0; p1Edge.Population = 0.0; p1Edge.PopulationSpeciesId = 1;
        var p1Cap = s.CellAt(new HexCoordinate(3, 0));
        p1Cap.OwnerPolityId = 1; p1Cap.DevelopmentTier = 1; p1Cap.Population = 4.0; p1Cap.PopulationSpeciesId = 1;
        // Sever every capital-capital path: contest the full q=1 and q=2 columns
        // (the polities still share the (1,0)-(2,0) border, so trade is attempted).
        foreach (var c in s.Cells.Where(c => c.Q == 1 || c.Q == 2)) c.Contested = true;
        IncomePhase.Run(s, 0);
        Assert.Equal(1.5, s.Polities[0].BlockadeLoss, 10);
        Assert.Equal(1.5, s.Polities[1].BlockadeLoss, 10);
        Assert.DoesNotContain(s.Events, e => e.Type == GalaxyEventType.TradeBlocked);
    }

    [Fact]
    public void FamineAndWarScar_StackOnTheSameCell()
    {
        var s = Fixture();
        var e = s.CellAt(new HexCoordinate(2, 0));
        e.Population = 10.0; e.DevelopmentTier = 1;   // starving, as in the famine test
        e.Contested = true; e.WarScarred = true;      // and besieged
        IncomePhase.Run(s, 0);
        // Famine ×0.8 then war-scar ×0.95: separate population pressures compound
        // (deferred-tickets spec §5).
        Assert.Equal(10.0 * 0.8 * 0.95, e.Population, 10);
    }
}
