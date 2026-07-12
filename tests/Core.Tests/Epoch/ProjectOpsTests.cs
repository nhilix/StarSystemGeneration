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
        var market = state.Markets[port];
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, pr.ActorId,
            pr.ActorId, port, state.Ports[port].Hex, yearsRequired: 50.0,
            ProjectPriority.Core, planOrder: 0);
        p.TargetId = port;
        p.PerYearBasket[(int)GoodId.Alloys] = 1.0;
        p.WagesPerYear = 0.0;
        int years = state.Config.Sim.YearsPerEpoch;              // 25
        // stock exactly 60% of the span's need, wipe reserves
        market.Inventory[(int)GoodId.Alloys] = 0.6 * years;
        pr.ReserveQty[(int)GoodId.Alloys] = 0;
        ProjectOps.AdvanceAll(state);
        Assert.Equal(0.6 * years, p.YearsDelivered, 6);
        Assert.Equal(0.6, p.LastFedFraction, 6);
        Assert.Equal(0.0, market.Inventory[(int)GoodId.Alloys], 6);
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
        var market = state.Markets[port];
        market.Inventory[(int)GoodId.Fuel] = 1.0 * years;   // one project's worth
        pr.ReserveQty[(int)GoodId.Fuel] = 0;
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
        // plenty of everything, deep treasury
        foreach (var q in def.BuildCost)
            market.Inventory[(int)q.Good] += q.Quantity * 2;
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
        var market = state.Markets[portId];
        market.Inventory[(int)GoodId.ShipComponents] += 100;
        market.Inventory[(int)GoodId.Armaments] += 100;
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
        state.Markets[own[0].Id].Inventory[(int)GoodId.Alloys] += 100;
        state.Markets[own[0].Id].Inventory[(int)GoodId.Machinery] += 100;
        state.Markets[own[0].Id].Inventory[(int)GoodId.Composites] += 100;
        pr.DevelopmentPoints += 1000;
        ProjectOps.AdvanceAll(state);
        Assert.True(p.Completed);
        Assert.True(LaneMath.IsLive(state, lane));
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
        state.Markets[port].Inventory[(int)GoodId.Armaments] += 100;
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
