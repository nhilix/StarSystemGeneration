using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice J — the handoff framing (narrative/handoff.md §Contents):
/// open threads surfaced deliberately, indexes as views over the log.
/// Nothing here is stored — artifact finalization compiles nothing.</summary>
public class HandoffViewTests
{
    private static SimState Run(ulong seed = 42)
    {
        var state = EpochTestKit.Seeded(seed).State;
        new EpochEngine().Run(state);
        return state;
    }

    [Fact]
    public void OpenThreads_AreDeterministic_AndUnstored()
    {
        var state = Run();
        var once = HandoffView.OpenThreads(state);
        var twice = HandoffView.OpenThreads(state);
        Assert.Equal(once.Count, twice.Count);
        for (int i = 0; i < once.Count; i++)
            Assert.Equal(once[i], twice[i]);
    }

    [Fact]
    public void EveryActiveWar_IsAnOpenThread()
    {
        var state = Run();
        var threads = HandoffView.OpenThreads(state);
        foreach (var war in state.Wars)
            if (war.Active)
                Assert.Contains(threads,
                    t => t.Kind == "war" && t.Text.Contains(war.Name));
    }

    [Fact]
    public void EveryBurningPlague_AndStandingOffer_Surface()
    {
        var state = Run();
        var threads = HandoffView.OpenThreads(state);
        foreach (var plague in state.Plagues)
            if (plague.Active)
                Assert.Contains(threads,
                    t => t.Kind == "plague" && t.Text.Contains(plague.Name));
        foreach (var rel in state.Relations)
            if (RelationsOps.BothLive(state, rel)
                && rel.OfferedRung != TreatyRung.None && rel.OfferedById >= 0)
                Assert.Contains(threads, t => t.Kind == "offer"
                    && t.Text.Contains(state.Actors[rel.OfferedById].Name));
    }

    [Fact]
    public void TheWorldArrives_InMotion()
    {
        // handoff.md: the final epoch is not tidied — a real history should
        // end with SOMETHING loaded, half-won, leveraged, or unanswered
        Assert.True(HandoffView.OpenThreads(Run()).Count > 0,
            "40 epochs ended as a museum — nothing in motion at handoff");
    }

    [Fact]
    public void PerWarIndex_CarriesOneCampaign_Whole()
    {
        var state = Run();
        var war = state.Wars.FirstOrDefault();
        if (war == null) return;   // a pacifist seed proves nothing
        var events = state.Log.ForWar(war.Id).ToList();
        // the declaration is always in the index
        Assert.Contains(events, e => e.Type == WorldEventType.WarDeclared
            && e.Payload is WarDeclaredPayload p && p.WarId == war.Id);
        // and nothing from any OTHER war leaks in
        foreach (var e in events)
            Assert.Equal(war.Id, ((IWarPayload)e.Payload!).WarId);
        // an ended war's index closes with its peace
        if (!war.Active)
            Assert.Contains(events, e => e.Type == WorldEventType.PeaceSettled);
    }
}
