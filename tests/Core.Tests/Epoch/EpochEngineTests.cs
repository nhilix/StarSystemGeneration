using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class EpochEngineTests
{
    private static SimState Seeded(ulong seed = 42) => EpochTestKit.Seeded(seed).State;

    [Fact]
    public void Step_RunsTheSevenPhases_InCanonicalOrder()
    {
        var state = Seeded();
        new EpochEngine().Step(state);
        Assert.Equal(new[]
        {
            "Perception", "Markets", "Allocation", "Intent",
            "Resolution", "Interior", "Chronicle",
        }, state.Trace.Select(t => t.Phase).ToArray());
        Assert.All(state.Trace, t => Assert.Equal(0, t.Epoch));
    }

    [Fact]
    public void Step_AdvancesEpochIndex_AndWorldYear_ByTheIntegrationStep()
    {
        var state = Seeded();
        var engine = new EpochEngine();
        engine.Step(state);
        engine.Step(state);
        Assert.Equal(2, state.EpochIndex);
        Assert.Equal(2 * state.Config.Sim.YearsPerEpoch, state.WorldYear);
    }

    [Fact]
    public void Interior_EntersActorsAtTheirEntryEpoch_ChronicleFinalizesTheEvent()
    {
        var state = Seeded();
        var engine = new EpochEngine();
        engine.Run(state);

        Assert.All(state.Actors, a => Assert.True(a.Entered || a.Retired));
        var emergences = state.Log.Events
            .Where(e => e.Type == WorldEventType.PolityEmerged).ToList();
        // every polity actor either emerged on the schedule, seceded,
        // or fused (slice H); corporations charter separately (slice G)
        var schisms = state.Log.Events
            .Where(e => e.Type == WorldEventType.SchismDeclared)
            .Select(e => ((SchismDeclaredPayload)e.Payload!).NewPolityId)
            .ToHashSet();
        var fusions = state.Log.Events
            .Where(e => e.Type == WorldEventType.FederationFormed)
            .Select(e => ((FederationFormedPayload)e.Payload!).NewPolityId)
            .ToHashSet();
        // civil-war splinters (contested coups, slice H): the provisional
        // polity is the war's attacker
        var splinters = state.Log.Events
            .Where(e => e.Type == WorldEventType.WarDeclared
                && ((WarDeclaredPayload)e.Payload!).Cause
                   == (int)CasusBelli.CivilWar)
            .Select(e => ((WarDeclaredPayload)e.Payload!).AttackerId)
            .ToHashSet();
        Assert.Equal(state.Actors.Count(a => a.Kind == ActorKind.Polity),
                     emergences.Count + schisms.Count + fusions.Count
                     + splinters.Count);
        foreach (var a in state.Actors)
        {
            if (a.Kind != ActorKind.Polity || schisms.Contains(a.Id)
                || fusions.Contains(a.Id) || splinters.Contains(a.Id))
                continue;
            var e = Assert.Single(emergences, e => e.Actors.Contains(a.Id));
            // events carry their world-year, dated at the entering epoch
            Assert.Equal(a.EntryEpoch * state.Config.Sim.YearsPerEpoch, e.WorldYear);
            Assert.Equal(a.Seat, e.Location);
            Assert.Equal(ClockStratum.Generational, e.Stratum);
            Assert.Equal(EventVisibility.Public, e.Visibility);
        }
    }

    [Fact]
    public void EventsFinalizeOnlyInChronicle_StagingIsEmptyBetweenSteps()
    {
        var state = Seeded();
        var engine = new EpochEngine();
        engine.Run(state);
        Assert.Empty(state.Staged);
        Assert.NotEmpty(state.Log.Events);
    }

    [Fact]
    public void Intent_IsTheOnlyControllerTouchpoint_EnteredActorsOnly()
    {
        var state = Seeded();
        var engine = new EpochEngine();
        engine.Step(state);
        int enteredAfterFirst = state.Actors.Count(a => a.Entered);
        engine.Step(state);
        // second step's Intent decides for exactly the actors already entered
        // before it (Interior admits newcomers after Intent has run)
        Assert.Equal(enteredAfterFirst, state.Decisions.Count);
        Assert.All(state.Decisions, d =>
            Assert.True(state.Actors[d.ActorId].Entered));
    }

    [Fact]
    public void Perception_GivesEnteredActorsAView_BeforeTheirFirstIntent()
    {
        // an actor entering during epoch 0's Interior gets its view at epoch 1's
        // Perception, ahead of its first Intent; a later entrant stays viewless
        var config = new EpochSimConfig();
        var skeleton = StarGen.Core.Galaxy.SkeletonBuilder.BuildShape(
            new StarGen.Core.Galaxy.GalaxyConfig { MasterSeed = 1, GalaxyRadiusCells = 4 });
        var state = new SimState(config, skeleton);
        state.Polities.Add(new PolityRecord(0, 0));
        state.Polities.Add(new PolityRecord(1, 0));
        state.Actors.Add(new Actor(0, ActorKind.Polity, "Early",
            new StarGen.Core.Model.HexCoordinate(0, 0), entryEpoch: 0, new TrivialController()));
        state.Actors.Add(new Actor(1, ActorKind.Polity, "Late",
            new StarGen.Core.Model.HexCoordinate(1, 1), entryEpoch: 5, new TrivialController()));
        var engine = new EpochEngine();

        engine.Step(state);   // epoch 0: Early enters after Intent, no view yet
        Assert.True(state.Actors[0].Entered);
        Assert.Null(state.Actors[0].Perception);
        Assert.Empty(state.Decisions);

        // the trace pluralizes correctly (byte-compared text: get it right now)
        Assert.Equal("1 polity enters",
            state.Trace.Single(t => t.Epoch == 0 && t.Phase == "Interior").Note);
        Assert.Equal("1 event finalized, 1 pulse emitted",
            state.Trace.Single(t => t.Epoch == 0 && t.Phase == "Chronicle").Note);

        engine.Step(state);   // epoch 1: Perception serves Early, Intent reads it
        var view = state.Actors[0].Perception;
        Assert.NotNull(view);
        Assert.Equal(0, view!.SelfId);
        Assert.Equal(state.Config.Sim.YearsPerEpoch, view.WorldYear);
        var d = Assert.Single(state.Decisions);
        Assert.Equal(0, d.ActorId);
        Assert.Null(state.Actors[1].Perception);
    }

    [Fact]
    public void Run_StepsTheConfiguredHistoryDepth()
    {
        var state = Seeded();
        new EpochEngine().Run(state);
        Assert.Equal(state.Config.Sim.EpochCount, state.EpochIndex);
        Assert.Equal(state.Config.Sim.EpochCount * state.Config.Sim.YearsPerEpoch,
                     state.WorldYear);
        Assert.Equal(state.Config.Sim.EpochCount * 7, state.Trace.Count);
    }
}
