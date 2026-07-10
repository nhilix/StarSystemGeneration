using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Which clock layer emitted an event (frame/time.md: the four clocks).</summary>
public enum ClockStratum { Cosmic, Evolutionary, Generational, Play }

/// <summary>The design's eight type families (narrative/chronicle-and-poi.md),
/// exactly and in this order.</summary>
public enum EventFamily
{
    Cosmic, Evolutionary, Economic, Political, Military, Diplomatic, Corporate,
    Character,
}

/// <summary>Public emits a news pulse; regional spreads by contact; secret emits
/// nothing (the substrate future spy systems need).</summary>
public enum EventVisibility { Public, Regional, Secret }

/// <summary>Concrete event types. Values are STABLE (never renumber or reuse a
/// shipped value) and live in 100-blocks per family — see
/// <see cref="WorldEventTypes.FamilyOf"/>. Later slices append.</summary>
public enum WorldEventType
{
    // 0–99 cosmic · 100–199 evolutionary · 200–299 economic · 300–399 political
    // 400–499 military · 500–599 diplomatic · 600–699 corporate · 700–799 character
    DwarfGalaxyMerged = 0,
    AgnIgnited = 1,
    GlobularFormed = 2,
    LaneOpened = 200,
    PortTierRaised = 201,
    FamineStruck = 202,
    FacilityBuilt = 203,
    LoanIssued = 204,
    LoanDefaulted = 205,
    MigrationWave = 206,
    PolityEmerged = 300,
    PortEstablished = 301,
    ShipClassLaunched = 400,
    FleetAttrition = 401,
    ConvoyDispatched = 402,
}

public static class WorldEventTypes
{
    /// <summary>Family from the type's stable 100-block. Throws on values
    /// outside the eight blocks — a mis-numbered type must fail loudly, not
    /// map to a phantom family.</summary>
    public static EventFamily FamilyOf(WorldEventType type)
    {
        int block = (int)type / 100;
        if ((int)type < 0 || block > (int)EventFamily.Character)
            throw new System.ArgumentOutOfRangeException(nameof(type), type,
                "event type outside the eight stable family blocks");
        return (EventFamily)block;
    }
}

/// <summary>Type-specific payload root; one derived record per event type.</summary>
public abstract record EventPayload;

/// <summary>An infalling dwarf galaxy arrives — the biggest source of
/// seed-to-seed structural variety (genesis/cosmic-genesis.md §Features).</summary>
public sealed record DwarfGalaxyMergedPayload(
    int FeatureId, string Name, double Mass) : EventPayload;

/// <summary>An AGN accretion epoch: a sterilization/enrichment wave over an
/// inner radius — the evolutionary clock reads it directly.</summary>
public sealed record AgnIgnitedPayload(
    int FeatureId, int WaveRadiusCells) : EventPayload;

/// <summary>An ancient compact metal-poor cluster forms in the earliest
/// steps — rare exotic terrain with its own hex-tier star-table bias.</summary>
public sealed record GlobularFormedPayload(int FeatureId, string Name) : EventPayload;

public sealed record PolityEmergedPayload(string PolityName) : EventPayload;

/// <summary>Claiming space is building a port (space-and-travel.md).</summary>
public sealed record PortEstablishedPayload(string PolityName, int PortId) : EventPayload;

/// <summary>Paired port infrastructure: the lane links two port registry ids.</summary>
public sealed record LaneOpenedPayload(int PortAId, int PortBId) : EventPayload;

public sealed record PortTierRaisedPayload(int PortId, int NewTier) : EventPayload;

/// <summary>Unmet subsistence at a port market — the famine consequence
/// (economy/markets.md §Clearing). Shortfall is the unmet fraction [0,1].</summary>
public sealed record FamineStruckPayload(int PortId, double Shortfall) : EventPayload;

public sealed record FacilityBuiltPayload(int FacilityId, int TypeId, int Tier) : EventPayload;

public sealed record LoanIssuedPayload(
    int LoanId, int LenderActorId, int BorrowerActorId, double Principal) : EventPayload;

/// <summary>Unpayable obligation: reputation damage, relations hit, collateral
/// seizure (economy/markets.md §Credit).</summary>
public sealed record LoanDefaultedPayload(
    int LoanId, int LenderActorId, int BorrowerActorId) : EventPayload;

/// <summary>A refugee exodus — the fast desperate migration variant
/// (population-and-identity.md §Migration).</summary>
public sealed record MigrationWavePayload(
    int FromPortId, int ToPortId, double Size) : EventPayload;

/// <summary>An improved mark joins a design lineage — visible cultural
/// history (fleets/ships-and-fleets.md §Chassis grid).</summary>
public sealed record ShipClassLaunchedPayload(
    int DesignId, string Name, int Mark) : EventPayload;

/// <summary>Hulls wrecked by failed supply — losses conserve into wreckage
/// at the hex where they died (fleets/ships-and-fleets.md §Attrition).</summary>
public sealed record FleetAttritionPayload(
    int FleetId, int HullsLost) : EventPayload;

/// <summary>A convoy departs under the Expedition posture — colony convoys
/// make founding physical (fleets doc §Postures, space-and-travel.md).</summary>
public sealed record ConvoyDispatchedPayload(
    int FleetId, int FromPortId, int TargetQ, int TargetR) : EventPayload;

/// <summary>One record of the single append-only stream — the event grammar v2
/// (narrative/chronicle-and-poi.md): one schema across all four clocks.
/// Actors are registry ids; the location is a physical hex address
/// (space-and-travel.md: no political fact without a physical carrier).
/// WorldYear is long: the deep-time strata write true world-years ("−6.2 Gyr"
/// is −6.2e9), far past int range (frame/time.md: timescale-aware from birth).</summary>
public sealed record WorldEvent(
    long Id,
    long WorldYear,
    ClockStratum Stratum,
    WorldEventType Type,
    IReadOnlyList<int> Actors,
    HexCoordinate Location,
    double Magnitude,
    double Valence,
    EventVisibility Visibility,
    EventPayload? Payload)
{
    public EventFamily Family => WorldEventTypes.FamilyOf(Type);
}
