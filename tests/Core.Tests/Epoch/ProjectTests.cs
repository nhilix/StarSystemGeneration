using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class ProjectTests
{
    [Fact]
    public void Project_ProgressAndDone_TrackDeliveredYears()
    {
        var p = new Project(0, ProjectKind.FacilityConstruction,
            ownerActorId: 1, funderActorId: 1, portId: 0,
            new HexCoordinate(0, 0), yearsRequired: 4.0, startedYear: 100);
        Assert.Equal(0.0, p.Progress, 9);
        Assert.True(p.InFlight);
        p.YearsDelivered = 2.0;
        Assert.Equal(0.5, p.Progress, 9);
        p.YearsDelivered = 4.0;
        Assert.Equal(1.0, p.Progress, 9);
    }

    [Fact]
    public void Project_ZeroDuration_IsInstantlyComplete()
    {
        var p = new Project(0, ProjectKind.ColonyExpedition, 1, 1, 0,
            new HexCoordinate(3, -1), yearsRequired: 0.0, startedYear: 50);
        Assert.Equal(1.0, p.Progress, 9);
    }
}
