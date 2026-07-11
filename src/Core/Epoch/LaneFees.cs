namespace StarGen.Core.Epoch;

/// <summary>Per-crossing fees decided by the destination-side gate's owner
/// (lane-economics spec §4): vertical integration crosses free, corp gates
/// toll, polity gates charge customs on foreign freight only — the existing
/// tariff machinery relocated to a physical collection point, charged once,
/// at the gate the shipment enters through.</summary>
public static class LaneFees
{
    /// <summary>Fee per unit at destination price, and who collects it
    /// (−1 when free). Legality/prohibition checks stay with the caller.</summary>
    public static double CrossingFeePerUnit(SimState state, Lane lane,
        int dstPortId, int good, double dstPrice, int shipperActorId,
        out int recipientActorId)
    {
        recipientActorId = -1;
        int gateId = lane.PortAId == dstPortId ? lane.GateAId : lane.GateBId;
        if (gateId < 0) return 0;
        int owner = state.Facilities[gateId].OwnerActorId;
        if (owner == shipperActorId) return 0;        // your gate, your road
        if (owner < 0 || owner >= state.Actors.Count) return 0;
        if (state.Actors[owner].Kind == ActorKind.Corporation)
        {
            recipientActorId = owner;
            return state.Config.Economy.GateTollRate * dstPrice;
        }
        // polity gate: customs on foreign freight, once, at entry
        var policies = state.Actors[owner].Policies as PolityPolicies
                       ?? PolityPolicies.Default;
        if (!policies.TariffSchedule.TryGetValue(good, out double rate)
            || rate <= 0) return 0;
        double fee = rate * dstPrice
                     * RelationsOps.TariffFactor(state, shipperActorId, owner);
        if (fee <= 0) return 0;
        recipientActorId = owner;
        return fee;
    }
}
