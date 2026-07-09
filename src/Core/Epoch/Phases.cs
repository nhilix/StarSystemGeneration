using System.Collections.Generic;

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
            a.Perception = new PerceptionView(a.Id, state.WorldYear, known);
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
/// shares (military, research…) evaporate until their slices land.</summary>
public sealed class AllocationPhase : ISimPhase
{
    public string Name => "Allocation";

    public string Run(SimState state)
    {
        var cfg = state.Config;
        int earning = 0;
        foreach (var pr in state.Polities)                    // actor-id order
        {
            var actor = state.Actors[pr.ActorId];
            if (!actor.Entered) continue;
            int ports = 0;
            foreach (var p in state.Ports)
                if (p.OwnerActorId == pr.ActorId) ports++;
            if (ports == 0) continue;
            earning++;
            double income = ports * cfg.Expansion.StubIncomePerPortPerYear
                            * cfg.Sim.YearsPerEpoch;
            var budget = (actor.Policies as PolityPolicies ?? PolityPolicies.Default).Budget;
            pr.ExpansionPoints += income * budget.Expansion;
            pr.DevelopmentPoints += income * budget.Development;
        }
        return earning == 0 ? "quiet"
            : $"income accrued for {earning} " + (earning == 1 ? "polity" : "polities");
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

/// <summary>Phase 5 — acts collide and resolve deterministically. Slice A has
/// no resolvers (the trivial AI emits no acts); expansion arrives in Slice B
/// (convoyless until Slice E), war in Slice H.</summary>
public sealed class ResolutionPhase : ISimPhase
{
    public string Name => "Resolution";

    public string Run(SimState state)
    {
        int acts = 0;
        foreach (var d in state.Decisions)
            acts += d.Decision.Acts.Count;
        return $"{acts} acts, 0 resolved (no resolvers yet)";
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
