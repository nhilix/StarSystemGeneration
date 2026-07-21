using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Distance-weighted facility staffing (locality slice §3): a
/// facility draws labor from its domain's segments, but proximity now
/// weights each segment's contribution — an airless mine can be crewed by
/// commute from a habitat one local-hop away, at a cost. Pure; the exact
/// falloff curve is a tunable, not a design fork. Changes production
/// magnitude only; wage distribution (MarketEngine.PayWages) is untouched.</summary>
public static class StaffingOps
{
    /// <summary>Proximity weight in (0, 1]: 1 on the facility's own body,
    /// falling with hex-hop + local-hop distance from it. The hex-hop is
    /// measured segment-hex → facility-hex (domain-hex-expansion design
    /// §3): a resident of a satellite hex crews it at full weight, while a
    /// port's distant households crew it weakly.</summary>
    public static double ProximityWeight(SimState state, Facility f,
                                         PopulationSegment seg)
    {
        var eco = state.Config.Economy;
        int hexHop = HexGrid.Distance(seg.Hex, f.Hex);
        int localHop = 0;
        if (hexHop == 0 && !seg.Body.IsNone && !f.Body.IsNone
            && state.SettledSystems.TryGetValue(f.Hex, out var system)
            && system != null)
            localHop = OrbitGeometry.OrbitDistance(system, seg.Body, f.Body,
                (int)eco.CrossStarHopOrbitSteps);
        double dist = hexHop + localHop;
        return 1.0 / (1.0 + eco.StaffingDistanceFalloff * dist);
    }

    /// <summary>Distance-weighted workforce a facility can draw from its
    /// attached port's segments — the labor sum SupplyLands consumes in
    /// place of the flat pool.</summary>
    public static double WeightedWorkforce(SimState state, Facility f, int portId)
    {
        double labor = 0;
        foreach (var seg in state.Segments)               // id order (P6)
        {
            if (seg.PortId != portId) continue;
            labor += seg.Size * ProximityWeight(state, f, seg);
        }
        return labor;
    }
}
