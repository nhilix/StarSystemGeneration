using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H task 7 — termination and settlement (war.md
/// §Termination, §Aftermath): a polity breaks when its politics break,
/// settlements read per-objective outcomes, reparations conserve, white
/// peace restores, and the residue (claims, veterans, legitimacy, tension
/// relief) is first-class.</summary>
public class WarResolutionTests
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

    private static War Declare(SimState state, int attacker, int defender,
                               WarDemand demand, params WarObjectiveSpec[] specs)
        => WarOps.DeclareWar(state, new DeclareWarAct(attacker, defender,
            (int)CasusBelli.BorderIncident, -1, specs, (int)demand))!;

    // Numeraire-weighted money total (currency-and-FX design): with real FX live,
    // summing NATIVE amounts across currencies is not conserved, but the numeraire
    // VALUE is — a conversion scales by rateA/rateB, so amount·rate is invariant.
    // Reparations move the loser's balance into the victor's own currency
    // converted, so this numeraire total is what holds across the settlement.
    private static double TotalCredits(SimState state)
    {
        double sum = 0;
        foreach (var p in state.Polities)
            sum += p.Credits * state.NumeraireRateOf(p.CurrencyId);
        foreach (var c in state.Corporations)
            sum += c.Credits;   // already numeraire (wallet total)
        foreach (var s in state.Segments)
            sum += s.Wealth * state.NumeraireRateOf(state.LocalCurrencySafe(s.PortId));
        foreach (var f in state.Factions)
        {
            int cur = -1;
            foreach (var p in state.Polities)
                if (p.ActorId == f.PolityId) { cur = p.CurrencyId; break; }
            sum += f.Wealth * state.NumeraireRateOf(cur);
        }
        return sum;
    }

    [Fact]
    public void WarScore_ReadsProgressAndExhaustion()
    {
        var state = Run();
        // a polity at peace scores neutral (histories shift with tuning —
        // find one)
        foreach (var pr in state.Polities)
            if (state.Actors[pr.ActorId].Entered
                && !WarOps.AtWar(state, pr.ActorId))
            {
                Assert.Equal(0.5, WarResolution.WarScore(state, pr.ActorId));
                break;
            }
        PolityRelation? rel = null;
        foreach (var r in state.Relations)
            if (RelationsOps.BothLive(state, r)
                && !WarOps.AtWar(state, r.PolityAId)
                && !WarOps.AtWar(state, r.PolityBId))
            { rel = r; break; }
        Assert.NotNull(rel);
        double attackerBefore = WarResolution.WarScore(state, rel!.PolityAId);
        double defenderBefore = WarResolution.WarScore(state, rel.PolityBId);
        var war = Declare(state, rel.PolityAId, rel.PolityBId,
            WarDemand.Reparations);
        war.AttackerExhaustion = 0.6;
        Assert.True(WarResolution.WarScore(state, rel.PolityAId)
                    < attackerBefore, "a grinding war saps the throne");
        war.Objectives[0].Status = ObjectiveStatus.Taken;
        war.AttackerExhaustion = 0.0;
        Assert.True(WarResolution.WarScore(state, rel.PolityAId)
                    > attackerBefore, "winning steadies it");
        Assert.True(WarResolution.WarScore(state, rel.PolityBId)
                    < defenderBefore,
            "the defender watches its objectives fall");
    }

    [Fact]
    public void Exhaustion_BreaksASide_WhitePeaceRestores()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        Port? target = null;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == rel.PolityBId) { target = p; break; }
        var war = Declare(state, rel.PolityAId, rel.PolityBId,
            WarDemand.CedeObjectives,
            new WarObjectiveSpec(WarObjectiveType.CapturePort, target!.Id));
        // the attacker captured it, then wore out
        target.OwnerActorId = rel.PolityAId;
        war.Objectives[0].Status = ObjectiveStatus.Taken;
        war.AttackerExhaustion = 1.0;
        // an in-flight project the conqueror broke ground on at the captured
        // port — its ownership must revert with the port (F6: the revert
        // routes through WarConduct.TransferPort)
        var proj = ProjectOps.Spawn(state, ProjectKind.PortRaise,
            rel.PolityAId, rel.PolityAId, target.Id, target.Hex, 5.0,
            ProjectPriority.Core, 0);
        proj.TargetId = target.Id;
        WarResolution.Terminate(state, null);
        Assert.False(war.Active);
        // white peace: status quo ante — the capture returned
        Assert.Equal(rel.PolityBId, target.OwnerActorId);
        // and its in-flight work reverted with it (F6)
        Assert.Equal(rel.PolityBId, proj.OwnerActorId);
        Assert.Equal(rel.PolityBId, proj.FunderActorId);
        Assert.Contains(state.Staged, e =>
            e.Type == WorldEventType.PeaceSettled
            && e.Payload is PeaceSettledPayload p
            && p.Outcome == (int)WarOutcome.WhitePeace);
    }

    [Fact]
    public void Victory_CedesTheTaken_AndLeavesAGrudge()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        Port? target = null;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == rel.PolityBId) { target = p; break; }
        var war = Declare(state, rel.PolityAId, rel.PolityBId,
            WarDemand.CedeObjectives,
            new WarObjectiveSpec(WarObjectiveType.CapturePort, target!.Id));
        target.OwnerActorId = rel.PolityAId;
        war.Objectives[0].Status = ObjectiveStatus.Taken;
        double legitimacyBefore =
            state.PolityOf(rel.PolityBId).Interior!.Legitimacy;
        WarResolution.Terminate(state, null);
        Assert.False(war.Active);
        Assert.Equal(rel.PolityAId, target.OwnerActorId);   // the cession holds
        Assert.True(rel.HasLiveClaim(ClaimType.LostTerritory, rel.PolityBId,
            target.Id), "cessions persist as standing claims");
        Assert.True(state.PolityOf(rel.PolityBId).Interior!.Legitimacy
                    < legitimacyBefore, "defeat cracks the throne");
    }

    [Fact]
    public void Reparations_Conserve()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        var war = Declare(state, rel.PolityAId, rel.PolityBId,
            WarDemand.Reparations);
        war.Objectives[0].Status = ObjectiveStatus.Taken;   // navy broken
        state.PolityOf(rel.PolityBId).Credits =
            System.Math.Max(100, state.PolityOf(rel.PolityBId).Credits);
        double before = TotalCredits(state);
        WarResolution.Terminate(state, null);
        Assert.False(war.Active);
        Assert.Equal(before, TotalCredits(state), 6);
        // select THIS war's settlement by id — the FX-shifted history can carry a
        // second war that Terminate settles in the same pass (its own PeaceSettled
        // event), so a bare First() could grab the wrong one.
        var settlement = state.Staged
            .Where(e => e.Type == WorldEventType.PeaceSettled)
            .Select(e => (PeaceSettledPayload)e.Payload!)
            .First(p => p.WarId == war.Id);
        Assert.True(settlement.Reparations > 0);
    }

    [Fact]
    public void VassalizeDemand_BindsTheLoser()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        var war = Declare(state, rel.PolityAId, rel.PolityBId,
            WarDemand.Vassalize);
        war.Objectives[0].Status = ObjectiveStatus.Taken;
        WarResolution.Terminate(state, null);
        Assert.False(war.Active);
        Assert.Equal(rel.PolityBId, rel.VassalPolityId);
        Assert.Equal(rel.PolityAId,
            FederationOps.OverlordOf(state, rel.PolityBId));
    }

    [Fact]
    public void Concession_SettlesTheWar()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        var war = Declare(state, rel.PolityAId, rel.PolityBId,
            WarDemand.Reparations);
        var concessions = new System.Collections.Generic
            .HashSet<(int, int)> { (war.Id, rel.PolityAId) };
        WarResolution.Terminate(state, concessions);
        Assert.False(war.Active);   // the attacker sued: white peace
    }

    [Fact]
    public void Demobilization_SendsTheFleetsHome()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        Port? target = null;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == rel.PolityBId) { target = p; break; }
        var war = Declare(state, rel.PolityAId, rel.PolityBId,
            WarDemand.CedeObjectives,
            new WarObjectiveSpec(WarObjectiveType.CapturePort, target!.Id));
        WarConduct.FightWars(state);   // mobilize
        Assert.Contains(state.Fleets, f =>
            f.OwnerActorId == rel.PolityAId
            && f.Posture == FleetPosture.Blockade
            && f.TargetId == target.Id);
        war.AttackerExhaustion = 1.0;
        WarResolution.Terminate(state, null);
        Assert.DoesNotContain(state.Fleets, f =>
            f.OwnerActorId == rel.PolityAId
            && f.Posture == FleetPosture.Blockade
            && f.TargetId == target.Id && f.TotalHulls > 0);
    }

    [Fact]
    public void FullHistory_WarsStartANDEnd()
    {
        var state = Run(40);
        int declared = state.Log.Events.Count(e =>
            e.Type == WorldEventType.WarDeclared);
        int settled = state.Log.Events.Count(e =>
            e.Type == WorldEventType.PeaceSettled);
        Assert.True(declared > 0, "no wars in a 40-epoch history");
        Assert.True(settled > 0, "wars start but never end");
        // no permanent galaxy-wide war: most pairs at peace at the end
        int activeWars = state.Wars.Count(w => w.Active);
        int livePairs = state.Relations.Count(r =>
            RelationsOps.BothLive(state, r));
        Assert.True(livePairs == 0 || activeWars <= livePairs / 2,
            $"{activeWars} active wars over {livePairs} live pairs");
    }
}
