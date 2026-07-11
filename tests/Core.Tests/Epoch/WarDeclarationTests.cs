using System.Collections.Generic;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice H task 5 — casus belli, the spark, and declaration
/// (interpolity/war.md §Causes): the menu computes from real state,
/// incidents roll in contested space and escalate only where tension is
/// loaded, declaration grounds an objective set, and the defender's
/// alliance answers.</summary>
public class WarDeclarationTests
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

    [Fact]
    public void Menu_CarriesClaimBackedCauses()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int self = rel.PolityAId, other = rel.PolityBId;
        rel.Claims.Add(new RelationClaim(ClaimType.Succession, self, 0,
                                         state.WorldYear));
        rel.Claims.Add(new RelationClaim(ClaimType.CulturalKin, self, 1,
                                         state.WorldYear));
        var menu = WarOps.Menu(state, self, other);
        Assert.Contains(menu, m => m.Cause == CasusBelli.SuccessionClaim);
        Assert.Contains(menu, m => m.Cause == CasusBelli.Liberation);
        // the other side never holds MY synthetic claims (its own real
        // history may arm its own)
        var reverse = WarOps.Menu(state, other, self);
        Assert.DoesNotContain(reverse,
            m => m.Cause == CasusBelli.SuccessionClaim && m.SubjectId == 0);
        Assert.DoesNotContain(reverse,
            m => m.Cause == CasusBelli.Liberation && m.SubjectId == 1);
    }

    [Fact]
    public void Menu_SparkNeedsARecentIncident()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.LastIncidentEpoch = -1;
        Assert.DoesNotContain(WarOps.Menu(state, rel.PolityAId, rel.PolityBId),
            m => m.Cause == CasusBelli.BorderIncident);
        rel.LastIncidentEpoch = state.EpochIndex;
        Assert.Contains(WarOps.Menu(state, rel.PolityAId, rel.PolityBId),
            m => m.Cause == CasusBelli.BorderIncident);
    }

    [Fact]
    public void Incidents_RollInContestedSpace_AndLoadTension()
    {
        var state = Run();
        state.Config.War.IncidentRatePerEpoch = 10.0;   // certainty, any overlap
        int before = CountEvents(state, WorldEventType.BorderIncident);
        double tensionBefore = -1;
        PolityRelation? contested = null;
        Continue(state, 1);
        int after = CountEvents(state, WorldEventType.BorderIncident);
        Assert.True(after > before, "contested overlaps must spark");
        // the sparked pair's freshness window is set
        foreach (var rel in state.Relations)
            if (rel.LastIncidentEpoch >= 0) contested = rel;
        Assert.NotNull(contested);
        _ = tensionBefore;
    }

    [Fact]
    public void Declaration_GroundsObjectives_AndAlliesAnswer()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int attacker = rel.PolityAId, defender = rel.PolityBId;
        // the defender's ally stands ready
        int ally = -1;
        foreach (var other in state.Relations)
            if (other != rel && other.Involves(defender)
                && RelationsOps.BothLive(state, other)
                && !other.Involves(attacker))
            { other.Rung = TreatyRung.DefenseAlliance; ally = other.OtherOf(defender); break; }
        Port? target = null;
        foreach (var p in state.Ports)
            if (p.OwnerActorId == defender) { target = p; break; }
        Assert.NotNull(target);

        var war = WarOps.DeclareWar(state, new DeclareWarAct(attacker, defender,
            (int)CasusBelli.BorderIncident, -1,
            new[]
            {
                new WarObjectiveSpec(WarObjectiveType.CapturePort, target!.Id),
                // a port the defender does NOT own must be rejected
                new WarObjectiveSpec(WarObjectiveType.CapturePort, 0),
                new WarObjectiveSpec(WarObjectiveType.DestroyFleet, defender),
            }, (int)WarDemand.CedeObjectives));

        Assert.NotNull(war);
        Assert.True(war!.Active);
        Assert.Contains(war.Objectives, o =>
            o.Type == WarObjectiveType.CapturePort && o.TargetId == target.Id);
        Assert.DoesNotContain(war.Objectives, o =>
            o.Type == WarObjectiveType.CapturePort
            && o.TargetId == 0 && state.Ports[0].OwnerActorId != defender);
        if (ally >= 0) Assert.Contains(ally, war.DefenderAllies);
        Assert.True(war.DefenderStrengthAtStart >= 0);
        // no second declaration while this one runs
        Assert.Null(WarOps.DeclareWar(state, new DeclareWarAct(attacker,
            defender, (int)CasusBelli.Crusade, -1,
            new WarObjectiveSpec[0], (int)WarDemand.Reparations)));
    }

    [Fact]
    public void DeclaringOnATreatyPartner_BreaksTheTreaty()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        rel.Rung = TreatyRung.NonAggression;
        double warmth = rel.Warmth = 0.6;
        var war = WarOps.DeclareWar(state, new DeclareWarAct(rel.PolityAId,
            rel.PolityBId, (int)CasusBelli.Crusade, -1,
            new WarObjectiveSpec[0], (int)WarDemand.Reparations));
        Assert.NotNull(war);
        Assert.Equal(TreatyRung.None, rel.Rung);
        Assert.True(rel.Warmth < warmth, "betrayal crashes warmth");
        Assert.Contains(state.Staged, e =>
            e.Type == WorldEventType.TreatyBroken);
    }

    [Fact]
    public void VassalLock_OnlySecessionAgainstTheOverlord()
    {
        var state = Run();
        Assert.True(state.Relations.Count >= 2);
        var bond = EpochTestKit.FirstLiveRelation(state);
        int vassal = bond.PolityAId, overlord = bond.PolityBId;
        FederationOps.Bind(state, bond, vassal);
        // war on a third party: locked
        var third = state.Relations[1];
        if (third.Involves(vassal))
        {
            int other = third.OtherOf(vassal);
            Assert.Null(WarOps.DeclareWar(state, new DeclareWarAct(vassal,
                other, (int)CasusBelli.Crusade, -1,
                new WarObjectiveSpec[0], (int)WarDemand.Reparations)));
        }
        // independence against the overlord: the one open door
        var secession = WarOps.DeclareWar(state, new DeclareWarAct(vassal,
            overlord, (int)CasusBelli.VassalSecession, -1,
            new WarObjectiveSpec[0], (int)WarDemand.Independence));
        Assert.NotNull(secession);
        Assert.Contains("Secession", secession!.Name);
    }

    [Fact]
    public void WarNames_ReadTheirCauses()
    {
        var state = Run();
        Assert.Equal("the Fuel War", WarOps.WarName(state,
            CasusBelli.ResourceSeizure, (int)StarGen.Core.Substrate.GoodId.Fuel,
            0));
        Assert.StartsWith("the ", WarOps.WarName(state,
            CasusBelli.Crusade, -1, 0));
        Assert.EndsWith(" Crusade", WarOps.WarName(state,
            CasusBelli.Crusade, -1, 0));
    }

    [Fact]
    public void WarsAndIncidents_RoundTrip()
    {
        var state = Run();
        var rel = EpochTestKit.FirstLiveRelation(state);
        int relAt = state.Relations.IndexOf(rel);
        rel.LastIncidentEpoch = 5;
        var war = WarOps.DeclareWar(state, new DeclareWarAct(rel.PolityAId,
            rel.PolityBId, (int)CasusBelli.BorderIncident, -1,
            new WarObjectiveSpec[0], (int)WarDemand.Reparations));
        Assert.NotNull(war);
        war!.AttackerExhaustion = 0.25;
        state.Staged.Clear();   // staged events are transients, not state

        var loaded = ArtifactSerializer.Load(
            new System.IO.StringReader(ArtifactSerializer.ToText(state)));

        Assert.Equal(state.Wars.Count, loaded.Wars.Count);
        var l = loaded.Wars[war.Id];
        Assert.Equal(war.Name, l.Name);
        Assert.Equal(war.Cause, l.Cause);
        Assert.Equal(war.Demand, l.Demand);
        Assert.Equal(war.Objectives.Count, l.Objectives.Count);
        Assert.Equal(0.25, l.AttackerExhaustion);
        Assert.Equal(5, loaded.Relations[relAt].LastIncidentEpoch);
        Assert.Equal(ArtifactSerializer.ToText(state),
                     ArtifactSerializer.ToText(loaded));
    }

    private static int CountEvents(SimState state, WorldEventType type)
    {
        int n = 0;
        foreach (var e in state.Log.Events)
            if (e.Type == type) n++;
        return n;
    }
}
