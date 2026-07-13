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
    /// <summary>The slow identity layer's registry. Seeded one per species;
    /// schisms and native emergences mint new entries (separation-drift
    /// splits and blending remain undone — slice J acceptance).</summary>
    public List<Culture> Cultures { get; } = new List<Culture>();
    public List<Loan> Loans { get; } = new List<Loan>();
    /// <summary>Sparse by construction (characters.md): role occupants and
    /// notables only, own id space, minted on demand deterministically.</summary>
    public List<Character> Characters { get; } = new List<Character>();
    public List<Dynasty> Dynasties { get; } = new List<Dynasty>();
    /// <summary>Interest blocs inside polities — pressure without a
    /// controller slot until graduation (frame/actors.md). Dead factions
    /// stay as history.</summary>
    public List<Faction> Factions { get; } = new List<Faction>();
    /// <summary>Emergent economic institutions (economy/corporations.md) —
    /// actors of Kind.Corporation with conserved books. Dead corps stay as
    /// history.</summary>
    public List<Corporation> Corporations { get; } = new List<Corporation>();
    /// <summary>Relations state per pair of polities that have met
    /// (interpolity/relations.md) — creation order (contact scans pairs
    /// ascending, P6). The pressure gauge war reads.</summary>
    public List<PolityRelation> Relations { get; } = new List<PolityRelation>();
    /// <summary>Wars declared and fought (interpolity/war.md) — id order
    /// (P6); ended wars stay as history.</summary>
    public List<War> Wars { get; } = new List<War>();
    /// <summary>Public events' word in transit (perception-and-news.md):
    /// emitted at Chronicle, delivered by Perception when age covers the
    /// news delay — id order (P6); expired pulses stay as history.</summary>
    public List<NewsPulse> Pulses { get; } = new List<NewsPulse>();
    /// <summary>Anchored points of interest compiled from residue every
    /// Chronicle (chronicle-and-poi.md) — id order (P6); depleted POIs
    /// stay as history.</summary>
    public List<PoiRecord> Pois { get; } = new List<PoiRecord>();
    /// <summary>Contagions riding the lanes (slice I) — id order (P6);
    /// burned-out plagues stay as history.</summary>
    public List<Plague> Plagues { get; } = new List<Plague>();
    /// <summary>In-flight work: every duration in the world is a project
    /// here (spec 2026-07-11 time-and-logistics §1) — id order (P6);
    /// completed and cancelled projects stay as history.</summary>
    public List<Project> Projects { get; } = new List<Project>();
    /// <summary>Goods in transit (spec §4b) — id order (P6). In-flight
    /// only: arrivals and losses leave the registry (freight is ambient,
    /// not history); NextShipmentId keeps identity stable across it.</summary>
    public List<Shipment> Shipments { get; } = new List<Shipment>();
    public int NextShipmentId { get; set; }
    /// <summary>The open order book (contract-economy spec §1) — id order
    /// (P6). Live orders only: fills and cancels leave the registry (the
    /// book is ambient, not history); NextOrderId keeps identity stable.</summary>
    public List<MarketOrder> Orders { get; } = new List<MarketOrder>();
    public int NextOrderId { get; set; }
    /// <summary>Open and in-transit courier contracts (spec §1) — id order
    /// (P6). Live only: delivered/lost/expired retire from the registry;
    /// NextCourierId keeps identity stable.</summary>
    public List<CourierContract> Couriers { get; } = new List<CourierContract>();
    public int NextCourierId { get; set; }
    /// <summary>The sim-health series the engine's always-on probe feeds —
    /// in-memory only, never serialized (sim-health spec §1).</summary>
    public MetricSeries Health { get; } = new MetricSeries();
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

    /// <summary>The conserved credit book behind any earning actor — a
    /// polity's record or a corporation's (slice G). Production and payouts
    /// move money through this, never caring who is earning (P4).</summary>
    public ICreditLedger LedgerOf(int actorId)
    {
        if (actorId < Polities.Count && Polities[actorId].ActorId == actorId)
            return Polities[actorId];
        foreach (var p in Polities)
            if (p.ActorId == actorId) return p;
        foreach (var c in Corporations)
            if (c.ActorId == actorId) return c;
        throw new KeyNotFoundException($"no credit ledger for actor {actorId}");
    }

    /// <summary>The relation between two polities, or null before contact.
    /// Order-insensitive (relations key the smaller actor id first).</summary>
    public PolityRelation? RelationOf(int polityA, int polityB)
    {
        int a = polityA < polityB ? polityA : polityB;
        int b = polityA < polityB ? polityB : polityA;
        foreach (var r in Relations)
            if (r.PolityAId == a && r.PolityBId == b) return r;
        return null;
    }

    /// <summary>The corporation record behind an actor id, or null.</summary>
    public Corporation? CorporationOf(int actorId)
    {
        foreach (var c in Corporations)
            if (c.ActorId == actorId) return c;
        return null;
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
