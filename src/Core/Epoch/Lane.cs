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
