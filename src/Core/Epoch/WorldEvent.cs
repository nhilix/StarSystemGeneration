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
    LaneOpened = 200,
    PortTierRaised = 201,
    FamineStruck = 202,
    FacilityBuilt = 203,
    LoanIssued = 204,
    LoanDefaulted = 205,
    PolityEmerged = 300,
    PortEstablished = 301,
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

/// <summary>One record of the single append-only stream — the event grammar v2
/// (narrative/chronicle-and-poi.md): one schema across all four clocks.
/// Actors are registry ids; the location is a physical hex address
/// (space-and-travel.md: no political fact without a physical carrier).</summary>
public sealed record WorldEvent(
    long Id,
    int WorldYear,
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
