using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>One cargo line of a shipment card — qty and grade ride
/// together (Shipment.Qty/Grade).</summary>
public sealed record CargoLine(GoodId Good, string GoodName,
                               double Qty, double Grade);

/// <summary>The Shipment card (K3, NEW at T2): everything `efreight`
/// prints, typed — route, cargo+grades, sailed/total, the live eta, and
/// STALLED when the current leg is closed (the fortress starves at the
/// pace of its last delivery).</summary>
public sealed record ShipmentCard(
    int Id, ShipmentChannel Channel, int OwnerActorId, string OwnerName,
    int OriginPortId, int DestPortId, int LaneCount,
    double SailedYears, double TotalYears, bool Stalled, long? EtaYear,
    IReadOnlyList<CargoLine> Cargo);

/// <summary>K3: the works-lens freight mark's panel query — `efreight`
/// parity (Repl.RenderFreight), same three-term stall check. NOTE the
/// K2-flagged clock edge resolves here: the explicit quarantine term reads
/// `&gt;`, but SeveredLaneIds already folds quarantines in at `&gt;=`, so
/// the EFFECTIVE stall edge is `&gt;=` — consistent with the lane lens.
/// Live eta = WorldYear + ceil(TotalYears − YearsInTransit).</summary>
public static class ShipmentPanel
{
    /// <summary>Every shipment in transit, id order (P6).</summary>
    public static List<ShipmentCard> Cards(AtlasReadModel model, EyeContext eye)
    {
        var state = model.State;
        var severed = FleetOps.SeveredLaneIds(state);
        var cards = new List<ShipmentCard>(state.Shipments.Count);
        foreach (var s in state.Shipments)                // id order (P6)
            cards.Add(CardOf(state, s, severed));
        return cards;
    }

    /// <summary>One shipment by id — the freight-mark click target.</summary>
    public static ShipmentCard? Card(AtlasReadModel model, EyeContext eye,
                                     int shipmentId)
    {
        var state = model.State;
        foreach (var s in state.Shipments)                // ids are sparse
            if (s.Id == shipmentId)
                return CardOf(state, s, FleetOps.SeveredLaneIds(state));
        return null;
    }

    private static ShipmentCard CardOf(SimState state, Shipment s,
                                       HashSet<int> severed)
    {
        string owner = s.OwnerActorId >= 0 && s.OwnerActorId < state.Actors.Count
            ? state.Actors[s.OwnerActorId].Name : "—";
        var cargo = new List<CargoLine>();
        for (int g = 0; g < s.Qty.Length; g++)
            if (s.Qty[g] > 0)
                cargo.Add(new CargoLine((GoodId)g, Goods.Get((GoodId)g).Name,
                                        s.Qty[g], s.Grade[g]));
        bool stalled = false;
        int leg = ShipmentOps.CurrentLeg(s);
        if (leg < s.RouteLaneIds.Count)
        {
            var lane = state.Lanes[s.RouteLaneIds[leg]];
            stalled = severed.Contains(lane.Id)
                      || lane.QuarantinedUntil > state.WorldYear
                      || !LaneMath.IsLive(state, lane);
        }
        long? eta = stalled ? null
            : state.WorldYear + (long)Math.Ceiling(s.TotalYears - s.YearsInTransit);
        return new ShipmentCard(s.Id, s.Channel, s.OwnerActorId, owner,
            s.OriginPortId, s.DestPortId, s.RouteLaneIds.Count,
            s.YearsInTransit, s.TotalYears, stalled, eta, cargo);
    }
}
