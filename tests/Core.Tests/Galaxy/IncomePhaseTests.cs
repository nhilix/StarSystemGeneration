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
}
