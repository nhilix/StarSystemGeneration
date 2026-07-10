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
            // drift happens at the yard, before this epoch's hulls lay
            // down; only cells the yard could actually afford a hull of
            // advance, so no class ever launches without a ship behind it
            double gradeAtHand = market.Inventory[(int)GoodId.ShipComponents] > 0
                ? market.InventoryGrade[(int)GoodId.ShipComponents] : 0.0;
            var queue = BuildQueue(state, pr.ActorId, priorities);
            if (queue.Count == 0) continue;
            if (gradeAtHand > 0)
                for (int i = 0; i < queue.Count; i++)
                {
                    if (!CanLayDown(state, pr, market, queue[i].Design)) continue;
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

    /// <summary>One hull of this design is physically and fiscally
    /// buildable at this market right now.</summary>
    private static bool CanLayDown(SimState state, PolityRecord pr,
                                   Market market, ShipDesign design)
    {
        var cfg = state.Config;
        double components = DesignMath.ComponentsPerHull(cfg.Fleet, design.Size);
        double armaments = DesignMath.ArmamentsPerHull(cfg.Fleet, design.Role,
                                                       design.Size);
        double value = components
            * Market.InitialPrice(cfg.Economy, GoodId.ShipComponents)
            + armaments * Market.InitialPrice(cfg.Economy, GoodId.Armaments);
        return market.Inventory[(int)GoodId.ShipComponents] >= components
               && market.Inventory[(int)GoodId.Armaments] >= armaments
               && pr.MilitaryPoints >= value;
    }

    /// <summary>The yard's queue: current-mark designs with their standing
    /// weights. Priorities were decided from last step's perception, so a
    /// lineage that advanced since then is still keyed by its previous
    /// mark — the weight follows the lineage, not the id (a yard never
    /// idles the epoch after a class launch). An empty priorities policy
    /// falls back to freight only — hulls someone always wants.</summary>
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
            double weight = 0;
            if (priorities.Count == 0)
                weight = current.Role == ShipRole.Freight ? 1.0 : 0.0;
            else
                // the lineage's weight: any mark of this cell that the
                // standing priorities name (highest mark named wins;
                // designs scan in id order, so later marks overwrite)
                foreach (var mark in state.Designs)       // id order (P6)
                    if (mark.OwnerActorId == actorId && mark.Role == d.Role
                        && mark.Size == d.Size
                        && priorities.TryGetValue(mark.Id, out double w))
                        weight = w;
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
    /// fleet-capacity interface (Σ cargo × availability), replacing the
    /// slice-D LaneMath stub: a lane without hulls moves nothing, and a
    /// starved fleet carries less (readiness is the availability term —
    /// supply failure bites the economy before the attrition cliff).</summary>
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
                    g.Count, speed, dist, years) * fleet.Readiness;
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
                         * speed / dist * fleet.Readiness;
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

    /// <summary>Fleet supply (fleets/ships-and-fleets.md §Movement and
    /// supply), run by Allocation after postures: every fleet draws upkeep
    /// from its home-port market — fuel plus armaments for warship hulls,
    /// ship components (spares) for civilian ones — paid from the military
    /// treasury at
    /// market prices, the spend recycling to the home port's households
    /// (navy money is somebody's income). Readiness drifts toward the met
    /// fraction (the facility-condition convention); below the attrition
    /// floor, hulls wreck at the fleet's hex (event 401). Returns hulls
    /// lost.</summary>
    public static int SupplyFleets(SimState state, PolityRecord pr)
    {
        var cfg = state.Config;
        var knobs = cfg.Fleet;
        int years = cfg.Sim.YearsPerEpoch;
        int lost = 0;
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.OwnerActorId != pr.ActorId || fleet.TotalHulls == 0)
                continue;
            int mIx = fleet.HomePortId;
            if (mIx < 0 || mIx >= state.Markets.Count) continue;
            var market = state.Markets[mIx];

            double posture = fleet.Posture == FleetPosture.Reserve
                ? knobs.ReserveUpkeepFactor : 1.0;
            double fuelNeed = 0, armsNeed = 0, partsNeed = 0;
            foreach (var g in fleet.Hulls)                // design-id order
            {
                var design = state.Designs[g.DesignId];
                var sheet = DesignRegistry.SheetOf(state, design);
                double draw = sheet[ShipStat.Upkeep] * g.Count
                              * knobs.UpkeepUnitsPerPointPerYear * years
                              * posture;
                fuelNeed += draw * knobs.UpkeepFuelShare;
                if (ShipCatalog.IsWarship(design.Role))
                    armsNeed += draw * (1 - knobs.UpkeepFuelShare);
                else
                    partsNeed += draw * (1 - knobs.UpkeepFuelShare);
            }

            // met is need-weighted, not min: an armaments drought hollows a
            // fueled fleet toward degraded readiness instead of erasing it —
            // militia rot, not evaporation (attrition still bites below the
            // floor when fuel fails too)
            double totalNeed = fuelNeed + armsNeed + partsNeed;
            double met = totalNeed <= 0 ? 1.0
                : (DrawUpkeep(state, pr, market, (int)GoodId.Fuel, fuelNeed)
                       * fuelNeed
                   + DrawUpkeep(state, pr, market, (int)GoodId.Armaments, armsNeed)
                       * armsNeed
                   + DrawUpkeep(state, pr, market,
                         (int)GoodId.ShipComponents, partsNeed)
                       * partsNeed) / totalNeed;

            if (met >= fleet.Readiness)
                fleet.Readiness = Math.Min(met, fleet.Readiness
                    + knobs.ReadinessRecoveryPerYear * years);
            else
                fleet.Readiness = Math.Max(met, fleet.Readiness
                    - knobs.ReadinessDecayPerYear * years);

            if (fleet.Readiness < knobs.AttritionReadinessFloor)
            {
                double expected = fleet.TotalHulls
                                  * knobs.AttritionHullLossPerYear * years;
                int toLose = Math.Max(1, (int)expected);
                lost += Wreck(state, fleet, toLose);
            }
        }
        return lost;
    }

    /// <summary>Draw one upkeep good from the home market first, then from
    /// the polity's strategic reserve — the design's "market/stockpile"
    /// (fleets doc §Movement and supply): navy logistics run on the
    /// quartermaster's stores where a frontier port's shelves are bare.
    /// Market draws are paid from the military treasury at the market
    /// price and land as home-port wages (navy money is somebody's
    /// income); reserve draws consume stock procurement already bought.
    /// Returns the met fraction.</summary>
    private static double DrawUpkeep(SimState state, PolityRecord pr,
                                     Market market, int good, double need)
    {
        if (need <= 0) return 1.0;
        double affordable = market.Price[good] > 0
            ? Math.Max(0.0, pr.MilitaryPoints) / market.Price[good]
            : need;
        double drawn = market.Draw(good, Math.Min(need, affordable));
        if (drawn > 0)
        {
            market.LastCleared[good] += drawn;
            double cost = drawn * market.Price[good];
            pr.MilitaryPoints -= cost;
            MarketEngine.PayWages(state, market.PortId, cost);
        }
        double shortfall = need - drawn;
        if (shortfall > 0 && pr.ReserveQty[good] > 0)
        {
            double fromReserve = Math.Min(shortfall, pr.ReserveQty[good]);
            pr.ReserveQty[good] -= fromReserve;
            if (pr.ReserveQty[good] <= 0) pr.ReserveGrade[good] = 0;
            drawn += fromReserve;
        }
        return drawn / need;
    }

    /// <summary>Wreck up to <paramref name="count"/> hulls out of a fleet,
    /// design-id order: wreckage records at the fleet's hex, the ledger
    /// moves Built → Wrecked, the chronicle carries the loss (401).</summary>
    public static int Wreck(SimState state, FleetRecord fleet, int count)
    {
        // whoever owns the hulls owns the ledger entry — polity or
        // corporation (slice G), the loss conserves the same way
        var corp = state.CorporationOf(fleet.OwnerActorId);
        var pr = corp == null ? state.PolityOf(fleet.OwnerActorId) : null;
        int wrecked = 0;
        while (count > 0 && fleet.Hulls.Count > 0)
        {
            var g = fleet.Hulls[0];
            int loss = Math.Min(g.Count, count);
            fleet.RemoveHulls(g.DesignId, loss);
            state.Wreckage.Add(new WreckageRecord(state.Wreckage.Count,
                fleet.Hex, g.DesignId, loss, state.WorldYear));
            if (corp != null) corp.HullsWrecked += loss;
            else pr!.HullsWrecked += loss;
            wrecked += loss;
            count -= loss;
        }
        if (wrecked > 0)
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.FleetAttrition,
                new[] { fleet.OwnerActorId }, fleet.Hex, Magnitude: wrecked,
                Valence: -1.0, EventVisibility.Regional,
                new FleetAttritionPayload(fleet.Id, wrecked)));
        return wrecked;
    }

    /// <summary>Fleet upkeep registers as demand at the home-port markets
    /// (the MilitaryUpkeep use-case): the price signal keeps fuel and
    /// armaments produced where navies base.</summary>
    public static void AddUpkeepDemand(SimState state, MarketStepScratch scratch)
    {
        var knobs = state.Config.Fleet;
        int years = state.Config.Sim.YearsPerEpoch;
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.TotalHulls == 0 || fleet.HomePortId < 0
                || fleet.HomePortId >= state.Markets.Count) continue;
            double posture = fleet.Posture == FleetPosture.Reserve
                ? knobs.ReserveUpkeepFactor : 1.0;
            foreach (var g in fleet.Hulls)                // design-id order
            {
                var design = state.Designs[g.DesignId];
                var sheet = DesignRegistry.SheetOf(state, design);
                double draw = sheet[ShipStat.Upkeep] * g.Count
                              * knobs.UpkeepUnitsPerPointPerYear * years
                              * posture;
                scratch.Demand[fleet.HomePortId][(int)GoodId.Fuel]
                    += draw * knobs.UpkeepFuelShare;
                int rest = ShipCatalog.IsWarship(design.Role)
                    ? (int)GoodId.Armaments : (int)GoodId.ShipComponents;
                scratch.Demand[fleet.HomePortId][rest]
                    += draw * (1 - knobs.UpkeepFuelShare);
            }
        }
    }

    /// <summary>Off-lane range of a design in hexes — the endurance floor
    /// in map units (the slowest hull limits a formation).</summary>
    public static int EnduranceHexes(SimState state, ShipDesign design) =>
        (int)(DesignRegistry.SheetOf(state, design)[ShipStat.OffLaneEndurance]
              * state.Config.Fleet.EnduranceHexesPerPoint);

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

    /// <summary>An actor's headline war weight: strike + sustained fire
    /// across its fleets, readiness-discounted — what vassal choices and
    /// war appetites size each other by (slice H).</summary>
    public static double WarStrength(SimState state, int actorId)
    {
        double strength = 0;
        foreach (var fleet in state.Fleets)                   // id order (P6)
        {
            if (fleet.OwnerActorId != actorId || fleet.TotalHulls == 0) continue;
            var v = Vectors(state, fleet);
            strength += (v.Strike + v.Sustained) * fleet.Readiness;
        }
        return strength;
    }
}
