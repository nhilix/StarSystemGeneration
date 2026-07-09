using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class EpochEngineTests
{
    private static SimState Seeded(ulong seed = 42) =>
        StubGenesis.Seed(new EpochSimConfig { MasterSeed = seed });

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

        Assert.All(state.Actors, a => Assert.True(a.Entered));
        var emergences = state.Log.Events
            .Where(e => e.Type == WorldEventType.PolityEmerged).ToList();
        Assert.Equal(state.Actors.Count, emergences.Count);
        foreach (var a in state.Actors)
        {
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
        var state = new SimState(new EpochSimConfig());
        state.Actors.Add(new Actor(0, ActorKind.Polity, "Early",
            new StarGen.Core.Model.HexCoordinate(0, 0), entryEpoch: 0, new TrivialController()));
        state.Actors.Add(new Actor(1, ActorKind.Polity, "Late",
            new StarGen.Core.Model.HexCoordinate(1, 1), entryEpoch: 5, new TrivialController()));
        var engine = new EpochEngine();

        engine.Step(state);   // epoch 0: Early enters after Intent, no view yet
        Assert.True(state.Actors[0].Entered);
        Assert.Null(state.Actors[0].Perception);
        Assert.Empty(state.Decisions);

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
