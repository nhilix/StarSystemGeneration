using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>Deterministic queries over the live-lane graph (lane-economics
/// spec §3): shortest network paths, gate-slot budgets, and the anti-web
/// eligibility rule the builders share. Everything iterates in id order and
/// tie-breaks on lower ids (P6).</summary>
public static class LaneNetwork
{
    /// <summary>Dijkstra over live lanes weighted by hex length. Returns
    /// (−1, empty) when no path. O(P²) scans — port counts are small and
    /// determinism beats a heap here.</summary>
    public static (int PathHexes, List<int> LaneIds) ShortestPath(
        SimState state, int fromPortId, int toPortId)
    {
        int n = state.Ports.Count;
        var dist = new int[n];
        var viaLane = new int[n];
        var done = new bool[n];
        for (int i = 0; i < n; i++) { dist[i] = int.MaxValue; viaLane[i] = -1; }
        dist[fromPortId] = 0;
        for (int round = 0; round < n; round++)
        {
            int u = -1;
            for (int i = 0; i < n; i++)          // lowest dist, tie: lower id
                if (!done[i] && dist[i] != int.MaxValue
                    && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0) break;
            done[u] = true;
            if (u == toPortId) break;
            foreach (var lane in state.Lanes)     // id order (P6)
            {
                if (!LaneMath.IsLive(state, lane)) continue;
                int v = lane.PortAId == u ? lane.PortBId
                      : lane.PortBId == u ? lane.PortAId : -1;
                if (v < 0 || done[v]) continue;
                int w = HexGrid.Distance(state.Ports[lane.PortAId].Hex,
                                         state.Ports[lane.PortBId].Hex);
                if (dist[u] + w < dist[v])
                { dist[v] = dist[u] + w; viaLane[v] = lane.Id; }
            }
        }
        if (dist[toPortId] == int.MaxValue) return (-1, new List<int>());
        var path = new List<int>();
        for (int at = toPortId; viaLane[at] >= 0;)
        {
            var lane = state.Lanes[viaLane[at]];
            path.Add(lane.Id);
            at = lane.PortAId == at ? lane.PortBId : lane.PortAId;
        }
        path.Reverse();
        return (dist[toPortId], path);
    }

    /// <summary>Gates standing at this port's hex — live or ruined, a slot
    /// is a slot (a wrecked gate still occupies its berth until repaired).</summary>
    public static int GateCount(SimState state, Port port)
    {
        int count = 0;
        foreach (var f in state.Facilities)
            if (f.TypeId == (int)Substrate.InfraTypeId.Gate
                && f.Hex.Equals(port.Hex)) count++;
        return count;
    }

    public static bool HasFreeGateSlot(SimState state, Port port) =>
        GateCount(state, port)
        < port.Tier * state.Config.Infrastructure.GateSlotsPerPortTier;

    /// <summary>Spec §3 rules 3–4: a direct lane is eligible when the pair
    /// is unreachable on the network, when the network detour is worse than
    /// DetourFactor × direct, or when every lane on the shortest path has
    /// run saturated long enough to earn the express bypass.</summary>
    public static bool DirectLaneEligible(SimState state, int portAId, int portBId)
    {
        var cfg = state.Config;
        var (pathHexes, laneIds) = ShortestPath(state, portAId, portBId);
        if (pathHexes < 0) return true;
        int direct = HexGrid.Distance(state.Ports[portAId].Hex,
                                      state.Ports[portBId].Hex);
        if (pathHexes > cfg.Expansion.DetourFactor * direct) return true;
        // the earn-in clock runs in world-years (P7): epochs × generation
        int needYears = cfg.Expansion.SaturatedEpochsForExpress
                        * cfg.Sim.GenerationYears;
        foreach (var laneId in laneIds)
            if (state.Lanes[laneId].SaturatedYears < needYears) return false;
        return true;
    }
}
