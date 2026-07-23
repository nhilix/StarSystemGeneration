using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>One cargo line of a shipment card — qty and grade ride
/// together (Shipment.Qty/Grade).</summary>
public sealed record CargoLine(GoodId Good, string GoodName,
                               double Qty, double Grade);

/// <summary>The Shipment card (K3, NEW at T2): everything `efreight`
/// prints, typed — route, cargo+grades, sailed/total, the live eta, and
/// STALLED when the current leg is closed (the fortress starves at the
/// pace of its last delivery). AC2.6 adds the derived Purpose and — for
/// Courier/WarConvoy — the Rider contract row (route/fee), the SAME row
/// the courier board prints (ContractsPanel.Row); no duplicated
/// formatting.</summary>
/// <summary>AC4.1 adds OffLane (the RenderFreight idiom,
/// RouteLaneIds.Count == 0 — LaneCount already carries the number, OffLane
/// spells out the read so callers don't re-derive it) and
/// CrossesPatrolledSpace: detection-risk CONTEXT only — a bool read of
/// PatrolCoverage.At sampled along the direct path, active-war/hostile-only
/// (PatrolCoverage's own §5 gate), never a probability duplicating
/// ShipmentOps' actual seizure roll. False for lane-routed shipments (the
/// off-lane hazard doesn't apply to lane traffic at all).</summary>
public sealed record ShipmentCard(
    int Id, ShipmentChannel Channel, int OwnerActorId, string OwnerName,
    int OriginPortId, int DestPortId, int LaneCount,
    double SailedYears, double TotalYears, bool Stalled, long? EtaYear,
    IReadOnlyList<CargoLine> Cargo, FreightPurpose Purpose,
    ContractRow? Rider, bool OffLane, bool CrossesPatrolledSpace);

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
            cards.Add(CardOf(model, eye, s, severed));
        return cards;
    }

    /// <summary>One shipment by id — the freight-mark click target.</summary>
    public static ShipmentCard? Card(AtlasReadModel model, EyeContext eye,
                                     int shipmentId)
    {
        var state = model.State;
        foreach (var s in state.Shipments)                // ids are sparse
            if (s.Id == shipmentId)
                return CardOf(model, eye, s, FleetOps.SeveredLaneIds(state));
        return null;
    }

    private static ShipmentCard CardOf(AtlasReadModel model, EyeContext eye,
                                       Shipment s, HashSet<int> severed)
    {
        var state = model.State;
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
        var purposeInfo = FreightPurposeQuery.Of(state, s);
        var rider = purposeInfo.RiderContractId is int riderId
            ? ContractsPanel.Row(model, eye, riderId) : null;
        bool offLane = s.RouteLaneIds.Count == 0;
        return new ShipmentCard(s.Id, s.Channel, s.OwnerActorId, owner,
            s.OriginPortId, s.DestPortId, s.RouteLaneIds.Count,
            s.YearsInTransit, s.TotalYears, stalled, eta, cargo,
            purposeInfo.Purpose, rider, offLane,
            offLane && CrossesHostileCoverage(state, s));
    }

    /// <summary>AC4.1 detection-risk context: does the crawl's direct
    /// origin→dest path pass through ANY hex where PatrolCoverage.At reads
    /// positive — hostile, active-war-only coverage (PatrolCoverage's own
    /// gate; a peacetime or allied patrol projects nothing). Sampled at
    /// one hex per hop along the line (the same lerp+round WorksLens.Freight
    /// uses for the moving mark's position) — a context read, never
    /// ShipmentOps' actual seizure roll (that reads coverage at the DEST
    /// only, the drop point; this reads the whole path so the player can
    /// see risk building before arrival).</summary>
    private static bool CrossesHostileCoverage(SimState state, Shipment s)
    {
        var from = state.Ports[s.OriginPortId].Hex;
        var to = state.Ports[s.DestPortId].Hex;
        int hops = HexGrid.Distance(from, to);
        for (int i = 0; i <= hops; i++)
        {
            double t = hops == 0 ? 0.0 : (double)i / hops;
            var hex = HexGrid.Round(from.Q + (to.Q - from.Q) * t,
                                    from.R + (to.R - from.R) * t);
            if (PatrolCoverage.At(state, hex, BodyRef.None, s.OwnerActorId) > 0)
                return true;
        }
        return false;
    }
}
