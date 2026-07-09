using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>An event emitted mid-step, awaiting Chronicle finalization (which
/// assigns id and world-year and appends to the log).</summary>
public sealed record StagedEvent(
    ClockStratum Stratum, WorldEventType Type, IReadOnlyList<int> Actors,
    HexCoordinate Location, double Magnitude, double Valence,
    EventVisibility Visibility, EventPayload? Payload);

/// <summary>One line of the phase execution trace — the REPL's step readout.</summary>
public sealed record PhaseTraceEntry(int Epoch, string Phase, string Note);

/// <summary>One actor's Intent-phase output, held for Resolution this step.</summary>
public sealed record ActorDecision(int ActorId, ControllerDecision Decision);

/// <summary>The generational sim-state container the seven phases step.
/// Iteration order is fixed everywhere: actors by id (P6).</summary>
public sealed class SimState
{
    public EpochSimConfig Config { get; }
    public int EpochIndex { get; set; }
    public int WorldYear { get; set; }
    /// <summary>Actor registry in id order — the fixed iteration order.</summary>
    public List<Actor> Actors { get; } = new List<Actor>();
    public EventLog Log { get; } = new EventLog();
    public List<PhaseTraceEntry> Trace { get; } = new List<PhaseTraceEntry>();
    /// <summary>Events emitted this step, finalized by Chronicle.</summary>
    public List<StagedEvent> Staged { get; } = new List<StagedEvent>();
    /// <summary>This step's Intent output in actor-id order, consumed by Resolution.</summary>
    public List<ActorDecision> Decisions { get; } = new List<ActorDecision>();

    public SimState(EpochSimConfig config)
    {
        Config = config;
    }
}
