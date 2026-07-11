using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice I task 4 — stances and reputation (perception-and-news.md
/// §Stances and reputation): news arrival moves the observer's stance toward
/// the actors involved, filtered through temperament; reputation is derived
/// per audience, feeds the warmth target, and decays as memory fades.</summary>
public class ReputationTests
{
    private static WorldEvent TreatyBreak(int breakerId, HexCoordinate hex) =>
        new WorldEvent(0, 100, ClockStratum.Generational,
            WorldEventType.TreatyBroken, new[] { breakerId, 3 }, hex, 1.0,
            -0.7, EventVisibility.Public,
            new TreatyBrokenPayload(breakerId, 3, "Breaker", "Betrayed", 2));

    [Fact]
    public void OpenTraders_SanctionTreatyBreakers_Harder()
    {
        var state = EpochTestKit.Seeded().State;
        var observer = state.Actors[0];
        observer.Entered = true;
        var species = state.Skeleton.Species[state.PolityOf(0).SpeciesId];
        var e = TreatyBreak(breakerId: 2, observer.Seat);

        species.Openness = 0.9;
        ReputationOps.Judge(state, observer, e, attenuation: 1.0);
        double openVerdict = ReputationOps.StanceOf(state, 0, 2);

        observer.Beliefs.Stances.Clear();
        species.Openness = 0.1;
        ReputationOps.Judge(state, observer, e, attenuation: 1.0);
        double insularVerdict = ReputationOps.StanceOf(state, 0, 2);

        Assert.True(openVerdict < insularVerdict && insularVerdict < 0,
            $"open {openVerdict} should judge harder than insular {insularVerdict}");
    }

    [Fact]
    public void MilitantCultures_RespectBoldConquest()
    {
        var state = EpochTestKit.Seeded().State;
        var observer = state.Actors[0];
        observer.Entered = true;
        var species = state.Skeleton.Species[state.PolityOf(0).SpeciesId];
        var e = new WorldEvent(0, 100, ClockStratum.Generational,
            WorldEventType.WarDeclared, new[] { 2, 3 }, observer.Seat, 2.0,
            -0.8, EventVisibility.Public,
            new WarDeclaredPayload(0, "The Test War", 2, 3, "Att", "Def",
                (int)CasusBelli.ResourceSeizure,
                (int)WarDemand.CedeObjectives));

        species.Militancy = 1.0;
        ReputationOps.Judge(state, observer, e, attenuation: 1.0);
        Assert.True(ReputationOps.StanceOf(state, 0, 2) > 0,
            "a hawk admires the bold");

        observer.Beliefs.Stances.Clear();
        species.Militancy = 0.0;
        ReputationOps.Judge(state, observer, e, attenuation: 1.0);
        Assert.True(ReputationOps.StanceOf(state, 0, 2) < 0,
            "a dove condemns the aggressor");
    }

    [Fact]
    public void Reputation_RepricesTheWarmthTarget()
    {
        var state = EpochTestKit.Seeded().State;
        state.Actors[0].Entered = true;
        state.Actors[1].Entered = true;
        var relation = new PolityRelation(0, 1, 0);
        state.Relations.Add(relation);
        double neutral = RelationsOps.WarmthTarget(state, relation, 0);

        state.Actors[0].Beliefs.Stances[1] = -1.0;
        state.Actors[1].Beliefs.Stances[0] = -1.0;
        double infamous = RelationsOps.WarmthTarget(state, relation, 0);
        Assert.True(infamous < neutral,
            "a monstrous reputation should cool the table");
        Assert.Equal(-state.Config.Relations.ReputationWarmthWeight,
                     relation.LastWarmthTerms[5], 6);
    }

    [Fact]
    public void Stances_Decay_TowardIndifference()
    {
        var state = EpochTestKit.Seeded().State;
        state.Actors[0].Beliefs.Stances[2] = -0.8;
        ReputationOps.DecayStances(state);
        double after = ReputationOps.StanceOf(state, 0, 2);
        Assert.True(after > -0.8 && after < 0,
            "memory fades but is not erased in one epoch");
    }

    [Fact]
    public void ReputationShock_TravelsAtNewsSpeed()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        var a1 = state.Actors[1];
        a0.Entered = true;
        a1.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, a1.Id,
            new HexCoordinate(a0.Seat.Q + 30, a0.Seat.R), 2, 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        state.WorldYear = 100;
        // the distant polity breaks a treaty at home: word crawls 60 years
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.TreatyBroken, new[] { 1, 3 }, pb.Hex, 1.0, -0.7,
            EventVisibility.Public,
            new TreatyBrokenPayload(1, 3, "Breaker", "Betrayed", 2)));
        new ChroniclePhase().Run(state);

        new PerceptionPhase().Run(state);
        Assert.Equal(0.0, ReputationOps.StanceOf(state, 0, 1), 6);

        state.WorldYear += 75;                            // the word lands
        new PerceptionPhase().Run(state);
        Assert.True(ReputationOps.StanceOf(state, 0, 1) < 0,
            "the shock reprices the stance when it arrives, not before");
    }

    [Fact]
    public void Stances_Serialize_AndSurviveTheRoundTrip()
    {
        var state = EpochTestKit.Seeded().State;
        state.Actors[0].Beliefs.Stances[1] = -0.42;
        state.Actors[0].Beliefs.Stances[2] = 0.17;
        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("\nSTANCE|0|1|", text);
        var loaded = ArtifactSerializer.Load(new StringReader(text));
        Assert.Equal(-0.42, loaded.Actors[0].Beliefs.Stances[1], 9);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
