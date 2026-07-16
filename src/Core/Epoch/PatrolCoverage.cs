using System;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Patrol enforcement coverage as a spatial field (locality slice
/// §2): a Patrol fleet's reach weakens with hex-hop + local-hop distance from
/// wherever it is docked, instead of a flat domain-wide multiplier. The
/// strongest hostile patrol coverage onto a point is what an off-lane runner
/// must evade (§5's detection roll reads this). Deterministic: max across
/// fleets is order-insensitive. Pure — no rolls, no state mutation.</summary>
public static class PatrolCoverage
{
    /// <summary>The strongest Patrol coverage any fleet NOT owned by
    /// <paramref name="ownerActorId"/> projects onto (hex, body): per Patrol
    /// fleet, coverage = max(0, 1 − falloff × (hexHop + localHop)) from its
    /// dock; the max across fleets. 0 where no hostile patrol reaches.</summary>
    public static double At(SimState state, HexCoordinate hex, BodyRef body,
                            int ownerActorId)
    {
        var war = state.Config.War;
        var eco = state.Config.Economy;
        double best = 0.0;
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.Posture != FleetPosture.Patrol) continue;
            if (fleet.OwnerActorId == ownerActorId) continue;
            int hexHop = HexGrid.Distance(fleet.Hex, hex);
            int localHop = 0;
            if (hexHop == 0 && !fleet.Body.IsNone && !body.IsNone
                && state.SettledSystems.TryGetValue(hex, out var system)
                && system != null)
                localHop = OrbitGeometry.OrbitDistance(system, fleet.Body, body,
                    (int)eco.CrossStarHopOrbitSteps);
            double cover = 1.0 - war.PatrolCoverageFalloff * (hexHop + localHop);
            if (cover > best) best = cover;
        }
        return Math.Max(0.0, best);
    }
}
