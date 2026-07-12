using System;
using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>The courier lifecycle (contract-economy spec §§1,3): post
/// escrows cargo + fee, accept dispatches the shipment, resolution pays or
/// refunds. Deterministic — contracts by id, no rolls of its own (the
/// shipment's piracy is the cargo's risk).</summary>
public static class CourierOps
{
    /// <summary>Post a courier: cargo out of the poster's origin larder
    /// (the poster must own the origin port — you ship YOUR OWN goods),
    /// fee out of the poster's ledger. Null when the origin isn't the
    /// poster's or nothing could be drawn.</summary>
    public static CourierContract? Post(SimState state, int posterActorId,
        int originPortId, int destPortId,
        IReadOnlyList<(int Good, double Qty)> basket, double fee,
        CourierPriority priority)
    {
        var origin = state.Ports[originPortId];
        if (origin.OwnerActorId != posterActorId) return null;
        var c = new CourierContract(state.NextCourierId++, posterActorId,
            originPortId, destPortId, fee, priority, state.WorldYear,
            state.WorldYear
            + (int)Math.Round(state.Config.Economy.OrderExpiryYears));
        bool any = false;
        foreach (var (good, qty) in basket)
        {
            double grade = origin.StockGrade[good];
            double drawn = origin.DrawStock(good, qty);
            if (drawn <= 0) continue;
            c.Qty[good] = drawn;
            c.Grade[good] = grade;
            any = true;
        }
        if (!any) { state.NextCourierId--; return null; }
        state.LedgerOf(posterActorId).Credits -= fee;  // escrowed on the record
        state.Couriers.Add(c);
        return c;
    }

    /// <summary>Take every open contract a carrier can serve, in (priority,
    /// id) order (spec §3): the fulfiller is whoever posts the deepest
    /// freight capacity on the route's first lane — the POSTER ITSELF when
    /// its own hulls are the deepest (self-fulfillment at cost: the fee
    /// pays back into its own ledger). A route with no lane at all is the
    /// state hauling its own goods off-lane, as ever. Returns acceptances.</summary>
    public static int AcceptOpen(SimState state)
    {
        var open = new List<CourierContract>();
        foreach (var c in state.Couriers)                 // id order (P6)
            if (c.Status == CourierStatus.Open) open.Add(c);
        open.Sort((x, y) =>
        {
            int p = x.Priority.CompareTo(y.Priority);
            return p != 0 ? p : x.Id.CompareTo(y.Id);
        });
        int accepted = 0;
        foreach (var c in open)
        {
            var (laneIds, _) = ShipmentOps.PlanRoute(state, c.OriginPortId,
                                                     c.DestPortId);
            int fulfiller = c.PosterActorId;              // off-lane: self
            if (laneIds.Count > 0)
            {
                var lane = state.Lanes[laneIds[0]];
                double best = 0;
                int bestOwner = -1;
                foreach (var fleet in state.Fleets)       // id order (P6)
                {
                    if (fleet.Posture != FleetPosture.Posted
                        || fleet.TargetId != lane.Id
                        || fleet.TotalHulls == 0) continue;
                    double cap = fleet.TotalHulls * fleet.Readiness;
                    if (cap > best
                        || (cap == best && bestOwner >= 0
                            && fleet.OwnerActorId < bestOwner))
                    { best = cap; bestOwner = fleet.OwnerActorId; }
                }
                if (bestOwner < 0) continue;              // no hulls, no haul
                fulfiller = bestOwner;
            }
            if (Accept(state, c, fulfiller)) accepted++;
        }
        return accepted;
    }

    /// <summary>Take the job: the cargo sails as a requisition-channel
    /// shipment (it banks into the destination larder — the poster's own
    /// goods, not a market trade). A sub-step transit resolves the
    /// contract immediately.</summary>
    public static bool Accept(SimState state, CourierContract c,
                              int fulfillerActorId)
    {
        if (c.Status != CourierStatus.Open) return false;
        c.FulfillerActorId = fulfillerActorId;
        var basket = new List<(int Good, double Qty, double Grade)>();
        for (int g = 0; g < c.Qty.Length; g++)
            if (c.Qty[g] > 0) basket.Add((g, c.Qty[g], c.Grade[g]));
        var s = ShipmentOps.Dispatch(state, c.PosterActorId,
            ShipmentChannel.Requisition, c.OriginPortId, c.DestPortId,
            basket, null, out var outcome);
        if (s != null)
        {
            c.Status = CourierStatus.InTransit;
            c.ShipmentId = s.Id;
            return true;
        }
        Resolve(state, c, outcome);
        return true;
    }

    /// <summary>A carried shipment resolved (ShipmentOps.Advance calls in):
    /// delivery pays the fee to the fulfiller, a piracy loss refunds it to
    /// the poster (nobody gets paid for cargo at the bottom of the sea);
    /// either way the contract retires.</summary>
    internal static void Resolve(SimState state, CourierContract c,
                                 ShipmentOps.SailOutcome outcome)
    {
        if (outcome == ShipmentOps.SailOutcome.Arrived)
        {
            var fulfiller = state.LedgerOf(c.FulfillerActorId);
            fulfiller.Credits += c.FeeEscrow;
            fulfiller.Receipts += c.FeeEscrow;
            c.Status = CourierStatus.Delivered;
        }
        else
        {
            state.LedgerOf(c.PosterActorId).Credits += c.FeeEscrow;
            c.Status = CourierStatus.Lost;
        }
        c.FeeEscrow = 0;
        state.Couriers.Remove(c);
    }

    /// <summary>The contract a shipment carries, if any.</summary>
    internal static CourierContract? OfShipment(SimState state, int shipmentId)
    {
        foreach (var c in state.Couriers)                 // id order (P6)
            if (c.Status == CourierStatus.InTransit
                && c.ShipmentId == shipmentId) return c;
        return null;
    }

    /// <summary>Sweep open contracts past their expiry: cargo home to the
    /// origin larder, fee back to the poster.</summary>
    public static int ExpireOpen(SimState state)
    {
        int expired = 0;
        for (int i = state.Couriers.Count - 1; i >= 0; i--)
        {
            var c = state.Couriers[i];
            if (c.Status != CourierStatus.Open
                || state.WorldYear <= c.ExpiryYear) continue;
            var origin = state.Ports[c.OriginPortId];
            for (int g = 0; g < c.Qty.Length; g++)
                if (c.Qty[g] > 0)
                    origin.DepositStock(g, c.Qty[g], c.Grade[g]);
            state.LedgerOf(c.PosterActorId).Credits += c.FeeEscrow;
            c.FeeEscrow = 0;
            c.Status = CourierStatus.Expired;
            state.Couriers.RemoveAt(i);
            expired++;
        }
        return expired;
    }
}
