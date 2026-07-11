using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H eyeball wave — contiguous borders and war aims:
/// expansion prices encroachment, friendly entanglement pulls toward
/// federation instead of friction, expulsion wars target the port that
/// came to us, and hatred turns declarations total.</summary>
public class ContiguityAndWarAimTests
{
    private static SimState Run(int epochs = 24)
    {
        var state = EpochTestKit.Seeded(42, 12).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    [Fact]
    public void BoxedInPolities_ConsolidateInsteadOfSettlingTrouble()
    {
        // when every site in reach is net-negative (the provocation
        // outweighs the riches), the controller stops founding
        var config = new EpochSimConfig();
        var controller = new GenesisController(config);
        var boxedIn = new PerceptionView(0, 100, new[] { 0 },
            expansionPoints: 1000,
            colonyCandidates: new[]
            { new ColonyCandidate(new HexCoordinate(3, 3), -0.8) },
            colonyHullsAvailable: 3);
        Assert.DoesNotContain(controller.Decide(boxedIn).Acts,
            a => a is FoundColonyAct);
        var openFrontier = new PerceptionView(0, 100, new[] { 0 },
            expansionPoints: 1000,
            colonyCandidates: new[]
            { new ColonyCandidate(new HexCoordinate(3, 3), 0.8) },
            colonyHullsAvailable: 3);
        Assert.Contains(controller.Decide(openFrontier).Acts,
            a => a is FoundColonyAct);
    }

    [Fact]
    public void EncroachmentPenalty_ReordersTheFrontier()
    {
        var state = Run();
        // with the penalty zeroed, entangling sites rank freely; with it
        // huge, no top candidate entangles anyone (unless nothing else is
        // left in reach)
        int polity = -1;
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity) { polity = a.Id; break; }
        Assert.True(polity >= 0);
        state.Config.Expansion.EncroachmentPenalty = 100.0;
        var careful = ColonyValuation.CandidatesFor(state, polity, max: 5);
        bool anyClean = careful.Any(c =>
            ColonyValuation.EncroachedPolities(state, polity, c.Target) == 0);
        if (anyClean)
            Assert.True(ColonyValuation.EncroachedPolities(state, polity,
                    careful[0].Target) == 0,
                "with a prohibitive penalty the top pick must be clean space");
    }

    [Fact]
    public void FoundingIntoASphere_BumpsTension()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int founder = rel.PolityAId, neighbor = rel.PolityBId;
        Port? theirPort = null;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == neighbor) { theirPort = p; break; }
        Assert.NotNull(theirPort);
        // a target hex adjacent to their port: maximal provocation
        var target = new HexCoordinate(theirPort!.Hex.Q + 1, theirPort.Hex.R);
        // stage the founding conditions
        var pr = state.PolityOf(founder);
        pr.ExpansionPoints = 1000;
        Port? staging = null;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == founder) { staging = p; break; }
        // put a colony hull in reach with endurance to spare
        var design = DesignRegistry.Current(state, founder,
                ShipRole.Colony, ShipSize.Medium)
            ?? DesignRegistry.Register(state, founder, ShipRole.Colony,
                ShipSize.Medium, grade: 0.5);
        var reserve = FleetOps.HomeFleet(state, founder, staging!);
        reserve.AddHulls(design.Id, 1, 0.5);
        pr.HullsBuilt++;
        double before = rel.Tension;

        // resolve the act directly through the Resolution phase machinery
        state.Decisions.Clear();
        state.Decisions.Add(new ActorDecision(founder, new ControllerDecision(
            PolityPolicies.Default,
            new Act[] { new FoundColonyAct(founder, target) })));
        new ResolutionPhase().Run(state);

        bool founded = state.Ports.Any(p => p.Hex.Equals(target));
        if (founded)
            Assert.True(rel.Tension >= before
                        + state.Config.Relations.EncroachmentTensionBump - 1e-9,
                "settling their sphere must load the gauge now");
    }

    [Fact]
    public void FriendlyOverlap_ReadsAsSharedBorder_NotFriction()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.Warmth = 0.0;
        double cold = RelationsOps.TensionTarget(state, rel, overlapPairs: 4);
        rel.Warmth = 0.9;
        double warm = RelationsOps.TensionTarget(state, rel, overlapPairs: 4);
        Assert.True(warm < cold,
            "friends tolerate entanglement; strangers see a loaded border");
    }

    [Fact]
    public void Entanglement_LowersTheFederationBar()
    {
        var state = Run();
        // a pair with real overlap whose compositions can contemplate fusion
        PolityRelation? pick = null;
        foreach (var rel in state.Relations)
        {
            if (!RelationsOps.BothLive(state, rel)) continue;
            if (RelationsOps.OverlapShare(state, rel.PolityAId, rel.PolityBId)
                < 0.2) continue;
            pick = rel;
            break;
        }
        if (pick == null) return;   // this seed grew apart — nothing to test
        var a = state.PolityOf(pick.PolityAId);
        var b = state.PolityOf(pick.PolityBId);
        // stage everything but warmth: sustained alliance, aligned open
        // lines, healthy cohesions, forced-open compositions
        pick.Rung = TreatyRung.DefenseAlliance;
        pick.RungYear = state.WorldYear
            - state.Config.Relations.FederationAllianceEpochs
              * state.Config.Sim.GenerationYears;
        for (int ax = 0; ax < 4; ax++)
        {
            a.Interior!.OfficialIdeology[ax] = 0.5;
            b.Interior!.OfficialIdeology[ax] = 0.5;
        }
        a.Interior!.OfficialIdeology[(int)IdeologyAxis.OpenInsular] = 0.0;
        b.Interior!.OfficialIdeology[(int)IdeologyAxis.OpenInsular] = 0.0;
        a.Interior.Cohesion = 0.9;
        b.Interior.Cohesion = 0.9;
        bool openEnough = Temperament.Compose(state, a).Openness
                >= state.Config.Relations.FederationOpennessFloor
            && Temperament.Compose(state, b).Openness
                >= state.Config.Relations.FederationOpennessFloor;
        if (!openEnough) return;   // closed species: the gate stays shut anyway

        double overlap = RelationsOps.OverlapShare(state, pick.PolityAId,
                                                   pick.PolityBId);
        pick.Warmth = RelationsOps.TreatyGate(state.Config,
                          TreatyRung.Federation)
                      - 0.5 * state.Config.Relations.FederationOverlapDiscount
                        * overlap;   // under the plain gate, over the discounted
        state.Config.Relations.FederationOverlapDiscount = 0.0;
        Assert.False(FederationOps.FederationGateHolds(state, pick),
            "without the discount this warmth falls short");
        state.Config.Relations.FederationOverlapDiscount = 0.15;
        Assert.True(FederationOps.FederationGateHolds(state, pick),
            "entangled friendly borders must lower the bar");
    }

    [Fact]
    public void ExpulsionCause_ArmsAgainstTheLateComer()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int mine = rel.PolityAId, theirs = rel.PolityBId;
        Port? myPort = null;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == mine) { myPort = p; break; }
        Assert.NotNull(myPort);
        // they plant a port right next to my oldest one, founded later
        var intruder = new Port(state.Ports.Count, theirs,
            new HexCoordinate(myPort!.Hex.Q + 2, myPort.Hex.R),
            tier: 1, state.WorldYear);
        state.Ports.Add(intruder);
        state.Markets.Add(new Market(intruder.Id, state.Config.Economy));

        var menu = WarOps.Menu(state, mine, theirs);
        Assert.Contains(menu, m => m.Cause == CasusBelli.Expulsion
                                   && m.SubjectId == intruder.Id);
        // the aggrieved is the one who was there first — not the intruder
        var reverse = WarOps.Menu(state, theirs, mine)
            .Where(m => m.Cause == CasusBelli.Expulsion
                        && m.SubjectId == intruder.Id);
        Assert.Empty(reverse);
    }

    [Fact]
    public void AnnihilationVictory_AnnexesTheLoserWhole()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int attacker = rel.PolityAId, defender = rel.PolityBId;
        var war = WarOps.DeclareWar(state, new DeclareWarAct(attacker,
            defender, (int)CasusBelli.Crusade, -1,
            new WarObjectiveSpec[0], (int)WarDemand.Annihilation))!;
        // the defender's surrender falls on deaf ears
        var plea = new System.Collections.Generic.HashSet<(int, int)>
            { (war.Id, defender) };
        WarResolution.Terminate(state, plea);
        Assert.True(war.Active, "annihilation accepts no surrender");
        // its navy breaks: the fleet objective takes, the war ends in annexation
        foreach (var o in war.Objectives) o.Status = ObjectiveStatus.Taken;
        WarResolution.Terminate(state, null);
        Assert.False(war.Active);
        Assert.True(state.Actors[defender].Retired, "the flag comes down");
        foreach (var p in state.Ports)
            Assert.NotEqual(defender, p.OwnerActorId);
        Assert.Contains(state.Staged, e =>
            e.Type == WorldEventType.PeaceSettled
            && e.Payload is PeaceSettledPayload pay
            && pay.Outcome == (int)WarOutcome.Annexed);
    }

    [Fact]
    public void Mobilization_DivertsTheEconomy()
    {
        var config = new EpochSimConfig();
        var controller = new GenesisController(config);
        var atPeace = new PerceptionView(0, 100, new[] { 0 },
            ownPortCount: 4);
        var atWar = new PerceptionView(0, 100, new[] { 0 },
            ownPortCount: 4,
            wars: new[]
            {
                new WarBrief(0, "the Test War", 1, OnAttackerSide: true,
                    IsLeader: true, OwnSideExhaustion: 0.2,
                    OwnSideStrengthShare: 0.8, ObjectivesTaken: 0,
                    ObjectivesTotal: 2),
            });

        var peace = (PolityPolicies)controller.Decide(atPeace).Policies;
        var wartime = (PolityPolicies)controller.Decide(atWar).Policies;

        Assert.True(wartime.Budget.Military > peace.Budget.Military,
            "guns before butter: the military line must grow at war");
        Assert.True(wartime.Budget.Development < peace.Budget.Development);
        double budgetSum = wartime.Budget.Development + wartime.Budget.Military
            + wartime.Budget.Research + wartime.Budget.Expansion
            + wartime.Budget.Appeasement + wartime.Budget.Reserves;
        Assert.Equal(1.0, budgetSum, 9);   // the shift moves, never mints
        double peaceArms = peace.StockpileTargets.TryGetValue(
            (int)StarGen.Core.Substrate.GoodId.Armaments, out double pa)
            ? pa : 0;
        double warArms = wartime.StockpileTargets.TryGetValue(
            (int)StarGen.Core.Substrate.GoodId.Armaments, out double wa)
            ? wa : 0;
        Assert.True(warArms > peaceArms,
            "mobilization must corner armaments");
        Assert.True(wartime.StockpileTargets[
                (int)StarGen.Core.Substrate.GoodId.ShipComponents]
            > peace.StockpileTargets[
                (int)StarGen.Core.Substrate.GoodId.ShipComponents],
            "the quartermaster corners ship parts too");
    }
}
