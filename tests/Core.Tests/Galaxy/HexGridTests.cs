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
}
