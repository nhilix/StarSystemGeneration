using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One flow captured at launch during the step that produced the
/// current keyframe (AC2.F2, user decision 2026-07-22): lane-borne
/// shipments mostly launch and arrive inside one 25-year step, so the
/// boundary registry (SimState.Shipments) is empty while the economy
/// moves — these captures are the record of what moved. RouteHexes is the
/// SAILED route as the ordered chain of port hexes (legs = Count−1; an
/// off-lane crawl is the endpoint pair), captured at launch so nothing
/// here depends on a lane registry that may have changed by the time a
/// scrubbed keyframe renders. In-memory only, held beside the TimeMachine
/// keyframe that stepped it; never serialized.</summary>
public readonly record struct RecentFlow(
    int ShipmentId, int OwnerActorId, FreightPurpose Purpose,
    int OriginPortId, int DestPortId,
    IReadOnlyList<HexCoordinate> RouteHexes, IReadOnlyList<double> Qty);

/// <summary>One corridor LEG's trail on the works lens: a single sailed
/// leg between two adjacent route ports (never the straight origin→dest
/// result — the eyeball-fix rule), purpose-tinted at reduced alpha — a
/// memory, not a thing. Flows counts every flow that crossed the leg in
/// either direction; overdraw reads as intensity (alpha rises with count,
/// capped), never mud.</summary>
public readonly record struct FlowTrailMark(
    HexCoordinate From, HexCoordinate To, FreightPurpose Purpose,
    int Flows, Rgba Color);

/// <summary>The recent-flow derivation the atlas trails and `eflows` both
/// read (core derives; REPL formats).</summary>
public static class RecentFlowQuery
{
    /// <summary>Trail alpha: floor well under the live freight marks'
    /// 220–250 and the lane strokes, rising per extra corridor flow to a
    /// cap that keeps the busiest corridor subordinate.</summary>
    public const byte TrailAlphaFloor = 70;
    public const byte TrailAlphaPerExtraFlow = 20;
    public const byte TrailAlphaCap = 130;

    /// <summary>A launch into a flow: purpose derived NOW, from the parts
    /// the tap carried — the registry can never answer for a sub-step
    /// courier (FreightPurposeQuery.FromParts is the one rule).</summary>
    public static RecentFlow Capture(ShipmentLaunch launch) => new(
        launch.ShipmentId, launch.OwnerActorId,
        FreightPurposeQuery.FromParts(launch.Channel,
            launch.RiderContractId >= 0, launch.RiderPriority),
        launch.OriginPortId, launch.DestPortId, launch.RouteHexes,
        launch.Qty);

    /// <summary>User scope: only courier and war-convoy flows RENDER.
    /// Capture records all four purposes; this filter is the render
    /// boundary (trails and `eflows` both sit behind it).</summary>
    public static bool Renders(FreightPurpose purpose) =>
        purpose is FreightPurpose.Courier or FreightPurpose.WarConvoy;

    /// <summary>The keyframe's flows as corridor trails, PER SAILED LEG
    /// (the eyeball-fix rule): each flow contributes one stroke per route
    /// leg — never a straight origin→dest line across hexes no lane
    /// connects (an off-lane crawl's single leg IS the direct line, the
    /// honest special case). Filtered to the rendering purposes;
    /// aggregated per (leg, purpose) with the leg's hex pair normalized so
    /// both sailing directions stack one corridor's intensity; emitted in
    /// first-seen order at first-seen orientation (capture order is
    /// dispatch order — deterministic); purpose tint at trail alpha.</summary>
    public static IReadOnlyList<FlowTrailMark> Trails(
        IReadOnlyList<RecentFlow> flows)
    {
        if (flows.Count == 0) return Array.Empty<FlowTrailMark>();
        var order = new List<(HexCoordinate From, HexCoordinate To,
                              FreightPurpose P)>();
        var counts = new Dictionary<(HexCoordinate, HexCoordinate,
                                     FreightPurpose), int>();
        foreach (var f in flows)
        {
            if (!Renders(f.Purpose)) continue;
            for (int leg = 0; leg + 1 < f.RouteHexes.Count; leg++)
            {
                var from = f.RouteHexes[leg];
                var to = f.RouteHexes[leg + 1];
                var key = Normalize(from, to, f.Purpose);
                if (counts.TryGetValue(key, out int n)) counts[key] = n + 1;
                else { counts[key] = 1; order.Add((from, to, f.Purpose)); }
            }
        }
        if (order.Count == 0) return Array.Empty<FlowTrailMark>();
        var marks = new List<FlowTrailMark>(order.Count);
        foreach (var (from, to, p) in order)
        {
            int n = counts[Normalize(from, to, p)];
            var tint = p == FreightPurpose.WarConvoy
                ? WorksLens.FreightWarConvoy : WorksLens.FreightCourier;
            byte a = (byte)Math.Min((int)TrailAlphaCap,
                TrailAlphaFloor + TrailAlphaPerExtraFlow * (n - 1));
            marks.Add(new FlowTrailMark(from, to, p, n,
                new Rgba(tint.R, tint.G, tint.B, a)));
        }
        return marks;
    }

    /// <summary>One corridor regardless of sailing direction: the leg's
    /// hex pair ordered by (Q, R) for the aggregation key.</summary>
    private static (HexCoordinate, HexCoordinate, FreightPurpose) Normalize(
        HexCoordinate a, HexCoordinate b, FreightPurpose p) =>
        b.Q < a.Q || (b.Q == a.Q && b.R < a.R) ? (b, a, p) : (a, b, p);
}
