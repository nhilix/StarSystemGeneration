using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice G task 3: factions — six-basis formation from real state,
/// strength/militancy/grievance, budget pressure, appeasement as a conserved
/// treasury flow.</summary>
public class FactionTests
{
    [Fact]
    public void FullRun_FactionsForm_ButPolitiesKeepFunctioning()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        // factions exist (a 40-epoch history breeds real interests)...
        Assert.True(state.Factions.Count > 0, "no faction ever coalesced");
        foreach (var f in state.Factions)
        {
            Assert.InRange(f.Strength, 0.0, 1.0);
            Assert.InRange(f.Militancy, 0.0, 1.0);
            Assert.True(f.Grievance >= 0);
            Assert.True(f.Wealth >= 0);
            Assert.Equal(0.0, f.PaidThisEpoch);   // never crosses an epoch
            // every faction has a leader character; an ACTIVE faction's
            // leader still belongs to its polity (a graduated one's may
            // rule the schism state it founded — slice G task 5)
            Assert.True(f.LeaderCharacterId >= 0, $"faction {f.Id} leaderless");
            var leader = state.Characters[f.LeaderCharacterId];
            if (f.Active) Assert.Equal(f.PolityId, leader.PolityId);
        }
        // at most one active faction per (polity, basis)
        var dupes = state.Factions.Where(f => f.Active)
            .GroupBy(f => (f.PolityId, f.Basis)).Where(g => g.Count() > 1);
        Assert.Empty(dupes);
    }

    [Fact]
    public void Formation_Chronicles()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        var formedEvents = state.Log.Events
            .Where(e => e.Type == WorldEventType.FactionFormed).ToList();
        Assert.Equal(state.Factions.Count, formedEvents.Count);
    }

    [Fact]
    public void Appeasement_FlowsFromTreasuryToFactionWealth()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        var run = new EpochEngine();
        // run until some faction exists, then check the flow arithmetic
        while (state.EpochIndex < state.Config.Sim.EpochCount
               && !state.Factions.Any(f => f.Active))
            run.Step(state);
        if (!state.Factions.Any(f => f.Active)) return;   // seed never factionalizes
        var pr = state.PolityOf(state.Factions.First(f => f.Active).PolityId);
        double creditsBefore = pr.Credits;
        double factionWealthBefore = state.Factions
            .Where(f => f.PolityId == pr.ActorId).Sum(f => f.Wealth);
        double spent = FactionOps.SpendAppeasement(state, pr, 10.0, 100.0);
        double factionWealthAfter = state.Factions
            .Where(f => f.PolityId == pr.ActorId).Sum(f => f.Wealth);
        Assert.True(spent > 0, "an active faction took no appeasement");
        Assert.Equal(factionWealthBefore + spent, factionWealthAfter, 9);
    }

    [Fact]
    public void Grievance_AccruesUnappeased_DecaysAppeased()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (state.EpochIndex < state.Config.Sim.EpochCount
               && !state.Factions.Any(f => f.Active))
            engine.Step(state);
        var faction = state.Factions.FirstOrDefault(f => f.Active);
        if (faction == null) return;
        // starve it: an unmeetable demand means nothing counts as appeased
        state.Config.Faction.AppeasementDemandShare = 1e9;
        double before = faction.Grievance;
        double strengthBefore = faction.Strength;
        engine.Step(state);
        if (faction.Active && strengthBefore > 0.05)
            Assert.True(faction.Grievance > before,
                $"unappeased grievance fell ({before} -> {faction.Grievance})");
        // satisfy it for free: zero demand reads as fully appeased
        state.Config.Faction.AppeasementDemandShare = 0.0;
        double high = faction.Grievance = 0.5;
        engine.Step(state);
        if (faction.Active)
            Assert.True(faction.Grievance < high,
                "fully appeased grievance failed to decay");
    }

    [Fact]
    public void PressedBudget_BendsTowardAgendas_NeverMints()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        while (state.EpochIndex < state.Config.Sim.EpochCount
               && !state.Factions.Any(f => f.Active && f.BudgetTarget != null))
            engine.Step(state);
        var faction = state.Factions
            .FirstOrDefault(f => f.Active && f.BudgetTarget != null);
        if (faction == null) return;
        faction.Strength = 0.8;   // make the pull visible
        var pr = state.PolityOf(faction.PolityId);
        var declared = PolityPolicies.Default.Budget;
        var pressed = FactionOps.PressedBudget(state, pr, declared);
        double declaredSum = declared.Development + declared.Military
            + declared.Research + declared.Expansion + declared.Appeasement
            + declared.Reserves;
        double pressedSum = pressed.Development + pressed.Military
            + pressed.Research + pressed.Expansion + pressed.Appeasement
            + pressed.Reserves;
        Assert.Equal(declaredSum, pressedSum, 9);   // redirected, never minted
        double tolerance = GovernmentForms.Get(
            pr.Interior!.FormId).FactionTolerance;
        if (tolerance > 0)
            Assert.NotEqual(declared, pressed);
    }

    [Fact]
    public void Factions_RoundTripThroughTheArtifact()
    {
        var (_, state) = EpochTestKit.Seeded();
        new EpochEngine().Run(state);
        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new System.IO.StringReader(text));
        Assert.Equal(state.Factions.Count, loaded.Factions.Count);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
