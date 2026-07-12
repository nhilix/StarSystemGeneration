using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>The shipment lifecycle (spec §4b): dispatch prices the route
/// over the live lane network at departure, Advance sails every in-flight
/// record each Markets step (stalling at closed legs), arrival lands the
/// cargo — a requisition into the destination port's stockpile, freight
/// into its market with the owner credited as supplier. Pure deterministic
/// math over ordered state; the piracy roll (channel 75) is the only draw.</summary>
public static class ShipmentOps
{
    /// <summary>Route a basket from port to port. Transit inside the
    /// current step's span is sub-step blur: delivered immediately, no
    /// record, null return. Otherwise the goods exist only in the returned
    /// shipment until arrival; departure sails the dispatching step's span.
    /// The basket is already drawn — the caller owns conservation up to
    /// this call.</summary>
    public static Shipment? Dispatch(SimState state, int ownerActorId,
        ShipmentChannel channel, int fromPortId, int toPortId,
        IReadOnlyList<(int Good, double Qty, double Grade)> basket,
        MarketStepScratch? scratch = null)
    {
        var (laneIds, legYears) = PlanRoute(state, fromPortId, toPortId);
        double total = 0;
        foreach (var y in legYears) total += y;
        int span = state.Config.Sim.YearsPerEpoch;
        if (total <= span)
        {
            var direct = new Shipment(-1, ownerActorId, channel, fromPortId,
                toPortId, state.WorldYear, laneIds, legYears);
            Fill(direct, basket);
            Deliver(state, scratch, direct);
            return null;
        }
        var s = new Shipment(state.NextShipmentId++, ownerActorId, channel,
            fromPortId, toPortId, state.WorldYear, laneIds, legYears)
        { YearsInTransit = span };
        Fill(s, basket);
        state.Shipments.Add(s);
        return s;
    }

    private static void Fill(Shipment s,
        IReadOnlyList<(int Good, double Qty, double Grade)> basket)
    {
        foreach (var (good, qty, grade) in basket)
        {
            if (qty <= 0) continue;
            double sum = s.Qty[good] + qty;
            s.Grade[good] = (s.Qty[good] * s.Grade[good] + qty * grade) / sum;
            s.Qty[good] = sum;
        }
    }

    /// <summary>The route and its leg years: the shortest live-lane path
    /// when one exists (leg speed = FreightHexesPerYearBase × the lane's
    /// gate-tier TransitSpeed), else one off-lane crawl leg (spec §4b).</summary>
    public static (IReadOnlyList<int> LaneIds, IReadOnlyList<double> LegYears)
        PlanRoute(SimState state, int fromPortId, int toPortId)
    {
        var eco = state.Config.Economy;
        var (pathHexes, laneIds) = LaneNetwork.ShortestPath(
            state, fromPortId, toPortId);
        if (pathHexes < 0)
        {
            double hexes = HexGrid.Distance(state.Ports[fromPortId].Hex,
                                            state.Ports[toPortId].Hex);
            return (new int[0], new[]
                { hexes / Math.Max(1e-9, eco.OffLaneFreightHexesPerYear) });
        }
        var legYears = new double[laneIds.Count];
        for (int i = 0; i < laneIds.Count; i++)
        {
            var lane = state.Lanes[laneIds[i]];
            double hexes = HexGrid.Distance(state.Ports[lane.PortAId].Hex,
                                            state.Ports[lane.PortBId].Hex);
            legYears[i] = hexes / Math.Max(1e-9,
                eco.FreightHexesPerYearBase * LaneMath.TransitSpeed(state, lane));
        }
        return (laneIds, legYears);
    }

    /// <summary>Sail every in-flight shipment by the step's span, in id
    /// order (P6). A closed current leg — severed by blockade, quarantined,
    /// or dead (a wrecked gate) — stalls the freight where it floats: the
    /// fortress starves at the pace of its last delivery. Arrivals deliver
    /// and leave the registry.</summary>
    public static void Advance(SimState state, MarketStepScratch scratch)
    {
        if (state.Shipments.Count == 0) return;
        int span = state.Config.Sim.YearsPerEpoch;
        foreach (var s in state.Shipments)                // id order (P6)
        {
            double budget = span;
            while (budget > 1e-9 && s.YearsInTransit < s.TotalYears - 1e-9)
            {
                int leg = CurrentLeg(s);
                if (leg < s.RouteLaneIds.Count
                    && LegClosed(state, scratch, s.RouteLaneIds[leg])) break;
                double legEnd = 0;
                for (int i = 0; i <= leg; i++) legEnd += s.LegYears[i];
                double sail = Math.Min(budget, legEnd - s.YearsInTransit);
                // float dust at a leg boundary: snap forward, re-resolve
                if (sail <= 0) { s.YearsInTransit = legEnd; continue; }
                s.YearsInTransit += sail;
                budget -= sail;
            }
        }
        for (int i = state.Shipments.Count - 1; i >= 0; i--)
        {
            var s = state.Shipments[i];
            if (s.YearsInTransit < s.TotalYears - 1e-9) continue;
            Deliver(state, scratch, s);
            state.Shipments.RemoveAt(i);
        }
    }

    /// <summary>Index of the leg the shipment is currently sailing.</summary>
    public static int CurrentLeg(Shipment s)
    {
        double sofar = 0;
        for (int i = 0; i < s.LegYears.Count; i++)
        {
            sofar += s.LegYears[i];
            if (s.YearsInTransit < sofar - 1e-9) return i;
        }
        return s.LegYears.Count - 1;
    }

    private static bool LegClosed(SimState state, MarketStepScratch scratch,
                                  int laneId)
    {
        var lane = state.Lanes[laneId];
        return scratch.Severed.Contains(laneId)
            || lane.QuarantinedUntil > state.WorldYear
            || !LaneMath.IsLive(state, lane);
    }

    /// <summary>Land the cargo: a requisition banks into the destination
    /// port's stockpile (capacity overflow spills onto the market shelf —
    /// goods conserve); freight deposits into the destination market with
    /// the owner credited as its supplier (paid at distribution, the
    /// Arbitrage convention). Without a scratch (a mid-phase dispatch has
    /// one; unit paths may not) freight falls back to a plain deposit.</summary>
    private static void Deliver(SimState state, MarketStepScratch? scratch,
                                Shipment s)
    {
        var port = state.Ports[s.DestPortId];
        var market = state.Markets[s.DestPortId];
        for (int g = 0; g < s.Qty.Length; g++)
        {
            if (s.Qty[g] <= 0) continue;
            if (s.Channel == ShipmentChannel.Requisition)
            {
                double cap = MarketEngine.StockCapacityAt(state, port);
                double room = Math.Max(0, cap - port.StockQty[g]);
                double banked = Math.Min(room, s.Qty[g]);
                port.DepositStock(g, banked, s.Grade[g]);
                if (s.Qty[g] - banked > 0)
                    market.Deposit(g, s.Qty[g] - banked, s.Grade[g]);
            }
            else if (scratch != null)
                MarketEngine.Deposit(state, scratch, s.DestPortId,
                    s.OwnerActorId, g, s.Qty[g], s.Grade[g]);
            else
                market.Deposit(g, s.Qty[g], s.Grade[g]);
        }
    }
}
