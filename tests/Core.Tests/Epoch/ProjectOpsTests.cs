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
