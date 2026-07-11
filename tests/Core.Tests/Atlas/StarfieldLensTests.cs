using System.Linq;
using StarGen.Core.Atlas;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The starfield — the density raster read as stars (the design
/// artifact's base layer: arms and bulge emerge from star density).
/// Placement is pure StableHash derivation: deterministic, never stored.</summary>
public class StarfieldLensTests
{
    [Fact]
    public void TheStarfieldIsDeterministic()
    {
        var (_, s1) = EpochTestKit.Seeded();
        var (_, s2) = EpochTestKit.Seeded();
        var a = StarfieldLens.Stars(new AtlasReadModel(s1));
        var b = StarfieldLens.Stars(new AtlasReadModel(s2));
        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++) Assert.Equal(a[i], b[i]);
    }

    [Fact]
    public void DenserCellsCarryMoreStars()
    {
        var (_, state) = EpochTestKit.Seeded();
        var model = new AtlasReadModel(state);
        var stars = StarfieldLens.Stars(model);
        Assert.True(stars.Count > model.Cells.Count / 2,
            "a seeded galaxy should carry a real starfield");
        // Aggregate check: mean density of cells weighted by star count
        // must exceed the unweighted mean — stars trace the arms.
        double meanDensity = model.Cells.Average(c => c.MeanDensity);
        double starWeighted = stars.Average(
            s => model.Cells[s.CellIndex].MeanDensity);
        Assert.True(starWeighted > meanDensity,
            $"stars should trace density: {starWeighted} vs {meanDensity}");
    }

    [Fact]
    public void StarsSitInsideTheirCell()
    {
        var (_, state) = EpochTestKit.Seeded();
        var model = new AtlasReadModel(state);
        foreach (var s in StarfieldLens.Stars(model).Take(200))
        {
            var cell = model.Cells[s.CellIndex];
            var (cx, cy) = StarGen.Core.Galaxy.HexGrid.HexToWorld(
                StarGen.Core.Galaxy.HexGrid.CellCenter(cell.Coord));
            double dx = s.X - cx, dy = s.Y - cy;
            // Within the superhex's circumradius (loose bound).
            Assert.True(dx * dx + dy * dy <= 12.0 * 12.0,
                $"star at ({s.X},{s.Y}) strays from cell center ({cx},{cy})");
            Assert.InRange(s.Brightness, 0.05, 1.0);
        }
    }
}
