using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>Traffic bands, matching EpochMapView's glyph thresholds
/// (',' no hulls · '-' &lt;0.5 · '=' &lt;2 · '+' &lt;5 · '#' 5+) so the
/// atlas and the REPL tell the same story.</summary>
public enum TrafficBand { None = 0, Trickle = 1, Light = 2, Steady = 3, Heavy = 4 }

/// <summary>One traffic-weighted lane: trips/year from the posted-fleet
/// derivation (FleetOps.TrafficPerYear — the slice-I news-speed data),
/// a saturating weight for width/brightness, and a color on the lane
/// cyan whose loudness follows the flow.</summary>
public readonly record struct TrafficSegment(
    int LaneId, HexCoordinate A, HexCoordinate B, double TripsPerYear,
    TrafficBand Band, double Weight, Rgba Color);

/// <summary>The traffic lens — busy lanes read wide and bright, posted
/// but idle lanes barely register (a lane exists, nothing moves, no
/// news). Weights derive from fleet state at query time; nothing here
/// is stored.</summary>
public static class TrafficLens
{
    // The lane cyan carries traffic too — same hue as LaneLens so the
    // two lenses read as one system, loudness the only variable.
    private static readonly Rgba LaneHue = new(86, 196, 220);
    private const byte IdleAlpha = 45;
    private const byte FullAlpha = 220;
    /// <summary>Trips/year where the weight saturates — the '#' band's
    /// floor; anything at or past it reads as a full highway.</summary>
    private const double SaturationTrips = 5.0;

    /// <summary>Band parity with EpochMapView.TrafficGlyph.</summary>
    public static TrafficBand BandOf(double tripsPerYear) => tripsPerYear switch
    {
        <= 0 => TrafficBand.None,
        < 0.5 => TrafficBand.Trickle,
        < 2.0 => TrafficBand.Light,
        < 5.0 => TrafficBand.Steady,
        _ => TrafficBand.Heavy,
    };

    /// <summary>Saturating width/brightness weight: 0 when nothing moves,
    /// sqrt-eased toward 1 at SaturationTrips (small flows stay visible,
    /// big ones don't blow out).</summary>
    public static double WeightOf(double tripsPerYear) =>
        tripsPerYear <= 0 ? 0.0
        : Math.Sqrt(Math.Min(1.0, tripsPerYear / SaturationTrips));

    public static IReadOnlyList<TrafficSegment> Segments(AtlasReadModel model,
                                                         EyeContext eye)
    {
        var segments = new TrafficSegment[model.State.Lanes.Count];
        for (int i = 0; i < segments.Length; i++)
        {
            var lane = model.State.Lanes[i];
            double trips = FleetOps.TrafficPerYear(model.State, lane);
            double weight = WeightOf(trips);
            segments[i] = new TrafficSegment(
                lane.Id,
                model.State.Ports[lane.PortAId].Hex,
                model.State.Ports[lane.PortBId].Hex,
                trips, BandOf(trips), weight,
                new Rgba(LaneHue.R, LaneHue.G, LaneHue.B,
                         (byte)(IdleAlpha + (FullAlpha - IdleAlpha) * weight)));
        }
        return segments;
    }
}
