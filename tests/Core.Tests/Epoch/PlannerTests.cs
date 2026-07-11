using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class PlannerTests
{
    private static PerceptionView ViewOf(SimState state, int actorId)
    {
        new PerceptionPhase().Run(state);
        return state.Actors[actorId].Perception!;
    }

    [Fact]
    public void Plan_IsDeterministic_ForTheSameView()
    {
        var (_, state) = EpochTestKit.Seeded();
        // fixture adaptation: Seeded() runs genesis only — polities enter and
        // found ports as history runs (spec §Genesis). A few epochs give the
        // first-wave polity a real perceived economy to plan against.
        state.Config.Sim.EpochCount = 3;
        new EpochEngine().Run(state);
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        var view = ViewOf(state, actor);
        var a = Planner.BuildPlan(view, PolityPolicies.Default, state.Config);
        var b = Planner.BuildPlan(view, PolityPolicies.Default, state.Config);
        Assert.Equal(a.Entries, b.Entries);
    }

    [Fact]
    public void Plan_NeverOverCommits_TheIncomeRate()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Config.Sim.EpochCount = 6;
        new EpochEngine().Run(state);              // real income history
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        var view = ViewOf(state, actor);
        var plan = Planner.BuildPlan(view, PolityPolicies.Default,
                                     state.Config);
        // rebuild the packed timeline and assert the cap held
        int H = state.Config.Sim.GenerationYears;
        var timeline = new double[H];
        foreach (var c in view.Capability!.Commitments)
            for (int y = 0; y < System.Math.Min(H,
                     (int)System.Math.Ceiling(c.YearsRemaining)); y++)
                timeline[y] += c.CostPerYear;
        foreach (var e in plan.Entries)
        {
            var (costPerYear, duration) = Planner.CostOf(e, view, state.Config);
            int s = e.StartYear - view.WorldYear;
            for (int y = s; y < System.Math.Min(H,
                     s + (int)System.Math.Ceiling(duration)); y++)
            {
                timeline[y] += costPerYear;
                Assert.True(timeline[y]
                    <= view.Capability.IncomePerYear + 1e-6,
                    $"year {y} over-committed");
            }
        }
    }

    [Fact]
    public void Plan_SchedulesUnaffordableWork_LaterNotNever()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.Config.Sim.EpochCount = 6;
        new EpochEngine().Run(state);
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        var view = ViewOf(state, actor);
        var plan = Planner.BuildPlan(view, PolityPolicies.Default,
                                     state.Config);
        // with real commitments some entries should start in the future —
        // the staggered schedule is the point (spec §3)
        if (plan.Entries.Count >= 2)
            Assert.Contains(plan.Entries,
                e => e.StartYear > view.WorldYear);
    }

    [Fact]
    public void Plan_RoundTrips_ByteIdentical()
    {
        var (_, state) = EpochTestKit.Seeded();
        // a nonempty plan on an entered polity's policies — the PLANE lines
        // follow its POLICY line and must survive a load byte-for-byte
        var entries = new PlanEntry[]
        {
            new PlanEntry(PlanEntryKind.Facility, ProjectPriority.Core,
                state.WorldYear + 3, (int)StarGen.Core.Substrate.InfraTypeId.Mine,
                0, new HexCoordinate(2, -1), 1),
            new PlanEntry(PlanEntryKind.PortRaise, ProjectPriority.Growth,
                state.WorldYear + 8, -1, 0, new HexCoordinate(0, 0), 1),
        };
        state.Actors[0].Policies = PolityPolicies.Default with
        {
            Plan = new StandingPlan(entries),
        };
        string text = ArtifactSerializer.ToText(state);
        using var reader = new System.IO.StringReader(text);
        var loaded = ArtifactSerializer.Load(reader);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
        var loadedPlan = ((PolityPolicies)loaded.Actors[0].Policies!).Plan;
        Assert.Equal(entries, loadedPlan.Entries);
    }
}
