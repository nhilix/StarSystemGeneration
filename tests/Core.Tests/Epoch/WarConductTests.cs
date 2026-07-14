using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H task 6 — the theater/objective model (war.md §Conduct):
/// mobilization posts real fleets to objectives, engagements resolve on
/// vectors and conserve losses into wreckage, blockades sever real lanes,
/// and sieges end with ports transferring domains segments-intact.</summary>
public class WarConductTests
{
    private static SimState Run(int epochs = 24)
    {
        var state = EpochTestKit.Seeded(42, 12).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    private static void Continue(SimState state, int epochs)
    {
        state.Config.Sim.EpochCount = state.EpochIndex + epochs;
        new EpochEngine().Run(state);
    }

    /// <summary>Arm the attacker to the teeth and declare on the pair's
    /// relation — the controlled war every test rides.</summary>
    private static (War War, int Attacker, int Defender, Port Target)
        StageWar(SimState state, int lineHulls = 120)
    {
        var rel = EpochTestKit.FirstLiveRelation(state);
        int attacker = rel.PolityAId, defender = rel.PolityBId;
        var design = DesignRegistry.Current(state, attacker,
                ShipRole.Line, ShipSize.Medium)
            ?? DesignRegistry.Register(state, attacker, ShipRole.Line,
                ShipSize.Medium, grade: 0.6);
        Port? home = null;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == attacker) { home = p; break; }
        var reserve = FleetOps.HomeFleet(state, attacker, home!);
        reserve.AddHulls(design.Id, lineHulls, 0.6);
        state.PolityOf(attacker).HullsBuilt += lineHulls;

        Port? target = null;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == defender) { target = p; break; }
        var war = WarOps.DeclareWar(state, new DeclareWarAct(attacker,
            defender, (int)CasusBelli.BorderIncident, -1,
            new[]
            {
                new WarObjectiveSpec(WarObjectiveType.CapturePort, target!.Id),
            }, (int)WarDemand.CedeObjectives));
        return (war!, attacker, defender, target);
    }

    private static void AssertHullLedger(SimState state)
    {
        var active = new System.Collections.Generic.Dictionary<int, int>();
        foreach (var fleet in state.Fleets)
        {
            active.TryGetValue(fleet.OwnerActorId, out int n);
            active[fleet.OwnerActorId] = n + fleet.TotalHulls;
        }
        foreach (var pr in state.Polities)
        {
            active.TryGetValue(pr.ActorId, out int flying);
            Assert.True(pr.HullsBuilt
                        == flying + pr.HullsWrecked + pr.HullsScrapped,
                $"polity {pr.ActorId}: built {pr.HullsBuilt} != "
                + $"{flying} flying + {pr.HullsWrecked} wrecked + "
                + $"{pr.HullsScrapped} scrapped");
        }
    }

    [Fact]
    public void Mobilization_PostsWarFleets_AtObjectives()
    {
        var state = Run();
        var (war, attacker, _, target) = StageWar(state);
        WarConduct.FightWars(state);
        var warFleet = state.Fleets.FirstOrDefault(f =>
            f.OwnerActorId == attacker && f.Posture == FleetPosture.Blockade
            && f.TargetId == target.Id);
        Assert.NotNull(warFleet);
        Assert.True(warFleet!.TotalHulls > 0, "the war fleet must muster");
        Assert.Equal(target.Hex, warFleet.Hex);
        Assert.True(war.Active);
        AssertHullLedger(state);
    }

    [Fact]
    public void Blockade_SeversRealLanes()
    {
        var state = Run();
        var (_, _, _, target) = StageWar(state);
        WarConduct.FightWars(state);
        var severed = FleetOps.SeveredLaneIds(state);
        foreach (var lane in state.Lanes)
            if (lane.PortAId == target.Id || lane.PortBId == target.Id)
                Assert.Contains(lane.Id, severed);
    }

    [Fact]
    public void Engagements_ConserveLossesIntoWreckage()
    {
        var state = Run();
        StageWar(state);
        int wrecksBefore = state.Wreckage.Count;
        Continue(state, 3);
        Assert.True(state.Wreckage.Count > wrecksBefore,
            "battles must leave wreckage at real hexes");
        Assert.Contains(state.Log.Events,
            e => e.Type == WorldEventType.BattleFought);
        AssertHullLedger(state);
    }

    [Fact]
    public void Siege_FallsThePort_SegmentsIntact()
    {
        // a CONTROLLED siege needs a peacetime backdrop: this test verifies
        // the siege MECHANIC (port transfers, segments intact) over an
        // 8-epoch continuation, not emergent-war politics. Once the
        // world-time economy relit emergent wars (slice t1), epoch 24 sits in
        // the war era — a real war pulls in coalitions and the staged war
        // settles (white peace, status quo ante) before the siege can
        // complete. Staging at epoch 16, still peacetime for seed 42, gives
        // the clean stage the test was written for; the teeth are unchanged.
        var state = Run(16);
        var (war, attacker, defender, target) = StageWar(state, 300);
        double populationBefore = 0;
        foreach (var s in state.Segments)
            if (s.PortId == target.Id) populationBefore += s.Size;
        Continue(state, 8);
        // the port fell (later schisms/mergers may have moved it again —
        // the capture event is the record)
        Assert.NotEqual(defender, state.Ports[target.Id].OwnerActorId);
        Assert.Contains(state.Log.Events,
            e => e.Type == WorldEventType.SiegeBegun);
        Assert.Contains(state.Log.Events,
            e => e.Type == WorldEventType.PortCaptured);
        // conquest composition is automatic: the people stayed put
        double populationAfter = 0;
        foreach (var s in state.Segments)
            if (s.PortId == target.Id) populationAfter += s.Size;
        Assert.True(populationAfter > 0, "captured ports keep their people");
        var objective = war.Objectives.First(o =>
            o.Type == WarObjectiveType.CapturePort);
        Assert.Equal(ObjectiveStatus.Taken, objective.Status);
        _ = attacker;
        _ = populationBefore;
    }

    [Fact]
    public void SiegeThreshold_ReadsLarderAndFortress()
    {
        var state = Run();
        var (war, _, defender, target) = StageWar(state);
        int prov = (int)StarGen.Core.Substrate.GoodId.Provisions;
        // isolate the port's OWN stockpile as the larder: start from an empty
        // book so `bare` is a true zero-larder floor. Post-slice-ME seed 42
        // leaves this small port's population low enough that even the
        // provisions already resting on its book saturate the larder cap on
        // their own — measuring the stockpile's contribution needs a clean base.
        foreach (var o in state.Orders.ToArray())
            if (o.PortId == target.Id && o.Side == OrderSide.Sell && o.Good == prov)
                OrderOps.CancelSell(state, o);
        target.StockQty[prov] = 0;
        int bare = WarConduct.SiegeThreshold(state, war, target);
        // the defender port's OWN stockpile is the siege larder (spec §4b —
        // a rich polity pool elsewhere feeds nobody behind these walls)
        target.StockQty[prov] = 5e5;
        int stocked = WarConduct.SiegeThreshold(state, war, target);
        Assert.True(stocked > bare, "the port's stock must extend the siege");
        // provisions for sale on the local book hold out too
        EpochTestKit.Stock(state, target.Id,
            (int)StarGen.Core.Substrate.GoodId.Provisions, 1e6, 0.5);
        int fed = WarConduct.SiegeThreshold(state, war, target);
        Assert.True(fed > bare, "provisions must extend the siege");
        // fortress tiers extend it further
        state.Facilities.Add(new Facility(state.Facilities.Count,
            (int)StarGen.Core.Substrate.InfraTypeId.Fortress, tier: 2,
            target.Hex, defender, state.WorldYear));
        int fortified = WarConduct.SiegeThreshold(state, war, target);
        // thresholds are world-years now (P7): tiers add generations
        Assert.Equal(fed + 2 * state.Config.Sim.GenerationYears, fortified);
    }

    [Fact]
    public void Exhaustion_AccruesWithTheWar()
    {
        var state = Run();
        var (war, _, _, _) = StageWar(state);
        Continue(state, 4);
        Assert.True(war.AttackerExhaustion > 0);
        Assert.True(war.DefenderExhaustion > 0);
        Assert.InRange(war.AttackerExhaustion, 0, 1);
    }

    [Fact]
    public void FortressType_GatesOnMilitaryTier()
    {
        var state = Run();
        // nobody below Military tier 2 has built one; the type is in the
        // buildable set only past the gate — assert no under-tier owner
        foreach (var f in state.Facilities)
        {
            if (f.TypeId != (int)StarGen.Core.Substrate.InfraTypeId.Fortress)
                continue;
            Assert.True(state.PolityOf(f.OwnerActorId)
                    .TechTier[(int)TechDomain.Military] >= 2,
                "a fortress stands under an under-tiered owner");
        }
    }
}
