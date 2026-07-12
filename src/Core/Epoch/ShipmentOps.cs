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
        => Dispatch(state, ownerActorId, channel, fromPortId, toPortId,
                    basket, scratch, out _);

    /// <summary>Dispatch reporting what became of a sub-step resolution —
    /// couriers need delivered vs pirated (slice CE).</summary>
    internal static Shipment? Dispatch(SimState state, int ownerActorId,
        ShipmentChannel channel, int fromPortId, int toPortId,
        IReadOnlyList<(int Good, double Qty, double Grade)> basket,
        MarketStepScratch? scratch, out SailOutcome outcome)
    {
        var (laneIds, legYears) = PlanRoute(state, fromPortId, toPortId);
        var s = new Shipment(state.NextShipmentId++, ownerActorId, channel,
            fromPortId, toPortId, state.WorldYear, laneIds, legYears);
        Fill(s, basket);
        var severed = scratch?.Severed ?? FleetOps.SeveredLaneIds(state);
        outcome = Sail(state, scratch, severed, HunterMap(state),
                       WarPresenceMap(state), s,
                       state.Config.Sim.YearsPerEpoch);
        if (outcome != SailOutcome.InTransit)
            return null;                     // delivered or taken this step
        state.Shipments.Add(s);
        return s;
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
        if (Sail(state, scratch, severed, HunterMap(state),
                WarPresenceMap(state), s,
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
        var presence = WarPresenceMap(state);
        List<int>? resolved = null;                       // indexes done
        for (int i = 0; i < state.Shipments.Count; i++)   // id order (P6)
        {
            var outcome = Sail(state, scratch, scratch.Severed, hunters,
                               presence, state.Shipments[i], span);
            if (outcome == SailOutcome.InTransit) continue;
            (resolved ??= new List<int>()).Add(i);
            // a courier's cargo resolved with its shipment: delivery pays
            // the fee, a loss refunds it (slice CE)
            var courier = CourierOps.OfShipment(state, state.Shipments[i].Id);
            if (courier != null) CourierOps.Resolve(state, courier, outcome);
        }
        if (resolved != null)
            for (int i = resolved.Count - 1; i >= 0; i--)
                state.Shipments.RemoveAt(resolved[i]);
    }

    internal enum SailOutcome { InTransit = 0, Arrived = 1, Lost = 2 }

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

    /// <summary>Warships bearing on each lane: war-stationed squadrons
    /// (Blockade, Expedition) within InterdictionReachHexes of either
    /// endpoint, plus Escort fleets riding the lane itself — the presence
    /// that contests an enemy's legs and screens a friend's (contract-
    /// economy spec §4). Fleet-id order per lane (P6). Lookup-only.</summary>
    private static Dictionary<int, List<(FleetRecord Fleet, int Warships)>>?
        WarPresenceMap(SimState state)
    {
        // no active war, no contested legs and no screens worth pricing —
        // skip the fleets × lanes sweep entirely (review wave, finding 8)
        bool anyWar = false;
        foreach (var w in state.Wars)                     // id order (P6)
            if (w.Active) { anyWar = true; break; }
        if (!anyWar) return null;
        Dictionary<int, List<(FleetRecord, int)>>? map = null;
        int reach = state.Config.War.InterdictionReachHexes;
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            bool stationed = fleet.Posture is FleetPosture.Blockade
                or FleetPosture.Expedition;
            bool riding = fleet.Posture == FleetPosture.Escort
                && fleet.TargetId >= 0;
            if (!stationed && !riding) continue;
            int warships = 0;
            foreach (var g in fleet.Hulls)                // design-id order
                if (ShipCatalog.IsWarship(state.Designs[g.DesignId].Role))
                    warships += g.Count;
            if (warships == 0) continue;
            if (riding)
            {
                map ??= new Dictionary<int, List<(FleetRecord, int)>>();
                (map.TryGetValue(fleet.TargetId, out var lst)
                    ? lst
                    : map[fleet.TargetId] = new List<(FleetRecord, int)>())
                    .Add((fleet, warships));
                continue;
            }
            foreach (var lane in state.Lanes)             // id order (P6)
            {
                if (Math.Min(
                        HexGrid.Distance(fleet.Hex,
                            state.Ports[lane.PortAId].Hex),
                        HexGrid.Distance(fleet.Hex,
                            state.Ports[lane.PortBId].Hex)) > reach)
                    continue;
                map ??= new Dictionary<int, List<(FleetRecord, int)>>();
                (map.TryGetValue(lane.Id, out var lst)
                    ? lst
                    : map[lane.Id] = new List<(FleetRecord, int)>())
                    .Add((fleet, warships));
            }
        }
        return map;
    }

    /// <summary>The one sailing rule (review fixes 1–2: dispatch and
    /// Advance share it): walk the legs for the span, stalling at a
    /// closed one; roll piracy (channel 75) once for the years sailed
    /// under a hunting band's guns — the loot lands at its haven, the
    /// band credited as supplier where a scratch exists (a plain deposit
    /// otherwise; Allocation-time dispatches have no pool to attribute);
    /// roll war interdiction (channel 76) once for the years sailed on
    /// legs contested by an enemy of the owner, friendly escorts damping
    /// the odds deterministically; deliver on arrival. Returns what
    /// became of the cargo.</summary>
    private static SailOutcome Sail(SimState state,
        MarketStepScratch? scratch, HashSet<int> severed,
        Dictionary<int, Corporation>? hunters,
        Dictionary<int, List<(FleetRecord Fleet, int Warships)>>? presence,
        Shipment s, double span)
    {
        double budget = span;
        double huntedYears = 0;
        Corporation? hunter = null;
        double contestedYears = 0;
        FleetRecord? interdictor = null;
        int escortHulls = 0;
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
            if (presence != null && leg < s.RouteLaneIds.Count
                && presence.TryGetValue(s.RouteLaneIds[leg], out var squadrons))
            {
                bool contested = false;
                int friendly = 0;
                foreach (var (fleet, warships) in squadrons) // fleet-id order
                {
                    if (fleet.OwnerActorId == s.OwnerActorId)
                        friendly += warships;
                    else if (WarOps.ActiveWarBetween(state,
                                 fleet.OwnerActorId, s.OwnerActorId) != null)
                    { contested = true; interdictor ??= fleet; }
                }
                if (contested)
                {
                    contestedYears += sail;
                    escortHulls = Math.Max(escortHulls, friendly);
                }
            }
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
                // taken: the loot goes up for sale at the haven as the
                // band's own asks — the fence pays the pirates at the
                // fill, conserved (P4; the old scratch-less plain-deposit
                // seam is closed — the book needs no attribution pool)
                for (int g = 0; g < s.Qty.Length; g++)
                    if (s.Qty[g] > 0)
                        BookOps.PostSupply(state, hunter.HomePortId,
                            hunter.ActorId, g, s.Qty[g], s.Grade[g]);
                return SailOutcome.Lost;
            }
        }
        // war interdiction (contract-economy spec §4), rolled after piracy
        // took its chance: the seizure probability compounds per contested
        // world-year, friendly warships screening the leg damp it as a
        // deterministic modifier — never a second roll. The prize lands at
        // the interdictor's nearest own port as its asks (P4 conserved); a
        // portless interdictor has nowhere to land one and takes nothing.
        if (interdictor != null && contestedYears > 0)
        {
            int prizePort = FleetOps.NearestOwnedPortId(state,
                interdictor.OwnerActorId, interdictor.Hex);
            var war = state.Config.War;
            double p = (1.0 - Math.Pow(
                    1.0 - war.InterdictionLossPerContestedYear,
                    contestedYears))
                / (1.0 + war.EscortDampPerHull * escortHulls);
            if (prizePort >= 0
                && EpochRolls.NextDouble(state.Config.MasterSeed,
                    Rng.RollChannel.ShipmentInterdiction, state.EpochIndex,
                    s.OwnerActorId, s.Id) < p)
            {
                double units = 0;
                for (int g = 0; g < s.Qty.Length; g++)
                {
                    if (s.Qty[g] <= 0) continue;
                    units += s.Qty[g];
                    BookOps.PostSupply(state, prizePort,
                        interdictor.OwnerActorId, g, s.Qty[g], s.Grade[g]);
                }
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.CargoSeized,
                    new[] { interdictor.OwnerActorId, s.OwnerActorId },
                    interdictor.Hex, Magnitude: units, Valence: -0.6,
                    EventVisibility.Regional,
                    new CargoSeizedPayload(s.Id, interdictor.OwnerActorId,
                                           units)));
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
                bool any = false;
                for (int g = 0; g < want.Length; g++)
                {
                    want[g] = 0;
                    if (p.PerYearBasket[g] <= 0) continue;
                    // coverage = larder + the works' laydown yard + inbound
                    // (the anonymous shelf is gone — bid fills land in the
                    // yard, requisitions in the larder)
                    double need = share * p.PerYearBasket[g] * cover
                        - site.StockQty[g] - share * p.DeliveredQty[g]
                        - Inbound(state, pr.ActorId, end, g);
                    if (need > 1e-6) { want[g] = need; any = true; }
                }
                if (!any) continue;
                raised += OrderFromOwnPorts(state, pr, end, want,
                    p.Priority == ProjectPriority.War
                        ? CourierPriority.War : CourierPriority.Normal);
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
                bool any = false;
                for (int g = 0; g < want.Length; g++)
                {
                    want[g] = 0;
                    if (basket[g] <= 0) continue;
                    double need = basket[g] * cover - site.StockQty[g]
                        - Inbound(state, pr.ActorId, entry.PortId, g);
                    if (need > 1e-6) { want[g] = need; any = true; }
                }
                if (!any) continue;
                raised += OrderFromOwnPorts(state, pr, entry.PortId, want,
                                            CourierPriority.Normal);
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

    /// <summary>Fill the site's want from own ports' stock as POSTED
    /// COURIER CONTRACTS (contract economy, spec §3): sources rank by
    /// DELIVERED TIME (route transit years, then port id) — nearest-first
    /// replaced the old port-id order — and the state's hauling now costs
    /// freight fees paid to whoever's hulls take the job. A source keeps
    /// its share of the standing stockpile target — those stores exist for
    /// fleets, sieges, and famines; requisitions move only the excess
    /// above policy.</summary>
    private static int OrderFromOwnPorts(SimState state, PolityRecord pr,
        int sitePortId, double[] want, CourierPriority priority)
    {
        var eco = state.Config.Economy;
        var targets = (state.Actors[pr.ActorId].Policies as PolityPolicies
                       ?? PolityPolicies.Default).StockpileTargets;
        int ownPorts = 0;
        foreach (var port in state.Ports)
            if (port.OwnerActorId == pr.ActorId) ownPorts++;
        // delivered-time sourcing: transit years ascending, port id tiebreak
        var sources = new List<(Port Port, double Years,
                                IReadOnlyList<int> LaneIds)>();
        foreach (var src in state.Ports)                  // id order (P6)
        {
            if (src.OwnerActorId != pr.ActorId || src.Id == sitePortId)
                continue;
            var (laneIds, legYears) = PlanRoute(state, src.Id, sitePortId);
            double years = 0;
            foreach (var y in legYears) years += y;
            sources.Add((src, years, laneIds));
        }
        sources.Sort((x, y) =>
        {
            int c = x.Years.CompareTo(y.Years);
            return c != 0 ? c : x.Port.Id.CompareTo(y.Port.Id);
        });
        int raised = 0;
        var basket = new List<(int Good, double Qty)>();
        foreach (var (src, _, laneIds) in sources)
        {
            basket.Clear();
            // capacity honesty: the order can't exceed what the route's
            // weakest lane carries over the provisioning window
            double window = state.Config.Sim.YearsPerEpoch
                            + eco.RequisitionLeadYears;
            double cap = double.MaxValue;
            foreach (var laneId in laneIds)
                cap = Math.Min(cap,
                    LaneMath.Capacity(state, state.Lanes[laneId]) * window);
            double units = 0;
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
                basket.Add((g, qty));
                want[g] -= qty;
                cap -= qty;
                units += qty;
            }
            if (basket.Count == 0) continue;
            // the state's hauling costs freight rates now: the fee prices
            // the whole route's distance and pays whoever takes the job
            double fee = units * eco.CourierFeePerUnitPerHex
                * HexGrid.Distance(src.Hex, state.Ports[sitePortId].Hex);
            if (CourierOps.Post(state, pr.ActorId, src.Id, sitePortId,
                                basket, fee, priority) != null)
                raised++;
        }
        return raised;
    }

    /// <summary>The war quartermaster (contract-economy spec §4): for every
    /// war-stationed fleet, forecast its upkeep burn over the step plus the
    /// lead window against its forward depot's stores, and raise
    /// War-priority couriers from the polity's rear stockpiles toward the
    /// shortfall. Convoys are ordinary shipments — map-visible,
    /// blockade-stalled, pirate-hunted, interdictable. Returns orders
    /// raised. Depots in port-id order (P6).</summary>
    public static int StockDepots(SimState state, PolityRecord pr)
    {
        double window = state.Config.Sim.YearsPerEpoch
                        + state.Config.Economy.RequisitionLeadYears;
        bool atWar = WarOps.AtWar(state, pr.ActorId);
        // deployed consumption aggregates per depot port
        var needByDepot = new SortedDictionary<int, double[]>();
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.OwnerActorId != pr.ActorId || fleet.TotalHulls == 0
                || fleet.Posture is not (FleetPosture.Blockade
                                         or FleetPosture.Expedition))
                continue;
            int depot = FleetOps.NearestOwnedPortId(state, pr.ActorId,
                                                    fleet.Hex);
            if (depot < 0) continue;
            var (fuel, arms, parts, rations) =
                FleetOps.UpkeepNeed(state, fleet, atWar, window);
            if (!needByDepot.TryGetValue(depot, out var need))
                needByDepot[depot] = need
                    = new double[Substrate.Goods.All.Count];
            need[(int)Substrate.GoodId.Fuel] += fuel;
            need[(int)Substrate.GoodId.Armaments] += arms;
            need[(int)Substrate.GoodId.ShipComponents] += parts;
            need[(int)Substrate.GoodId.Provisions] += rations;
        }
        int raised = 0;
        var want = new double[Substrate.Goods.All.Count];
        foreach (var (depot, need) in needByDepot)        // port-id order
        {
            var site = state.Ports[depot];
            // the warehouse bounds the ambition (review wave, finding 6):
            // arrivals over capacity re-post as book asks, so an uncapped
            // forecast reorders forever — and the book itself is coverage
            // (the fleet draw lifts asks before it touches the larder)
            double cap = MarketEngine.StockCapacityAt(state, site);
            bool any = false;
            for (int g = 0; g < want.Length; g++)
            {
                want[g] = 0;
                if (need[g] <= 0) continue;
                double shortfall = Math.Min(need[g], cap) - site.StockQty[g]
                                   - BookOps.AskQty(state, depot, g)
                                   - Inbound(state, pr.ActorId, depot, g);
                if (shortfall > 1e-6) { want[g] = shortfall; any = true; }
            }
            if (!any) continue;
            raised += OrderFromOwnPorts(state, pr, depot, want,
                                        CourierPriority.War);
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
    /// owner — plus cargo escrowed on still-open couriers headed there —
    /// counted so replans don't double-order.</summary>
    public static double Inbound(SimState state, int ownerActorId,
                                 int destPortId, int good)
    {
        double sum = 0;
        foreach (var s in state.Shipments)                // id order (P6)
            if (s.Channel == ShipmentChannel.Requisition
                && s.OwnerActorId == ownerActorId
                && s.DestPortId == destPortId)
                sum += s.Qty[good];
        foreach (var c in state.Couriers)                 // id order (P6)
            if (c.Status == CourierStatus.Open
                && c.PosterActorId == ownerActorId
                && c.DestPortId == destPortId)
                sum += c.Qty[good];
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
    /// port's stockpile (capacity overflow posts as the owner's sell
    /// orders — goods conserve); freight posts on the destination book as
    /// the owner's asks and sells into whatever bids exist (the spread-run
    /// rule — no reservation, contract-economy spec §2).</summary>
    private static void Deliver(SimState state, MarketStepScratch? scratch,
                                Shipment s)
    {
        var port = state.Ports[s.DestPortId];
        // a requisition into a port that FELL in transit must not resupply
        // the captor's larder (review wave, finding 9 — the review-fix-3
        // rule at post time, re-checked at the dock): the cargo posts as
        // the owner's asks instead — goods conserve, to the right flag
        bool ownLarder = port.OwnerActorId == s.OwnerActorId;
        for (int g = 0; g < s.Qty.Length; g++)
        {
            if (s.Qty[g] <= 0) continue;
            if (s.Channel == ShipmentChannel.Requisition && ownLarder)
            {
                double cap = MarketEngine.StockCapacityAt(state, port);
                double room = Math.Max(0, cap - port.StockQty[g]);
                double banked = Math.Min(room, s.Qty[g]);
                port.DepositStock(g, banked, s.Grade[g]);
                if (s.Qty[g] - banked > 0)
                    BookOps.PostSupply(state, s.DestPortId, s.OwnerActorId,
                        g, s.Qty[g] - banked, s.Grade[g]);
            }
            else
                BookOps.PostSupply(state, s.DestPortId, s.OwnerActorId,
                    g, s.Qty[g], s.Grade[g]);
        }
    }
}
