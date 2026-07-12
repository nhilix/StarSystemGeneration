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
    /// <summary>Route a basket from port to port. Transit inside the
    /// current step's span is sub-step blur: delivered immediately, no
    /// record, null return. Otherwise the goods exist only in the returned
    /// shipment until arrival; departure sails the dispatching step's span.
    /// The basket is already drawn — the caller owns conservation up to
    /// this call.</summary>
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
        double total = 0;
        foreach (var y in legYears) total += y;
        int span = state.Config.Sim.YearsPerEpoch;
        if (total <= span)
        {
            var direct = new Shipment(-1, ownerActorId, channel, fromPortId,
                toPortId, state.WorldYear, laneIds, legYears);
            Fill(direct, basket);
            Deliver(state, scratch, direct);
            return null;
        }
        var s = new Shipment(state.NextShipmentId++, ownerActorId, channel,
            fromPortId, toPortId, state.WorldYear, laneIds, legYears)
        { YearsInTransit = span };
        Fill(s, basket);
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
        // hunted lanes: active raiding bands, first band by id claims (P6)
        Dictionary<int, Corporation>? hunters = null;
        foreach (var corp in state.Corporations)          // id order (P6)
            if (corp.Active && corp.Niche == CorporateNiche.Raiding
                && corp.TargetId >= 0)
            {
                hunters ??= new Dictionary<int, Corporation>();
                if (!hunters.ContainsKey(corp.TargetId))
                    hunters[corp.TargetId] = corp;
            }
        HashSet<int>? lost = null;
        foreach (var s in state.Shipments)                // id order (P6)
        {
            double budget = span;
            double huntedYears = 0;
            Corporation? hunter = null;
            while (budget > 1e-9 && s.YearsInTransit < s.TotalYears - 1e-9)
            {
                int leg = CurrentLeg(s);
                if (leg < s.RouteLaneIds.Count
                    && LegClosed(state, scratch, s.RouteLaneIds[leg])) break;
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
            if (hunter == null || huntedYears <= 0) continue;
            // one roll per step (channel 75): exposure scales with the
            // years actually sailed under the band's guns
            double p = 1.0 - Math.Pow(
                1.0 - state.Config.Corporate.ShipmentLossPerHuntedYear,
                huntedYears);
            if (EpochRolls.NextDouble(state.Config.MasterSeed,
                    Rng.RollChannel.ShipmentPiracy, state.EpochIndex,
                    s.OwnerActorId, s.Id) >= p) continue;
            // taken: the loot lands at the haven, the band is its supplier
            // (the fence pays the pirates — conserved, P4)
            for (int g = 0; g < s.Qty.Length; g++)
                if (s.Qty[g] > 0)
                    MarketEngine.Deposit(state, scratch, hunter.HomePortId,
                        hunter.ActorId, g, s.Qty[g], s.Grade[g]);
            (lost ??= new HashSet<int>()).Add(s.Id);
        }
        for (int i = state.Shipments.Count - 1; i >= 0; i--)
        {
            var s = state.Shipments[i];
            if (lost != null && lost.Contains(s.Id))
            { state.Shipments.RemoveAt(i); continue; }
            if (s.YearsInTransit < s.TotalYears - 1e-9) continue;
            Deliver(state, scratch, s);
            state.Shipments.RemoveAt(i);
        }
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
        if (mine.Count == 0) return 0;
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
        return raised;
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

    private static bool LegClosed(SimState state, MarketStepScratch scratch,
                                  int laneId)
    {
        var lane = state.Lanes[laneId];
        return scratch.Severed.Contains(laneId)
            || lane.QuarantinedUntil > state.WorldYear
            || !LaneMath.IsLive(state, lane);
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
