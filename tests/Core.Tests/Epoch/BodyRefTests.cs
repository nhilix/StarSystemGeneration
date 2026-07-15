using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class BodyRefTests
{
    [Fact]
    public void None_IsNegativeOne_AndReportsIsNone()
    {
        Assert.Equal(-1, BodyRef.None.StarIndex);
        Assert.Equal(-1, BodyRef.None.SlotIndex);
        Assert.True(BodyRef.None.IsNone);
    }

    [Fact]
    public void RealAddress_IsNotNone_AndComparesByValue()
    {
        var a = new BodyRef(0, 2);
        var b = new BodyRef(0, 2);
        Assert.False(a.IsNone);
        Assert.Equal(a, b);
        Assert.NotEqual(a, new BodyRef(1, 2));
    }

    [Fact]
    public void SiteAnchoredRecords_DefaultBodyRef_IsNone()
    {
        var f = new StarGen.Core.Epoch.Facility(0, 0, 1,
            new StarGen.Core.Model.HexCoordinate(0, 0), 0, 100);
        var p = new StarGen.Core.Epoch.Project(0,
            StarGen.Core.Epoch.ProjectKind.FacilityConstruction, 1, 1, 0,
            new StarGen.Core.Model.HexCoordinate(0, 0), 4.0, 100);
        var seg = new StarGen.Core.Epoch.PopulationSegment(0, 0, 0, 0, 1.0);
        var fleet = new StarGen.Core.Epoch.FleetRecord(0, 0,
            new StarGen.Core.Model.HexCoordinate(0, 0));
        Assert.True(f.Body.IsNone);
        Assert.True(p.Body.IsNone);
        Assert.True(seg.Body.IsNone);
        Assert.True(fleet.Body.IsNone);
        f.Body = new BodyRef(0, 3);
        Assert.Equal(new BodyRef(0, 3), f.Body);
    }
}
