using System.Collections.Generic;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>Which pipe filled the hold (spec §4b): market freight is bought
/// and sold; a requisition is the state moving its own goods — bypassing
/// price, never time, route, or capacity.</summary>
public enum ShipmentChannel { Freight = 0, Requisition = 1 }

/// <summary>Goods with an address in between addresses (spec §4b): origin,
/// destination, basket, and a route over the lane network whose leg years
/// were priced at departure. In-transit goods are conserved state — visible
/// on the lane (P1), stalled by blockade or quarantine, lost to piracy.
/// Registry in SimState.Shipments, id order (P6); arrived and lost
/// shipments leave the registry (freight is ambient, not history — the
/// id counter in SimState.NextShipmentId keeps identity stable).</summary>
public sealed class Shipment
{
    public int Id { get; }
    /// <summary>Whose goods these are: the exporting polity for freight
    /// (paid as the destination's supplier on arrival), the requisitioning
    /// polity for state logistics.</summary>
    public int OwnerActorId { get; }
    public ShipmentChannel Channel { get; }
    public int OriginPortId { get; }
    public int DestPortId { get; }
    public int DepartureYear { get; }
    /// <summary>Cargo per good; grade rides alongside.</summary>
    public double[] Qty { get; } = new double[Goods.All.Count];
    public double[] Grade { get; } = new double[Goods.All.Count];
    /// <summary>Lane ids along the route, origin-first; empty = a single
    /// off-lane crawl leg.</summary>
    public IReadOnlyList<int> RouteLaneIds { get; }
    /// <summary>Transit years per leg, priced at departure (a gate raised
    /// mid-flight neither speeds nor slows cargo already under way).</summary>
    public IReadOnlyList<double> LegYears { get; }
    /// <summary>Years actually sailed — advances only while the current
    /// leg's lane is open; the gap to TotalYears is the live ETA.</summary>
    public double YearsInTransit { get; set; }
    public double TotalYears { get; }

    public Shipment(int id, int ownerActorId, ShipmentChannel channel,
                    int originPortId, int destPortId, int departureYear,
                    IReadOnlyList<int> routeLaneIds,
                    IReadOnlyList<double> legYears)
    {
        Id = id;
        OwnerActorId = ownerActorId;
        Channel = channel;
        OriginPortId = originPortId;
        DestPortId = destPortId;
        DepartureYear = departureYear;
        RouteLaneIds = routeLaneIds;
        LegYears = legYears;
        double total = 0;
        foreach (var y in legYears) total += y;
        TotalYears = total;
    }
}
