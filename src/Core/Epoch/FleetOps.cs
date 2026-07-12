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
                weights[i] = LaneMath.IsLive(state, lanes[i])
                    ? LaneMath.Capacity(state, lanes[i]) : 0.0;
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
            // fleets on war stations keep their hulls — mobilization owns
            // them until the settlement demobilizes (slice H)
            if (fleet.Posture is FleetPosture.Blockade
                or FleetPosture.Expedition) continue;
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
        if (!LaneMath.IsLive(state, lane)) return 0;   // dead gates move nothing
        var a = state.Ports[lane.PortAId];
        var b = state.Ports[lane.PortBId];
        int dist = HexGrid.Distance(a.Hex, b.Hex);
        double speed = LaneMath.TransitSpeed(state, lane);
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

    /// <summary>One posted fleet's lift for a step on its lane, in units —
    /// the same capacity a spread run gets, shared with the courier job
    /// board so acceptance charges real hulls (review wave, finding 5).</summary>
    public static double PostedLift(SimState state, FleetRecord fleet,
                                    Lane lane)
    {
        var a = state.Ports[lane.PortAId];
        var b = state.Ports[lane.PortBId];
        int dist = HexGrid.Distance(a.Hex, b.Hex);
        double speed = LaneMath.TransitSpeed(state, lane);
        int years = state.Config.Sim.YearsPerEpoch;
        double capacity = 0;
        foreach (var g in fleet.Hulls)                    // design-id order
            capacity += FleetMath.PostedCapacityPerEpoch(state.Config.Fleet,
                DesignRegistry.SheetOf(state, state.Designs[g.DesignId]),
                g.Count, speed, dist, years) * fleet.Readiness;
        return capacity;
    }

    /// <summary>Traffic frequency of a lane: posted round trips per
    /// world-year — the news-speed data Perception consumes in slice I
    /// (busy lanes carry news fast, backwaters slowly, wilds barely).</summary>
    public static double TrafficPerYear(SimState state, Lane lane)
    {
        // a quarantined lane is physically closed: no shipping, so no
        // traffic-borne news or contagion — word still crawls at the base
        // carriage (review fix 3)
        if (lane.QuarantinedUntil >= state.WorldYear) return 0;
        if (!LaneMath.IsLive(state, lane)) return 0;   // dead gates carry no news
        var a = state.Ports[lane.PortAId];
        var b = state.Ports[lane.PortBId];
        int dist = HexGrid.Distance(a.Hex, b.Hex);
        if (dist <= 0) return 0;
        double speed = LaneMath.TransitSpeed(state, lane);
        double trips = 0;
        foreach (var fleet in state.Fleets)               // id order (P6)
            if (fleet.Posture == FleetPosture.Posted && fleet.TargetId == lane.Id)
                trips += fleet.TotalHulls
                         * state.Config.Fleet.FreightTripsPerYearBase
                         * speed / dist * fleet.Readiness;
        return trips;
    }

    /// <summary>Lanes closed to freight this step: every lane touching a
    /// blockaded port (a Blockade-posture fleet stationed at its
    /// approaches — interdiction is one hex address, space-and-travel.md).
    /// Derived from fleet state, never stored — real interdiction replaced
    /// the slice-E debug cut hook (slice H).</summary>
    public static HashSet<int> SeveredLaneIds(SimState state)
    {
        var severed = new HashSet<int>();
        foreach (var fleet in state.Fleets)               // id order (P6)
        {
            if (fleet.Posture != FleetPosture.Blockade || fleet.TargetId < 0
                || fleet.TotalHulls == 0) continue;
            foreach (var lane in state.Lanes)
                if (lane.PortAId == fleet.TargetId || lane.PortBId == fleet.TargetId)
                    severed.Add(lane.Id);
        }
        // self-imposed closures cut freight and contagion alike (slice I);
        // they are NOT blockade progress — war objectives read fleet
        // postures above, never this flag
        foreach (var lane in state.Lanes)                 // id order (P6)
            if (lane.QuarantinedUntil >= state.WorldYear)
                severed.Add(lane.Id);
        return severed;
    }

    /// <summary>The actor's port nearest a hex, ties to the lower id — the
    /// forward-depot pick (contract-economy spec §4) and the interdictor's
    /// prize port. −1 when the actor holds no ports.</summary>
    public static int NearestOwnedPortId(SimState state, int actorId,
                                         HexCoordinate hex)
    {
        int best = -1, bestDist = int.MaxValue;
        foreach (var port in state.Ports)                 // id order (P6)
        {
            if (port.OwnerActorId != actorId) continue;
            int d = HexGrid.Distance(port.Hex, hex);
            if (d < bestDist) { bestDist = d; best = port.Id; }
        }
        return best;
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
            // the front is a demander (contract-economy spec §4): a
            // war-stationed force victuals at the nearest owned port — its
            // forward depot — not the home port a sector behind the line
            int mIx = fleet.Posture is FleetPosture.Blockade
                          or FleetPosture.Expedition
                ? NearestOwnedPortId(state, pr.ActorId, fleet.Hex)
                : fleet.HomePortId;
            if (mIx < 0 || mIx >= state.Markets.Count) continue;
            var market = state.Markets[mIx];

            bool atWar = WarOps.AtWar(state, pr.ActorId);
            var (fuelNeed, armsNeed, partsNeed, rationsNeed) =
                UpkeepNeed(state, fleet, atWar, years);

            // met is need-weighted, not min: an armaments drought hollows a
            // fueled fleet toward degraded readiness instead of erasing it —
            // militia rot, not evaporation (attrition still bites below the
            // floor when fuel fails too)
            double totalNeed = fuelNeed + armsNeed + partsNeed + rationsNeed;
            double met = totalNeed <= 0 ? 1.0
                : (DrawUpkeep(state, pr, market, (int)GoodId.Fuel, fuelNeed)
                       * fuelNeed
                   + DrawUpkeep(state, pr, market, (int)GoodId.Armaments, armsNeed)
                       * armsNeed
                   + DrawUpkeep(state, pr, market,
                         (int)GoodId.ShipComponents, partsNeed)
                       * partsNeed
                   + DrawUpkeep(state, pr, market,
                         (int)GoodId.Provisions, rationsNeed)
                       * rationsNeed) / totalNeed;

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

    /// <summary>One fleet's upkeep basket over a span of world-years — fuel
    /// plus armaments for warship hulls, ship components for civilian ones,
    /// rations for warships at war (slice H eyeball: a belligerent's crews
    /// eat from the same markets the households do). Shared between the
    /// supply draw and the quartermaster's depot forecast (spec §4) so the
    /// convoys carry exactly what the front will burn.</summary>
    internal static (double Fuel, double Arms, double Parts, double Rations)
        UpkeepNeed(SimState state, FleetRecord fleet, bool atWar, double years)
    {
        var knobs = state.Config.Fleet;
        double posture = fleet.Posture == FleetPosture.Reserve
            ? knobs.ReserveUpkeepFactor : 1.0;
        double fuel = 0, arms = 0, parts = 0, rations = 0;
        foreach (var g in fleet.Hulls)                    // design-id order
        {
            var design = state.Designs[g.DesignId];
            var sheet = DesignRegistry.SheetOf(state, design);
            double draw = sheet[ShipStat.Upkeep] * g.Count
                          * knobs.UpkeepUnitsPerPointPerYear * years
                          * posture;
            fuel += draw * knobs.UpkeepFuelShare;
            if (ShipCatalog.IsWarship(design.Role))
            {
                arms += draw * (1 - knobs.UpkeepFuelShare);
                if (atWar)
                    rations += g.Count * posture * years
                        * state.Config.War.RationsPerHullPerYear;
            }
            else
                parts += draw * (1 - knobs.UpkeepFuelShare);
        }
        return (fuel, arms, parts, rations);
    }

    /// <summary>Buy one upkeep good off the home book first, then draw the
    /// HOME PORT's stockpile — the design's "market/stockpile" (fleets doc
    /// §Movement and supply), located per spec §4b: navy logistics run on
    /// the local quartermaster's stores where a frontier port's book is
    /// bare; a rich larder two systems over feeds nobody here. Book lifts
    /// are paid from the military treasury at the sellers' asks (navy
    /// money is somebody's income); stock draws consume procurement
    /// already bought. Returns the met fraction.</summary>
    private static double DrawUpkeep(SimState state, PolityRecord pr,
                                     Market market, int good, double need)
    {
        if (need <= 0) return 1.0;
        var (drawn, _, cost) = BookOps.LiftAsks(state, market.PortId, good,
            need, budget: Math.Max(0.0, pr.MilitaryPoints));
        pr.MilitaryPoints -= cost;
        double shortfall = need - drawn;
        if (shortfall > 0)
            drawn += state.Ports[market.PortId].DrawStock(good, shortfall);
        return drawn / need;
    }

    /// <summary>Wreck up to <paramref name="count"/> hulls out of a fleet,
    /// design-id order: wreckage records at the fleet's hex, the ledger
    /// moves Built → Wrecked, the chronicle carries the loss (401) —
    /// unless quiet (battles stage their own richer event, slice H).</summary>
    public static int Wreck(SimState state, FleetRecord fleet, int count,
                            bool quiet = false)
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
        if (wrecked > 0 && !quiet)
            state.Staged.Add(new StagedEvent(
                ClockStratum.Generational, WorldEventType.FleetAttrition,
                new[] { fleet.OwnerActorId }, fleet.Hex, Magnitude: wrecked,
                Valence: -1.0, EventVisibility.Regional,
                new FleetAttritionPayload(fleet.Id, wrecked)));
        return wrecked;
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
        // the war economy multiplies the standing force: a fed mobilization
        // ramp reaches the full MobilizationFactor surge (spec §5)
        if (state.Actors[actorId].Kind == ActorKind.Polity)
            strength *= 1.0 + (state.Config.War.MobilizationFactor - 1.0)
                        * state.PolityOf(actorId).Mobilization;
        return strength;
    }
}
