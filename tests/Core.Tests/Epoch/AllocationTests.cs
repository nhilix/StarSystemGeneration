using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class AllocationTests
{
    [Fact]
    public void Allocation_AccruesBudgetSharesFromRealIncome()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        // step until someone has entered (Interior founds the homeworld port)
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        var actor = state.Actors.First(a => a.Entered);
        double expBefore = state.PolityOf(actor.Id).ExpansionPoints;

        engine.Step(state);      // the next Allocation sees the market income

        // the entry endowment (and any market income) is allocatable credits:
        // the expansion share accrues by the standing budget weights
        Assert.True(state.PolityOf(actor.Id).ExpansionPoints > expBefore,
            "real income should fill the expansion treasury");
    }

    [Fact]
    public void Allocation_IgnoresUnenteredPolities()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        engine.Step(state);
        foreach (var a in state.Actors.Where(a => !a.Entered))
        {
            Assert.Equal(0.0, state.PolityOf(a.Id).ExpansionPoints);
            Assert.Equal(0.0, state.PolityOf(a.Id).DevelopmentPoints);
        }
    }

    [Fact]
    public void FullRun_BuildsLanes_RaisesTiers_Deterministically()
    {
        var (_, s1) = EpochTestKit.Seeded();
        var (_, s2) = EpochTestKit.Seeded();
        new EpochEngine().Run(s1);
        new EpochEngine().Run(s2);

        Assert.True(s1.Lanes.Count > 0, "no lanes built in 40 epochs");
        Assert.Equal(s1.Lanes.Count,
                     s1.Lanes.Select(l => (l.PortAId, l.PortBId)).Distinct().Count());
        foreach (var lane in s1.Lanes)
        {
            Assert.True(lane.PortAId < lane.PortBId);
            var a = s1.Ports[lane.PortAId];
            var b = s1.Ports[lane.PortBId];
            // same polity's paired infrastructure at build time; a schism
            // (slice G) may since have drawn a border across the lane
            if (a.OwnerActorId != b.OwnerActorId)
                Assert.Contains(s1.Log.Events,
                    e => e.Type == WorldEventType.SchismDeclared);
            // in range at current tiers + the best Astrogation bonus any
            // builder could have had (slice G stretches the reach)
            int maxAstro = 0;
            foreach (var pr in s1.Polities)
                maxAstro = System.Math.Max(maxAstro,
                    pr.TechTier[(int)TechDomain.Astrogation]);
            int bonus = s1.Config.Tech.AstroRangePerTierHexes
                        * System.Math.Max(0, maxAstro - Tech.EraStandardTier);
            Assert.True(StarGen.Core.Galaxy.HexGrid.Distance(a.Hex, b.Hex)
                    <= LaneMath.ReachHexes(s1.Config, 3) + bonus,
                "lane endpoints out of range");
        }
        // development raised some colony port above its founding tier
        Assert.Contains(s1.Ports, p => p.Tier > 1 && p.FoundedYear > 0);
        Assert.All(s1.Ports, p => Assert.InRange(p.Tier, 1, s1.Config.Infrastructure.MaxPortTier));
        Assert.Contains(s1.Log.Events, e => e.Type == WorldEventType.LaneOpened);
        Assert.Contains(s1.Log.Events, e => e.Type == WorldEventType.PortTierRaised);

        Assert.Equal(s1.Lanes.Select(l => (l.Id, l.PortAId, l.PortBId, l.BuiltYear)),
                     s2.Lanes.Select(l => (l.Id, l.PortAId, l.PortBId, l.BuiltYear)));
        Assert.Equal(s1.Ports.Select(p => (p.Id, p.OwnerActorId, p.Hex, p.Tier, p.FoundedYear)),
                     s2.Ports.Select(p => (p.Id, p.OwnerActorId, p.Hex, p.Tier, p.FoundedYear)));
    }

    [Fact]
    public void Intent_StoresStandingPolicies_OnTheActor()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        engine.Step(state);
        engine.Step(state);      // entered actors decided at least once
        foreach (var a in state.Actors.Where(a => a.Entered && a.EntryYear == 0))
            Assert.IsType<PolityPolicies>(a.Policies);
    }
}
