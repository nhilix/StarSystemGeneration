using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Task 8 (slice L2): facility groundbreaking carries a per-PORT
/// world-time cadence gate, mirroring Expansion.FoundingCadenceYears — a
/// port's crews break ground on at most one new facility per
/// Infrastructure.FacilityGroundbreakCadenceYears, so a finer clock cannot
/// accrete facilities (and downstream shipyards/hull batches) faster than a
/// coarse one over the same world-years (time-not-ticks, P7). The gate is
/// PER PORT, not per polity: a multi-port empire still builds at every port
/// concurrently.</summary>
public class FacilityCadenceTests
{
    // A non-extraction facility type rides the port body and is never
    // rejected for a None body (BodySiting.IsExtraction is false), so the
    // groundbreak's only ceilings are cap/treasury/cadence — exactly what
    // these tests exercise.
    private const int NonExtractionType = (int)InfraTypeId.Fabricator;

    /// <summary>Stand up an isolated own port far from every other port so a
    /// facility built beside it attaches to it (not a distant neighbour).</summary>
    private static Port AddIsolatedPort(SimState state, int ownerActorId, int qOffset)
    {
        var hex = new HexCoordinate(qOffset, 0);
        var port = new Port(state.Ports.Count, ownerActorId, hex, tier: 1,
                            state.WorldYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, state.Config.Economy));
        return port;
    }

    private static int FacilityProjectsAt(SimState state, int portId)
    {
        int n = 0;
        foreach (var p in state.Projects)
            if (p.Kind == ProjectKind.FacilityConstruction && p.PortId == portId)
                n++;
        return n;
    }

    private static void SetPlan(SimState state, int actorId, params PlanEntry[] entries)
    {
        var actor = state.Actors[actorId];
        actor.Policies = PolityPolicies.Default with
        {
            Plan = new StandingPlan(entries),
        };
    }

    // ---- within one cadence window, one port breaks ground once ----
    [Fact]
    public void TwoGroundbreaksAtOnePort_WithinCadence_OnlyOneBuilds()
    {
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        var pr = state.Polities[ProjectOpsTests.FirstEnteredPolity(state)];
        pr.DevelopmentPoints += 1e9;
        var port = AddIsolatedPort(state, pr.ActorId, qOffset: 500);
        // two DIFFERENT empty hexes beside the port (so neither is "site
        // taken" by the other), both due now
        var hexA = new HexCoordinate(port.Hex.Q + 1, port.Hex.R);
        var hexB = new HexCoordinate(port.Hex.Q + 2, port.Hex.R);
        SetPlan(state, pr.ActorId,
            new PlanEntry(PlanEntryKind.Facility, ProjectPriority.Core,
                state.WorldYear, NonExtractionType, port.Id, hexA, 1),
            new PlanEntry(PlanEntryKind.Facility, ProjectPriority.Core,
                state.WorldYear, NonExtractionType, port.Id, hexB, 1));
        new AllocationPhase().Run(state);
        // the cadence gate holds the second groundbreak — one build per port
        // per window, no matter how many entries come due together
        Assert.Equal(1, FacilityProjectsAt(state, port.Id));
    }

    // ---- once the cadence window elapses, the port can build again ----
    [Fact]
    public void SecondGroundbreak_AfterCadenceElapses_CanBuild()
    {
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        var pr = state.Polities[ProjectOpsTests.FirstEnteredPolity(state)];
        pr.DevelopmentPoints += 1e9;
        var port = AddIsolatedPort(state, pr.ActorId, qOffset: 500);
        var hexA = new HexCoordinate(port.Hex.Q + 1, port.Hex.R);
        var hexB = new HexCoordinate(port.Hex.Q + 2, port.Hex.R);
        SetPlan(state, pr.ActorId,
            new PlanEntry(PlanEntryKind.Facility, ProjectPriority.Core,
                state.WorldYear, NonExtractionType, port.Id, hexA, 1));
        new AllocationPhase().Run(state);
        Assert.Equal(1, FacilityProjectsAt(state, port.Id));   // first build
        // advance world-time past the cadence window, then attempt again
        int cadence = (int)state.Config.Infrastructure
            .FacilityGroundbreakCadenceYears;
        state.WorldYear += cadence;
        SetPlan(state, pr.ActorId,
            new PlanEntry(PlanEntryKind.Facility, ProjectPriority.Core,
                state.WorldYear, NonExtractionType, port.Id, hexB, 1));
        new AllocationPhase().Run(state);
        Assert.Equal(2, FacilityProjectsAt(state, port.Id));   // cadence elapsed
    }

    // ---- the gate is per-port: two ports build concurrently ----
    [Fact]
    public void TwoDifferentPorts_AreNotCrossBlocked()
    {
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        var pr = state.Polities[ProjectOpsTests.FirstEnteredPolity(state)];
        pr.DevelopmentPoints += 1e9;
        var portA = AddIsolatedPort(state, pr.ActorId, qOffset: 500);
        var portB = AddIsolatedPort(state, pr.ActorId, qOffset: 600);
        var hexA = new HexCoordinate(portA.Hex.Q + 1, portA.Hex.R);
        var hexB = new HexCoordinate(portB.Hex.Q + 1, portB.Hex.R);
        SetPlan(state, pr.ActorId,
            new PlanEntry(PlanEntryKind.Facility, ProjectPriority.Core,
                state.WorldYear, NonExtractionType, portA.Id, hexA, 1),
            new PlanEntry(PlanEntryKind.Facility, ProjectPriority.Core,
                state.WorldYear, NonExtractionType, portB.Id, hexB, 1));
        new AllocationPhase().Run(state);
        // per-port cadence never cross-blocks: each port breaks its own ground
        Assert.Equal(1, FacilityProjectsAt(state, portA.Id));
        Assert.Equal(1, FacilityProjectsAt(state, portB.Id));
    }
}
