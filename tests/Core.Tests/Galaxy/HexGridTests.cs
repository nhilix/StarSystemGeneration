using System.Collections.Generic;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class HexGridTests
{
    [Fact]
    public void Directions_AreTheSixPinnedFlatTopVectors()
    {
        var expected = new[]
        {
            new HexCoordinate(1, 0), new HexCoordinate(1, -1), new HexCoordinate(0, -1),
            new HexCoordinate(-1, 0), new HexCoordinate(-1, 1), new HexCoordinate(0, 1),
        };
        Assert.Equal(expected, HexGrid.Directions);
    }

    [Fact]
    public void Neighbors_AreSymmetric()
    {
        var a = new HexCoordinate(3, -7);
        foreach (var b in HexGrid.Neighbors(a))
            Assert.Contains(a, HexGrid.Neighbors(b));
    }

    [Fact]
    public void Distance_IsAMetric()
    {
        var hexes = new List<HexCoordinate>();
        for (int q = -4; q <= 4; q += 2)
            for (int r = -4; r <= 4; r += 2)
                hexes.Add(new HexCoordinate(q, r));
        foreach (var a in hexes)
            foreach (var b in hexes)
            {
                Assert.Equal(HexGrid.Distance(a, b), HexGrid.Distance(b, a));
                Assert.True(HexGrid.Distance(a, b) == 0 == a.Equals(b));
                foreach (var c in hexes)
                    Assert.True(HexGrid.Distance(a, c)
                        <= HexGrid.Distance(a, b) + HexGrid.Distance(b, c));
            }
        Assert.Equal(1, HexGrid.Distance(new HexCoordinate(0, 0), new HexCoordinate(1, -1)));
        Assert.Equal(7, HexGrid.Distance(new HexCoordinate(0, 0), new HexCoordinate(7, -3)));
    }

    [Fact]
    public void Ring_HasExactly6R_AllAtDistanceR()
    {
        var center = new HexCoordinate(2, 1);
        for (int radius = 1; radius <= 6; radius++)
        {
            var ring = HexGrid.Ring(center, radius).ToList();
            Assert.Equal(6 * radius, ring.Count);
            Assert.Equal(ring.Count, ring.Distinct().Count());
            Assert.All(ring, h => Assert.Equal(radius, HexGrid.Distance(center, h)));
        }
    }

    [Fact]
    public void Spiral_HasCenteredHexagonalCount_AndIsDeterministic()
    {
        var center = new HexCoordinate(0, 0);
        for (int radius = 0; radius <= 6; radius++)
        {
            var spiral = HexGrid.Spiral(center, radius).ToList();
            Assert.Equal(3 * radius * (radius + 1) + 1, spiral.Count);
            Assert.Equal(spiral.Count, spiral.Distinct().Count());
            Assert.Equal(center, spiral[0]);
        }
        Assert.Equal(HexGrid.Spiral(center, 5).ToList(), HexGrid.Spiral(center, 5).ToList());
    }

    [Fact]
    public void WorldToHex_InvertsHexToWorld()
    {
        for (int q = -15; q <= 15; q += 3)
            for (int r = -15; r <= 15; r += 3)
            {
                var hex = new HexCoordinate(q, r);
                var (x, y) = HexGrid.HexToWorld(hex);
                Assert.Equal(hex, HexGrid.WorldToHex(x, y));
                // points near the center still round to the same hex
                Assert.Equal(hex, HexGrid.WorldToHex(x + 0.3, y - 0.3));
            }
    }

    [Fact]
    public void HexToWorld_NeighborsAreEquidistant()
    {
        var origin = new HexCoordinate(0, 0);
        var (ox, oy) = HexGrid.HexToWorld(origin);
        var distances = HexGrid.Neighbors(origin).Select(n =>
        {
            var (x, y) = HexGrid.HexToWorld(n);
            return System.Math.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy));
        }).ToList();
        Assert.All(distances, d => Assert.Equal(distances[0], d, 9));
    }

    [Fact]
    public void OffsetConversions_RoundTrip_AndStaggerOddColumns()
    {
        for (int q = -9; q <= 9; q++)
            for (int r = -9; r <= 9; r++)
            {
                var hex = new HexCoordinate(q, r);
                var (col, row) = HexGrid.ToOffset(hex);
                Assert.Equal(q, col);
                Assert.Equal(hex, HexGrid.FromOffset(col, row));
            }
        // odd-q: hex (1,0) sits half a hex lower in world y than (0,0)
        var y0 = HexGrid.HexToWorld(new HexCoordinate(0, 0)).Y;
        var y1 = HexGrid.HexToWorld(new HexCoordinate(1, 0)).Y;
        Assert.True(y1 > y0, "odd columns stagger downward in world space");
    }

    [Fact]
    public void Corners_SixUnitOffsets_SharedBetweenNeighbors()
    {
        Assert.Equal(6, HexGrid.CornerOffsets.Length);
        // flat-top: first corner due east at unit distance, 60° apart
        Assert.Equal(1.0, HexGrid.CornerOffsets[0].X, 9);
        Assert.Equal(0.0, HexGrid.CornerOffsets[0].Y, 9);
        foreach (var (x, y) in HexGrid.CornerOffsets)
            Assert.Equal(1.0, System.Math.Sqrt(x * x + y * y), 9);
        // adjacent hexes share exactly two corner positions
        var a = HexGrid.HexToWorld(new HexCoordinate(0, 0));
        var b = HexGrid.HexToWorld(new HexCoordinate(1, 0));
        int shared = 0;
        foreach (var ca in HexGrid.CornerOffsets)
            foreach (var cb in HexGrid.CornerOffsets)
                if (System.Math.Abs(a.X + ca.X - (b.X + cb.X)) < 1e-9
                    && System.Math.Abs(a.Y + ca.Y - (b.Y + cb.Y)) < 1e-9)
                    shared++;
        Assert.Equal(2, shared);
    }
}
