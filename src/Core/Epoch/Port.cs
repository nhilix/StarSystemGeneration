using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

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
    /// <summary>Last world-year anyone lived here (updated each Chronicle;
    /// founding counts) — the dead-city grace clock reads how long the
    /// port has sat EMPTY, not how old it is (slice I, ports layer v2).</summary>
    public long LastPopulatedYear { get; set; }
    /// <summary>Located stockpile per good (time-and-logistics spec §4b):
    /// stock has an address — banked HERE, owned by whoever owns the port,
    /// so conquest, federation, and schism move stock by moving the port.</summary>
    public double[] StockQty { get; } = new double[Goods.All.Count];
    /// <summary>Quantity-weighted mean grade of the stock (0 when empty).</summary>
    public double[] StockGrade { get; } = new double[Goods.All.Count];

    public Port(int id, int ownerActorId, HexCoordinate hex, int tier, int foundedYear)
    {
        Id = id;
        OwnerActorId = ownerActorId;
        Hex = hex;
        Tier = tier;
        FoundedYear = foundedYear;
        LastPopulatedYear = foundedYear;
    }

    /// <summary>Blend a delivery into the stockpile: quantities add, grade is
    /// the quantity-weighted mean (the Market.Deposit convention).</summary>
    public void DepositStock(int good, double quantity, double grade)
    {
        if (quantity <= 0) return;
        double total = StockQty[good] + quantity;
        StockGrade[good] =
            (StockQty[good] * StockGrade[good] + quantity * grade) / total;
        StockQty[good] = total;
    }

    /// <summary>Draw up to <paramref name="quantity"/> from the stockpile at
    /// the mean grade; returns the quantity actually drawn.</summary>
    public double DrawStock(int good, double quantity)
    {
        double drawn = quantity < StockQty[good] ? quantity : StockQty[good];
        if (drawn <= 0) return 0;
        StockQty[good] -= drawn;
        if (StockQty[good] <= 0) { StockQty[good] = 0; StockGrade[good] = 0; }
        return drawn;
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
