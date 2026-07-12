using System;

namespace StarGen.Core.Epoch;

/// <summary>Paired port infrastructure linking two ports within inter-port
/// range (space-and-travel.md): the bulk-economy channel. Built, not given —
/// the map's highways are somebody's investment (P5). Capacity and transit
/// speed derive from the ports' tiers (LaneMath), never stored.</summary>
public sealed class Lane
{
    public int Id { get; }
    /// <summary>Lower port id — the pair is stored ordered so lane identity
    /// is canonical (fixed iteration order, P6).</summary>
    public int PortAId { get; }
    public int PortBId { get; }
    public int BuiltYear { get; }
    /// <summary>Self-imposed closure (a QuarantineAct carried, slice I):
    /// the lane is severed to freight, migration, and contagion until this
    /// world-year; −1 open. Lanes layer v2.</summary>
    public long QuarantinedUntil { get; set; } = -1;
    /// <summary>Gate facility at each end (lane-economics spec §2): GateAId
    /// stands at PortAId's system. −1 only mid-construction; a lane whose
    /// gate is destroyed keeps the id — the ruin is the half-built state.
    /// Lanes layer v3.</summary>
    public int GateAId { get; set; } = -1;
    public int GateBId { get; set; } = -1;
    /// <summary>Consecutive saturated world-YEARS (used/capacity ≥
    /// ExpressSaturationFloor each Markets step, accumulated by the step's
    /// year span) — the express-bypass earn-in clock. Years, not steps, so
    /// fine ticks earn expresses at the same world-time rate (P7).</summary>
    public int SaturatedYears { get; set; }

    public Lane(int id, int portAId, int portBId, int builtYear)
    {
        if (portAId >= portBId)
            throw new ArgumentException(
                $"lane port ids must be ordered: {portAId} >= {portBId}", nameof(portAId));
        Id = id;
        PortAId = portAId;
        PortBId = portBId;
        BuiltYear = builtYear;
    }
}

/// <summary>Lane quantities derived from the two gate facilities' tiers
/// (lane-economics spec §2) — functions, never stored state. Reach,
/// capacity, and speed all live in the built thing.</summary>
public static class LaneMath
{
    public static int ReachHexes(EpochSimConfig cfg, int gateTier) => gateTier switch
    {
        1 => cfg.Infrastructure.GateReachTier1Hexes,
        2 => cfg.Infrastructure.GateReachTier2Hexes,
        _ => cfg.Infrastructure.GateReachTier3Hexes,
    };

    /// <summary>Smallest gate tier whose reach (plus the builder's
    /// Astrogation bonus, slice G) covers the distance; −1 when even
    /// tier 3 can't.</summary>
    public static int RequiredGateTier(EpochSimConfig cfg, int distanceHexes,
                                       int astroBonusHexes)
    {
        for (int tier = 1; tier <= 3; tier++)
            if (distanceHexes <= ReachHexes(cfg, tier) + astroBonusHexes)
                return tier;
        return -1;
    }

    /// <summary>Live iff both gates stand, are commissioned
    /// (CommissionedYear ≥ 0 — a still-building founding pair opens no lane),
    /// and function (condition above the functional floor) — a raided gate
    /// severs the lane without touching the port.</summary>
    public static bool IsLive(SimState state, Lane lane) =>
        lane.GateAId >= 0 && lane.GateBId >= 0
        && state.Facilities[lane.GateAId].CommissionedYear >= 0
        && state.Facilities[lane.GateBId].CommissionedYear >= 0
        && state.Facilities[lane.GateAId].Condition
           >= state.Config.Infrastructure.GateFunctionalCondition
        && state.Facilities[lane.GateBId].Condition
           >= state.Config.Infrastructure.GateFunctionalCondition;

    /// <summary>Bulk throughput per world-year unit: the gate-tier sum, halved.</summary>
    public static double Capacity(SimState state, Lane lane) =>
        (state.Facilities[lane.GateAId].Tier
         + state.Facilities[lane.GateBId].Tier) * 0.5;

    /// <summary>Transit speed multiplier over off-lane crossing: the weaker
    /// gate bounds the lane.</summary>
    public static double TransitSpeed(SimState state, Lane lane) =>
        1.0 + 0.5 * System.Math.Min(state.Facilities[lane.GateAId].Tier,
                                    state.Facilities[lane.GateBId].Tier);
}
