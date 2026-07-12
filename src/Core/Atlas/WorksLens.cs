using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One construction site: an in-flight project's anchor, with
/// progress for the ring and last-fed fraction for the starvation read
/// (a starving site is a story, not a bug).</summary>
public readonly record struct SiteMark(
    int ProjectId, ProjectKind Kind, HexCoordinate Hex, double Progress,
    double FedFraction, Rgba Color);

/// <summary>Goods on the move: a shipment interpolated along its
/// origin→destination line by sailed fraction. Stalled = the current
/// leg's lane is closed (blockade, quarantine, dead gate) — efreight's
/// STALLED, on the map.</summary>
public readonly record struct FreightMark(
    int ShipmentId, ShipmentChannel Channel, int OwnerActorId,
    HexCoordinate Hex, double Fraction, bool Stalled, double RemainingYears,
    Rgba Color);

/// <summary>An expedition convoy at its live hex — colony kit and war
/// columns in the field read as travel, not sites.</summary>
public readonly record struct ConvoyMark(
    int FleetId, HexCoordinate Hex, int OwnerActorId, Rgba Color);

/// <summary>The works lens — the in-flight world (emap works parity, the
/// T2 layer): construction sites and freight on the move are residue
/// while they happen. In-flight only; arrivals and completions leave the
/// registries and thus the lens.</summary>
public static class WorksLens
{
    public static readonly Rgba SiteAmber = new(240, 195, 95, 230);
    public static readonly Rgba FreightMoving = new(190, 225, 240, 210);
    public static readonly Rgba FreightStalled = new(240, 90, 70, 240);
    public static readonly Rgba ConvoyWhite = new(235, 230, 210, 220);

    /// <summary>Every in-flight project's anchor; gate pairs mark both
    /// ends (EpochMapView.WorkCells, addressed). Travel kinds are
    /// convoys, not sites.</summary>
    public static IReadOnlyList<SiteMark> Sites(AtlasReadModel model,
                                                EyeContext eye)
    {
        var state = model.State;
        var marks = new List<SiteMark>();
        foreach (var p in state.Projects)                 // id order (P6)
        {
            if (!p.InFlight || p.Kind == ProjectKind.ColonyExpedition)
                continue;
            marks.Add(Mark(p, p.Hex));
            if (p.Kind == ProjectKind.GatePair && p.TargetId >= 0)
            {
                var lane = state.Lanes[p.TargetId];
                var a = state.Ports[lane.PortAId].Hex;
                var b = state.Ports[lane.PortBId].Hex;
                if (!a.Equals(p.Hex)) marks.Add(Mark(p, a));
                if (!b.Equals(p.Hex)) marks.Add(Mark(p, b));
            }
        }
        return marks;

        static SiteMark Mark(Project p, HexCoordinate hex) =>
            new(p.Id, p.Kind, hex, p.Progress, p.LastFedFraction, SiteAmber);
    }

    /// <summary>Shipments interpolated along their origin→destination
    /// line by sailed fraction (EpochMapView.FreightCells parity); the
    /// stalled read is efreight's — the current leg's lane closed.</summary>
    public static IReadOnlyList<FreightMark> Freight(AtlasReadModel model,
                                                     EyeContext eye)
    {
        var state = model.State;
        if (state.Shipments.Count == 0) return Array.Empty<FreightMark>();
        var severed = FleetOps.SeveredLaneIds(state);
        var marks = new FreightMark[state.Shipments.Count];
        for (int i = 0; i < marks.Length; i++)
        {
            var s = state.Shipments[i];
            var from = state.Ports[s.OriginPortId].Hex;
            var to = state.Ports[s.DestPortId].Hex;
            double f = s.TotalYears > 0
                ? Math.Min(1.0, s.YearsInTransit / s.TotalYears) : 1.0;
            bool stalled = false;
            int leg = ShipmentOps.CurrentLeg(s);
            if (leg >= 0 && leg < s.RouteLaneIds.Count)
            {
                var lane = state.Lanes[s.RouteLaneIds[leg]];
                stalled = severed.Contains(lane.Id)
                          || lane.QuarantinedUntil > state.WorldYear
                          || !LaneMath.IsLive(state, lane);
            }
            marks[i] = new FreightMark(
                s.Id, s.Channel, s.OwnerActorId,
                HexGrid.Round(from.Q + (to.Q - from.Q) * f,
                              from.R + (to.R - from.R) * f),
                f, stalled, Math.Max(0.0, s.TotalYears - s.YearsInTransit),
                stalled ? FreightStalled : FreightMoving);
        }
        return marks;
    }

    /// <summary>Expedition fleets in the field (EpochMapView.FreightCells'
    /// convoy half) — hulls required; an empty record is not a column.</summary>
    public static IReadOnlyList<ConvoyMark> Convoys(AtlasReadModel model,
                                                    EyeContext eye)
    {
        var marks = new List<ConvoyMark>();
        foreach (var fleet in model.State.Fleets)         // id order (P6)
            if (fleet.TotalHulls > 0
                && fleet.Posture == FleetPosture.Expedition)
                marks.Add(new ConvoyMark(fleet.Id, fleet.Hex,
                                         fleet.OwnerActorId, ConvoyWhite));
        return marks;
    }
}
