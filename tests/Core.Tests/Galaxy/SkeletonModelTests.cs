using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class SkeletonModelTests
{
    [Fact]
    public void Skeleton_CellLookups_Work()
    {
        var config = new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 };
        var skeleton = new GalaxySkeleton(config);
        Assert.Equal(3 * 3 * 4 + 1, skeleton.Cells.Count);   // 37 cells
        Assert.Equal(0, skeleton.Cells[0].SpiralIndex);
        Assert.Equal(new HexCoordinate(0, 0), skeleton.Cells[0].Coord);
        var cell = skeleton.CellAt(new HexCoordinate(2, -1));
        Assert.Equal(2, cell.Q);
        Assert.Equal(-1, cell.R);
        // any hex inside that cell's cluster maps back to it
        var member = HexGrid.CellCenter(new HexCoordinate(2, -1));
        Assert.Same(cell, skeleton.CellForHex(member));
        Assert.False(skeleton.TryGetCell(new HexCoordinate(99, 0), out _));
    }

    [Fact]
    public void Footprint_IsCircular_NotHexagonal()
    {
        var config = new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 8 };
        var skeleton = new GalaxySkeleton(config);
        var origin = new HexCoordinate(0, 0);

        // The circle circumscribes the old hexagonal footprint: every hexagon
        // cell is kept (ring-8 corners sit exactly on the circle)...
        Assert.True(skeleton.TryGetCell(new HexCoordinate(8, 0), out _));
        Assert.True(skeleton.Cells.Count > 3 * 8 * 9 + 1);
        // ...and cells past lattice ring 8 near the old flat sides are now in.
        Assert.Contains(skeleton.Cells, c => HexGrid.Distance(c.Coord, origin) > 8);

        // Every member cell's center lies within the world-space circle whose
        // radius is GalaxyRadiusCells lattice steps.
        double unit = CellWorldDistance(new HexCoordinate(1, 0));
        foreach (var cell in skeleton.Cells)
            Assert.True(CellWorldDistance(cell.Coord) <= 8 * unit + 1e-9,
                $"cell ({cell.Q},{cell.R}) outside the circular footprint");

        // Spiral order remains the determinism key.
        for (int i = 0; i < skeleton.Cells.Count; i++)
            Assert.Equal(i, skeleton.Cells[i].SpiralIndex);

        // Hex-level membership agrees with cell membership on both sides.
        var ringNineIn = skeleton.Cells.First(c => HexGrid.Distance(c.Coord, origin) > 8);
        Assert.True(DensityField.InGalaxy(config, HexGrid.CellCenter(ringNineIn.Coord)));
        Assert.False(DensityField.InGalaxy(config, HexGrid.CellCenter(new HexCoordinate(9, 0))));
    }

    private static double CellWorldDistance(HexCoordinate cellCoord)
    {
        var (x, y) = HexGrid.HexToWorld(HexGrid.CellCenter(cellCoord));
        return System.Math.Sqrt(x * x + y * y);
    }

    [Fact]
    public void RegionCell_Defaults()
    {
        var cell = new RegionCell { Q = 1, R = 2 };
        Assert.Equal(-1, cell.OwnerPolityId);
        Assert.Empty(cell.Anchors);
        Assert.False(cell.Contested);
        Assert.False(cell.WarScarred);
    }

    [Fact]
    public void EconModel_DefaultsAreNeutral()
    {
        var p = new Polity();
        Assert.Equal(0.0, p.MilitaryStockpile);
        Assert.Equal(0, p.TechTier);
        Assert.Equal(0.0, p.Wealth);
        var c = new RegionCell();
        Assert.Equal(0.0, c.Population);
        Assert.Equal(-1, c.PopulationSpeciesId);
        Assert.Equal(0.0, c.RouteThroughput);
        var w = new War();
        Assert.False(w.Ended);
        Assert.Equal(WarOutcome.Ongoing, w.Outcome);
    }

    [Fact]
    public void Homeworlds_SeedPopulation()
    {
        var s = SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 8 });
        foreach (var p in s.Polities)
        {
            // Seeding populates the homeworld anchor cell with the founding species; the
            // sim may shrink the quantity (famine, war scarring) but the species tag is
            // never reassigned and multiplicative shrinks never reach zero.
            var home = s.Cells.Single(c =>
                c.Anchors.Any(a => a.Type == AnchorType.Homeworld && a.SpeciesId == p.SpeciesId));
            Assert.Equal(p.SpeciesId, home.PopulationSpeciesId);
            Assert.True(home.Population > 0, "homeworld population never zeroes");
        }
    }

    [Fact]
    public void AtWar_ReadsLiveWarsOnly()
    {
        var s = new GalaxySkeleton(new GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 3 });
        s.Wars.Add(new War { Id = 0, AttackerId = 0, DefenderId = 1 });
        s.Wars.Add(new War { Id = 1, AttackerId = 2, DefenderId = 3, Ended = true });
        Assert.True(s.AtWar(0, 1));
        Assert.True(s.AtWar(1, 0));
        Assert.False(s.AtWar(2, 3));
        Assert.False(s.AtWar(0, 2));
    }
}
