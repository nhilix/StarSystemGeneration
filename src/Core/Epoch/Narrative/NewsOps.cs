using System;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The news graph (narrative/perception-and-news.md): word travels
/// the lane network at traffic-derived speed — busy lanes carry news fast,
/// backwaters slowly, wilds barely — and crawls off-lane otherwise. The
/// delay field is derived from live fleet state every epoch and never
/// persisted (P3: the news-speed knob is emergent).</summary>
public static class NewsOps
{
    /// <summary>News carriage of one lane in hexes per world-year: the base
    /// (a standing lane always carries some word) plus a bonus saturating
    /// with posted round-trip traffic.</summary>
    public static double LaneNewsSpeed(SimState state, Lane lane)
    {
        var knobs = state.Config.News;
        double traffic = FleetOps.TrafficPerYear(state, lane);
        double saturation = knobs.TrafficSaturationTripsPerYear <= 0 ? 1.0
            : Math.Min(1.0, traffic / knobs.TrafficSaturationTripsPerYear);
        return knobs.BaseLaneSpeedHexPerYear
               + knobs.TrafficSpeedBonus * saturation;
    }

    /// <summary>World-years for news born at a hex to reach every port:
    /// Dijkstra over the port graph — each port seeded with the direct
    /// off-lane crawl from the origin, lanes relaxing at their traffic
    /// speed. Deterministic: ties pick the lower port id (P6).</summary>
    public static double[] DelayFromHex(SimState state, HexCoordinate origin)
    {
        var knobs = state.Config.News;
        int n = state.Ports.Count;
        var delay = new double[n];
        var done = new bool[n];
        for (int i = 0; i < n; i++)
            delay[i] = knobs.OffLaneSpeedHexPerYear <= 0
                ? double.PositiveInfinity
                : HexGrid.Distance(origin, state.Ports[i].Hex)
                  / knobs.OffLaneSpeedHexPerYear;
        for (int round = 0; round < n; round++)
        {
            int pick = -1;
            for (int i = 0; i < n; i++)
                if (!done[i] && (pick < 0 || delay[i] < delay[pick]))
                    pick = i;
            if (pick < 0 || double.IsPositiveInfinity(delay[pick])) break;
            done[pick] = true;
            foreach (var lane in state.Lanes)             // id order (P6)
            {
                int other = lane.PortAId == pick ? lane.PortBId
                    : lane.PortBId == pick ? lane.PortAId : -1;
                if (other < 0 || done[other]) continue;
                int dist = HexGrid.Distance(state.Ports[lane.PortAId].Hex,
                                            state.Ports[lane.PortBId].Hex);
                double speed = LaneNewsSpeed(state, lane);
                if (speed <= 0) continue;
                double reach = delay[pick] + dist / speed;
                if (reach < delay[other]) delay[other] = reach;
            }
        }
        return delay;
    }

    /// <summary>World-years before one actor hears news born at a hex — its
    /// nearest ear: the minimum over its own ports (internal relay is its
    /// own machinery), falling back to the off-lane crawl to its seat for
    /// the portless.</summary>
    public static double DelayYears(SimState state, int actorId,
                                    double[] delayField, HexCoordinate origin)
    {
        double best = double.PositiveInfinity;
        foreach (var p in state.Ports)                    // id order (P6)
            if (p.OwnerActorId == actorId && delayField[p.Id] < best)
                best = delayField[p.Id];
        if (!double.IsPositiveInfinity(best)) return best;
        var knobs = state.Config.News;
        if (knobs.OffLaneSpeedHexPerYear <= 0) return double.PositiveInfinity;
        return HexGrid.Distance(origin, state.Actors[actorId].Seat)
               / knobs.OffLaneSpeedHexPerYear;
    }
}
