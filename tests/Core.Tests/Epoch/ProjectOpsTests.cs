using System;
using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class ProjectOpsTests
{
    /// <summary>A project fed 100% delivers the span's years; fed ~60%
    /// delivers ~60% of them (starvation semantics, spec §1).</summary>
    [Fact]
    public void Advance_StarvedProject_DeliversTheFedFraction()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int port = OwnPort(state, pr.ActorId);
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, port, state.Ports[port].Hex, yearsRequired: 50.0,
            ProjectPriority.Core, planOrder: 0);
        p.TargetId = port;
        p.PerYearBasket[(int)GoodId.Alloys] = 1.0;
        p.WagesPerYear = 0.0;
        int years = state.Config.Sim.YearsPerEpoch;              // 25
        // the laydown yard holds exactly 60% of the span's need; the site
        // larder stays bare (contract economy: bids fill the yard)
        p.DeliveredQty[(int)GoodId.Alloys] = 0.6 * years;
        p.DeliveredGrade[(int)GoodId.Alloys] = 0.5;
        state.Ports[port].StockQty[(int)GoodId.Alloys] = 0;
        ProjectOps.AdvanceAll(state);
        Assert.Equal(0.6 * years, p.YearsDelivered, 6);
        Assert.Equal(0.6, p.LastFedFraction, 6);
        // conservation: goods removed from the yard equal goods delivered
        // into the work (basket × years delivered)
        Assert.Equal(0.0, p.DeliveredQty[(int)GoodId.Alloys], 6);
    }

    /// <summary>Spec §4b carried into the book world: draws are local-only
    /// — the works' laydown yard first, then the SITE port's own stockpile;
    /// a shortfall met from the local larder feeds the work, nothing
    /// teleports in.</summary>
    [Fact]
    public void Advance_DrawsFromTheSitePortStockpile()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int port = OwnPort(state, pr.ActorId);
        var site = state.Ports[port];
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, port, site.Hex, yearsRequired: 50.0,
            ProjectPriority.Core, planOrder: 0);
        p.TargetId = port;
        p.PerYearBasket[(int)GoodId.Alloys] = 1.0;
        p.WagesPerYear = 0.0;
        int years = state.Config.Sim.YearsPerEpoch;              // 25
        // 40% in the laydown yard, 60% in the port's own larder
        p.DeliveredQty[(int)GoodId.Alloys] = 0.4 * years;
        p.DeliveredGrade[(int)GoodId.Alloys] = 0.5;
        site.StockQty[(int)GoodId.Alloys] = 0.6 * years;
        site.StockGrade[(int)GoodId.Alloys] = 0.5;
        ProjectOps.AdvanceAll(state);
        Assert.Equal(years, p.YearsDelivered, 6);        // fully fed, locally
        Assert.Equal(0.0, p.DeliveredQty[(int)GoodId.Alloys], 6);
        Assert.Equal(0.0, site.StockQty[(int)GoodId.Alloys], 6);
    }

    /// <summary>Priority order: the War-class project drinks the shared
    /// market dry before the Growth-class one sees a unit (spec §4).</summary>
    [Fact]
    public void Advance_PriorityCascade_WarDrinksFirst()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int port = OwnPort(state, pr.ActorId);
        int years = state.Config.Sim.YearsPerEpoch;
        // one project's worth in the SHARED site larder — the cascade's
        // contested pool now that the anonymous shelf is gone
        state.Ports[port].StockQty[(int)GoodId.Fuel] = 1.0 * years;
        state.Ports[port].StockGrade[(int)GoodId.Fuel] = 0.5;
        var growth = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, port, state.Ports[port].Hex, 50.0,
            ProjectPriority.Growth, 0);
        growth.TargetId = port;
        growth.PerYearBasket[(int)GoodId.Fuel] = 1.0;
        var war = ProjectOps.Spawn(state, ProjectKind.Mobilization, pr.ActorId,
            pr.ActorId, port, state.Ports[port].Hex, 50.0,
            ProjectPriority.War, 0);
        war.PerYearBasket[(int)GoodId.Fuel] = 1.0;
        ProjectOps.AdvanceAll(state);
        Assert.Equal(years, war.YearsDelivered, 6);          // fully fed
        Assert.Equal(0.0, growth.YearsDelivered, 6);         // starved
    }

    /// <summary>Completion fires once: a PortRaise raises its port tier
    /// the step its years are delivered, never before.</summary>
    [Fact]
    public void Advance_PortRaise_CompletesAndRaisesTheTier()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        int tierBefore = state.Ports[portId].Tier;
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, portId, state.Ports[portId].Hex, yearsRequired: 5.0,
            ProjectPriority.Core, 0);
        p.TargetId = portId;                    // empty basket: time-only
        ProjectOps.AdvanceAll(state);           // 25y span covers 5y need
        Assert.True(p.Completed);
        Assert.Equal(tierBefore + 1, state.Ports[portId].Tier);
    }

    /// <summary>Mid-span scheduled starts only credit the overlap: a
    /// project scheduled 20 years into a 25-year span delivers 5.</summary>
    [Fact]
    public void Advance_MidSpanStart_CreditsOnlyTheOverlap()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        var p = ProjectOps.SpawnAt(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, portId, state.Ports[portId].Hex, yearsRequired: 50.0,
            ProjectPriority.Core, 0, startedYear: state.WorldYear + 20);
        p.TargetId = portId;
        ProjectOps.AdvanceAll(state);
        Assert.Equal(5.0, p.YearsDelivered, 6);
    }

    /// <summary>Groundbreaking spawns a project whose basket × years
    /// reconstructs the old lump cost (conservation, spec §2); the facility
    /// row exists uncommissioned from year one and only goes active once
    /// AdvanceAll delivers the full construction span.</summary>
    [Fact]
    public void FacilityConstruction_ConsumesOverYears_ThenCommissions()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        var market = state.Markets[portId];
        var def = StarGen.Core.Substrate.Infrastructure.Get(
            StarGen.Core.Substrate.InfraTypeId.Mine);
        // plenty of everything in the site larder, deep treasury
        foreach (var q in def.BuildCost)
            state.Ports[portId].DepositStock((int)q.Good,
                q.Quantity * 2, 0.5);
        pr.DevelopmentPoints += 1000;
        var candidate = new ConstructionCandidate(
            (int)StarGen.Core.Substrate.InfraTypeId.Mine,
            state.Ports[portId].Hex, portId, 1.0);
        var p = ProjectOps.SpawnFacilityConstruction(state, pr.ActorId,
            pr.ActorId, candidate, ProjectPriority.Core, 0);
        var f = state.Facilities[p.TargetId];
        Assert.Equal(-1, f.CommissionedYear);            // site, not facility
        Assert.False(MarketEngine.IsActive(state, f));
        // basket × years == the old lump (conservation invariant)
        foreach (var q in def.BuildCost)
            Assert.Equal(q.Quantity,
                p.PerYearBasket[(int)q.Good] * p.YearsRequired, 6);
        ProjectOps.AdvanceAll(state);                    // 25y span >= 2y build
        Assert.True(p.Completed);
        Assert.True(MarketEngine.IsActive(state, f));
    }

    /// <summary>Hull batches take years: the completion payload (hull
    /// commissioning) fires only once AdvanceAll delivers the full build
    /// span, never at spawn (spec's time-not-ticks discipline).</summary>
    [Fact]
    public void HullBatch_CommissionsHullsAtCompletion_NotBefore()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        ShipDesign? design = null;
        foreach (var d in state.Designs)
            if (d.OwnerActorId == pr.ActorId) { design = d; break; }
        Assert.NotNull(design);
        state.Ports[portId].DepositStock((int)GoodId.ShipComponents, 100, 0.5);
        state.Ports[portId].DepositStock((int)GoodId.Armaments, 100, 0.5);
        pr.MilitaryPoints += 1000;
        int built = pr.HullsBuilt;
        var p = ProjectOps.SpawnHullBatch(state, pr.ActorId, portId,
            design!, count: 2, ProjectPriority.Growth, 0);
        Assert.Equal(built, pr.HullsBuilt);          // nothing yet
        ProjectOps.AdvanceAll(state);                // span covers the build
        Assert.True(p.Completed);
        Assert.Equal(built + 2, pr.HullsBuilt);
    }

    /// <summary>A gate pair breaks ground with both gates uncommissioned and
    /// its lane dead; the lane only goes live once AdvanceAll delivers the
    /// full construction span (Task 9 — half a highway is no highway).</summary>
    [Fact]
    public void GatePair_LaneOpensOnlyWhenCommissioned()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        var own = new System.Collections.Generic.List<Port>();
        foreach (var port in state.Ports)
            if (port.OwnerActorId == pr.ActorId) own.Add(port);
        // guarantee two own ports without a mutual lane (the seed may leave
        // the first-wave polity with only its homeworld this early)
        if (own.Count < 2)
        {
            var home = own[0];
            var second = new Port(state.Ports.Count, pr.ActorId,
                new StarGen.Core.Model.HexCoordinate(home.Hex.Q + 6, home.Hex.R),
                tier: 1, state.WorldYear);
            state.Ports.Add(second);
            state.Markets.Add(new Market(second.Id, state.Config.Economy));
            own.Add(second);
        }
        var p = ProjectOps.SpawnGatePair(state, pr.ActorId, pr.ActorId,
            own[0], own[1], tier: 1, ProjectPriority.Core, 0);
        var lane = state.Lanes[p.TargetId];
        Assert.False(LaneMath.IsLive(state, lane));   // half a highway is none
        // the pair's bids pull toward both ends; the works share one
        // laydown yard (B1 approximation, flagged in the ledger) — stock
        // the yard with the whole pair basket
        p.DeliveredQty[(int)GoodId.Alloys] += 200;
        p.DeliveredQty[(int)GoodId.Machinery] += 200;
        p.DeliveredQty[(int)GoodId.Composites] += 200;
        p.DeliveredGrade[(int)GoodId.Alloys] = 0.5;
        p.DeliveredGrade[(int)GoodId.Machinery] = 0.5;
        p.DeliveredGrade[(int)GoodId.Composites] = 0.5;
        pr.DevelopmentPoints += 1000;
        ProjectOps.AdvanceAll(state);
        Assert.True(p.Completed);
        Assert.True(LaneMath.IsLive(state, lane));
    }

    /// <summary>Stage 2 residue: completion stamps carry the INTERPOLATED
    /// world-year — a 2-year build inside a 25-year span commissions in
    /// year 2 of the span, not at its start.</summary>
    [Fact]
    public void Complete_StampsTheInterpolatedYear_NotTheSpanStart()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int portId = OwnPort(state, pr.ActorId);
        var def = StarGen.Core.Substrate.Infrastructure.Get(
            StarGen.Core.Substrate.InfraTypeId.Mine);
        foreach (var q in def.BuildCost)
            state.Ports[portId].DepositStock((int)q.Good,
                q.Quantity * 2, 0.5);
        pr.DevelopmentPoints += 1000;
        var candidate = new ConstructionCandidate(
            (int)StarGen.Core.Substrate.InfraTypeId.Mine,
            state.Ports[portId].Hex, portId, 1.0);
        var p = ProjectOps.SpawnFacilityConstruction(state, pr.ActorId,
            pr.ActorId, candidate, ProjectPriority.Core, 0);
        ProjectOps.AdvanceAll(state);                    // fully fed
        Assert.True(p.Completed);
        var f = state.Facilities[p.TargetId];
        Assert.Equal(state.WorldYear + (int)System.Math.Round(p.YearsRequired),
                     f.CommissionedYear);
    }

    /// <summary>Stage 2 residue: the founding kit is sized to the link the
    /// crossing actually needs — a long link ships a tier-2/3 pair's
    /// basket, recorded on the expedition as cargo.</summary>
    [Fact]
    public void FoundingKit_IsTierScaled_AndRidesTheExpedition()
    {
        var (state, pr, port) = FleetFixture();
        pr.ExpansionPoints = state.Config.Expansion.ColonyCost * 2;
        pr.Credits += 100000;   // the kit is bought off the book now
        var gateDef = StarGen.Core.Substrate.Infrastructure.Get(
            StarGen.Core.Substrate.InfraTypeId.Gate);
        foreach (var q in gateDef.BuildCost)
            EpochTestKit.Stock(state, port.Id, (int)q.Good,
                q.Quantity * 100, 0.5);
        // a target beyond tier-1 gate reach: the link needs a higher tier
        int t1Reach = state.Config.Infrastructure.GateReachTier1Hexes;
        var candidates = ColonyValuation.CandidatesFor(state, pr.ActorId);
        StarGen.Core.Model.HexCoordinate? far = null;
        foreach (var c in candidates)
            if (StarGen.Core.Galaxy.HexGrid.Distance(port.Hex, c.Target)
                > t1Reach) { far = c.Target; break; }
        Assert.True(far.HasValue, "fixture needs a beyond-tier-1 candidate");
        state.Decisions.Add(new ActorDecision(pr.ActorId,
            new ControllerDecision(PolityPolicies.Default, new Act[]
            { new FoundColonyAct(pr.ActorId, far!.Value) })));

        new ResolutionPhase().Run(state);

        Project? expedition = null;
        foreach (var p in state.Projects)
            if (p.Kind == ProjectKind.ColonyExpedition) expedition = p;
        Assert.NotNull(expedition);
        int dist = StarGen.Core.Galaxy.HexGrid.Distance(port.Hex, far.Value);
        int tier = LaneMath.RequiredGateTier(state.Config, dist, 0);
        Assert.True(tier >= 2);
        double scale = StarGen.Core.Substrate.Production.TierCostFactor(tier);
        foreach (var q in gateDef.BuildCost)
            Assert.Equal(2.0 * q.Quantity * scale,
                expedition!.PerYearBasket[(int)q.Good], 6);
    }

    /// <summary>Stage 2 residue: a convoy that turns back brings the kit
    /// home — the staging larder banks it; sunk no more.</summary>
    [Fact]
    public void FailedExpedition_BringsTheKitHome()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int staging = OwnPort(state, pr.ActorId);
        var target = new StarGen.Core.Model.HexCoordinate(
            state.Ports[staging].Hex.Q + 12, state.Ports[staging].Hex.R);
        var p = ProjectOps.SpawnExpedition(state, pr.ActorId, staging,
            target, convoyFleetId: -1, offLaneHexes: 12);
        p.PerYearBasket[(int)GoodId.Alloys] = 8.0;       // the shipped kit
        // the hex gets taken mid-flight: the convoy must turn back
        state.Ports.Add(new Port(state.Ports.Count, pr.ActorId, target,
                                 tier: 1, state.WorldYear));
        state.Markets.Add(new Market(state.Ports.Count - 1,
                                     state.Config.Economy));
        double before = state.Ports[staging].StockQty[(int)GoodId.Alloys];

        ProjectOps.AdvanceAll(state);

        Assert.True(p.Completed);
        Assert.Equal(before + 8.0,
            state.Ports[staging].StockQty[(int)GoodId.Alloys], 6);
    }

    private static (SimState State, PolityRecord Polity, Port Port)
        FleetFixture()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Step(state);
        foreach (var a in state.Actors)
            if (a.Entered)
                foreach (var p in state.Ports)
                    if (p.OwnerActorId == a.Id)
                        return (state, state.PolityOf(a.Id), p);
        throw new Xunit.Sdk.XunitException("no polity entered after one epoch");
    }

    /// <summary>A colony expedition takes world-time: the founding body fires
    /// only when the off-lane crossing is delivered, never at dispatch — the
    /// port count is unchanged in flight and up by one on arrival.</summary>
    [Fact]
    public void Expedition_FoundsThePort_OnlyOnArrival()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int staging = OwnPort(state, pr.ActorId);
        int portsBefore = state.Ports.Count;
        var convoy = new FleetRecord(state.Fleets.Count, pr.ActorId,
            state.Ports[staging].Hex)
        { Posture = FleetPosture.Expedition, HomePortId = staging };
        state.Fleets.Add(convoy);
        var target = new StarGen.Core.Model.HexCoordinate(
            state.Ports[staging].Hex.Q + 12, state.Ports[staging].Hex.R);
        if (!ValidTarget(state, target))
        {
            var candidates = ColonyValuation.CandidatesFor(state, pr.ActorId);
            Assert.NotEmpty(candidates);
            target = candidates[0].Target;
        }
        var p = ProjectOps.SpawnExpedition(state, pr.ActorId, staging,
            target, convoy.Id, offLaneHexes: 12);
        Assert.True(p.YearsRequired > 0);
        // in flight: no port at the target, port count unchanged by our spawn
        Assert.Equal(portsBefore, state.Ports.Count);
        Assert.False(PortAt(state, target));
        ProjectOps.AdvanceAll(state);                  // 25y >= 12/6 = 2y
        Assert.True(p.Completed);
        Assert.True(PortAt(state, target));            // arrived → founded
        // review fix 7: the founding stamp is the interpolated ARRIVAL
        // year, not the span start — the crossing took its two years
        Assert.Equal(state.WorldYear + 2,
                     state.Ports[state.Ports.Count - 1].FoundedYear);
    }

    /// <summary>A fed Mobilization project completes and readiness reaches
    /// full — Task 4's completion path already ratchets pr.Mobilization to
    /// 1.0, so this assertion is expected to hold even before Task 10's own
    /// code lands; kept alongside the decay-path test below (spec §5).</summary>
    [Fact]
    public void Mobilization_RampsWithFeed_AndReachesFullReadiness()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int port = OwnPort(state, pr.ActorId);
        var p = ProjectOps.Spawn(state, ProjectKind.Mobilization,
            pr.ActorId, pr.ActorId, port, state.Ports[port].Hex,
            yearsRequired: 3.0, ProjectPriority.War, 0);
        p.PerYearBasket[(int)GoodId.Armaments] = 1.0;
        state.Ports[port].DepositStock((int)GoodId.Armaments, 100, 0.5);
        pr.MilitaryPoints += 100;
        ProjectOps.AdvanceAll(state);
        Assert.True(p.Completed);
        Assert.Equal(1.0, pr.Mobilization, 6);
    }

    /// <summary>The decay path Task 10 actually adds: AllocationPhase's
    /// per-polity SpawnMobilizations call demobilizes a peaceful polity's
    /// standing war economy by DemobilizationPerYear per world-year — at
    /// this knob's default (0.15/yr over a 25-year epoch) a full epoch
    /// always demobilizes fully, floored at zero (spec §5).</summary>
    [Fact]
    public void Mobilization_DecaysAtPeace_ByDemobilizationRate()
    {
        var state = EpochTestKit.Seeded().State;
        var actor = state.Actors[0];
        actor.Entered = true;
        var port = new Port(0, actor.Id, actor.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(port);
        state.Markets.Add(new Market(0, state.Config.Economy));
        int species = state.PolityOf(actor.Id).SpeciesId;
        state.Segments.Add(new PopulationSegment(0, 0, species, species, 3.0));
        var pr = state.PolityOf(actor.Id);
        pr.Credits = 100;
        pr.Mobilization = 1.0;
        Assert.False(WarOps.AtWar(state, actor.Id));       // peaceful (guard)
        int years = state.Config.Sim.YearsPerEpoch;
        double expected = Math.Max(0.0,
            1.0 - state.Config.War.DemobilizationPerYear * years);
        new AllocationPhase().Run(state);
        Assert.Equal(expected, pr.Mobilization, 6);
    }

    /// <summary>Conquest is site-anchored: a captured port carries its
    /// in-flight project to the new owner at whatever progress it had
    /// (spec §1) — the conqueror's next replan keeps or cancels it.</summary>
    [Fact]
    public void Conquest_TransfersInFlightProjects_AtCurrentProgress()
    {
        var (_, state) = EpochTestKit.Seeded();
        RunHistory(state);
        var pr = state.Polities[FirstEnteredPolity(state)];
        int port = OwnPort(state, pr.ActorId);
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, port, state.Ports[port].Hex, 5.0,
            ProjectPriority.Core, 0);
        p.TargetId = port;
        p.YearsDelivered = 2.0;
        // find any other entered polity to play conqueror
        int other = -1;
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity
                && a.Id != pr.ActorId) { other = a.Id; break; }
        if (other < 0) return;                            // seed-shaped: skip
        WarConduct.TransferPort(state, port, other);
        Assert.Equal(other, p.OwnerActorId);
        Assert.Equal(other, p.FunderActorId);
        Assert.Equal(2.0, p.YearsDelivered, 9);           // progress kept
    }

    private static bool PortAt(SimState state,
                               StarGen.Core.Model.HexCoordinate target)
    {
        foreach (var port in state.Ports)
            if (port.Hex.Equals(target)) return true;
        return false;
    }

    /// <summary>A colonizable hex: a real, non-void cell with no port yet.</summary>
    private static bool ValidTarget(SimState state,
                                    StarGen.Core.Model.HexCoordinate target)
    {
        if (!state.Skeleton.TryGetCell(
                StarGen.Core.Galaxy.HexGrid.CellOf(target), out var cell)
            || cell.IsVoid) return false;
        foreach (var port in state.Ports)
            if (port.Hex.Equals(target)) return false;
        return true;
    }

    /// <summary>Fixture adaptation (brief's tests assume ports/markets
    /// already exist; EpochTestKit.Seeded() enters polities only as
    /// history runs — spec §Genesis). A few epochs are enough for the
    /// first-wave polities to enter and found their homeworld port; run
    /// history BEFORE spawning the test's own project so AdvanceAll below
    /// only ever sees that one project in flight.</summary>
    internal static void RunHistory(SimState state)
    {
        state.Config.Sim.EpochCount = 3;
        new EpochEngine().Run(state);
    }

    internal static int FirstEnteredPolity(SimState state)
    {
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity) return a.Id;
        throw new Xunit.Sdk.XunitException("no entered polity");
    }

    internal static int OwnPort(SimState state, int actorId)
    {
        foreach (var port in state.Ports)
            if (port.OwnerActorId == actorId) return port.Id;
        throw new Xunit.Sdk.XunitException("no port");
    }
}
