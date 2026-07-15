using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;        // HexGrid (completion event midpoints)
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The project lifecycle (spec §§1,4): spawn at groundbreaking,
/// feed and advance every Allocation in priority order, fire the completion
/// payload when the years are delivered. Pure deterministic math over
/// ordered state — no rolls.</summary>
public static class ProjectOps
{
    public static Project Spawn(SimState state, ProjectKind kind,
        int ownerActorId, int funderActorId, int portId, HexCoordinate hex,
        double yearsRequired, ProjectPriority priority, int planOrder) =>
        SpawnAt(state, kind, ownerActorId, funderActorId, portId, hex,
                yearsRequired, priority, planOrder, state.WorldYear);

    public static Project SpawnAt(SimState state, ProjectKind kind,
        int ownerActorId, int funderActorId, int portId, HexCoordinate hex,
        double yearsRequired, ProjectPriority priority, int planOrder,
        int startedYear)
    {
        var p = new Project(state.Projects.Count, kind, ownerActorId,
                            funderActorId, portId, hex, yearsRequired,
                            startedYear)
        { Priority = priority, PlanOrder = planOrder };
        state.Projects.Add(p);
        return p;
    }

    /// <summary>Groundbreak a facility: the Facility row exists NOW at the
    /// hex, uncommissioned (P1 — the construction site is residue); basket
    /// = BuildCost / ConstructionYears (the conservation invariant); wages
    /// = administered value / ConstructionYears.</summary>
    public static Project SpawnFacilityConstruction(SimState state,
        int ownerActorId, int funderActorId, ConstructionCandidate c,
        ProjectPriority priority, int planOrder, int startedYear = int.MinValue)
    {
        var type = (Substrate.InfraTypeId)c.TypeId;
        var def = Substrate.Infrastructure.Get(type);
        var facility = new Facility(state.Facilities.Count, c.TypeId,
            tier: 1, c.Hex, ownerActorId, state.WorldYear)
        { CommissionedYear = -1 };
        // groundbreaking is the §1 commit trigger: freeze the hex's system,
        // then decide this facility's body once — seeing bodies already
        // claimed by other facilities at this hex (the two-mines fix).
        var system = SystemRegistry.Commit(state, c.Hex);
        var portBody = BodySiting.PortBody(system);
        var claimed = new List<BodyRef>();
        foreach (var other in state.Facilities)           // id order (P6)
            if (other.Hex.Equals(c.Hex) && !other.Body.IsNone)
                claimed.Add(other.Body);
        facility.Body = BodySiting.Assign(system, type, portBody, claimed);
        state.Facilities.Add(facility);
        double years = Math.Max(1.0, def.ConstructionYears);
        var p = SpawnAt(state, ProjectKind.FacilityConstruction, ownerActorId,
                      funderActorId, c.PortId, c.Hex, years, priority,
                      planOrder,
                      startedYear == int.MinValue ? state.WorldYear : startedYear);
        double value = 0;
        foreach (var q in def.BuildCost)
        {
            p.PerYearBasket[(int)q.Good] = q.Quantity / years;
            value += q.Quantity
                     * Market.InitialPrice(state.Config.Economy, q.Good);
        }
        p.WagesPerYear = value / years;
        p.TypeId = c.TypeId;
        p.TargetId = facility.Id;
        p.Body = facility.Body;
        return p;
    }

    /// <summary>Groundbreak a hull batch: the yard commits to <paramref
    /// name="count"/> hulls of one design, paid as a wage stream over the
    /// build years (fleets/ships-and-fleets.md, time-and-logistics.md) —
    /// the navy arrives late by design, no instant laydown. Duration scales
    /// with the design's size (bigger hulls take longer, medium-size is the
    /// base); basket/wages are per world-year across the whole batch.</summary>
    public static Project SpawnHullBatch(SimState state, int ownerActorId,
        int portId, ShipDesign design, int count, ProjectPriority priority,
        int planOrder, int startedYear = int.MinValue)
    {
        var cfg = state.Config;
        double medium = DesignMath.ComponentsPerHull(cfg.Fleet, ShipSize.Medium);
        double comp = DesignMath.ComponentsPerHull(cfg.Fleet, design.Size);
        double years = Math.Max(1.0,
            cfg.Fleet.HullBuildYearsBase * (comp / medium));
        double armaments = DesignMath.ArmamentsPerHull(cfg.Fleet, design.Role,
                                                        design.Size);
        var p = SpawnAt(state, ProjectKind.HullBatch, ownerActorId, ownerActorId,
                      portId, state.Ports[portId].Hex, years, priority,
                      planOrder,
                      startedYear == int.MinValue ? state.WorldYear : startedYear);
        p.PerYearBasket[(int)GoodId.ShipComponents] = comp * count / years;
        double value = comp
            * Market.InitialPrice(cfg.Economy, GoodId.ShipComponents) * count;
        if (armaments > 0)
        {
            p.PerYearBasket[(int)GoodId.Armaments] = armaments * count / years;
            value += armaments
                * Market.InitialPrice(cfg.Economy, GoodId.Armaments) * count;
        }
        p.WagesPerYear = value / years;
        p.TypeId = design.Id;
        p.Count = count;
        return p;
    }

    /// <summary>Groundbreak a gate pair (lane-economics + time-not-ticks):
    /// BOTH gate facilities exist NOW at each port's hex, uncommissioned
    /// (CommissionedYear = −1) — they hold their gate slots from
    /// groundbreaking. The Lane row exists now carrying both gate ids; it
    /// reads dead until both commission (LaneMath.IsLive). One project
    /// delivers the pair over the gate's ConstructionYears: basket = the full
    /// PAIR build cost per year, wages = the pair's administered value per
    /// year — a half-built highway opens no lane. Founding links stream no
    /// goods: the colony expedition shipped the pair's basket from its
    /// staging market at departure (time-and-logistics spec §4, "Founding
    /// links get subsumed") — the project runs on time + wages alone.</summary>
    public static Project SpawnGatePair(SimState state, int ownerActorId,
        int funderActorId, Port a, Port b, int tier, ProjectPriority priority,
        int planOrder, bool foundingLink = false)
    {
        if (a.Id > b.Id) (a, b) = (b, a);
        var cfg = state.Config;
        var def = Substrate.Infrastructure.Get(Substrate.InfraTypeId.Gate);
        double scale = Substrate.Production.TierCostFactor(tier);
        double years = Math.Max(1.0, def.ConstructionYears);
        var gateA = new Facility(state.Facilities.Count,
            (int)Substrate.InfraTypeId.Gate, tier, a.Hex, ownerActorId,
            state.WorldYear) { CommissionedYear = -1 };
        state.Facilities.Add(gateA);
        var gateB = new Facility(state.Facilities.Count,
            (int)Substrate.InfraTypeId.Gate, tier, b.Hex, ownerActorId,
            state.WorldYear) { CommissionedYear = -1 };
        state.Facilities.Add(gateB);
        var lane = new Lane(state.Lanes.Count, a.Id, b.Id, state.WorldYear)
        { GateAId = gateA.Id, GateBId = gateB.Id };
        state.Lanes.Add(lane);
        // the basket is the PAIR's rate; Feed draws it per end — each gate
        // at its own market and larder (spec §5, landed in stage 2)
        var p = Spawn(state, ProjectKind.GatePair, ownerActorId, funderActorId,
                      a.Id, a.Hex, years, priority, planOrder);
        double value = 0;
        foreach (var q in def.BuildCost)
        {
            // spec §4: a founding link's goods arrived with the expedition —
            // empty basket, wages and duration unchanged
            if (!foundingLink)
                p.PerYearBasket[(int)q.Good] = 2.0 * q.Quantity * scale / years;
            value += 2.0 * q.Quantity * scale
                     * Market.InitialPrice(cfg.Economy, q.Good);
        }
        p.WagesPerYear = value / years;
        p.TypeId = (int)Substrate.InfraTypeId.Gate;
        p.TargetId = lane.Id;
        return p;
    }

    /// <summary>Launch a colony expedition (space-and-travel.md
    /// §Colonization): the convoy is already dispatched (Resolution charged
    /// the act, burned the fuel, staged ConvoyDispatched); this project is
    /// the off-lane crossing in world-time — duration = off-lane hexes over
    /// the convoy's speed, empty basket, no wages. Completion founds the
    /// port (or turns the convoy back if the hex was taken meanwhile).</summary>
    public static Project SpawnExpedition(SimState state, int ownerActorId,
        int stagingPortId, HexCoordinate target, int convoyFleetId,
        int offLaneHexes)
    {
        double years = offLaneHexes / state.Config.Fleet.ExpeditionHexesPerYear;
        var p = Spawn(state, ProjectKind.ColonyExpedition, ownerActorId,
                      ownerActorId, stagingPortId, target, years,
                      ProjectPriority.Core, 0);
        p.TargetId = convoyFleetId;
        return p;
    }

    /// <summary>Pass 1 (spec §4): per funder in entered actor-id order,
    /// projects in (priority, plan order, id) order — the scarcest input
    /// paces each project; earlier draws starve later ones at a shared
    /// market. Returns completions this step.</summary>
    public static int AdvanceAll(SimState state)
    {
        int years = state.Config.Sim.YearsPerEpoch;
        int spanEnd = state.WorldYear + years;
        int completions = 0;
        var mine = new List<Project>();
        foreach (var actor in state.Actors)                   // id order (P6)
        {
            if (!actor.Entered) continue;
            mine.Clear();
            foreach (var p in state.Projects)                 // id order (P6)
                if (p.InFlight && p.FunderActorId == actor.Id) mine.Add(p);
            if (mine.Count == 0) continue;
            mine.Sort((x, y) =>
            {
                int c = x.Priority.CompareTo(y.Priority);
                if (c != 0) return c;
                c = x.PlanOrder.CompareTo(y.PlanOrder);
                return c != 0 ? c : x.Id.CompareTo(y.Id);
            });
            foreach (var p in mine)
            {
                double span = Math.Min(years, spanEnd - p.StartedYear);
                if (span <= 0) continue;                      // not yet due
                double before = p.YearsDelivered;
                double need = Math.Min(span,
                    p.YearsRequired - p.YearsDelivered);
                if (need > 0) Feed(state, p, need);
                // hopeless work is cancelled, not carried forever: a
                // project starved past the abandon window releases its
                // slots and chronicles the ruin (spec §3; P1 residue)
                if (p.InFlight && p.StarvedYears
                        >= state.Config.Economy.ProjectAbandonYears
                    && p.Kind != ProjectKind.ColonyExpedition)
                {
                    Cancel(state, p);
                    state.Staged.Add(new StagedEvent(
                        ClockStratum.Generational,
                        WorldEventType.ProjectAbandoned,
                        new[] { p.OwnerActorId }, p.Hex,
                        Magnitude: p.Progress, Valence: -0.4,
                        EventVisibility.Regional,
                        new ProjectAbandonedPayload(p.Id, (int)p.Kind,
                                                    p.YearsDelivered)));
                    continue;
                }
                if (p.YearsDelivered >= p.YearsRequired - 1e-9)
                {
                    // the true delivery year sits inside the span: the
                    // feeding window's start plus the years the last
                    // stretch took at its fed pace (stage 2 residue fix)
                    double windowStart = Math.Max(state.WorldYear,
                                                  p.StartedYear);
                    double f = p.LastFedFraction;
                    int completionYear = (int)Math.Round(windowStart
                        + (f > 0 ? (p.YearsRequired - before) / f : 0));
                    Complete(state, p, completionYear);
                    completions++;
                }
            }
        }
        return completions;
    }

    /// <summary>Draw needYears of the basket from the works' own supplies
    /// (contract-economy spec §2): the laydown yard (goods the project's
    /// posted bids bought) plus the site port's stockpile when the funder
    /// owns it — nothing teleports in; remote goods arrive as shipments or
    /// bid fills before this draw. The met fraction (min across goods AND
    /// the wage stream) scales progress, consumption, and wages alike: a
    /// starved project neither hoards goods nor pays idle crews.</summary>
    private static void Feed(SimState state, Project p, double needYears)
    {
        // travel kinds carry no basket and pay no wages: they skip the goods
        // loop and simply advance, dragging their convoy along the hex line
        if (p.Kind == ProjectKind.ColonyExpedition)
        {
            p.YearsDelivered = Math.Min(p.YearsRequired,
                p.YearsDelivered + needYears);
            p.LastFedFraction = 1.0;
            if (p.TargetId >= 0)
            {
                var convoy = state.Fleets[p.TargetId];
                var from = state.Ports[p.PortId].Hex;
                convoy.Hex = HexGrid.Round(
                    from.Q + (p.Hex.Q - from.Q) * p.Progress,
                    from.R + (p.Hex.R - from.R) * p.Progress);
            }
            return;
        }
        // a gate pair's bids pull toward both ends, but the works share one
        // laydown yard; the funder-owned larder check runs per end below
        var site = state.Ports[p.PortId];
        // the local larder feeds the state's own works: stock belongs to
        // the port's owner, so a corp building on a host port lives off
        // its bid fills alone
        bool ownStock = site.OwnerActorId == p.FunderActorId;
        double fraction = 1.0;
        for (int g = 0; g < p.PerYearBasket.Length; g++)
        {
            double want = p.PerYearBasket[g] * needYears;
            if (want <= 0) continue;
            double have = p.DeliveredQty[g]
                + (ownStock ? site.StockQty[g] : 0.0);
            fraction = Math.Min(fraction, Math.Min(1.0, have / want));
        }
        double wages = p.WagesPerYear * needYears;
        if (wages > 0)
        {
            double treasury = TreasuryAvailable(state, p);
            fraction = Math.Min(fraction,
                Math.Min(1.0, treasury / wages));
        }
        if (fraction > 0)
            for (int g = 0; g < p.PerYearBasket.Length; g++)
            {
                double take = p.PerYearBasket[g] * needYears * fraction;
                if (take <= 0) continue;
                double fromYard = Math.Min(take, p.DeliveredQty[g]);
                double yardGrade = p.DeliveredGrade[g];
                p.DeliveredQty[g] -= fromYard;
                if (p.DeliveredQty[g] <= 0) p.DeliveredGrade[g] = 0;
                double grade = yardGrade;
                double shortfall = take - fromYard;
                if (shortfall > 0 && ownStock)
                {
                    // blend, don't replace: yard units carry their own
                    // grade — a shortfall topped up from the port's stock
                    // is a quantity-weighted mix (F8)
                    double stockGrade = site.StockGrade[g];
                    double fromStock = site.DrawStock(g, shortfall);
                    double total = fromYard + fromStock;
                    if (total > 0)
                        grade = (yardGrade * fromYard
                            + stockGrade * fromStock) / total;
                }
                if (g == (int)GoodId.ShipComponents && take > 0)
                {
                    p.AccumGrade = (p.AccumGrade * p.AccumGradeWeight
                        + grade * take) / (p.AccumGradeWeight + take);
                    p.AccumGradeWeight += take;
                }
            }
        if (wages > 0 && fraction > 0)
        {
            SpendTreasury(state, p, wages * fraction);
            if (p.Kind == ProjectKind.GatePair && p.TargetId >= 0)
            {
                // the crews split the stream: half to each end's households
                var lane = state.Lanes[p.TargetId];
                MarketEngine.PayWages(state, lane.PortAId,
                                      0.5 * wages * fraction);
                MarketEngine.PayWages(state, lane.PortBId,
                                      0.5 * wages * fraction);
            }
            else
                MarketEngine.PayWages(state, p.PortId, wages * fraction);
        }
        p.YearsDelivered = Math.Min(p.YearsRequired,
            p.YearsDelivered + fraction * needYears);
        p.LastFedFraction = fraction;
        // the abandon clock: essentially-unfed spans accumulate, real
        // feeding resets (spec §3 — hopeless work gets cancelled)
        if (fraction < 0.05) p.StarvedYears += needYears;
        else p.StarvedYears = 0;
        if (p.Kind == ProjectKind.Mobilization && FunderPolity(state,
                p.OwnerActorId) is PolityRecord mob)
            mob.Mobilization = Math.Max(mob.Mobilization, p.Progress);
    }

    /// <summary>The treasury a kind streams wages from: development for
    /// civil works, military for hulls and mobilization, corp credits for
    /// corporate funders. Expeditions charged at the act, no stream.</summary>
    internal static double TreasuryAvailable(SimState state, Project p)
    {
        var corp = state.CorporationOf(p.FunderActorId);
        if (corp != null) return Math.Max(0, corp.Credits);
        var pr = state.PolityOf(p.FunderActorId);
        return p.Kind switch
        {
            ProjectKind.HullBatch or ProjectKind.Mobilization
                => Math.Max(0, pr.MilitaryPoints),
            ProjectKind.ColonyExpedition => double.MaxValue,
            _ => Math.Max(0, pr.DevelopmentPoints),
        };
    }

    internal static void SpendTreasury(SimState state, Project p, double amount)
    {
        var corp = state.CorporationOf(p.FunderActorId);
        if (corp != null) { corp.Credits -= amount; return; }
        var pr = state.PolityOf(p.FunderActorId);
        switch (p.Kind)
        {
            case ProjectKind.HullBatch:
            case ProjectKind.Mobilization: pr.MilitaryPoints -= amount; break;
            case ProjectKind.ColonyExpedition: break;
            default: pr.DevelopmentPoints -= amount; break;
        }
    }

    /// <summary>Return unspent bid escrow to the pool it came from — the
    /// reverse of SpendTreasury (contract-economy spec §2).</summary>
    internal static void RefundTreasury(SimState state, Project p,
                                        double amount)
        => SpendTreasury(state, p, -amount);

    private static PolityRecord? FunderPolity(SimState state, int actorId)
    {
        foreach (var pr in state.Polities)
            if (pr.ActorId == actorId) return pr;
        return null;
    }

    /// <summary>The completion payload (spec §1). Kind cases land across
    /// tasks 5–11; each stages its chronicle event.
    /// <paramref name="completionYear"/> is the interpolated delivery year
    /// inside the span (stage 2) — state stamps carry it; staged events
    /// still take Chronicle's step year (flagged, not built).</summary>
    public static void Complete(SimState state, Project p,
                                int completionYear = int.MinValue)
    {
        if (completionYear == int.MinValue) completionYear = state.WorldYear;
        p.Completed = true;
        // surplus in the laydown yard banks at the site (P4): the works
        // over-bought against a starved future that never came
        if (p.Kind != ProjectKind.ColonyExpedition)
        {
            var yard = state.Ports[p.PortId];
            for (int g = 0; g < p.DeliveredQty.Length; g++)
            {
                if (p.DeliveredQty[g] <= 0) continue;
                yard.DepositStock(g, p.DeliveredQty[g], p.DeliveredGrade[g]);
                p.DeliveredQty[g] = 0;
                p.DeliveredGrade[g] = 0;
            }
        }
        switch (p.Kind)
        {
            case ProjectKind.FacilityConstruction:
            {
                var f = state.Facilities[p.TargetId];
                f.CommissionedYear = completionYear;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.FacilityBuilt,
                    new[] { p.OwnerActorId }, f.Hex, Magnitude: f.Tier,
                    Valence: 1.0, EventVisibility.Regional,
                    new FacilityBuiltPayload(f.Id, f.TypeId, f.Tier)));
                break;
            }
            case ProjectKind.PortRaise:
            {
                var port = state.Ports[p.TargetId];
                port.Tier++;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.PortTierRaised,
                    new[] { p.OwnerActorId }, port.Hex,
                    Magnitude: port.Tier, Valence: 1.0,
                    EventVisibility.Regional,
                    new PortTierRaisedPayload(port.Id, port.Tier)));
                break;
            }
            case ProjectKind.GatePair:
            {
                var lane = state.Lanes[p.TargetId];
                state.Facilities[lane.GateAId].CommissionedYear = completionYear;
                state.Facilities[lane.GateBId].CommissionedYear = completionYear;
                state.Staged.Add(new StagedEvent(
                    ClockStratum.Generational, WorldEventType.LaneOpened,
                    new[] { p.OwnerActorId },
                    HexGrid.Round(
                        (state.Ports[lane.PortAId].Hex.Q
                         + state.Ports[lane.PortBId].Hex.Q) * 0.5,
                        (state.Ports[lane.PortAId].Hex.R
                         + state.Ports[lane.PortBId].Hex.R) * 0.5),
                    Magnitude: state.Facilities[lane.GateAId].Tier,
                    Valence: 1.0, EventVisibility.Regional,
                    new LaneOpenedPayload(lane.PortAId, lane.PortBId)));
                break;
            }
            case ProjectKind.HullBatch:
            {
                var design = state.Designs[p.TypeId];
                double grade = p.AccumGradeWeight > 0 ? p.AccumGrade : 0.5;
                var advanced = DesignRegistry.MaybeAdvanceMark(state, design,
                    grade, state.Ports[p.PortId].Hex);
                FleetOps.HomeFleet(state, p.OwnerActorId,
                    state.Ports[p.PortId]).AddHulls(advanced.Id, p.Count,
                                                    grade);
                state.PolityOf(p.OwnerActorId).HullsBuilt += p.Count;
                break;
            }
            case ProjectKind.ColonyExpedition:
                CompleteExpedition(state, p, completionYear);
                break;
            case ProjectKind.Mobilization:
            {
                var pr = state.PolityOf(p.OwnerActorId);
                pr.Mobilization = 1.0;
                break;
            }
        }
    }

    /// <summary>The colony expedition arrives (moved here from
    /// ResolutionPhase.TryFound — everything from the convoy's landing on):
    /// the port + market + colony segment (carrying the official line),
    /// founding facilities commissioned at birth (the expedition shipped the
    /// equipment — the existing convention), the convoy docked as the
    /// colony's first reserve fleet with its colony hull scrapped, the
    /// PortEstablished chronicle, the founder mint, and the encroachment
    /// tension bumps. Failed founding: if the hex gained a port mid-flight,
    /// the convoy simply turns back to its staging port — no port, no event
    /// this slice.</summary>
    private static void CompleteExpedition(SimState state, Project p,
                                           int completionYear)
    {
        var cfg = state.Config;
        var record = state.PolityOf(p.OwnerActorId);
        var convoy = p.TargetId >= 0 ? state.Fleets[p.TargetId] : null;
        foreach (var existing in state.Ports)            // id order (P6)
            if (existing.Hex.Equals(p.Hex))
            {
                // the target was colonized while the convoy flew: turn back.
                // the settlers' stake returns with them — refund the colony
                // cost charged at dispatch so the ledger conserves (P4); the
                // colony segment that would have absorbed it is never born
                record.ExpansionPoints += cfg.Expansion.ColonyCost;
                // the founding kit comes home too (stage 2 — goods have a
                // transit home now): banked in the staging larder, overflow
                // to the shelf; the draw's grade blend is long gone, so the
                // system's neutral midpoint carries it
                var home = state.Ports[p.PortId];
                for (int g = 0; g < p.PerYearBasket.Length; g++)
                {
                    if (p.PerYearBasket[g] <= 0) continue;
                    double room = Math.Max(0,
                        MarketEngine.StockCapacityAt(state, home)
                        - home.StockQty[g]);
                    double banked = Math.Min(room, p.PerYearBasket[g]);
                    home.DepositStock(g, banked, 0.5);
                    if (p.PerYearBasket[g] - banked > 0)
                        // larder overflow goes up for sale as the owner's
                        // ask — the shelf is gone (contract economy)
                        BookOps.PostSupply(state, p.PortId, p.OwnerActorId,
                            g, p.PerYearBasket[g] - banked, 0.5);
                    p.PerYearBasket[g] = 0;
                }
                if (convoy != null)
                {
                    convoy.Posture = FleetPosture.Reserve;
                    convoy.HomePortId = p.PortId;
                    convoy.Hex = state.Ports[p.PortId].Hex;
                }
                return;
            }
        var actor = state.Actors[p.OwnerActorId];
        // the colony ship becomes the colony — the scrap counter follows
        // the CONVOY's owner: a staging-port conquest can transfer the
        // project while the fleet keeps its flag, and crediting the
        // conqueror double-books both ledgers (hull-ledger fix, slice CE)
        if (convoy != null)
            foreach (var g in convoy.Hulls)              // design-id order
                if (state.Designs[g.DesignId].Role == ShipRole.Colony)
                {
                    convoy.RemoveHulls(g.DesignId, 1);
                    state.PolityOf(convoy.OwnerActorId).HullsScrapped++;
                    break;
                }
        // the founding stamps carry the interpolated ARRIVAL year (review
        // fix 7) — the crossing took its years inside the span
        var port = new Port(state.Ports.Count, p.OwnerActorId, p.Hex,
                            tier: 1, completionYear);
        state.Ports.Add(port);
        state.Markets.Add(new Market(port.Id, cfg.Economy));
        var colonySegment = new PopulationSegment(state.Segments.Count, port.Id,
            record.SpeciesId, record.SpeciesId, cfg.Expansion.ColonySegmentSize)
        {
            // the expedition cost recycles to the settlers (P4)
            Wealth = cfg.Expansion.ColonyCost,
        };
        // settlers sent by the state carry the official line (slice G)
        if (record.Interior != null)
            for (int ax = 0; ax < 4; ax++)
                colonySegment.Ideology[ax] = record.Interior.OfficialIdeology[ax];
        state.Segments.Add(colonySegment);
        // the expedition ships the equipment for what it came for: the
        // founding facility matches the site's best extraction potential,
        // plus a subsistence farm when that isn't farming
        var founding = FoundingIndustry(state, p.Hex);
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)founding, tier: 1, p.Hex, p.OwnerActorId, completionYear));
        if (founding != Substrate.InfraTypeId.AgriComplex)
            state.Facilities.Add(new Facility(state.Facilities.Count,
                (int)Substrate.InfraTypeId.AgriComplex, tier: 1, p.Hex,
                p.OwnerActorId, completionYear));
        // the convoy's survivors dock as the colony's first reserve fleet
        if (convoy != null)
        {
            convoy.Posture = FleetPosture.Reserve;
            convoy.HomePortId = port.Id;
            convoy.Hex = p.Hex;
        }
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.PortEstablished,
            new[] { p.OwnerActorId }, p.Hex, Magnitude: 1.0, Valence: 1.0,
            EventVisibility.Public, new PortEstablishedPayload(actor.Name, port.Id)));
        // a founding convoy mints its founder (characters.md §Notables)
        CharacterOps.MintNotable(state, p.OwnerActorId, NotableType.Founder,
                                 p.Hex);
        // settling into someone's sphere is a provocation: every entangled
        // neighbor's gauge jumps now (slice H — expansion carries risk)
        foreach (var other in state.Ports)               // id order (P6)
        {
            if (other.OwnerActorId == p.OwnerActorId
                || !state.Actors[other.OwnerActorId].Entered) continue;
            if (HexGrid.Distance(other.Hex, p.Hex)
                > PortDomains.ServiceRadius(cfg, 1)
                  + PortDomains.ServiceRadius(cfg, other.Tier)
                  + TechOps.AstroRadiusBonus(state, other.OwnerActorId))
                continue;
            var relation = state.RelationOf(p.OwnerActorId, other.OwnerActorId);
            if (relation != null)
                relation.Tension = Math.Min(1.0, relation.Tension
                    + cfg.Relations.EncroachmentTensionBump);
        }
    }

    /// <summary>The extraction type matching the colony site's strongest
    /// potential. Food security carries a premium: extraction wins only when
    /// it clearly out-values the farmland — otherwise settlers farm. (Moved
    /// from ResolutionPhase in Task 9; the founding body lives here now.)</summary>
    private static Substrate.InfraTypeId FoundingIndustry(SimState state,
                                                          HexCoordinate target)
    {
        var fields = MarketEngine.FieldsAt(state, target);
        var best = Substrate.InfraTypeId.AgriComplex;
        double bar = Substrate.Potentials.Biosphere(fields)
                     * state.Config.Infrastructure.FoodSecurityPremium;
        if (Substrate.Potentials.Ore(fields) > bar)
        { best = Substrate.InfraTypeId.Mine; bar = Substrate.Potentials.Ore(fields); }
        if (Substrate.Potentials.Volatiles(fields) > bar)
        { best = Substrate.InfraTypeId.Skimmer; bar = Substrate.Potentials.Volatiles(fields); }
        if (Substrate.Potentials.Exotics(fields) > bar)
            best = Substrate.InfraTypeId.ExcavationSite;
        return best;
    }

    /// <summary>A cancelled site is residue with a date and an owner of
    /// record (P1) — sunk goods stay sunk; the next replan simply stops
    /// feeding it.</summary>
    public static void Cancel(SimState state, Project p)
    {
        p.Cancelled = true;
        // the laydown yard doesn't evaporate (P4): abandoned materials bank
        // in the site port's larder — salvage goes to whoever holds the
        // ground, part of the abandoned-works residue (P1)
        var site = state.Ports[p.PortId];
        for (int g = 0; g < p.DeliveredQty.Length; g++)
        {
            if (p.DeliveredQty[g] <= 0) continue;
            site.DepositStock(g, p.DeliveredQty[g], p.DeliveredGrade[g]);
            p.DeliveredQty[g] = 0;
            p.DeliveredGrade[g] = 0;
        }
    }
}
