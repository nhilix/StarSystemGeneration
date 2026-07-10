using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>Fleet-state operations over SimState: yard production, registry
/// lookups; posture management and supply land here through the slice.
/// Iteration is fleet-id then design-id order everywhere (P6).</summary>
public static class FleetOps
{
    /// <summary>Yard production (fleets/ships-and-fleets.md §Production):
    /// each port with active shipyards converts market Ship Components
    /// (+ Armaments for warships) into hulls of the polity's current
    /// designs, throughput from yard tiers, queue split by the standing
    /// ShipbuildingPriorities (D'Hondt over the weights — deterministic and
    /// proportional). The military treasury pays administered prices as
    /// yard wages (the treasury-spending convention: goods drawn, credits
    /// recycled to the building port's households). Returns hulls laid.</summary>
    public static int BuildFleets(SimState state, PolityRecord pr,
                                  List<Port> ownPorts)
    {
        var cfg = state.Config;
        int years = cfg.Sim.YearsPerEpoch;
        var priorities = (state.Actors[pr.ActorId].Policies as PolityPolicies
                          ?? PolityPolicies.Default).ShipbuildingPriorities;
        int laid = 0;
        foreach (var port in ownPorts)                    // id order (P6)
        {
            double throughput = 0;
            foreach (var f in state.Facilities)           // id order (P6)
            {
                if (f.OwnerActorId != pr.ActorId
                    || f.TypeId != (int)InfraTypeId.Shipyard
                    || !MarketEngine.IsActive(state, f)
                    || MarketEngine.AttachedMarketIndex(state, f) != port.Id)
                    continue;
                throughput += f.Tier * cfg.Fleet.YardHullsPerTierPerYear
                              * years * f.Condition;
            }
            int slots = (int)throughput;
            if (slots <= 0) continue;
            var market = state.Markets[port.Id];

            // the components at hand may out-grade the design — lineage
            // drift happens at the yard, before this epoch's hulls lay down
            double gradeAtHand = market.Inventory[(int)GoodId.ShipComponents] > 0
                ? market.InventoryGrade[(int)GoodId.ShipComponents] : 0.0;
            var queue = BuildQueue(state, pr.ActorId, priorities);
            if (queue.Count == 0) continue;
            if (gradeAtHand > 0)
                for (int i = 0; i < queue.Count; i++)
                {
                    var advanced = DesignRegistry.MaybeAdvanceMark(state,
                        queue[i].Design, gradeAtHand, port.Hex);
                    if (!ReferenceEquals(advanced, queue[i].Design))
                        queue[i] = (advanced, queue[i].Weight, 0);
                }

            // D'Hondt: each slot goes to the design with the highest
            // weight / (hulls already granted + 1) — ties to lower id
            for (int slot = 0; slot < slots; slot++)
            {
                int pick = -1;
                double best = 0;
                for (int i = 0; i < queue.Count; i++)
                {
                    double claim = queue[i].Weight / (queue[i].Granted + 1);
                    if (claim > best) { best = claim; pick = i; }
                }
                if (pick < 0) break;
                var design = queue[pick].Design;
                double components = DesignMath.ComponentsPerHull(cfg.Fleet,
                                                                 design.Size);
                double armaments = DesignMath.ArmamentsPerHull(cfg.Fleet,
                                        design.Role, design.Size);
                double value = components
                    * Market.InitialPrice(cfg.Economy, GoodId.ShipComponents)
                    + armaments
                    * Market.InitialPrice(cfg.Economy, GoodId.Armaments);
                if (market.Inventory[(int)GoodId.ShipComponents] < components
                    || market.Inventory[(int)GoodId.Armaments] < armaments
                    || pr.MilitaryPoints < value)
                {
                    // this design can't lay down; drop it and let the
                    // remaining queue claim the slot
                    queue.RemoveAt(pick);
                    if (queue.Count == 0) break;
                    slot--;
                    continue;
                }
                double hullGrade = market.InventoryGrade[(int)GoodId.ShipComponents];
                market.LastCleared[(int)GoodId.ShipComponents] +=
                    market.Draw((int)GoodId.ShipComponents, components);
                if (armaments > 0)
                    market.LastCleared[(int)GoodId.Armaments] +=
                        market.Draw((int)GoodId.Armaments, armaments);
                pr.MilitaryPoints -= value;
                MarketEngine.PayWages(state, port.Id, value);   // yard wages
                HomeFleet(state, pr.ActorId, port).AddHulls(design.Id, 1, hullGrade);
                pr.HullsBuilt++;
                queue[pick] = (design, queue[pick].Weight, queue[pick].Granted + 1);
                laid++;
            }
        }
        return laid;
    }

    /// <summary>The yard's queue: current-mark designs with their standing
    /// weights. An empty priorities policy falls back to freight only —
    /// hulls someone always wants.</summary>
    private static List<(ShipDesign Design, double Weight, int Granted)>
        BuildQueue(SimState state, int actorId,
                   IReadOnlyDictionary<int, double> priorities)
    {
        var queue = new List<(ShipDesign, double, int)>();
        var seen = new HashSet<(ShipRole, ShipSize)>();
        foreach (var d in state.Designs)                  // id order (P6)
        {
            if (d.OwnerActorId != actorId || !seen.Add((d.Role, d.Size))) continue;
            var current = DesignRegistry.Current(state, actorId, d.Role, d.Size)!;
            double weight = priorities.TryGetValue(current.Id, out double w)
                ? w
                : priorities.Count == 0 && current.Role == ShipRole.Freight
                    ? 1.0 : 0.0;
            if (weight > 0) queue.Add((current, weight, 0));
        }
        return queue;
    }

    /// <summary>The polity's Reserve fleet docked at this port — founded on
    /// first use (new hulls join the home reserve until postures assign them).</summary>
    public static FleetRecord HomeFleet(SimState state, int actorId, Port port)
    {
        foreach (var fleet in state.Fleets)               // id order (P6)
            if (fleet.OwnerActorId == actorId
                && fleet.Posture == FleetPosture.Reserve
                && fleet.HomePortId == port.Id)
                return fleet;
        var founded = new FleetRecord(state.Fleets.Count, actorId, port.Hex)
        {
            HomePortId = port.Id,
        };
        state.Fleets.Add(founded);
        return founded;
    }

    /// <summary>The homeworld starter fleet — genesis furniture beside the
    /// starter industry: a spacefaring species arrives with haulers, one
    /// colony convoy's worth of hulls, a scout, and escorts by militancy.
    /// Counted as built (the hull ledger conserves from genesis).</summary>
    public static void SeedStarterFleet(SimState state, int actorId, Port port,
                                        double militancy)
    {
        var knobs = state.Config.Fleet;
        var fleet = HomeFleet(state, actorId, port);
        const double grade = 0.5;   // standard-issue, like the entry designs
        AddStarter(state, fleet, actorId, ShipRole.Freight, ShipSize.Medium,
                   knobs.StarterFreightHulls, grade);
        AddStarter(state, fleet, actorId, ShipRole.Colony, ShipSize.Medium,
                   knobs.StarterColonyHulls, grade);
        AddStarter(state, fleet, actorId, ShipRole.Scout, ShipSize.Light, 1, grade);
        AddStarter(state, fleet, actorId, ShipRole.Escort, ShipSize.Light,
                   (int)Math.Round(militancy * knobs.StarterEscortPerMilitancy),
                   grade);
    }

    private static void AddStarter(SimState state, FleetRecord fleet, int actorId,
                                   ShipRole role, ShipSize size, int count,
                                   double grade)
    {
        if (count <= 0) return;
        var design = DesignRegistry.Current(state, actorId, role, size);
        if (design == null) return;                       // meek species skip escorts
        fleet.AddHulls(design.Id, count, grade);
        state.PolityOf(actorId).HullsBuilt += count;
    }
    /// <summary>Standing-policy posture management, run by Allocation after
    /// the yards: freight hulls rebalance across the polity's lanes as
    /// Posted fleets (D'Hondt over lane throughput — busy corridors get the
    /// hulls), escorts consolidate into a Patrol fleet at the capital
    /// (legality-enforcement data for the black books, H consumes it).
    /// Line, carrier, scout, and colony hulls hold their reserve stations —
    /// convoys and war assemble them later. Deterministic: lanes, fleets,
    /// and designs in id order (P6).</summary>
    public static void ManagePostures(SimState state, PolityRecord pr,
                                      List<Port> ownPorts)
    {
        if (ownPorts.Count == 0) return;
        int actor = pr.ActorId;
        var capital = ownPorts[0];

        var freightPool = PoolHulls(state, actor, ShipRole.Freight);
        var lanes = new List<Lane>();
        foreach (var lane in state.Lanes)                 // id order (P6)
            if (state.Ports[lane.PortAId].OwnerActorId == actor
                || state.Ports[lane.PortBId].OwnerActorId == actor)
                lanes.Add(lane);
        int totalFreight = 0;
        foreach (var g in freightPool) totalFreight += g.Count;
        if (lanes.Count > 0 && totalFreight > 0)
        {
            // D'Hondt over lane throughput: slots to the lane with the
            // highest weight / (hulls granted + 1), ties to lower lane id
            var granted = new int[lanes.Count];
            var weights = new double[lanes.Count];
            for (int i = 0; i < lanes.Count; i++)
                weights[i] = LaneMath.Capacity(state.Ports[lanes[i].PortAId],
                                               state.Ports[lanes[i].PortBId]);
            for (int slot = 0; slot < totalFreight; slot++)
            {
                int pick = 0;
                double best = 0;
                for (int i = 0; i < lanes.Count; i++)
                {
                    double claim = weights[i] / (granted[i] + 1);
                    if (claim > best) { best = claim; pick = i; }
                }
                granted[pick]++;
            }
            for (int i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                var fleet = PostureFleet(state, actor, FleetPosture.Posted,
                                         lane.Id);
                var home = state.Ports[lane.PortAId].OwnerActorId == actor
                    ? state.Ports[lane.PortAId] : state.Ports[lane.PortBId];
                fleet.HomePortId = home.Id;
                fleet.Hex = home.Hex;
                DealHulls(freightPool, fleet, granted[i]);
            }
        }

        var escortPool = PoolHulls(state, actor, ShipRole.Escort);
        int escorts = 0;
        foreach (var g in escortPool) escorts += g.Count;
        if (escorts > 0)
        {
            var patrol = PostureFleet(state, actor, FleetPosture.Patrol,
                                      capital.Id);
            patrol.HomePortId = capital.Id;
            patrol.Hex = capital.Hex;
            DealHulls(escortPool, patrol, escorts);
        }

        // whatever the deal left over docks at the capital reserve
        var leftovers = HomeFleet(state, actor, capital);
        foreach (var g in freightPool)
            leftovers.AddHulls(g.DesignId, g.Count, g.Grade);
        foreach (var g in escortPool)
            leftovers.AddHulls(g.DesignId, g.Count, g.Grade);
    }

    /// <summary>Strip every hull of one role from the actor's fleets into a
    /// virtual pool (grades blend per design) — the rebalancing source.</summary>
    private static List<HullGroup> PoolHulls(SimState state, int actorId,
                                             ShipRole role)
    {
        var pool = new List<HullGroup>();
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.OwnerActorId != actorId) continue;
            for (int i = fleet.Hulls.Count - 1; i >= 0; i--)
            {
                var g = fleet.Hulls[i];
                if (state.Designs[g.DesignId].Role != role) continue;
                Blend(pool, g.DesignId, g.Count, g.Grade);
                fleet.Hulls.RemoveAt(i);
            }
        }
        pool.Sort((x, y) => x.DesignId.CompareTo(y.DesignId));
        return pool;
    }

    private static void Blend(List<HullGroup> pool, int designId, int count,
                              double grade)
    {
        foreach (var g in pool)
            if (g.DesignId == designId)
            {
                g.Grade = (g.Count * g.Grade + count * grade) / (g.Count + count);
                g.Count += count;
                return;
            }
        pool.Add(new HullGroup(designId, count, grade));
    }

    /// <summary>Move up to <paramref name="count"/> hulls from the pool into
    /// a fleet, design-id order.</summary>
    private static void DealHulls(List<HullGroup> pool, FleetRecord fleet,
                                  int count)
    {
        foreach (var g in pool)
        {
            if (count <= 0) return;
            int take = Math.Min(g.Count, count);
            if (take <= 0) continue;
            fleet.AddHulls(g.DesignId, take, g.Grade);
            g.Count -= take;
            count -= take;
        }
    }

    /// <summary>The actor's fleet standing at one posture/target, founded on
    /// first assignment.</summary>
    public static FleetRecord PostureFleet(SimState state, int actorId,
                                           FleetPosture posture, int targetId)
    {
        foreach (var fleet in state.Fleets)               // id order (P6)
            if (fleet.OwnerActorId == actorId && fleet.Posture == posture
                && fleet.TargetId == targetId)
                return fleet;
        var founded = new FleetRecord(state.Fleets.Count, actorId,
                                      default(HexCoordinate))
        {
            Posture = posture,
            TargetId = targetId,
        };
        state.Fleets.Add(founded);
        return founded;
    }

    /// <summary>Posted freight capacity of a lane this epoch — the design's
    /// fleet-capacity interface, replacing the slice-D LaneMath stub: a lane
    /// without hulls moves nothing.</summary>
    public static double PostedCapacity(SimState state, Lane lane)
    {
        var a = state.Ports[lane.PortAId];
        var b = state.Ports[lane.PortBId];
        int dist = HexGrid.Distance(a.Hex, b.Hex);
        double speed = LaneMath.TransitSpeed(a, b);
        int years = state.Config.Sim.YearsPerEpoch;
        double capacity = 0;
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.Posture != FleetPosture.Posted
                || fleet.TargetId != lane.Id) continue;
            foreach (var g in fleet.Hulls)                // design-id order
                capacity += FleetMath.PostedCapacityPerEpoch(state.Config.Fleet,
                    DesignRegistry.SheetOf(state, state.Designs[g.DesignId]),
                    g.Count, speed, dist, years);
        }
        return capacity;
    }

    /// <summary>Traffic frequency of a lane: posted round trips per
    /// world-year — the news-speed data Perception consumes in slice I
    /// (busy lanes carry news fast, backwaters slowly, wilds barely).</summary>
    public static double TrafficPerYear(SimState state, Lane lane)
    {
        var a = state.Ports[lane.PortAId];
        var b = state.Ports[lane.PortBId];
        int dist = HexGrid.Distance(a.Hex, b.Hex);
        if (dist <= 0) return 0;
        double speed = LaneMath.TransitSpeed(a, b);
        double trips = 0;
        foreach (var fleet in state.Fleets)               // id order (P6)
            if (fleet.Posture == FleetPosture.Posted && fleet.TargetId == lane.Id)
                trips += fleet.TotalHulls
                         * state.Config.Fleet.FreightTripsPerYearBase
                         * speed / dist;
        return trips;
    }

    /// <summary>Lanes closed to freight this step: the REPL's debug cuts
    /// plus every lane touching a blockaded port (a Blockade-posture fleet
    /// stationed at its approaches — interdiction is one hex address,
    /// space-and-travel.md). Derived from fleet state, never stored.</summary>
    public static HashSet<int> SeveredLaneIds(SimState state)
    {
        var severed = new HashSet<int>(state.SeveredLanes);
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.Posture != FleetPosture.Blockade || fleet.TargetId < 0
                || fleet.TotalHulls == 0) continue;
            foreach (var lane in state.Lanes)
                if (lane.PortAId == fleet.TargetId || lane.PortBId == fleet.TargetId)
                    severed.Add(lane.Id);
        }
        return severed;
    }

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
