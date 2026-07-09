using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class AllocationTests
{
    [Fact]
    public void Allocation_AccruesBudgetSharesFromPortIncome()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        // step until someone has entered (Interior founds the homeworld port)
        while (!state.Actors.Any(a => a.Entered)) engine.Step(state);
        var actor = state.Actors.First(a => a.Entered);
        int portsOwned = state.Ports.Count(p => p.OwnerActorId == actor.Id);
        double expBefore = state.PolityOf(actor.Id).ExpansionPoints;
        double devBefore = state.PolityOf(actor.Id).DevelopmentPoints;

        engine.Step(state);      // the next Allocation sees the port

        double income = portsOwned * state.Config.Expansion.StubIncomePerPortPerYear
                        * state.Config.Sim.YearsPerEpoch;
        var budget = PolityPolicies.Default.Budget;
        Assert.True(state.PolityOf(actor.Id).ExpansionPoints
                    >= expBefore + income * budget.Expansion - 1e9 * double.Epsilon);
        Assert.Equal(expBefore + income * budget.Expansion,
                     state.PolityOf(actor.Id).ExpansionPoints, 10);
        Assert.Equal(devBefore + income * budget.Development,
                     state.PolityOf(actor.Id).DevelopmentPoints, 10);
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
    public void Intent_StoresStandingPolicies_OnTheActor()
    {
        var (_, state) = EpochTestKit.Seeded();
        var engine = new EpochEngine();
        engine.Step(state);
        engine.Step(state);      // entered actors decided at least once
        foreach (var a in state.Actors.Where(a => a.Entered && a.EntryEpoch == 0))
            Assert.IsType<PolityPolicies>(a.Policies);
    }
}
