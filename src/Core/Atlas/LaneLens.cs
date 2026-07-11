using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

public enum LaneStatus { Open, Quarantined, Severed }

/// <summary>One drawable lane: endpoints are the two ports' hexes (the
/// presentation lerps its own line), status and color carry the live
/// state. Traffic weighting joins in K2.</summary>
public readonly record struct LaneSegment(
    int LaneId, HexCoordinate A, HexCoordinate B, LaneStatus Status, Rgba Color);

/// <summary>The lanes lens — built highways as literal lines
/// (space-and-travel.md §P1). Severance derives from blockade fleets at
/// render time (FleetOps.SeveredLaneIds); quarantine reads the lane's own
/// lapse clock against the eye's world-year.</summary>
public static class LaneLens
{
    private static readonly Rgba OpenColor = new(255, 190, 80, 210);
    private static readonly Rgba QuarantinedColor = new(180, 200, 60, 210);
    private static readonly Rgba SeveredColor = new(230, 70, 60, 210);

    public static IReadOnlyList<LaneSegment> Segments(AtlasReadModel model,
                                                      EyeContext eye)
    {
        var severed = FleetOps.SeveredLaneIds(model.State);
        var segments = new LaneSegment[model.State.Lanes.Count];
        for (int i = 0; i < segments.Length; i++)
        {
            var lane = model.State.Lanes[i];
            // Quarantine first: SeveredLaneIds folds quarantined lanes into
            // its freight-closure set, but the lens keeps the two states
            // visually distinct (self-imposed closure vs interdiction).
            // Clocks read the state's own world-year — the eye never time
            // travels; scrubbing swaps the state (TimeMachine keyframes).
            var status = lane.QuarantinedUntil >= model.State.WorldYear
                    ? LaneStatus.Quarantined
                : severed.Contains(lane.Id) ? LaneStatus.Severed
                : LaneStatus.Open;
            segments[i] = new LaneSegment(
                lane.Id,
                model.State.Ports[lane.PortAId].Hex,
                model.State.Ports[lane.PortBId].Hex,
                status,
                status switch
                {
                    LaneStatus.Severed => SeveredColor,
                    LaneStatus.Quarantined => QuarantinedColor,
                    _ => OpenColor,
                });
        }
        return segments;
    }
}
