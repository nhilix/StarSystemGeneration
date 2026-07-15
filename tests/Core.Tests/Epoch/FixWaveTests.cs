using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Final whole-branch review fix wave (slice t1): captured
/// mobilizations cancel (F1); actor-exit paths sweep projects (F2);
/// Groundbreak honors the staggered schedule (F3); the yard-capacity truth
/// check (F4); the duplicate founding-link guard (F5).</summary>
public class FixWaveTests
{
    private static SimState RunFor(int epochs)
    {
        var state = EpochTestKit.Seeded(42, 12).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    // ---- F1: a captured mobilization cancels, not transfers ----
    [Fact]
    public void TransferPort_CancelsCapturedMobilization_AttackerUntouched()
    {
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        var defender = state.Polities[ProjectOpsTests.FirstEnteredPolity(state)];
        int defPort = ProjectOpsTests.OwnPort(state, defender.ActorId);
        int attacker = -1;
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity
                && a.Id != defender.ActorId) { attacker = a.Id; break; }
        if (attacker < 0) return;                       // seed-shaped: skip
        int atkPort = ProjectOpsTests.OwnPort(state, attacker);
        var defMob = ProjectOps.Spawn(state, ProjectKind.Mobilization,
            defender.ActorId, defender.ActorId, defPort,
            state.Ports[defPort].Hex, 10.0, ProjectPriority.War, 0);
        var atkMob = ProjectOps.Spawn(state, ProjectKind.Mobilization,
            attacker, attacker, atkPort, state.Ports[atkPort].Hex, 10.0,
            ProjectPriority.War, 0);
        WarConduct.TransferPort(state, defPort, attacker);
        Assert.True(defMob.Cancelled);                  // enemy ramp cancels
        Assert.Equal(defender.ActorId, defMob.OwnerActorId);  // NOT transferred
        Assert.True(atkMob.InFlight);                   // attacker's own untouched
        Assert.Equal(attacker, atkMob.FunderActorId);
    }

    // ---- F2: Nationalize transfers the corp's projects to the polity ----
    [Fact]
    public void Nationalize_TransfersCorpProjects_ToThePolity()
    {
        var state = RunFor(20);
        Corporation? corp = null;
        foreach (var c in state.Corporations)
            if (c.Active && c.HostPolityId >= 0) { corp = c; break; }
        if (corp == null) return;                       // no hosted corp: skip
        var proj = ProjectOps.Spawn(state, ProjectKind.PortRaise,
            corp.ActorId, corp.ActorId, corp.HomePortId,
            state.Ports[corp.HomePortId].Hex, 5.0, ProjectPriority.Core, 0);
        Assert.True(CorporationOps.Nationalize(state, corp.HostPolityId, corp.Id));
        Assert.True(proj.InFlight);
        Assert.Equal(corp.HostPolityId, proj.OwnerActorId);
        Assert.Equal(corp.HostPolityId, proj.FunderActorId);
    }

    // ---- F2: Dissolve cancels the corp's in-flight projects ----
    [Fact]
    public void Dissolve_CancelsCorpProjects()
    {
        var state = RunFor(20);
        Corporation? corp = null;
        foreach (var c in state.Corporations)
            if (c.Active && c.HostPolityId >= 0) { corp = c; break; }
        if (corp == null) return;                       // no hosted corp: skip
        var proj = ProjectOps.Spawn(state, ProjectKind.PortRaise,
            corp.ActorId, corp.ActorId, corp.HomePortId,
            state.Ports[corp.HomePortId].Hex, 5.0, ProjectPriority.Core, 0);
        // force death via the niche-death clock: a corp wallet can no longer go
        // negative (Withdraw caps at holdings, task 7 — the balance-sheet
        // bankruptcy branch is now unreachable by construction), so starve the
        // niche instead — zero receipts with the lean clock already run out.
        corp.Receipts = 0;
        corp.LeanYears = 1_000_000;
        CorporationOps.Operate(state);                  // runs the death check
        Assert.False(corp.Active);
        Assert.True(proj.Cancelled);
    }

    // ---- F2: MergeInto transfers projects, cancels mobilizations ----
    [Fact]
    public void MergeInto_TransfersProjects_CancelsMobilization()
    {
        var state = RunFor(12);
        int from = -1, into = -1;
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity)
            {
                if (from < 0) from = a.Id;
                else { into = a.Id; break; }
            }
        if (from < 0 || into < 0) return;               // seed-shaped: skip
        int fromPort = ProjectOpsTests.OwnPort(state, from);
        var work = ProjectOps.Spawn(state, ProjectKind.PortRaise, from, from,
            fromPort, state.Ports[fromPort].Hex, 5.0, ProjectPriority.Core, 0);
        var mob = ProjectOps.Spawn(state, ProjectKind.Mobilization, from, from,
            fromPort, state.Ports[fromPort].Hex, 5.0, ProjectPriority.War, 0);
        FederationOps.MergeInto(state, from, into);
        Assert.True(work.InFlight);
        Assert.Equal(into, work.OwnerActorId);
        Assert.Equal(into, work.FunderActorId);
        Assert.True(mob.Cancelled);                     // the parent's war ramp dies
    }

    // ---- F2: no in-flight project is funded by a dead actor (zombie sweep) ----
    [Fact]
    public void NoInFlightProject_IsFundedByADeadActor()
    {
        var state = RunFor(40);
        foreach (var p in state.Projects)
        {
            if (!p.InFlight) continue;
            var corp = state.CorporationOf(p.FunderActorId);
            if (corp != null)
                Assert.True(corp.Active,
                    $"project {p.Id} funded by dissolved corp {corp.Id}");
            else
            {
                var actor = state.Actors[p.FunderActorId];
                Assert.True(actor.Entered && !actor.Retired,
                    $"project {p.Id} funded by dead actor {actor.Id}");
            }
        }
    }

    // ---- F3: Groundbreak honors PlanEntry.StartYear ----
    [Fact]
    public void Groundbreak_HonorsStaggeredStartYear()
    {
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        var pr = state.Polities[ProjectOpsTests.FirstEnteredPolity(state)];
        var actor = state.Actors[pr.ActorId];
        // a fresh own port below max tier with no raise already in flight
        int portId = -1;
        foreach (var port in state.Ports)
        {
            if (port.OwnerActorId != pr.ActorId
                || port.Tier >= state.Config.Infrastructure.MaxPortTier) continue;
            bool busy = false;
            foreach (var p in state.Projects)
                if (p.InFlight && p.Kind == ProjectKind.PortRaise
                    && p.TargetId == port.Id) { busy = true; break; }
            if (!busy) { portId = port.Id; break; }
        }
        if (portId < 0) return;                          // seed-shaped: skip
        pr.DevelopmentPoints += 100000;
        int startYear = state.WorldYear + 20;
        actor.Policies = PolityPolicies.Default with
        {
            Plan = new StandingPlan(new[]
            {
                new PlanEntry(PlanEntryKind.PortRaise, ProjectPriority.Core,
                    startYear, -1, portId, new HexCoordinate(0, 0), 1),
            }),
        };
        new AllocationPhase().Run(state);
        Project? raise = null;
        foreach (var p in state.Projects)
            if (p.Kind == ProjectKind.PortRaise && p.PortId == portId
                && p.StartedYear == startYear) { raise = p; break; }
        Assert.NotNull(raise);                           // spawned at the schedule
        Assert.Equal(startYear, raise!.StartedYear);     // NOT the span start
    }

    // ---- F4: a single tier-1 yard refuses a second concurrent batch ----
    [Fact]
    public void GroundbreakHullBatch_RespectsYardCapacity()
    {
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        var pr = state.Polities[ProjectOpsTests.FirstEnteredPolity(state)];
        var actor = state.Actors[pr.ActorId];
        int home = ProjectOpsTests.OwnPort(state, pr.ActorId);
        // an isolated own port with exactly one commissioned tier-1 yard
        var hex = new HexCoordinate(state.Ports[home].Hex.Q + 40,
                                    state.Ports[home].Hex.R);
        var yardPort = new Port(state.Ports.Count, pr.ActorId, hex,
                                tier: 1, state.WorldYear);
        state.Ports.Add(yardPort);
        state.Markets.Add(new Market(yardPort.Id, state.Config.Economy));
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)InfraTypeId.Shipyard, tier: 1, hex, pr.ActorId,
            state.WorldYear) { CommissionedYear = state.WorldYear });
        ShipDesign? design = null;
        foreach (var d in state.Designs)
            if (d.OwnerActorId == pr.ActorId) { design = d; break; }
        if (design == null) return;                      // seed-shaped: skip
        pr.MilitaryPoints += 1e9;
        actor.Policies = PolityPolicies.Default with
        {
            Plan = new StandingPlan(new[]
            {
                new PlanEntry(PlanEntryKind.HullBatch, ProjectPriority.Growth,
                    state.WorldYear, design.Id, yardPort.Id,
                    new HexCoordinate(0, 0), 1),
                new PlanEntry(PlanEntryKind.HullBatch, ProjectPriority.Growth,
                    state.WorldYear, design.Id, yardPort.Id,
                    new HexCoordinate(0, 0), 1),
            }),
        };
        new AllocationPhase().Run(state);
        int batches = 0;
        foreach (var p in state.Projects)
            if (p.Kind == ProjectKind.HullBatch && p.PortId == yardPort.Id)
                batches++;
        Assert.Equal(1, batches);                        // one yard, one batch
    }

    // ---- F5: a new colony gets exactly one founding link, not two ----
    [Fact]
    public void FoundingLink_NotDuplicated_WhileGateStillBuilding()
    {
        var (_, state) = EpochTestKit.Seeded();
        // a short history: the first-wave polity holds only its homeworld
        state.Config.Sim.EpochCount = 1;
        new EpochEngine().Run(state);
        var pr = state.Polities[ProjectOpsTests.FirstEnteredPolity(state)];
        var own = new List<Port>();
        foreach (var p in state.Ports)
            if (p.OwnerActorId == pr.ActorId) own.Add(p);
        if (own.Count != 1) return;                      // need a clean 2-port map
        var home = own[0];
        // a colony within tier-1 gate reach of the homeworld, no lane yet
        var colony = new Port(state.Ports.Count, pr.ActorId,
            new HexCoordinate(home.Hex.Q + 5, home.Hex.R), tier: 1,
            state.WorldYear);
        state.Ports.Add(colony);
        state.Markets.Add(new Market(colony.Id, state.Config.Economy));
        // an in-flight founding link (lane row exists, gates uncommissioned)
        ProjectOps.SpawnGatePair(state, pr.ActorId, pr.ActorId, home, colony,
            tier: 1, ProjectPriority.Growth, 0, foundingLink: true);
        // deep dev treasury: a pre-fix build would happily fund a 2nd link
        pr.DevelopmentPoints += 1e9;
        new AllocationPhase().Run(state);
        int touchingColony = 0;
        foreach (var p in state.Projects)
        {
            if (p.Kind != ProjectKind.GatePair || p.TargetId < 0) continue;
            var lane = state.Lanes[p.TargetId];
            if (lane.PortAId == colony.Id || lane.PortBId == colony.Id)
                touchingColony++;
        }
        Assert.Equal(1, touchingColony);                 // no conservation hole
    }
}
