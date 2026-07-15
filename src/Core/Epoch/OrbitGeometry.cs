using System;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Intra-system geometry (locality slice §2): a deliberately
/// discrete orbit-distance metric, one level down from HexGrid.Distance —
/// slot-index gaps within a star, a fixed cross-star hop between stars.
/// Discrete on purpose: continuous orbital mechanics would be the one
/// outlier layer buying realism nothing else in the sim needs (P6/P7).</summary>
public static class OrbitGeometry
{
    /// <summary>Discrete distance between two bodies. Same star → the slot
    /// index gap; different stars → a fixed cross-star constant plus each
    /// body's distance to its own star's innermost slot. Zero if either ref
    /// is None (nothing to cross to).</summary>
    public static int OrbitDistance(StarSystem system, BodyRef a, BodyRef b,
                                    int crossStarSteps)
    {
        if (a.IsNone || b.IsNone) return 0;
        if (a.StarIndex == b.StarIndex)
            return Math.Abs(a.SlotIndex - b.SlotIndex);
        return crossStarSteps + DistToInner(system, a) + DistToInner(system, b);
    }

    private static int DistToInner(StarSystem system, BodyRef r)
    {
        if (r.StarIndex < 0 || r.StarIndex >= system.Stars.Count) return 0;
        var star = system.Stars[r.StarIndex];
        int min = int.MaxValue;
        foreach (var slot in star.Slots)
            if (slot.Index < min) min = slot.Index;
        return min == int.MaxValue ? 0 : Math.Abs(r.SlotIndex - min);
    }

    /// <summary>The local hop's transit years: OrbitDistance × the local-hop
    /// rate knob. Kept cheap relative to a lane-hop — an intra-system move is
    /// sub-step blur beside inter-port freight (locality slice §2).</summary>
    public static double LocalHopYears(int orbitDistance, EconomyKnobs eco) =>
        orbitDistance * eco.LocalHopYearsPerOrbitStep;
}
