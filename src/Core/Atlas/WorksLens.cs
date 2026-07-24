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
/// STALLED, on the map. AC2.6 adds Purpose (FreightPurposeQuery, the SAME
/// derivation the ShipmentPanel and `efreight` read) — Color tints by it
/// while moving; STALLED still overrides to the one loud red regardless
/// of purpose (a closed leg is the same alarm for anyone's cargo).</summary>
public readonly record struct FreightMark(
    int ShipmentId, ShipmentChannel Channel, int OwnerActorId,
    FreightPurpose Purpose, HexCoordinate Hex, double Fraction, bool Stalled,
    double RemainingYears, Rgba Color);

/// <summary>An expedition convoy at its live hex — colony kit and war
/// columns in the field read as travel, not sites.</summary>
public readonly record struct ConvoyMark(
    int FleetId, HexCoordinate Hex, int OwnerActorId, Rgba Color);

/// <summary>One dash of an off-lane shipment's crawl path (AC4.1): the
/// direct origin→dest line a lane-less runner sails for decades, split
/// into dashes so it reads as a LIVE crawl — distinct from AC2.F2's solid,
/// faded recent-flow trails (a memory) and from the lane strokes (never a
/// lane at all). From/ToFraction locate the dash along the origin→dest
/// line in [0,1]; the renderer lerps between the two PORT hexes' world
/// positions directly — no per-hex rounding, so a long crawl draws the
/// true straight line instead of zigzagging hex to hex.</summary>
public readonly record struct CrawlDashMark(
    int ShipmentId, HexCoordinate Origin, HexCoordinate Dest,
    double FromFraction, double ToFraction, Rgba Color);

/// <summary>The works lens — the in-flight world (emap works parity, the
/// T2 layer): construction sites and freight on the move are residue
/// while they happen. In-flight only; arrivals and completions leave the
/// registries and thus the lens.</summary>
public static class WorksLens
{
    public static readonly Rgba SiteAmber = new(240, 195, 95, 230);
    /// <summary>Purpose tints, moving only — STALLED overrides all four to
    /// FreightStalled (below), the one universal "broken" alarm. State
    /// haul keeps the original pale freight-blue (no behavior change for
    /// the most common case); spread run borrows the trade-margin gold
    /// (TradeLens.MarginGold — a trader's own margin, literally); courier
    /// gets a violet identity the rest of the vocabulary doesn't use;
    /// war convoy reuses WarLens.StationBurn — red already means "war"
    /// throughout the atlas (DomainLens.WarShade, WarLens itself) — at
    /// full alpha, the loudest of the four.</summary>
    public static readonly Rgba FreightStateHaul = new(190, 225, 240, 210);
    public static readonly Rgba FreightSpreadRun = new(240, 195, 95, 220);
    public static readonly Rgba FreightCourier = new(190, 150, 235, 220);
    public static readonly Rgba FreightWarConvoy = new(235, 75, 55, 250);
    public static readonly Rgba FreightStalled = new(240, 90, 70, 240);
    public static readonly Rgba ConvoyWhite = new(235, 230, 210, 220);

    /// <summary>The freight mark's color: STALLED is the one loud red for
    /// any purpose; moving reads by purpose (war convoy loudest).</summary>
    public static Rgba FreightColorOf(FreightPurpose purpose, bool stalled)
    {
        if (stalled) return FreightStalled;
        return purpose switch
        {
            FreightPurpose.WarConvoy => FreightWarConvoy,
            FreightPurpose.Courier => FreightCourier,
            FreightPurpose.SpreadRun => FreightSpreadRun,
            _ => FreightStateHaul,
        };
    }

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
            var purpose = FreightPurposeQuery.Of(state, s).Purpose;
            marks[i] = new FreightMark(
                s.Id, s.Channel, s.OwnerActorId, purpose,
                HexGrid.Round(from.Q + (to.Q - from.Q) * f,
                              from.R + (to.R - from.R) * f),
                f, stalled, Math.Max(0.0, s.TotalYears - s.YearsInTransit),
                FreightColorOf(purpose, stalled));
        }
        return marks;
    }

    /// <summary>Dash geometry (AC4.1, taste call — noted for Eyeball 4):
    /// dash count scales with the hex span so short and long crawls both
    /// read as a dashed LINE rather than one giant or one microscopic
    /// segment; the on-fraction leaves a visible gap between dashes. The
    /// path renders at a fixed alpha well under the freight mark's own
    /// 190–250 (WorksLens.FreightColorOf) — brighter mark riding a dimmer
    /// path, so the live position always reads over the intended line.</summary>
    public const int CrawlDashMin = 6;
    public const int CrawlDashMax = 24;
    public const double CrawlDashOnFraction = 0.55;
    public const byte CrawlPathAlpha = 110;

    /// <summary>Every LIVE off-lane shipment's dashed direct path — lane
    /// traffic (RouteLaneIds non-empty) never dashes; it rides the lane
    /// strokes instead (the RenderFreight/efreight off-lane idiom, AC4.1
    /// decision #4). Purpose-tinted at CrawlPathAlpha (never STALLED — an
    /// off-lane crawl has no lane leg to close, WorksLensTests
    /// AnOffLaneCrawlNeverStalls).</summary>
    public static IReadOnlyList<CrawlDashMark> CrawlPaths(AtlasReadModel model,
                                                           EyeContext eye)
    {
        var state = model.State;
        if (state.Shipments.Count == 0) return Array.Empty<CrawlDashMark>();
        var marks = new List<CrawlDashMark>();
        foreach (var s in state.Shipments)             // id order (P6)
        {
            if (s.RouteLaneIds.Count != 0) continue;
            var from = state.Ports[s.OriginPortId].Hex;
            var to = state.Ports[s.DestPortId].Hex;
            var purpose = FreightPurposeQuery.Of(state, s).Purpose;
            var tint = FreightColorOf(purpose, stalled: false);
            var color = new Rgba(tint.R, tint.G, tint.B, CrawlPathAlpha);
            int hexSpan = HexGrid.Distance(from, to);
            int dashes = Math.Clamp(hexSpan, CrawlDashMin, CrawlDashMax);
            double cell = 1.0 / dashes;
            for (int i = 0; i < dashes; i++)
            {
                double start = i * cell;
                marks.Add(new CrawlDashMark(s.Id, from, to, start,
                    start + cell * CrawlDashOnFraction, color));
            }
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
