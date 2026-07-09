using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Phase 1 — news arrives; each actor's believed world updates.
/// Slice-A stub: perfect information, the view is rebuilt from truth every
/// step. Compressed belief and news pulses replace this in Slice I; the
/// contract (Intent reads only the view) holds either way.</summary>
public sealed class PerceptionPhase : ISimPhase
{
    public string Name => "Perception";

    public string Run(SimState state)
    {
        var known = new List<int>();
        foreach (var a in state.Actors)
            if (a.Entered)
                known.Add(a.Id);
        int perceiving = 0;
        foreach (var a in state.Actors)
        {
            if (!a.Entered) continue;
            double expansion = a.Kind == ActorKind.Polity
                ? state.PolityOf(a.Id).ExpansionPoints : 0.0;
            var candidates = a.Kind == ActorKind.Polity
                ? ColonyValuation.CandidatesFor(state, a.Id) : null;
            a.Perception = new PerceptionView(a.Id, state.WorldYear, known,
                                              expansion, candidates);
            perceiving++;
        }
        return $"{perceiving} actors perceive (perfect-info stub)";
    }
}

/// <summary>Phase 2 — production, demand, price formation, trade flows.
/// Empty frame until Slice D.</summary>
public sealed class MarketsPhase : ISimPhase
{
    public string Name => "Markets";
    public string Run(SimState state) => "idle (markets land in slice D)";
}

/// <summary>Phase 3 — standing policies applied mechanically. Slice B:
/// per-port stub income (Markets replaces the source in slice D) split by the
/// actor's standing budget weights into the polity treasuries; unmodeled
/// shares (military, research…) evaporate until their slices land. The
/// development treasury then builds lanes (network first) and raises port
/// tiers — the map's highways are somebody's investment (P5).</summary>
public sealed class AllocationPhase : ISimPhase
{
    public string Name => "Allocation";

    public string Run(SimState state)
    {
        var cfg = state.Config;
        int earning = 0, lanesBuilt = 0, portsRaised = 0;
        var ownPorts = new List<Port>();
        foreach (var pr in state.Polities)                    // actor-id order
        {
            var actor = state.Actors[pr.ActorId];
            if (!actor.Entered) continue;
            ownPorts.Clear();
            foreach (var p in state.Ports)
                if (p.OwnerActorId == pr.ActorId) ownPorts.Add(p);
            if (ownPorts.Count == 0) continue;
            earning++;
            double income = ownPorts.Count * cfg.Expansion.StubIncomePerPortPerYear
                            * cfg.Sim.YearsPerEpoch;
            var budget = (actor.Policies as PolityPolicies ?? PolityPolicies.Default).Budget;
            pr.ExpansionPoints += income * budget.Expansion;
            pr.DevelopmentPoints += income * budget.Development;
            lanesBuilt += BuildLanes(state, pr, ownPorts);
            portsRaised += RaisePorts(state, pr, ownPorts);
        }
        string note = earning == 0 ? "quiet"
            : $"income accrued for {earning} " + (earning == 1 ? "polity" : "polities");
        if (lanesBuilt > 0) note += $", {lanesBuilt} " + (lanesBuilt == 1 ? "lane built" : "lanes built");
        if (portsRaised > 0) note += $", {portsRaised} " + (portsRaised == 1 ? "port raised" : "ports raised");
        return note;
    }

    /// <summary>Missing in-range same-owner pairs, nearest first (tie: lower
    /// ids), built while the development treasury affords them.</summary>
    private static int BuildLanes(SimState state, PolityRecord pr, List<Port> ownPorts)
    {
        var cfg = state.Config;
        int built = 0;
        while (pr.DevelopmentPoints >= cfg.Expansion.LaneCost)
        {
            Port? bestA = null, bestB = null;
            int bestDist = int.MaxValue;
            for (int i = 0; i < ownPorts.Count; i++)
                for (int j = i + 1; j < ownPorts.Count; j++)
                {
                    var a = ownPorts[i]; var b = ownPorts[j];
                    if (a.Id > b.Id) (a, b) = (b, a);
                    if (!LaneMath.InRange(cfg, a, b)) continue;
                    if (LaneExists(state, a.Id, b.Id)) continue;
                    int dist = HexGrid.Distance(a.Hex, b.Hex);
                    if (dist < bestDist
                        || (dist == bestDist && (a.Id < bestA!.Id
                            || (a.Id == bestA.Id && b.Id < bestB!.Id))))
                    { bestDist = dist; bestA = a; bestB = b; }
                }
            if (bestA == null) break;
            pr.DevelopmentPoints -= cfg.Expansion.LaneCost;
            var lane = new Lane(state.Lanes.Count, bestA.Id, bestB!.Id, state.WorldYear);
            state.Lanes.Add(lane);
            built++;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.LaneOpened,
                new[] { pr.ActorId }, Midpoint(bestA.Hex, bestB.Hex),
                Magnitude: 1.0, Valence: 1.0, EventVisibility.Regional,
                new LaneOpenedPayload(bestA.Id, bestB.Id)));
        }
        return built;
    }

    /// <summary>Lowest-tier port first (tie: lowest id); cost = base × current
    /// tier; raised while affordable.</summary>
    private static int RaisePorts(SimState state, PolityRecord pr, List<Port> ownPorts)
    {
        var cfg = state.Config;
        int raised = 0;
        while (true)
        {
            Port? pick = null;
            foreach (var p in ownPorts)
                if (p.Tier < cfg.Infrastructure.MaxPortTier
                    && (pick == null || p.Tier < pick.Tier
                        || (p.Tier == pick.Tier && p.Id < pick.Id)))
                    pick = p;
            if (pick == null) break;
            double cost = cfg.Expansion.PortUpgradeCostBase * pick.Tier;
            if (pr.DevelopmentPoints < cost) break;
            pr.DevelopmentPoints -= cost;
            pick.Tier++;
            raised++;
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.PortTierRaised,
                new[] { pr.ActorId }, pick.Hex,
                Magnitude: pick.Tier, Valence: 1.0, EventVisibility.Regional,
                new PortTierRaisedPayload(pick.Id, pick.Tier)));
        }
        return raised;
    }

    private static bool LaneExists(SimState state, int aId, int bId)
    {
        foreach (var l in state.Lanes)
            if (l.PortAId == aId && l.PortBId == bId) return true;
        return false;
    }

    /// <summary>Hex-line midpoint via cube lerp at t=0.5 with standard cube
    /// rounding — the lane-opened event's address.</summary>
    private static HexCoordinate Midpoint(HexCoordinate a, HexCoordinate b)
    {
        double x = (a.Q + b.Q) * 0.5;
        double z = (a.R + b.R) * 0.5;
        double y = -x - z;
        int rx = (int)System.Math.Round(x), ry = (int)System.Math.Round(y),
            rz = (int)System.Math.Round(z);
        double dx = System.Math.Abs(rx - x), dy = System.Math.Abs(ry - y),
               dz = System.Math.Abs(rz - z);
        if (dx > dy && dx > dz) rx = -ry - rz;
        else if (dy > dz) { /* y is derived; nothing to fix for axial */ }
        else rz = -rx - ry;
        return new HexCoordinate(rx, rz);
    }
}

/// <summary>Phase 4 — the one controller touchpoint (P2): every entered
/// decision-making actor emits policies + acts from its perceived state.</summary>
public sealed class IntentPhase : ISimPhase
{
    public string Name => "Intent";

    public string Run(SimState state)
    {
        state.Decisions.Clear();
        int acts = 0;
        foreach (var a in state.Actors)
        {
            if (!a.Entered) continue;
            var decision = a.Controller.Decide(a.Perception!);
            a.Policies = decision.Policies;   // standing policies: next step's
                                              // Allocation applies them (Move 1)
            state.Decisions.Add(new ActorDecision(a.Id, decision));
            acts += decision.Acts.Count;
        }
        return $"{state.Decisions.Count} decisions, {acts} acts";
    }
}

/// <summary>Phase 5 — acts collide and resolve deterministically. Slice B
/// resolves FoundColonyAct: claiming space is building a port
/// (space-and-travel.md) — convoyless until slice E gives the journey hulls.
/// Collisions on one hex resolve in actor-id order; losers are not charged.</summary>
public sealed class ResolutionPhase : ISimPhase
{
    public string Name => "Resolution";

    public string Run(SimState state)
    {
        int acts = 0, founded = 0;
        foreach (var d in state.Decisions)               // actor-id order
            foreach (var act in d.Decision.Acts)
            {
                acts++;
                if (act is FoundColonyAct f && TryFound(state, f)) founded++;
            }
        return founded == 0 ? $"{acts} acts, 0 resolved"
            : $"{acts} acts, {founded} " + (founded == 1 ? "port established" : "ports established");
    }

    /// <summary>Every check runs against truth: consequences on truth, even
    /// though the decision ran on perception (Move 2).</summary>
    private static bool TryFound(SimState state, FoundColonyAct act)
    {
        var cfg = state.Config;
        var actor = state.Actors[act.ActorId];
        if (!actor.Entered || actor.Kind != ActorKind.Polity) return false;
        var record = state.PolityOf(act.ActorId);
        if (record.ExpansionPoints < cfg.Expansion.ColonyCost) return false;
        if (!state.Skeleton.TryGetCell(HexGrid.CellOf(act.Target), out var cell)
            || cell.IsVoid) return false;
        foreach (var p in state.Ports)
            if (p.Hex.Equals(act.Target)) return false;   // hex taken (or lost the collision)
        bool inReach = false;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == act.ActorId
                && HexGrid.Distance(p.Hex, act.Target) <= cfg.Expansion.ColonizationReachHexes)
            { inReach = true; break; }
        if (!inReach) return false;

        record.ExpansionPoints -= cfg.Expansion.ColonyCost;
        var port = new Port(state.Ports.Count, act.ActorId, act.Target,
                            tier: 1, state.WorldYear);
        state.Ports.Add(port);
        state.Segments.Add(new PopulationSegment(state.Segments.Count, port.Id,
            record.SpeciesId, cfg.Expansion.ColonySegmentSize));
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.PortEstablished,
            new[] { act.ActorId }, act.Target, Magnitude: 1.0, Valence: 1.0,
            EventVisibility.Public, new PortEstablishedPayload(actor.Name, port.Id)));
        return true;
    }
}

/// <summary>Phase 6 — interiors and demographics. Slice B carries the stub
/// emergence schedule (frame/time.md §Asymmetric emergence) and homeworld
/// founding: a polity enters by establishing its first port at its seat —
/// homeworlds are simply the first ports (space-and-travel.md).</summary>
public sealed class InteriorPhase : ISimPhase
{
    public string Name => "Interior";

    public string Run(SimState state)
    {
        int entered = 0;
        foreach (var a in state.Actors)
        {
            if (a.Entered || a.EntryEpoch > state.EpochIndex) continue;
            a.Entered = true;
            entered++;
            var port = new Port(state.Ports.Count, a.Id, a.Seat,
                state.Config.Infrastructure.HomeworldPortTier, state.WorldYear);
            state.Ports.Add(port);
            state.Segments.Add(new PopulationSegment(state.Segments.Count, port.Id,
                state.PolityOf(a.Id).SpeciesId,
                state.Config.Expansion.HomeworldSegmentSize));
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.PolityEmerged,
                new[] { a.Id }, a.Seat, Magnitude: 1.0, Valence: 1.0,
                EventVisibility.Public, new PolityEmergedPayload(a.Name)));
        }
        return entered switch
        {
            0 => "quiet",
            1 => "1 polity enters",
            _ => $"{entered} polities enter",
        };
    }
}

/// <summary>Phase 7 — events finalized with world-years and appended to the
/// one log. News pulses and map residue attach in later slices; chronicle
/// runs last so next step's news is this step's history.</summary>
public sealed class ChroniclePhase : ISimPhase
{
    public string Name => "Chronicle";

    public string Run(SimState state)
    {
        foreach (var e in state.Staged)
            state.Log.Append(state.WorldYear, e.Stratum, e.Type, e.Actors,
                             e.Location, e.Magnitude, e.Valence, e.Visibility,
                             e.Payload);
        int count = state.Staged.Count;
        state.Staged.Clear();
        return count == 1 ? "1 event finalized" : $"{count} events finalized";
    }
}
