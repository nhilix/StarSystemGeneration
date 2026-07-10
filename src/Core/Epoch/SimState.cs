using System.Collections.Generic;
using StarGen.Core.Galaxy;
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

/// <summary>The generational sim-state container the seven phases step:
/// sparse hex-addressed registries over the natural raster
/// (space-and-travel.md) — there is no per-cell political state. Iteration
/// order is fixed everywhere: registries by id, cells by spiral index (P6).</summary>
public sealed class SimState
{
    public EpochSimConfig Config { get; }
    /// <summary>The natural raster — nature's fields, no political meaning.</summary>
    public GalaxySkeleton Skeleton { get; }
    public int EpochIndex { get; set; }
    public int WorldYear { get; set; }
    /// <summary>Actor registry in id order — the fixed iteration order.</summary>
    public List<Actor> Actors { get; } = new List<Actor>();
    /// <summary>Polity-specific state beside the actor substrate, actor-id order.</summary>
    public List<PolityRecord> Polities { get; } = new List<PolityRecord>();
    /// <summary>The keystone registry: political geography derives from it.</summary>
    public List<Port> Ports { get; } = new List<Port>();
    public List<Lane> Lanes { get; } = new List<Lane>();
    public List<Facility> Facilities { get; } = new List<Facility>();
    public List<FleetRecord> Fleets { get; } = new List<FleetRecord>();
    public List<PopulationSegment> Segments { get; } = new List<PopulationSegment>();
    public EventLog Log { get; } = new EventLog();
    public List<PhaseTraceEntry> Trace { get; } = new List<PhaseTraceEntry>();
    /// <summary>Events emitted this step, finalized by Chronicle.</summary>
    public List<StagedEvent> Staged { get; } = new List<StagedEvent>();
    /// <summary>This step's Intent output in actor-id order, consumed by Resolution.</summary>
    public List<ActorDecision> Decisions { get; } = new List<ActorDecision>();

    public SimState(EpochSimConfig config, GalaxySkeleton skeleton)
    {
        Config = config;
        Skeleton = skeleton;
    }

    /// <summary>The polity record for an actor id (registry is actor-id ordered
    /// and dense over polity actors seeded at genesis).</summary>
    public PolityRecord PolityOf(int actorId)
    {
        // polity actors are seeded first and densely, so id == index today;
        // fall back to a scan if later slices interleave other actor kinds
        if (actorId < Polities.Count && Polities[actorId].ActorId == actorId)
            return Polities[actorId];
        foreach (var p in Polities)
            if (p.ActorId == actorId) return p;
        throw new KeyNotFoundException($"no polity record for actor {actorId}");
    }
}
