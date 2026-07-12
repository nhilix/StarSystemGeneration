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
                // the cap is income plus the horizon-spread savings —
                // treasuries exist to be spent (contract economy)
                Assert.True(timeline[y]
                    <= view.Capability.IncomePerYear
                       + view.Capability.SavingsPerYear + 1e-6,
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
        // the staggered schedule is the point (spec §3). Honest guard: a
        // plan whose ENTIRE cost fits the income rate concurrently is
        // correctly unstaggered (a small plan on a healthy economy — the
        // spec defers work only past the affordability horizon), so only
        // assert deferral when starting everything now would over-commit.
        if (plan.Entries.Count < 2) return;
        double concurrent = 0;
        foreach (var c in view.Capability!.Commitments)
            if (c.YearsRemaining > 0) concurrent += c.CostPerYear;
        foreach (var e in plan.Entries)
            concurrent += Planner.CostOf(e, view, state.Config).CostPerYear;
        if (concurrent <= view.Capability.IncomePerYear + 1e-6) return;
        Assert.Contains(plan.Entries,
            e => e.StartYear > view.WorldYear);
    }

    /// <summary>Stage 2 carried gap (spec §6, P7): yard throughput accrues
    /// in WORLD-TIME — 25 one-year plans grant exactly the hull slots one
    /// 25-year plan grants. The old Max(1, tier·rate·span) floor fired a
    /// unit batch every step at fine tick.</summary>
    [Fact]
    public void Plan_HullBatchSlots_AccrueInWorldTime_NotPerStep()
    {
        var cfg = new EpochSimConfig();          // YardHullsPerTierPerYear .2
        var design = new DesignBrief(0, ShipRole.Freight, ShipSize.Medium, 1);
        var ports = new[] { new PortBrief(0, Tier: 2, YardTiers: 1) };
        var cap = new CapabilityBrief(1e6, 0.0,
            new double[StarGen.Core.Substrate.Goods.All.Count],
            new CommitmentBrief[0]);
        var policies = PolityPolicies.Default with
        {
            ShipbuildingPriorities =
                new System.Collections.Generic.Dictionary<int, double>
                { [0] = 1.0 },
        };
        int SlotsAt(int worldYear, int span)
        {
            cfg.Sim.YearsPerEpoch = span;
            var view = new PerceptionView(0, worldYear, new int[0],
                ownDesigns: new[] { design }, capability: cap,
                ownPorts: ports);
            int hulls = 0;
            foreach (var e in Planner.BuildPlan(view, policies, cfg).Entries)
                if (e.Kind == PlanEntryKind.HullBatch) hulls += e.Count;
            return hulls;
        }
        int coarse = SlotsAt(1000, 25);          // one generation step
        int fine = 0;
        for (int y = 0; y < 25; y++) fine += SlotsAt(1000 + y, 1);
        Assert.True(coarse > 0, "the yard should win slots at coarse tick");
        Assert.Equal(coarse, fine);
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
