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

    [Fact]
    public void Projects_RoundTrip_ByteIdentical()
    {
        var (_, state) = EpochTestKit.Seeded();
        // Seeded() runs genesis only — give the fixture the port the
        // project's site references and an in-flight facility
        // (CommissionedYear = -1, facilities-v2's round-trip case).
        var a0 = state.Actors[0];
        var port = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        // markets parallel ports (market index == port id) — Load's PORT
        // case adds one, so the fixture must too or the round trip diverges.
        state.Markets.Add(new Market(port.Id, state.Config.Economy));
        var facility = new Facility(0,
            (int)StarGen.Core.Substrate.InfraTypeId.Gate, 2, a0.Seat, a0.Id,
            state.WorldYear)
        { CommissionedYear = -1 };
        state.Facilities.Add(facility);

        var pr0 = state.Polities[0];
        var p = new Project(0, ProjectKind.PortRaise, pr0.ActorId,
            pr0.ActorId, 0, state.Ports[0].Hex, yearsRequired: 5.0,
            startedYear: 25)
        { TargetId = 0, Priority = ProjectPriority.Core, PlanOrder = 3,
          WagesPerYear = 8.0, YearsDelivered = 1.25, LastFedFraction = 0.6 };
        p.PerYearBasket[(int)StarGen.Core.Substrate.GoodId.Alloys] = 2.0;
        state.Projects.Add(p);
        pr0.LastIncomePerYear = 12.5;
        pr0.Mobilization = 0.4;
        string text = ArtifactSerializer.ToText(state);
        using var reader = new System.IO.StringReader(text);
        var loaded = ArtifactSerializer.Load(reader);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
        Assert.Single(loaded.Projects);
        Assert.Equal(5.0, loaded.Projects[0].YearsRequired, 9);
        // brief's literal PolityOf(1) doesn't address pr0 (ActorId 0, not 1)
        // — this fixture has exactly one polity, so PolityOf(pr0.ActorId) is
        // the intended round-trip check on the record actually modified.
        Assert.Equal(0.4, loaded.PolityOf(pr0.ActorId).Mobilization, 9);
        Assert.Equal(-1, loaded.Facilities[0].CommissionedYear);
    }
}
