using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class EpochSimTests
{
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = 8 });

    [Fact]
    public void Sim_IsDeterministic()
    {
        var a = Build();
        var b = Build();
        for (int i = 0; i < a.Cells.Count; i++)
        {
            Assert.Equal(a.Cells[i].OwnerPolityId, b.Cells[i].OwnerPolityId);
            Assert.Equal(a.Cells[i].DevelopmentTier, b.Cells[i].DevelopmentTier);
        }
        Assert.Equal(a.Events.Count, b.Events.Count);
    }

    [Fact]
    public void Polities_Expand_ButWildsRemain()
    {
        var s = Build();
        var claimable = s.Cells.Where(c => !c.IsVoid).ToList();
        int claimed = claimable.Count(c => c.OwnerPolityId >= 0);
        Assert.True(claimed > s.Polities.Count, "polities should grow beyond their homeworlds");
        Assert.True(claimed < claimable.Count, "unclaimed wilds must remain (spec §7.8)");
    }

    [Fact]
    public void ClaimedFraction_AtReferenceConfig_IsWithinAcceptanceBand()
    {
        // Spec §10 shape band: polities should visibly expand without paving over the
        // whole galaxy. Reference config: seed 42, the small reference config (hex lattice,
        // GalaxyRadiusCells = 8). Band finalized in task 10 to match
        // EconomyInvariantTests.ShapeBands_ReferenceConfig's ceiling (0.8, tightened from
        // 0.85 during claimed-fraction pressure review). Observed fraction at this config
        // (post task-10 tuning: ProvisionsPerPop 0.5, IncomePhase.FamineShrink 0.8) is
        // ~0.515 (84/163).
        var s = Build();
        var claimable = s.Cells.Where(c => !c.IsVoid).ToList();
        int claimed = claimable.Count(c => c.OwnerPolityId >= 0);
        double frac = (double)claimed / claimable.Count;
        Assert.InRange(frac, 0.2, 0.8);
    }

    [Fact]
    public void OwnedCells_TraceToRegistry_AndVoidsStayUnclaimed()
    {
        var s = Build();
        foreach (var cell in s.Cells)
        {
            if (cell.OwnerPolityId >= 0)
                Assert.Contains(s.Polities, p => p.Id == cell.OwnerPolityId);
            if (cell.IsVoid)
                Assert.Equal(-1, cell.OwnerPolityId);
        }
    }

    [Fact]
    public void EventLog_IsChronological_AndReferentiallyIntact()
    {
        var s = Build();
        Assert.NotEmpty(s.Events);
        int lastEpoch = 0;
        foreach (var e in s.Events)
        {
            Assert.True(e.Epoch >= lastEpoch, "event log must be chronological");
            lastEpoch = e.Epoch;
            Assert.Contains(s.Polities, p => p.Id == e.ActorPolityId);
            Assert.True(s.TryGetCell(new HexCoordinate(e.Q, e.R), out _),
                $"event references cell ({e.Q},{e.R}) outside the lattice");
        }
    }

    [Fact]
    public void ExtinctPolities_AreRetainedInRegistry()
    {
        // Whether extinction happens depends on the seed; assert the invariant holds
        // across several seeds and that at least the registry never shrinks.
        for (ulong seed = 40; seed < 46; seed++)
        {
            var s = Build(seed);
            foreach (var e in s.Events.Where(e => e.Type == GalaxyEventType.PolityExtinct))
            {
                var polity = s.Polities.Single(p => p.Id == e.TargetPolityId);
                Assert.True(polity.Extinct);
                Assert.DoesNotContain(s.Cells, c => c.OwnerPolityId == polity.Id);
            }
        }
    }
}
