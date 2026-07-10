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

/// <summary>Lane quantities derived from the linked ports' tiers
/// (space-and-travel.md: capacity and transit speed derive from the ports'
/// tiers and technology) — functions, never stored state.</summary>
public static class LaneMath
{
    /// <summary>Inter-port reach in hexes: base + per-tier step above tier 1 —
    /// the second, independent port growth axis.</summary>
    public static int InterPortRange(EpochSimConfig cfg, int tier) =>
        cfg.Infrastructure.InterPortRangeBaseHexes
        + cfg.Infrastructure.InterPortRangePerTierHexes * (tier - 1);

    /// <summary>Pairable iff both ends reach: distance ≤ min of the two
    /// ranges, plus the builder's Astrogation bonus (slice G).</summary>
    public static bool InRange(EpochSimConfig cfg, Port a, Port b,
                               int astroBonusHexes = 0)
    {
        int range = System.Math.Min(InterPortRange(cfg, a.Tier), InterPortRange(cfg, b.Tier))
                    + astroBonusHexes;
        return StarGen.Core.Galaxy.HexGrid.Distance(a.Hex, b.Hex) <= range;
    }

    /// <summary>Bulk throughput per world-year unit: the tier sum, halved.</summary>
    public static double Capacity(Port a, Port b) => (a.Tier + b.Tier) * 0.5;

    /// <summary>Transit speed multiplier over off-lane crossing: the weaker
    /// terminus bounds the lane.</summary>
    public static double TransitSpeed(Port a, Port b) =>
        1.0 + 0.5 * System.Math.Min(a.Tier, b.Tier);
}
