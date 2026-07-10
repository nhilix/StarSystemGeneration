using System;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

/// <summary>Tier-1 per-hex density since slice F: interpolated simulated
/// cell density × hex-scale clumping noise — a pure function of (config,
/// coordinate) through the cell layer. Geometry (membership, rim) is
/// config-only and unchanged.</summary>
public class DensityFieldTests
{
    private static GalaxySkeleton Skeleton(ulong seed = 42, int radius = 8) =>
        SkeletonBuilder.BuildShape(new GalaxyConfig
        { MasterSeed = seed, GalaxyRadiusCells = radius });

    [Fact]
    public void At_IsDeterministic_AndBounded()
    {
        var s = Skeleton();
        foreach (var hex in HexGrid.Spiral(new HexCoordinate(0, 0), 40)
                     .Where((_, i) => i % 7 == 0))
        {
            var v = DensityField.At(s, hex);
            Assert.Equal(v, DensityField.At(s, hex));
            Assert.InRange(v, 0.0, 1.0);
        }
    }

    [Fact]
    public void OutsideGalaxy_IsZero_AndNotInGalaxy()
    {
        var s = Skeleton();
        var far = new HexCoordinate(400, 0);
        Assert.False(DensityField.InGalaxy(s.Config, far));
        Assert.Equal(0.0, DensityField.At(s, far));
        Assert.True(DensityField.InGalaxy(s.Config, new HexCoordinate(0, 0)));
    }

    [Fact]
    public void HexDensity_FollowsTheSimulatedCellLayer()
    {
        var s = Skeleton();
        // hexes inside the densest and emptiest cells should read the
        // difference (averaged over the cell's spiral to smooth the noise)
        var densest = s.Cells.OrderByDescending(c => c.MeanDensity).First();
        var emptiest = s.Cells.OrderBy(c => c.MeanDensity).First();
        double Avg(RegionCell cell) =>
            HexGrid.Spiral(HexGrid.CellCenter(cell.Coord), HexGrid.CellRadius)
                .Average(h => DensityField.At(s, h));
        Assert.True(Avg(densest) > Avg(emptiest) + 0.2,
            $"dense cell {Avg(densest):F3} vs void cell {Avg(emptiest):F3}");
    }

    [Fact]
    public void MeanHexDensity_TracksTheCellNormalization()
    {
        var s = Skeleton();
        double sum = 0; int count = 0;
        foreach (var cell in s.Cells)
        {
            // cell centers sample every cell once — cheap proxy for the disc
            sum += DensityField.At(s, HexGrid.CellCenter(cell.Coord));
            count++;
        }
        // cells are normalized to MeanDensityTarget; the noise modulation is
        // mean-≈1 and the [0,1] clamp bites at the top, so allow a band
        Assert.InRange(sum / count, s.Config.MeanDensityTarget - 0.15,
                       s.Config.MeanDensityTarget + 0.15);
    }

    [Fact]
    public void InterpolationSmoothsCellEdges()
    {
        var s = Skeleton();
        // walk a straight hex line across several cells: no adjacent-hex
        // jump should exceed what the noise alone can produce plus a smooth
        // share of the cell contrast (paint jumped a full cell at the edge)
        double previous = -1;
        int bigJumps = 0, samples = 0;
        for (int q = -30; q <= 30; q++)
        {
            double v = DensityField.At(s, new HexCoordinate(q, 0));
            if (previous >= 0 && Math.Abs(v - previous) > 0.5) bigJumps++;
            previous = v; samples++;
        }
        Assert.True(bigJumps <= samples / 10,
            $"{bigJumps} hard jumps in {samples} — cell edges are printing through");
    }
}
