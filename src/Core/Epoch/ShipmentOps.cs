using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>The shipment lifecycle (spec §4b): dispatch prices the route
/// over the live lane network at departure, Advance sails every in-flight
/// record each Markets step (stalling at closed legs), arrival lands the
/// cargo — a requisition into the destination port's stockpile, freight
/// into its market with the owner credited as supplier. Pure deterministic
/// math over ordered state; the piracy roll (channel 75) is the only draw.</summary>
public static class ShipmentOps
{
    /// <summary>Route a basket from port to port. Departure sails the
    /// dispatching step's span through the SAME world Advance sails —
    /// closed legs stall it at the dock, hunted legs roll the same piracy
    /// (review fixes 1–2: dispatch is not exempt); an open route inside
    /// the span is sub-step blur and delivers now (null return, no
    /// record). The basket is already drawn — the caller owns conservation
    /// up to this call.</summary>
    public static Shipment? Dispatch(SimState state, int ownerActorId,
        ShipmentChannel channel, int fromPortId, int toPortId,
        IReadOnlyList<(int Good, double Qty, double Grade)> basket,
        MarketStepScratch? scratch = null)
    {
        var (laneIds, legYears) = PlanRoute(state, fromPortId, toPortId);
        return DispatchVia(state, ownerActorId, channel, fromPortId,
            toPortId, laneIds, legYears, basket, scratch);
    }

    /// <summary>Dispatch over an already-chosen route — arbitrage moves on
    /// THE lane its haulers are posted to, not a recomputed path.</summary>
    public static Shipment? DispatchVia(SimState state, int ownerActorId,
        ShipmentChannel channel, int fromPortId, int toPortId,
        IReadOnlyList<int> laneIds, IReadOnlyList<double> legYears,
        IReadOnlyList<(int Good, double Qty, double Grade)> basket,
        MarketStepScratch? scratch = null)
    {
        var s = new Shipment(state.NextShipmentId++, ownerActorId, channel,
            fromPortId, toPortId, state.WorldYear, laneIds, legYears);
        Fill(s, basket);
        var severed = scratch?.Severed ?? FleetOps.SeveredLaneIds(state);
        if (Sail(state, scratch, severed, HunterMap(state), s,
                state.Config.Sim.YearsPerEpoch) != SailOutcome.InTransit)
            return null;                     // delivered or taken this step
        state.Shipments.Add(s);
        return s;
    }

    private static void Fill(Shipment s,
        IReadOnlyList<(int Good, double Qty, double Grade)> basket)
    {
        foreach (var (good, qty, grade) in basket)
        {
            if (qty <= 0) continue;
            double sum = s.Qty[good] + qty;
            s.Grade[good] = (s.Qty[good] * s.Grade[good] + qty * grade) / sum;
            s.Qty[good] = sum;
        }
    }

    /// <summary>One lane's transit years for freight (spec §4b: gate tier
    /// sets speed).</summary>
    public static double LaneLegYears(SimState state, Lane lane)
    {
        double hexes = HexGrid.Distance(state.Ports[lane.PortAId].Hex,
                                        state.Ports[lane.PortBId].Hex);
        return hexes / Math.Max(1e-9,
            state.Config.Economy.FreightHexesPerYearBase
            * LaneMath.TransitSpeed(state, lane));
    }

    /// <summary>The route and its leg years: the shortest live-lane path
    /// when one exists (leg speed = FreightHexesPerYearBase × the lane's
    /// gate-tier TransitSpeed), else one off-lane crawl leg (spec §4b).</summary>
    public static (IReadOnlyList<int> LaneIds, IReadOnlyList<double> LegYears)
        PlanRoute(SimState state, int fromPortId, int toPortId)
    {
        var eco = state.Config.Economy;
        var (pathHexes, laneIds) = LaneNetwork.ShortestPath(
            state, fromPortId, toPortId);
        if (pathHexes < 0)
        {
            double hexes = HexGrid.Distance(state.Ports[fromPortId].Hex,
                                            state.Ports[toPortId].Hex);
            return (new int[0], new[]
                { hexes / Math.Max(1e-9, eco.OffLaneFreightHexesPerYear) });
        }
        var legYears = new double[laneIds.Count];
        for (int i = 0; i < laneIds.Count; i++)
            legYears[i] = LaneLegYears(state, state.Lanes[laneIds[i]]);
        return (laneIds, legYears);
    }

    /// <summary>Sail every in-flight shipment by the step's span, in id
    /// order (P6). A closed current leg — severed by blockade, quarantined,
    /// or dead (a wrecked gate) — stalls the freight where it floats: the
    /// fortress starves at the pace of its last delivery. Arrivals deliver
    /// and leave the registry.</summary>
    public static void Advance(SimState state, MarketStepScratch scratch)
    {
        if (state.Shipments.Count == 0) return;
        int span = state.Config.Sim.YearsPerEpoch;
        var hunters = HunterMap(state);
        List<int>? resolved = null;                       // indexes done
        for (int i = 0; i < state.Shipments.Count; i++)   // id order (P6)
            if (Sail(state, scratch, scratch.Severed, hunters,
                    state.Shipments[i], span) != SailOutcome.InTransit)
                (resolved ??= new List<int>()).Add(i);
        if (resolved != null)
            for (int i = resolved.Count - 1; i >= 0; i--)
                state.Shipments.RemoveAt(resolved[i]);
    }

    private enum SailOutcome { InTransit = 0, Arrived = 1, Lost = 2 }

    /// <summary>Hunted lanes: active raiding bands, first band by corp id
    /// claims a lane (P6). Lookup-only — iteration never touches it.</summary>
    private static Dictionary<int, Corporation>? HunterMap(SimState state)
    {
        Dictionary<int, Corporation>? hunters = null;
        foreach (var corp in state.Corporations)          // id order (P6)
            if (corp.Active && corp.Niche == CorporateNiche.Raiding
                && corp.TargetId >= 0)
            {
                hunters ??= new Dictionary<int, Corporation>();
                if (!hunters.ContainsKey(corp.TargetId))
                    hunters[corp.TargetId] = corp;
            }
        return hunters;
    }

    /// <summary>The one sailing rule (review fixes 1–2: dispatch and
    /// Advance share it): walk the legs for the span, stalling at a
    /// closed one; roll piracy (channel 75) once for the years sailed
    /// under a hunting band's guns — the loot lands at its haven, the
    /// band credited as supplier where a scratch exists (a plain deposit
    /// otherwise; Allocation-time dispatches have no pool to attribute);
    /// deliver on arrival. Returns what became of the cargo.</summary>
    private static SailOutcome Sail(SimState state,
        MarketStepScratch? scratch, HashSet<int> severed,
        Dictionary<int, Corporation>? hunters, Shipment s, double span)
    {
        double budget = span;
        double huntedYears = 0;
        Corporation? hunter = null;
        while (budget > 1e-9 && s.YearsInTransit < s.TotalYears - 1e-9)
        {
            int leg = CurrentLeg(s);
            if (leg < s.RouteLaneIds.Count)
            {
                var lane = state.Lanes[s.RouteLaneIds[leg]];
                if (severed.Contains(lane.Id)
                    || lane.QuarantinedUntil > state.WorldYear
                    || !LaneMath.IsLive(state, lane)) break;
            }
            double legEnd = 0;
            for (int i = 0; i <= leg; i++) legEnd += s.LegYears[i];
            double sail = Math.Min(budget, legEnd - s.YearsInTransit);
            // float dust at a leg boundary: snap forward, re-resolve
            if (sail <= 0) { s.YearsInTransit = legEnd; continue; }
            if (hunters != null && leg < s.RouteLaneIds.Count
                && hunters.TryGetValue(s.RouteLaneIds[leg], out var band))
            { huntedYears += sail; hunter ??= band; }
            s.YearsInTransit += sail;
            budget -= sail;
        }
        if (hunter != null && huntedYears > 0)
        {
            double p = 1.0 - Math.Pow(
                1.0 - state.Config.Corporate.ShipmentLossPerHuntedYear,
                huntedYears);
            if (EpochRolls.NextDouble(state.Config.MasterSeed,
                    Rng.RollChannel.ShipmentPiracy, state.EpochIndex,
                    s.OwnerActorId, s.Id) < p)
            {
                // taken: the loot lands at the haven (the fence pays the
                // pirates — conserved, P4)
                var havenMarket = state.Markets[hunter.HomePortId];
                for (int g = 0; g < s.Qty.Length; g++)
                {
                    if (s.Qty[g] <= 0) continue;
                    if (scratch != null)
                        MarketEngine.Deposit(state, scratch,
                            hunter.HomePortId, hunter.ActorId, g, s.Qty[g],
                            s.Grade[g]);
                    else
                        havenMarket.Deposit(g, s.Qty[g], s.Grade[g]);
                }
                return SailOutcome.Lost;
            }
        }
        if (s.YearsInTransit >= s.TotalYears - 1e-9)
        {
            Deliver(state, scratch, s);
            return SailOutcome.Arrived;
        }
        return SailOutcome.InTransit;
    }

    /// <summary>The requisition channel (spec §4b): for every in-flight
    /// project this polity funds, in (priority, plan order, id) order,
    /// compare the site's coverage — larder + shelf + inbound requisitions
    /// — against the basket over the step plus the lead window, and raise
    /// shipping orders from the polity's OTHER ports' stockpiles toward
    /// the shortfall. Bypasses price (no credits move — the state hauling
    /// its own goods); never bypasses time (Dispatch prices the route),
    /// route, or capacity (an order is capped at what the route's weakest
    /// lane carries over the window). Returns orders raised.</summary>
    public static int RaiseRequisitions(SimState state, PolityRecord pr)
    {
        int span = state.Config.Sim.YearsPerEpoch;
        double window = span + state.Config.Economy.RequisitionLeadYears;
        int raised = 0;
        var mine = new List<Project>();
        foreach (var p in state.Projects)                 // id order (P6)
            if (p.InFlight && p.FunderActorId == pr.ActorId
                && p.Kind != ProjectKind.ColonyExpedition) mine.Add(p);
        mine.Sort((x, y) =>
        {
            int c = x.Priority.CompareTo(y.Priority);
            if (c != 0) return c;
            c = x.PlanOrder.CompareTo(y.PlanOrder);
            return c != 0 ? c : x.Id.CompareTo(y.Id);
        });
        var want = new double[Substrate.Goods.All.Count];
        foreach (var p in mine)
        {
            double cover = Math.Min(p.YearsRequired - p.YearsDelivered,
                                    window);
            if (cover <= 0) continue;
            // a gate pair provisions both ends, half the basket each
            bool pair = p.Kind == ProjectKind.GatePair && p.TargetId >= 0;
            Span<int> ends = pair
                ? stackalloc int[2] { state.Lanes[p.TargetId].PortAId,
                                      state.Lanes[p.TargetId].PortBId }
                : stackalloc int[1] { p.PortId };
            double share = pair ? 0.5 : 1.0;
            foreach (int end in ends)
            {
                var site = state.Ports[end];
                // never ship into a larder the funder cannot draw from —
                // a site whose port fell to someone else gets the market
                // channel only (review fix 3)
                if (site.OwnerActorId != pr.ActorId) continue;
                var market = state.Markets[end];
                bool any = false;
                for (int g = 0; g < want.Length; g++)
                {
                    want[g] = 0;
                    if (p.PerYearBasket[g] <= 0) continue;
                    double need = share * p.PerYearBasket[g] * cover
                        - site.StockQty[g] - market.Inventory[g]
                        - Inbound(state, pr.ActorId, end, g);
                    if (need > 1e-6) { want[g] = need; any = true; }
                }
                if (!any) continue;
                raised += OrderFromOwnPorts(state, pr, end, want);
            }
        }
        // pre-positioning (spec §4b "Planner consequence"): due-soon plan
        // entries get their baskets shipped AHEAD of groundbreaking, so a
        // remote site opens with a stocked larder instead of starving
        // through its first lead time
        if (state.Actors[pr.ActorId].Policies is PolityPolicies policies)
        {
            var basket = new double[Substrate.Goods.All.Count];
            foreach (var entry in policies.Plan.Entries)  // plan order (P6)
            {
                double lead = entry.StartYear - state.WorldYear;
                if (lead < 0 || lead > window) continue;
                if (entry.PortId < 0 || entry.PortId >= state.Ports.Count
                    || state.Ports[entry.PortId].OwnerActorId != pr.ActorId)
                    continue;                             // review fix 3
                if (GroundBroken(state, pr.ActorId, entry)) continue;
                var (role, size) = entry.Kind == PlanEntryKind.HullBatch
                    && entry.TypeId >= 0 && entry.TypeId < state.Designs.Count
                    ? (state.Designs[entry.TypeId].Role,
                       state.Designs[entry.TypeId].Size)
                    : (ShipRole.Freight, ShipSize.Medium);
                double duration = Planner.EntryBasketPerYear(state.Config,
                    entry.Kind, entry.TypeId, entry.Count,
                    state.Ports[entry.PortId].Tier, role, size, basket);
                double cover = Math.Min(duration, window);
                var site = state.Ports[entry.PortId];
                var market = state.Markets[entry.PortId];
                bool any = false;
                for (int g = 0; g < want.Length; g++)
                {
                    want[g] = 0;
                    if (basket[g] <= 0) continue;
                    double need = basket[g] * cover - site.StockQty[g]
                        - market.Inventory[g]
                        - Inbound(state, pr.ActorId, entry.PortId, g);
                    if (need > 1e-6) { want[g] = need; any = true; }
                }
                if (!any) continue;
                raised += OrderFromOwnPorts(state, pr, entry.PortId, want);
            }
        }
        return raised;
    }

    /// <summary>True when the entry's work already broke ground at its
    /// port — the in-flight pass above covers it from here on.</summary>
    private static bool GroundBroken(SimState state, int funderActorId,
                                     PlanEntry entry)
    {
        foreach (var p in state.Projects)                 // id order (P6)
        {
            if (!p.InFlight || p.FunderActorId != funderActorId) continue;
            switch (entry.Kind)
            {
                case PlanEntryKind.Facility:
                    if (p.Kind == ProjectKind.FacilityConstruction
                        && p.PortId == entry.PortId
                        && p.TypeId == entry.TypeId) return true;
                    break;
                case PlanEntryKind.PortRaise:
                    if (p.Kind == ProjectKind.PortRaise
                        && p.TargetId == entry.PortId) return true;
                    break;
                case PlanEntryKind.HullBatch:
                    if (p.Kind == ProjectKind.HullBatch
                        && p.PortId == entry.PortId
                        && p.TypeId == entry.TypeId) return true;
                    break;
            }
        }
        return false;
    }

    /// <summary>Fill the site's want from own ports' stock, port-id order
    /// (P6; nearest-first sourcing is the contract economy's refinement).
    /// A source keeps its share of the standing stockpile target — those
    /// stores exist for fleets, sieges, and famines; requisitions move
    /// only the excess above policy.</summary>
    private static int OrderFromOwnPorts(SimState state, PolityRecord pr,
                                         int sitePortId, double[] want)
    {
        var targets = (state.Actors[pr.ActorId].Policies as PolityPolicies
                       ?? PolityPolicies.Default).StockpileTargets;
        int ownPorts = 0;
        foreach (var port in state.Ports)
            if (port.OwnerActorId == pr.ActorId) ownPorts++;
        int raised = 0;
        var basket = new List<(int Good, double Qty, double Grade)>();
        foreach (var src in state.Ports)                  // id order (P6)
        {
            if (src.OwnerActorId != pr.ActorId || src.Id == sitePortId)
                continue;
            basket.Clear();
            var (laneIds, legYears) = PlanRoute(state, src.Id, sitePortId);
            // capacity honesty: the order can't exceed what the route's
            // weakest lane carries over the provisioning window
            double window = state.Config.Sim.YearsPerEpoch
                            + state.Config.Economy.RequisitionLeadYears;
            double cap = double.MaxValue;
            foreach (var laneId in laneIds)
                cap = Math.Min(cap,
                    LaneMath.Capacity(state, state.Lanes[laneId]) * window);
            foreach (var g in OrderedGoods(want))
            {
                if (cap <= 1e-9) break;
                // consumption buffers stay home (fleets, sieges, famines);
                // construction materials were banked to be shipped — the
                // two purposes the controller's target comment names
                double keep = IsConsumptionStore(g)
                    && targets.TryGetValue(g, out double target)
                    ? target / Math.Max(1, ownPorts) : 0.0;
                double spare = src.StockQty[g] - keep;
                double qty = Math.Min(Math.Min(want[g], spare), cap);
                if (qty <= 1e-9) continue;
                double grade = src.StockGrade[g];
                basket.Add((g, src.DrawStock(g, qty), grade));
                want[g] -= qty;
                cap -= qty;
            }
            if (basket.Count == 0) continue;
            DispatchVia(state, pr.ActorId, ShipmentChannel.Requisition,
                src.Id, sitePortId, laneIds, legYears, basket);
            raised++;
        }
        return raised;
    }

    /// <summary>Stock held against consumption — the quartermaster's fleet
    /// stores and the famine/siege larder — as opposed to construction
    /// materials banked for the works. Requisitions never strip these
    /// below the port's target share (draining them wrecked every navy).</summary>
    private static bool IsConsumptionStore(int good) =>
        good == (int)Substrate.GoodId.Provisions
        || good == (int)Substrate.GoodId.Fuel
        || good == (int)Substrate.GoodId.ShipComponents
        || good == (int)Substrate.GoodId.Armaments;

    private static IEnumerable<int> OrderedGoods(double[] want)
    {
        for (int g = 0; g < want.Length; g++)
            if (want[g] > 1e-9) yield return g;
    }

    /// <summary>Requisition cargo already sailing toward the port for this
    /// owner — counted so replans don't double-order.</summary>
    public static double Inbound(SimState state, int ownerActorId,
                                 int destPortId, int good)
    {
        double sum = 0;
        foreach (var s in state.Shipments)                // id order (P6)
            if (s.Channel == ShipmentChannel.Requisition
                && s.OwnerActorId == ownerActorId
                && s.DestPortId == destPortId)
                sum += s.Qty[good];
        return sum;
    }

    /// <summary>Index of the leg the shipment is currently sailing.</summary>
    public static int CurrentLeg(Shipment s)
    {
        double sofar = 0;
        for (int i = 0; i < s.LegYears.Count; i++)
        {
            sofar += s.LegYears[i];
            if (s.YearsInTransit < sofar - 1e-9) return i;
        }
        return s.LegYears.Count - 1;
    }

    /// <summary>Land the cargo: a requisition banks into the destination
    /// port's stockpile (capacity overflow spills onto the market shelf —
    /// goods conserve); freight deposits into the destination market with
    /// the owner credited as its supplier (paid at distribution, the
    /// Arbitrage convention). Without a scratch (a mid-phase dispatch has
    /// one; unit paths may not) freight falls back to a plain deposit.</summary>
    private static void Deliver(SimState state, MarketStepScratch? scratch,
                                Shipment s)
    {
        var port = state.Ports[s.DestPortId];
        var market = state.Markets[s.DestPortId];
        for (int g = 0; g < s.Qty.Length; g++)
        {
            if (s.Qty[g] <= 0) continue;
            if (s.Channel == ShipmentChannel.Requisition)
            {
                double cap = MarketEngine.StockCapacityAt(state, port);
                double room = Math.Max(0, cap - port.StockQty[g]);
                double banked = Math.Min(room, s.Qty[g]);
                port.DepositStock(g, banked, s.Grade[g]);
                if (s.Qty[g] - banked > 0)
                    market.Deposit(g, s.Qty[g] - banked, s.Grade[g]);
            }
            else if (scratch != null)
                MarketEngine.Deposit(state, scratch, s.DestPortId,
                    s.OwnerActorId, g, s.Qty[g], s.Grade[g]);
            else
                market.Deposit(g, s.Qty[g], s.Grade[g]);
        }
    }
}
