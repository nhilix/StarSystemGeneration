using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>The per-currency supply pass (currency-and-FX design, slice CU-1
/// task 9, deliverable 1). Run once at the END of every epoch — after every
/// phase has moved money and before the next epoch's <see cref="FxOps.
/// RecomputeRates"/> reads "the prior epoch's ending Supply" — it walks every
/// conserved money store, partitions it by the currency it is denominated in,
/// and WRITES the aggregate back onto each live <see cref="Currency.Supply"/>.
///
/// Without this pass <see cref="Currency.Supply"/> stays 0 forever, pinning
/// every <see cref="Currency.NumeraireRate"/> at exactly 1.0
/// (<c>1/(1+k·0)=1</c>) and leaving the whole FX mechanism dormant regardless
/// of how correct its conversion math is.
///
/// The walk is the per-currency decomposition of the same conserved holder
/// classes <see cref="MetricsOps.Money"/> sums galaxy-wide: a polity's
/// treasury AND its investment pools (both denominated in that polity's own
/// currency), every corporation's per-currency wallet buckets, segment and
/// faction wealth resolved through the owning polity, and the three escrow
/// kinds (order, courier, colony-expedition purse) resolved to the currency
/// they are actually held in. Pools are included — they are the polity's money
/// in its own currency, and omitting them would break the per-currency
/// conservation residual the instant a treasury allocates into a pool.
///
/// Deterministic: registries walk in their fixed id order, corporation wallet
/// buckets in ascending currency-id order, no hash rolls, no floating
/// iteration. A currency's supply accumulates same-currency contributions in
/// one fixed pass, so it is byte-identical run to run.</summary>
public static class SupplyOps
{
    /// <summary>Recompute and write every currency's ending supply. Idempotent
    /// given fixed state; the single mutating counterpart to the read-only
    /// <see cref="MetricsOps"/> probe.</summary>
    public static void Recompute(SimState state)
    {
        double[] supply = WalkNative(state);
        // registry is dense (id == index, per SimState.FoundCurrency); assign
        // by index so a retired currency still receives its dangling total
        for (int i = 0; i < state.Currencies.Count; i++)
            state.Currencies[i].Supply = supply[i];
    }

    /// <summary>The per-currency native supply, indexed by currency id (dense).
    /// Amounts denominated in an unwired currency (pre-genesis sentinel −1, or
    /// an id not in the registry) are dropped — only a live post-genesis run
    /// has real currencies, which is the only run whose rates matter.</summary>
    public static double[] WalkNative(SimState state)
    {
        var supply = new double[state.Currencies.Count];

        void Add(int currencyId, double amount)
        {
            if (currencyId >= 0 && currencyId < supply.Length)
                supply[currencyId] += amount;
        }

        // polities: treasury + investment pools, in the polity's own currency
        foreach (var pr in state.Polities)                    // actor-id order (P6)
            Add(pr.CurrencyId,
                pr.Credits + pr.ExpansionPoints + pr.DevelopmentPoints
                + pr.MilitaryPoints + pr.ReservePoints);

        // corporations: each wallet bucket in the currency it is held in
        foreach (var corp in state.Corporations)              // id order (P6)
        {
            var ids = new List<int>(corp.Holdings.Keys);
            ids.Sort();                                       // ascending id (P6)
            foreach (int id in ids)
                Add(id, corp.Holdings[id]);
        }

        // household wealth: resolved to the owning (port-owner) polity's currency
        foreach (var s in state.Segments)                     // id order (P6)
            Add(PortCurrency(state, s.PortId), s.Wealth);

        // faction war chests: resolved to their polity's currency
        foreach (var f in state.Factions)                     // id order (P6)
            Add(PolityCurrency(state, f.PolityId), f.Wealth);

        // resting buy-order escrow: held in the market's (port-owner's) currency
        foreach (var o in state.Orders)                       // id order (P6)
            Add(PortCurrency(state, o.PortId), o.EscrowCredits);

        // courier fee escrow: the poster owns the origin port, so the escrow
        // is in that port's local currency (CourierOps re-derives it the same
        // way at resolution — no persisted currency field)
        foreach (var c in state.Couriers)                     // id order (P6)
            if (c.Status == CourierStatus.Open
                || c.Status == CourierStatus.InTransit)
                Add(PortCurrency(state, c.OriginPortId), c.FeeEscrow);

        // colony-expedition purses aboard in-flight convoys: the founding
        // polity charged them from its expansion pool, so they are its currency
        foreach (var p in state.Projects)                     // id order (P6)
            if (p.Kind == ProjectKind.ColonyExpedition && p.InFlight)
                Add(PolityCurrency(state, p.FunderActorId),
                    state.Config.Expansion.ColonyCost);

        return supply;
    }

    /// <summary>The currency an actor's polity mints, or −1 when the actor is
    /// not a live polity (a corporation, or an unresolved id) — a safe scan
    /// that never throws the way <see cref="SimState.PolityOf"/> would, so the
    /// probe stays robust over any registry shape.</summary>
    private static int PolityCurrency(SimState state, int actorId)
    {
        if (actorId < 0) return -1;
        if (actorId < state.Polities.Count
            && state.Polities[actorId].ActorId == actorId)
            return state.Polities[actorId].CurrencyId;
        foreach (var p in state.Polities)
            if (p.ActorId == actorId) return p.CurrencyId;
        return -1;
    }

    /// <summary>The local currency of a port's market — the port-owning polity's
    /// currency, or −1 if the port has no live-polity owner.</summary>
    private static int PortCurrency(SimState state, int portId)
    {
        if (portId < 0 || portId >= state.Ports.Count) return -1;
        return PolityCurrency(state, state.Ports[portId].OwnerActorId);
    }
}
