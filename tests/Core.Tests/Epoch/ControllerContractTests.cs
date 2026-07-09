using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class ControllerContractTests
{
    private static PerceptionView View(int selfId = 0) =>
        new PerceptionView(selfId, worldYear: 100, knownPolityIds: new[] { 0, 1 });

    [Fact]
    public void TrivialController_ReturnsDefaultPolicies_AndNoActs()
    {
        var decision = new TrivialController().Decide(View());
        Assert.Empty(decision.Acts);
        var policies = Assert.IsType<PolityPolicies>(decision.Policies);
        Assert.Equal(PolityPolicies.Default, policies);
    }

    [Fact]
    public void DefaultBudgetWeights_AreNormalized()
    {
        var b = PolityPolicies.Default.Budget;
        double sum = b.Development + b.Military + b.Research
                   + b.Expansion + b.Appeasement + b.Reserves;
        Assert.Equal(1.0, sum, 10);
    }

    [Fact]
    public void PolityActs_CarryTheirContractFields()
    {
        // representative acts from frame/controller-contract.md — records with
        // the fields the contract names
        var found = new FoundColonyAct(ActorId: 2, Target: new HexCoordinate(5, -3));
        Assert.Equal(2, found.ActorId);
        Assert.Equal(new HexCoordinate(5, -3), found.Target);

        var war = new DeclareWarAct(ActorId: 1, TargetPolityId: 4,
                                    CasusBelli: "border dispute",
                                    Objectives: new[] { "seize port" },
                                    Demand: "cede the overlap zone");
        Assert.Equal(4, war.TargetPolityId);
        Assert.IsAssignableFrom<Act>(war);
    }

    [Fact]
    public void Decision_IsPoliciesPlusActs()
    {
        var acts = new Act[] { new FoundColonyAct(0, new HexCoordinate(1, 1)) };
        var d = new ControllerDecision(PolityPolicies.Default, acts);
        Assert.Same(PolityPolicies.Default, d.Policies);
        Assert.Single(d.Acts);
    }

    [Fact]
    public void PerceptionView_IsAllTheControllerSees()
    {
        // Decide takes only the view — P3: no actor reads global truth.
        // (Perfect-info stub until Slice I: the view is built FROM truth, but
        // the controller signature only admits the view.)
        var method = typeof(IController).GetMethod(nameof(IController.Decide))!;
        var p = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(PerceptionView), p.ParameterType);
    }
}
