using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The keystone infrastructure (space-and-travel.md): a starport at a
/// specific hex with two independent growth axes — local service radius and
/// inter-port range — both derived from tier, never stored. Registry entry in
/// SimState.Ports, id order fixed (P6). Claiming space is building a port;
/// homeworlds are simply the first ports.</summary>
public sealed class Port
{
    public int Id { get; }
    /// <summary>Owning polity's actor id. Settable: conquest transfers ports
    /// intact (slice H).</summary>
    public int OwnerActorId { get; set; }
    /// <summary>The physical carrier — no political fact without one (P4).</summary>
    public HexCoordinate Hex { get; }
    /// <summary>1..MaxPortTier (outpost → starport → nexus), raised by
    /// Allocation-phase investment.</summary>
    public int Tier { get; set; }
    public int FoundedYear { get; }

    public Port(int id, int ownerActorId, HexCoordinate hex, int tier, int foundedYear)
    {
        Id = id;
        OwnerActorId = ownerActorId;
        Hex = hex;
        Tier = tier;
        FoundedYear = foundedYear;
    }
}

/// <summary>Derived political geography (space-and-travel.md): territory is
/// the union of port service areas, computed from the port registry on demand,
/// never stored — no per-hex or per-cell ownership paint (P4, P5). Domain
/// overlap is allowed and meaningful: ≥2 distinct owners at a hex is a
/// contested-influence zone.</summary>
public static class PortDomains
{
    /// <summary>Local service radius in hexes: base + per-tier step above tier 1.</summary>
    public static int ServiceRadius(EpochSimConfig cfg, int tier) =>
        cfg.Infrastructure.ServiceRadiusBaseHexes
        + cfg.Infrastructure.ServiceRadiusPerTierHexes * (tier - 1);

    /// <summary>True iff the port's service area covers the hex: within the
    /// tier's radius AND the hex's cell exists and is not void — voids dilute
    /// effective range; the wilds stay dark.</summary>
    public static bool Services(GalaxySkeleton sk, EpochSimConfig cfg, Port port,
                                HexCoordinate hex) =>
        HexGrid.Distance(port.Hex, hex) <= ServiceRadius(cfg, port.Tier)
        && sk.TryGetCell(HexGrid.CellOf(hex), out var cell) && !cell.IsVoid;

    /// <summary>Distinct owner actor ids whose ports service the hex, ascending
    /// into the caller-owned list (cleared first — no per-call allocation in
    /// rendering loops). Count 0 = wilds; 1 = territory; ≥2 = contested.</summary>
    public static void OwnersAt(GalaxySkeleton sk, EpochSimConfig cfg,
                                IReadOnlyList<Port> ports, HexCoordinate hex, List<int> into)
    {
        into.Clear();
        for (int i = 0; i < ports.Count; i++)
        {
            var port = ports[i];
            if (!Services(sk, cfg, port, hex)) continue;
            int at = into.BinarySearch(port.OwnerActorId);
            if (at < 0) into.Insert(~at, port.OwnerActorId);
        }
    }

    /// <summary>≥2 distinct polities' service ranges overlap here — border
    /// friction, dispute fuel, war goals.</summary>
    public static bool IsContested(GalaxySkeleton sk, EpochSimConfig cfg,
                                   IReadOnlyList<Port> ports, HexCoordinate hex)
    {
        int first = -1;
        for (int i = 0; i < ports.Count; i++)
        {
            var port = ports[i];
            if (!Services(sk, cfg, port, hex)) continue;
            if (first < 0) first = port.OwnerActorId;
            else if (port.OwnerActorId != first) return true;
        }
        return false;
    }
}
