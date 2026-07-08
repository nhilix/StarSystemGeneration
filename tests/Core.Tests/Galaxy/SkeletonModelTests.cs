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
        var config = new GalaxyConfig { MasterSeed = 1, SizeSectors = 2 };  // 8x8 cells
        var skeleton = new GalaxySkeleton(config);
        Assert.Equal(config.CellsX * config.CellsY, skeleton.Cells.Length);
        var cell = skeleton.CellAt(3, 5);
        Assert.Equal(3, cell.Cx);
        Assert.Equal(5, cell.Cy);
        // hex (25, 52) -> cell (25/8, 52/10) = (3, 5)
        Assert.Same(cell, skeleton.CellForHex(new HexCoordinate(25, 52)));
    }

    [Fact]
    public void RegionCell_Defaults()
    {
        var cell = new RegionCell { Cx = 1, Cy = 2 };
        Assert.Equal(-1, cell.OwnerPolityId);
        Assert.Empty(cell.Anchors);
        Assert.False(cell.Contested);
        Assert.False(cell.WarScarred);
    }
}
