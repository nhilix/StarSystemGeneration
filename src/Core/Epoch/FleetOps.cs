using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>Fleet-state operations over SimState: registry lookups now;
/// yard production, posture management, and supply land here through the
/// slice. Iteration is fleet-id then design-id order everywhere (P6).</summary>
public static class FleetOps
{
    /// <summary>Colony hulls docked in an actor's Reserve fleets — what an
    /// expedition can actually assemble (the founding gate's count).</summary>
    public static int ColonyHullsInReserve(SimState state, int actorId)
    {
        int hulls = 0;
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.OwnerActorId != actorId
                || fleet.Posture != FleetPosture.Reserve) continue;
            foreach (var g in fleet.Hulls)
                if (state.Designs[g.DesignId].Role == ShipRole.Colony)
                    hulls += g.Count;
        }
        return hulls;
    }

    /// <summary>The fleet's composition as (sheet, count) pairs for the
    /// vector aggregation — sheets derived on demand from the designs.</summary>
    public static List<(DesignSheet Sheet, int Count)> Composition(
        SimState state, FleetRecord fleet)
    {
        var composition = new List<(DesignSheet, int)>(fleet.Hulls.Count);
        foreach (var g in fleet.Hulls)                    // design-id order
            composition.Add((DesignRegistry.SheetOf(state, state.Designs[g.DesignId]),
                             g.Count));
        return composition;
    }

    /// <summary>Layer-2 vectors of one fleet, computed on demand.</summary>
    public static FleetVectors Vectors(SimState state, FleetRecord fleet) =>
        FleetMath.Vectors(Composition(state, fleet));
}
