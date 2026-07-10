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
    /// <summary>Per-polity ship designs — lineage entries in id order;
    /// improved marks append, never edit (fleets/ships-and-fleets.md).</summary>
    public List<ShipDesign> Designs { get; } = new List<ShipDesign>();
    public List<FleetRecord> Fleets { get; } = new List<FleetRecord>();
    /// <summary>Losses conserve into wreckage at real hexes — salvage
    /// sites; the narrative layer compiles them in I (P4).</summary>
    public List<WreckageRecord> Wreckage { get; } = new List<WreckageRecord>();
    public List<PopulationSegment> Segments { get; } = new List<PopulationSegment>();
    /// <summary>One market per port, parallel to Ports (market id = port id).</summary>
    public List<Market> Markets { get; } = new List<Market>();
    /// <summary>The slow identity layer's registry — id == species id until a
    /// split mechanic lands.</summary>
    public List<Culture> Cultures { get; } = new List<Culture>();
    public List<Loan> Loans { get; } = new List<Loan>();
    /// <summary>Sparse by construction (characters.md): role occupants and
    /// notables only, own id space, minted on demand deterministically.</summary>
    public List<Character> Characters { get; } = new List<Character>();
    public List<Dynasty> Dynasties { get; } = new List<Dynasty>();
    /// <summary>Debug-only lane cuts for the REPL blockade hook — transient,
    /// never serialized; slice H replaces this with real interdiction.</summary>
    public HashSet<int> SeveredLanes { get; } = new HashSet<int>();
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
