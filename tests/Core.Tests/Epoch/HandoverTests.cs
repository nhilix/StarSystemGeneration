using System.Collections.Generic;
using System.IO;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice J — controller handover certification (narrative/
/// handoff.md §Controller handover, P2's final test): the player occupies
/// any controller slot by answering the same Intent question the AI
/// answers. Scope changes are slot changes; nothing inside the sim cares
/// who is driving.</summary>
public class HandoverTests
{
    private static SimState Prologue(ulong seed = 42, int epochs = 10)
    {
        var state = EpochTestKit.Seeded(seed).State;
        state.Config.Sim.EpochCount = epochs;
        new EpochEngine().Run(state);
        return state;
    }

    private static void Continue(SimState state, int steps)
    {
        var engine = new EpochEngine();
        for (int i = 0; i < steps; i++) engine.Step(state);
    }

    /// <summary>A different driver giving the same answers — the sim must
    /// be provably unable to tell (P2: identity is not an input).</summary>
    private sealed class HandedOverController : IController
    {
        private readonly IController _inner;
        public HandedOverController(IController inner) { _inner = inner; }
        public ControllerDecision Decide(PerceptionView perceived) =>
            _inner.Decide(perceived);
    }

    /// <summary>A player script: standing policies plus a queue of acts
    /// played one per step — the minimal "human at the controls".</summary>
    private sealed class ScriptedController : IController
    {
        private readonly PolicySet _policies;
        private readonly Queue<Act> _script;
        public ScriptedController(PolicySet policies, params Act[] script)
        {
            _policies = policies;
            _script = new Queue<Act>(script);
        }
        public ControllerDecision Decide(PerceptionView perceived) =>
            new ControllerDecision(_policies,
                _script.Count > 0 ? new[] { _script.Dequeue() } : new Act[0]);
    }

    [Fact]
    public void SlotSwap_WithTheSameAnswers_IsByteInvisible()
    {
        var baseline = Prologue();
        var swapped = Prologue();
        foreach (var a in swapped.Actors)
            a.Controller = new HandedOverController(a.Controller);
        Continue(baseline, 4);
        Continue(swapped, 4);
        Assert.Equal(ArtifactSerializer.ToText(baseline),
                     ArtifactSerializer.ToText(swapped));
    }

    [Fact]
    public void APlayer_TakesAPolityThrone_MidRun()
    {
        // 11, not the shared 10: this test needs a relation to exist before it
        // can stage the throne, and slice MC's EntryYear fix pushes seed 42's
        // first one from epoch 10 to 11. Entry used to truncate DOWN to the 25y
        // grid, admitting polities up to 24 years early; they now enter on their
        // real calendar year, so first contact slips by one epoch. The other
        // tests here keep the 10-epoch prologue — nothing about the handover
        // mechanic moved.
        var state = Prologue(epochs: 11);
        var rel = EpochTestKit.FirstLiveRelation(state);
        int self = rel.PolityAId, other = rel.PolityBId;
        // clear the table so the scripted offer is unambiguous, and cool
        // the pair so the OTHER side's AI stays out of the exchange (the
        // ladder starts at TradePact — one rung at a time)
        rel.OfferedRung = TreatyRung.None;
        rel.OfferedById = -1;
        rel.Rung = TreatyRung.None;
        rel.RungYear = -1;
        rel.Warmth = 0.0;
        rel.Tension = 0.2;

        // the player's reign: a distinctive tax code and one diplomatic act
        var policies = PolityPolicies.Default with { TaxRate = 0.31 };
        state.Actors[self].Controller = new ScriptedController(policies,
            new TreatyAct(self, other, (int)TreatyRung.TradePact,
                          TreatyVerb.Offer));
        Continue(state, 1);

        // the act resolved exactly as an AI's would
        Assert.Equal(TreatyRung.TradePact, rel.OfferedRung);
        Assert.Equal(self, rel.OfferedById);
        // the standing policies took the slot: Intent stamped them on the
        // actor, and the next Allocation taxes at the player's rate
        var stamped = Assert.IsType<PolityPolicies>(
            state.Actors[self].Policies);
        Assert.Equal(0.31, stamped.TaxRate);
    }

    [Fact]
    public void APlayer_TakesACorporationBoard_MidRun()
    {
        var state = Prologue();
        Corporation? corp = null;
        foreach (var c in state.Corporations)
            if (c.Active && c.HostPolityId >= 0) { corp = c; break; }
        if (corp == null) return;   // this seed chartered nothing hosted

        var policies = CorporationPolicies.Default with { DividendRate = 0.9 };
        state.Actors[corp.ActorId].Controller =
            new ScriptedController(policies);
        Continue(state, 1);

        var stamped = Assert.IsType<CorporationPolicies>(
            state.Actors[corp.ActorId].Policies);
        Assert.Equal(0.9, stamped.DividendRate);
    }

    [Fact]
    public void Load_ReattachesStockControllers()
    {
        // the artifact persists no controller identity (transients are not
        // state): a save made mid-occupation loads with the stock AI in
        // every slot, and the client re-occupies its slot on its own
        var state = Prologue();
        state.Actors[0].Controller =
            new ScriptedController(PolityPolicies.Default);
        using var reader = new StringReader(ArtifactSerializer.ToText(state));
        var loaded = ArtifactSerializer.Load(reader);
        foreach (var a in loaded.Actors)
            Assert.True(a.Controller is GenesisController
                        or CorporateController or TrivialController,
                $"actor {a.Id} loaded with a non-stock controller "
                + $"({a.Controller.GetType().Name})");
    }
}
