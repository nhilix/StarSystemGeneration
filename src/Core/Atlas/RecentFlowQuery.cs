using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One flow captured at launch during the step that produced the
/// current keyframe (AC2.F2, user decision 2026-07-22): lane-borne
/// shipments mostly launch and arrive inside one 25-year step, so the
/// boundary registry (SimState.Shipments) is empty while the economy
/// moves — these captures are the record of what moved. In-memory only,
/// held beside the TimeMachine keyframe that stepped them; never
/// serialized.</summary>
public readonly record struct RecentFlow(
    int ShipmentId, int OwnerActorId, FreightPurpose Purpose,
    int OriginPortId, int DestPortId, IReadOnlyList<double> Qty);

/// <summary>A corridor's trail on the works lens: origin port hex to dest
/// port hex, purpose-tinted at reduced alpha — a memory, not a thing.
/// Flows counts the launches aggregated onto the corridor; overdraw reads
/// as intensity (alpha rises with count, capped), never mud.</summary>
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
        launch.OriginPortId, launch.DestPortId, launch.Qty);

    /// <summary>User scope: only courier and war-convoy flows RENDER.
    /// Capture records all four purposes; this filter is the render
    /// boundary (trails and `eflows` both sit behind it).</summary>
    public static bool Renders(FreightPurpose purpose) =>
        purpose is FreightPurpose.Courier or FreightPurpose.WarConvoy;

    /// <summary>The keyframe's flows as corridor trails: filtered to the
    /// rendering purposes, aggregated per (origin, dest, purpose) in
    /// first-seen order (capture order is dispatch order — deterministic),
    /// port hexes resolved, purpose tint at trail alpha.</summary>
    public static IReadOnlyList<FlowTrailMark> Trails(SimState state,
        IReadOnlyList<RecentFlow> flows)
    {
        if (flows.Count == 0) return Array.Empty<FlowTrailMark>();
        var order = new List<(int Origin, int Dest, FreightPurpose P)>();
        var counts = new Dictionary<(int, int, FreightPurpose), int>();
        foreach (var f in flows)
        {
            if (!Renders(f.Purpose)) continue;
            var key = (f.OriginPortId, f.DestPortId, f.Purpose);
            if (counts.TryGetValue(key, out int n)) counts[key] = n + 1;
            else { counts[key] = 1; order.Add(key); }
        }
        if (order.Count == 0) return Array.Empty<FlowTrailMark>();
        var marks = new List<FlowTrailMark>(order.Count);
        foreach (var key in order)
        {
            int n = counts[(key.Origin, key.Dest, key.P)];
            var tint = key.P == FreightPurpose.WarConvoy
                ? WorksLens.FreightWarConvoy : WorksLens.FreightCourier;
            byte a = (byte)Math.Min((int)TrailAlphaCap,
                TrailAlphaFloor + TrailAlphaPerExtraFlow * (n - 1));
            marks.Add(new FlowTrailMark(
                state.Ports[key.Origin].Hex, state.Ports[key.Dest].Hex,
                key.P, n, new Rgba(tint.R, tint.G, tint.B, a)));
        }
        return marks;
    }
}
