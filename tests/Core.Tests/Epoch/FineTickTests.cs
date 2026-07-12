using System.Collections.Generic;
using System.IO;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice J — the resumability contract (narrative/handoff.md §The
/// resumability contract, P7's final test): the same seven-phase machine
/// steps a loaded artifact at play-tick resolution. No genesis-only
/// mechanic; determinism byte-identity at fine tick; conservation holds;
/// the fine integration tracks the coarse one inside honest bands.</summary>
public class FineTickTests
{
    /// <summary>A finished coarse prologue: 10 generations of real history
    /// on the standard test galaxy.</summary>
    private static SimState Prologue(ulong seed = 42)
    {
        var state = EpochTestKit.Seeded(seed).State;
        state.Config.Sim.EpochCount = 10;
        new EpochEngine().Run(state);
        return state;
    }

    private static SimState Reload(SimState state)
    {
        using var reader = new StringReader(ArtifactSerializer.ToText(state));
        return ArtifactSerializer.Load(reader);
    }

    /// <summary>Continue a state at fine resolution: the SAME engine, a
    /// smaller integration step (frame/time.md — sampling differs, rules
    /// do not).</summary>
    private static void ContinueFine(SimState state, int steps, int yearsPerEpoch)
    {
        state.Config.Sim.YearsPerEpoch = yearsPerEpoch;
        var engine = new EpochEngine();
        for (int i = 0; i < steps; i++) engine.Step(state);
    }

    [Fact]
    public void FineTick_Determinism_ByteIdentical()
    {
        var artifact = ArtifactSerializer.ToText(Prologue());
        SimState RunFine()
        {
            using var reader = new StringReader(artifact);
            var s = ArtifactSerializer.Load(reader);
            ContinueFine(s, steps: 20, yearsPerEpoch: 5);
            return s;
        }
        Assert.Equal(ArtifactSerializer.ToText(RunFine()),
                     ArtifactSerializer.ToText(RunFine()));
    }

    [Fact]
    public void FineTick_LoadThenContinue_EqualsTheLiveRun()
    {
        // the strongest resumability gate at play resolution: a loaded
        // artifact continued fine is byte-identical to the live state
        // continued fine
        var live = Prologue();
        var loaded = Reload(live);
        ContinueFine(live, steps: 15, yearsPerEpoch: 5);
        ContinueFine(loaded, steps: 15, yearsPerEpoch: 5);
        Assert.Equal(ArtifactSerializer.ToText(live),
                     ArtifactSerializer.ToText(loaded));
    }

    [Fact]
    public void FineTick_SevenPhasesRun_AndTheClockIsHonest()
    {
        var state = Prologue();
        int yearBefore = state.WorldYear;
        int traceFrom = state.Trace.Count;
        ContinueFine(state, steps: 5, yearsPerEpoch: 1);
        Assert.Equal(yearBefore + 5, state.WorldYear);
        var phases = new[] { "Perception", "Markets", "Allocation", "Intent",
                             "Resolution", "Interior", "Chronicle" };
        for (int step = 0; step < 5; step++)
            for (int p = 0; p < 7; p++)
                Assert.Equal(phases[p],
                    state.Trace[traceFrom + step * 7 + p].Phase);
    }

    [Fact]
    public void FineTick_HullLedger_Conserves()
    {
        var state = Prologue();
        ContinueFine(state, steps: 30, yearsPerEpoch: 1);
        int wrecksTotal = 0;
        foreach (var wr in state.Wreckage) wrecksTotal += wr.Hulls;
        int wreckedLedger = 0;
        foreach (var pr in state.Polities)
        {
            int active = 0;
            foreach (var f in state.Fleets)
                if (f.OwnerActorId == pr.ActorId) active += f.TotalHulls;
            Assert.Equal(pr.HullsBuilt,
                         active + pr.HullsWrecked + pr.HullsScrapped);
            wreckedLedger += pr.HullsWrecked;
        }
        foreach (var corp in state.Corporations)
        {
            int active = 0;
            foreach (var f in state.Fleets)
                if (f.OwnerActorId == corp.ActorId) active += f.TotalHulls;
            Assert.Equal(corp.HullsBuilt,
                         active + corp.HullsWrecked + corp.HullsScrapped);
            wreckedLedger += corp.HullsWrecked;
        }
        Assert.Equal(wreckedLedger, wrecksTotal);
    }

    [Theory]
    [InlineData(42ul)]
    [InlineData(7ul)]
    public void FineTick_TracksTheCoarseRun_InsideHonestBands(ulong seed)
    {
        // the P7 net: 100 world-years integrated as 4 generation steps vs
        // 20 fine steps must land in the same macro neighborhood — a
        // mechanic secretly counting steps diverges ~5x and blows these.
        // The bands are wide on purpose: rolls key (step, actor), so the
        // two runs are legitimately different HISTORIES (different sparks,
        // outbreaks, colonies) — the bands bound systematic rate bias,
        // not chaos (this net caught the demand-vs-stock price crash)
        var coarse = Prologue(seed);
        var fine = Reload(coarse);
        int sharedPorts = coarse.Ports.Count;   // founded before the fork
        ContinueFine(coarse, steps: 4, yearsPerEpoch: 25);
        ContinueFine(fine, steps: 20, yearsPerEpoch: 5);
        Assert.Equal(coarse.WorldYear, fine.WorldYear);

        double coarsePop = 0, finePop = 0;
        foreach (var s in coarse.Segments) coarsePop += s.Size;
        foreach (var s in fine.Segments) finePop += s.Size;
        AssertBand("population", coarsePop, finePop, 0.5);

        AssertBand("ports", coarse.Ports.Count, fine.Ports.Count, 0.5);

        int coarseHulls = 0, fineHulls = 0;
        foreach (var f in coarse.Fleets) coarseHulls += f.TotalHulls;
        foreach (var f in fine.Fleets) fineHulls += f.TotalHulls;
        AssertBand("hulls", coarseHulls, fineHulls, 0.6);

        // prices drift between clearings instead of teleporting: the MEDIAN
        // provisions price stays in the same neighborhood — compared over
        // the ports both histories share (colonies founded after the fork
        // belong to one history only). Median, not mean: with the sparse
        // gate-economics networks a single unserved port at the price
        // ceiling swings a mean 3× on connectivity luck alone, and the band
        // exists to bound systematic rate bias, not chaos.
        double coarsePrice = MedianProvisionsPrice(coarse, sharedPorts);
        double finePrice = MedianProvisionsPrice(fine, sharedPorts);
        AssertBand("provisions price", coarsePrice, finePrice, 0.6);

        // history kept happening: the fine run logged real events too
        Assert.True(fine.Log.Events.Count > coarse.Log.Events.Count / 4,
            "the fine continuation went eerily quiet");
    }

    [Theory]
    [InlineData(42ul)]
    [InlineData(7ul)]
    public void FineTick_NoGenesisOnlyMechanic_TheWorldStaysAlive(ulong seed)
    {
        // 4 more generations at play tick: people are born, markets clear,
        // characters die and are succeeded — nothing was epoch-gated
        var state = Prologue(seed);
        int eventsBefore = state.Log.Events.Count;
        int deathsBefore = DeadCharacters(state);
        ContinueFine(state, steps: 100, yearsPerEpoch: 1);
        Assert.True(state.Log.Events.Count > eventsBefore,
            "a century at fine tick logged nothing");
        Assert.True(DeadCharacters(state) > deathsBefore,
            "a century passed and nobody died — genesis-only mortality?");
        int liveRulers = 0;
        foreach (var pr in state.Polities)
            if (pr.Interior is { RulerCharacterId: >= 0 } interior
                && state.Actors[pr.ActorId].Entered
                && !state.Actors[pr.ActorId].Retired
                && state.Characters[interior.RulerCharacterId].Alive)
                liveRulers++;
        Assert.True(liveRulers > 0, "every throne is empty — succession dead?");
    }

    /// <summary>THE durations test (spec §6): the same artifact stepped
    /// coarse (25y) and fine (1y) commissions its BUILT WORLD at the same
    /// WORLD-YEARS within an honest band — construction is world-time state,
    /// not a step artifact.
    ///
    /// Scope: the built-world project kinds — facilities, port raises, hull
    /// batches — counted in UNITS (hulls for a batch, 1 otherwise): batch
    /// granularity is cadence-dependent by design (D'Hondt slots mature one
    /// at a time on a fine clock, in bundles on a coarse one — the stage-2
    /// world-time slot clock makes the totals telescope), so records are
    /// not comparable but units are. Expansion/logistics foundings (colony
    /// expeditions, gate pairs) are deliberately excluded: even with the
    /// stage-2 founding cadence normalized to world-time
    /// (Expansion.FoundingCadenceYears in TryFound), WHICH hexes get
    /// targeted and which expeditions collide still follows the decision
    /// cadence — expected divergence in WHICH projects run, per the
    /// durations spec. The exclusion does not hide the failure this test
    /// guards: whether the built world commissions AT ALL, and at a
    /// comparable world-time rate, across tick resolutions.</summary>
    [Fact]
    public void FineTick_ProjectCompletions_LandOnWorldYears_NotSteps()
    {
        var artifact = ArtifactSerializer.ToText(Prologue());
        static bool IsBuiltWorld(ProjectKind k) =>
            k == ProjectKind.FacilityConstruction
            || k == ProjectKind.PortRaise
            || k == ProjectKind.HullBatch;
        int CompletedUnitsAfter(int steps, int yearsPerEpoch)
        {
            using var reader = new StringReader(artifact);
            var s = ArtifactSerializer.Load(reader);
            int before = s.Projects.Count;
            ContinueFine(s, steps, yearsPerEpoch);
            int units = 0;
            for (int i = before; i < s.Projects.Count; i++)
            {
                var p = s.Projects[i];
                if (!p.Completed || !IsBuiltWorld(p.Kind)) continue;
                units += p.Kind == ProjectKind.HullBatch
                    ? System.Math.Max(1, p.Count) : 1;
            }
            return units;
        }
        int coarse = CompletedUnitsAfter(steps: 2, yearsPerEpoch: 25);
        int fine = CompletedUnitsAfter(steps: 50, yearsPerEpoch: 1);
        // both clocks commission built work; the fine clock is never SLOWER
        // in world-time than the coarse by more than one coarse span
        Assert.True(coarse > 0, "the coarse clock commissioned nothing");
        Assert.True(fine > 0, "the fine clock commissioned nothing");
        Assert.InRange(fine, (int)(coarse * 0.5),
                       (int)System.Math.Ceiling(coarse * 2.0));
    }

    private static double MedianProvisionsPrice(SimState state, int portCap)
    {
        var prices = new System.Collections.Generic.List<double>();
        foreach (var m in state.Markets)
            if (m.PortId < portCap)
                prices.Add(m.Price[(int)StarGen.Core.Substrate.GoodId.Provisions]);
        if (prices.Count == 0) return 0;
        prices.Sort();
        int mid = prices.Count / 2;
        return prices.Count % 2 == 1 ? prices[mid]
            : (prices[mid - 1] + prices[mid]) * 0.5;
    }

    private static int DeadCharacters(SimState state)
    {
        int dead = 0;
        foreach (var c in state.Characters) if (!c.Alive) dead++;
        return dead;
    }

    private static void AssertBand(string label, double coarse, double fine,
                                   double tolerance)
    {
        if (coarse <= 0 && fine <= 0) return;
        double hi = System.Math.Max(coarse, fine);
        double lo = System.Math.Min(coarse, fine);
        Assert.True(lo >= hi * (1.0 - tolerance),
            $"{label} diverged across tick resolutions: coarse {coarse}, "
            + $"fine {fine} (band ±{tolerance:P0})");
    }
}
