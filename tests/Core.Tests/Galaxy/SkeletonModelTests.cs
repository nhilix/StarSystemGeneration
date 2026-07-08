using System.Collections.Generic;
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
    public void RegionCell_Defaults()
    {
        var cell = new RegionCell { Q = 1, R = 2 };
        Assert.Equal(-1, cell.OwnerPolityId);
        Assert.Empty(cell.Anchors);
        Assert.False(cell.Contested);
        Assert.False(cell.WarScarred);
    }
}
