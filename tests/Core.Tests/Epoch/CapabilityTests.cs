using StarGen.Core.Epoch;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class CapabilityTests
{
    [Fact]
    public void Brief_CommittedRates_ReflectInFlightProjects()
    {
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        int port = ProjectOpsTests.OwnPort(state, actor);
        var before = CapabilityOps.BriefFor(state, actor);
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, actor, actor,
            port, state.Ports[port].Hex, 5.0, ProjectPriority.Core, 0);
        p.TargetId = port;
        p.PerYearBasket[(int)GoodId.Alloys] = 2.0;
        p.WagesPerYear = 4.0;
        var after = CapabilityOps.BriefFor(state, actor);
        double alloyPrice = Market.InitialPrice(state.Config.Economy,
                                                GoodId.Alloys);
        Assert.Equal(before.CommittedCostPerYear
                     + 2.0 * alloyPrice + 4.0,
                     after.CommittedCostPerYear, 6);
    }

    [Fact]
    public void Brief_ArrivesInPerceptionView()
    {
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        new PerceptionPhase().Run(state);
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        var view = state.Actors[actor].Perception!;
        Assert.NotNull(view.Capability);
        Assert.NotEmpty(view.OwnPorts);
    }

    [Fact]
    public void ConstructionPull_ReadsInFlightProjects()
    {
        var (_, state) = EpochTestKit.Seeded();
        ProjectOpsTests.RunHistory(state);
        int actor = ProjectOpsTests.FirstEnteredPolity(state);
        int port = ProjectOpsTests.OwnPort(state, actor);
        var p = ProjectOps.Spawn(state, ProjectKind.PortRaise, actor, actor,
            port, state.Ports[port].Hex, 5.0, ProjectPriority.Core, 0);
        p.TargetId = port;
        p.PerYearBasket[(int)GoodId.Machinery] = 2.0;
        var scratch = new MarketStepScratch(state);
        MarketEngine.AddConstructionPull(state, scratch);
        // stage 2: the pull tapers to the remaining work — a 5-year raise
        // pulls its 5 years of basket, never the whole 25-year span's
        Assert.True(scratch.Demand[port][(int)GoodId.Machinery]
                    >= 2.0 * p.YearsRequired);
    }
}
