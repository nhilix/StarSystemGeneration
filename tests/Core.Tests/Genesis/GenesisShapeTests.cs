using System;
using System.Diagnostics;
using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Genesis;

/// <summary>Shape-acceptance bands across seeds (slice F gate): the causal
/// galaxy stays playable — polity counts in the design band, entry dates
/// spread across the window, ruins bounded, the disc traversable, and the
/// whole genesis inside its budget. Bands, not exact values: these fail on
/// regressions, not on tuning.</summary>
public class GenesisShapeTests
{
    private static readonly (ulong Seed, int Radius)[] Grid =
        { (42, 8), (7, 8), (42, 12), (99, 12) };

    [Fact]
    public void CurrentPolities_LandInThePlayableBand()
    {
        foreach (var (seed, radius) in Grid)
        {
            var s = SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = seed, GalaxyRadiusCells = radius });
            int current = s.Origins.Count(o => o.Era == OriginEra.Current);
            Assert.InRange(current, 2, 30);
        }
    }

    [Fact]
    public void VoidFraction_KeepsTheDiscTraversable()
    {
        foreach (var (seed, radius) in Grid)
        {
            var s = SkeletonBuilder.BuildShape(new GalaxyConfig
            { MasterSeed = seed, GalaxyRadiusCells = radius });
            double voids = s.Cells.Count(c => c.IsVoid) / (double)s.Cells.Count;
            Assert.InRange(voids, 0.15, 0.60);
        }
    }

    [Fact]
    public void EntryYears_SpreadAcrossTheWindow()
    {
        foreach (var (seed, radius) in Grid)
        {
            var gc = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius };
            var state = EpochGenesis.Seed(SkeletonBuilder.Build(gc),
                new EpochSimConfig { MasterSeed = seed });
            var years = state.Actors.Select(a => a.EntryYear).ToList();
            // the schedule is world-years, so the window is the window — the
            // clock division that used to sit here is what made the unit
            // ambiguous in the first place (slice MC)
            int window = state.Config.Genesis.EmergenceWindowYears;
            Assert.All(years, y => Assert.InRange(y, 0, window));
            Assert.Equal(0, years.Min());           // the eldest anchors the era
            if (years.Count >= 3)
                Assert.True(years.Max() - years.Min() >= window / 4,
                    $"seed {seed} r{radius}: staggering should use the window "
                    + $"({years.Min()}–{years.Max()} of {window})");
        }
    }

    [Fact]
    public void PrecursorResidue_StaysBounded()
    {
        foreach (var (seed, radius) in Grid)
        {
            var s = SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = seed, GalaxyRadiusCells = radius });
            Assert.InRange(s.PrecursorWaves.Count, 1, s.Cells.Count / 4);
            int sites = s.PrecursorWaves.Sum(w => w.Sites.Count);
            Assert.InRange(sites, 1, s.Cells.Count * 2);
            int claimed = s.PrecursorWaves.Sum(w => w.Cells.Count);
            Assert.True(claimed <= s.Cells.Count
                    * s.Config.Evolution.DomainBudgetFraction + s.PrecursorWaves.Count,
                $"claimed {claimed} exceeds the domain budget");
            Assert.InRange(s.Features.Count, 3, 60);
        }
    }

    [Fact]
    public void GenesisBudget_StaysInTheOneSecondClass()
    {
        // full radius-21 genesis (cosmic + evolution + arcs + seeding);
        // generous bound so Debug/CI noise can't flake it
        var sw = Stopwatch.StartNew();
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 21 });
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 3000,
            $"genesis took {sw.ElapsedMilliseconds} ms — the budget is the ~1s class");
    }

    [Fact]
    public void FortyEpochHistory_StaysAlive_OnTheCausalGalaxy()
    {
        var gc = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 12 };
        var state = EpochGenesis.Seed(SkeletonBuilder.Build(gc),
            new EpochSimConfig { MasterSeed = 42 });
        new EpochEngine().Run(state);

        // entered on schedule — or retired since (slice H: mergers)
        Assert.All(state.Actors, a => Assert.True(a.Entered || a.Retired));
        // polity actors only: corporations are actors without homeworlds,
        // and counting them let a lively corporate sector raise the bar
        // against colonization (slice CE fixed the sloppy proxy)
        int polities = 0;
        foreach (var a in state.Actors)
            if (a.Kind == ActorKind.Polity) polities++;
        Assert.True(state.Ports.Count > polities,
            $"colonization should outrun the homeworlds "
            + $"({state.Ports.Count} ports, {polities} polities)");
        Assert.True(state.Lanes.Count > 0, "lane networks should build");
        Assert.True(state.Fleets.Count > 0, "navies should exist");
        // the deep chronicle stays the floor; the generational story stacks on it
        Assert.Contains(state.Log.Events, e => e.Stratum == ClockStratum.Generational);
        Assert.Contains(state.Log.Events, e => e.Stratum == ClockStratum.Cosmic);
    }
}
