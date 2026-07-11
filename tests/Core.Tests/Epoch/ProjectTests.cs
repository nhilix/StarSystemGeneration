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

    [Fact]
    public void Facility_Uncommissioned_IsInactive_AndLaneIsDead()
    {
        var (_, state) = EpochTestKit.Seeded();
        // Seeded() runs genesis only — ports are founded later by Phases;
        // give the fixture the two ports AddLane needs (sibling tests'
        // convention, e.g. PlagueTests.cs).
        var a0 = state.Actors[0];
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a0.Id,
            new HexCoordinate(a0.Seat.Q + 10, a0.Seat.R), tier: 2, foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        var lane = EpochTestKit.AddLane(state, 0, 1);
        var gateA = state.Facilities[lane.GateAId];
        Assert.True(MarketEngine.IsActive(state, gateA));
        Assert.True(LaneMath.IsLive(state, lane));
        gateA.CommissionedYear = -1;               // under construction
        Assert.False(MarketEngine.IsActive(state, gateA));
        Assert.False(LaneMath.IsLive(state, lane));
    }
}
