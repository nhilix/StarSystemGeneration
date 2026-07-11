using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice I task 2 — compressed belief (perception-and-news.md
/// §Perception state per actor): self-facts read fresh, other-side facts
/// read through snapshots that refresh at news speed and freeze between
/// refreshes. Decisions on belief, consequences on truth (P3).</summary>
public class BeliefTests
{
    /// <summary>Two entered polities, one port each, 30 hexes apart —
    /// far enough that the off-lane crawl (0.5 hex/yr → 60 years) outlasts
    /// a 25-year epoch, so word takes several epochs without a lane.</summary>
    private static (SimState State, Port A, Port B) DistantPairFixture()
    {
        var state = EpochTestKit.Seeded().State;
        var a0 = state.Actors[0];
        var a1 = state.Actors[1];
        a0.Entered = true;
        a1.Entered = true;
        var pa = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        var hexB = new HexCoordinate(a0.Seat.Q + 30, a0.Seat.R);
        var pb = new Port(1, a1.Id, hexB, tier: 2, foundedYear: 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        state.Relations.Add(new PolityRelation(a0.Id, a1.Id, 0));
        state.WorldYear = 100;
        return (state, pa, pb);
    }

    private static RelationBrief BriefOf(SimState state, int selfId, int otherId)
    {
        new PerceptionPhase().Run(state);
        foreach (var rel in state.Actors[selfId].Perception!.Relations)
            if (rel.OtherPolityId == otherId) return rel;
        throw new System.InvalidOperationException("no brief for the pair");
    }

    private static void ArmTheDistantNavy(SimState state, int actorId, Port home)
    {
        var design = DesignRegistry.Current(state, actorId,
                ShipRole.Line, ShipSize.Medium)
            ?? DesignRegistry.Register(state, actorId,
                ShipRole.Line, ShipSize.Medium, grade: 0.5);
        var fleet = new FleetRecord(state.Fleets.Count, actorId, home.Hex)
        {
            Posture = FleetPosture.Reserve,
            HomePortId = home.Id,
        };
        fleet.AddHulls(design.Id, 6, 0.5);
        state.Fleets.Add(fleet);
        state.PolityOf(actorId).HullsBuilt += 6;
    }

    [Fact]
    public void OtherStrength_StalesAtDistance_ThenTheNewsArrives()
    {
        var (state, _, pb) = DistantPairFixture();
        // first sight: contact is an information event — belief fresh
        var brief = BriefOf(state, 0, 1);
        Assert.Equal(0.0, brief.OtherStrength, 6);

        // the distant navy arms; no lane carries the word (60-year crawl)
        ArmTheDistantNavy(state, 1, pb);
        double truth = FleetOps.WarStrength(state, 1);
        Assert.True(truth > 0, "the armed navy should weigh something");

        state.WorldYear += 25;
        brief = BriefOf(state, 0, 1);
        Assert.Equal(0.0, brief.OtherStrength, 6);   // still the old world

        state.WorldYear += 50;                        // 75 ≥ 60: word arrives
        brief = BriefOf(state, 0, 1);
        Assert.Equal(truth, brief.OtherStrength, 6);
    }

    [Fact]
    public void BusyLane_CarriesTheWord_WithinAnEpoch()
    {
        var (state, _, pb) = DistantPairFixture();
        state.Lanes.Add(new Lane(0, 0, 1, builtYear: 0));
        EpochTestKit.PostFreight(state, 0, laneId: 0, hulls: 8);
        var brief = BriefOf(state, 0, 1);
        Assert.Equal(0.0, brief.OtherStrength, 6);

        ArmTheDistantNavy(state, 1, pb);
        double truth = FleetOps.WarStrength(state, 1);
        state.WorldYear += 25;
        brief = BriefOf(state, 0, 1);
        Assert.Equal(truth, brief.OtherStrength, 6);  // the corridor talks
    }

    [Fact]
    public void WarBriefs_ReportThroughTheFog()
    {
        var (state, _, _) = DistantPairFixture();
        var war = new War(0, "The Test War", 0, 1, CasusBelli.BorderIncident,
                          -1, WarDemand.Reparations, state.WorldYear)
        {
            AttackerStrengthAtStart = 1.0,
            DefenderStrengthAtStart = 1.0,
        };
        state.Wars.Add(war);
        // first sight is fresh — a declaration happens TO you
        new PerceptionPhase().Run(state);
        Assert.Equal(0.0,
            state.Actors[1].Perception!.Wars[0].OwnSideExhaustion, 6);

        // the front grinds, but the defender's court is 60 news-years away
        war.DefenderExhaustion = 0.9;
        state.WorldYear += 25;
        new PerceptionPhase().Run(state);
        Assert.Equal(0.0,
            state.Actors[1].Perception!.Wars[0].OwnSideExhaustion, 6);

        state.WorldYear += 50;                        // the reports land
        new PerceptionPhase().Run(state);
        Assert.Equal(0.9,
            state.Actors[1].Perception!.Wars[0].OwnSideExhaustion, 6);
    }

    /// <summary>The fog of war end to end (slice I task 5): the concession
    /// decision reads the believed exhaustion, so a distant loser keeps
    /// fighting until the front reports reach its court — wars run past
    /// their rational end (war.md §Termination, P3).</summary>
    [Fact]
    public void TheDistantLoser_FightsOn_UntilTheNewsArrives()
    {
        var (state, _, _) = DistantPairFixture();
        var war = new War(0, "The Fog War", 0, 1, CasusBelli.BorderIncident,
                          -1, WarDemand.Reparations, state.WorldYear);
        state.Wars.Add(war);
        new PerceptionPhase().Run(state);      // fresh sight: nothing wrong yet
        war.DefenderExhaustion = 0.9;          // then the fronts collapse

        state.WorldYear += 25;
        new PerceptionPhase().Run(state);
        new IntentPhase().Run(state);
        Assert.False(SuesForPeace(state, 1),
            "the court hasn't heard — the war runs past its rational end");

        state.WorldYear += 50;                 // the reports finally land
        new PerceptionPhase().Run(state);
        new IntentPhase().Run(state);
        Assert.True(SuesForPeace(state, 1),
            "the arrived truth breaks the will to fight");
    }

    private static bool SuesForPeace(SimState state, int actorId)
    {
        foreach (var d in state.Decisions)
            foreach (var act in d.Decision.Acts)
                if (act is SettlementResponseAct sue && sue.Accept
                    && sue.ActorId == actorId)
                    return true;
        return false;
    }

    [Fact]
    public void Beliefs_Serialize_AndSurviveTheRoundTrip()
    {
        var (state, _, pb) = DistantPairFixture();
        ArmTheDistantNavy(state, 1, pb);
        new PerceptionPhase().Run(state);
        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("\nPBEL|0|1|", text);

        var loaded = ArtifactSerializer.Load(new StringReader(text));
        var original = state.Actors[0].Beliefs.Polities[1];
        var revived = loaded.Actors[0].Beliefs.Polities[1];
        Assert.Equal(original.HeardYear, revived.HeardYear);
        Assert.Equal(original.Strength, revived.Strength, 9);
        Assert.Equal(original.DefensiveStrength, revived.DefensiveStrength, 9);
        Assert.Equal(original.Menu.Count, revived.Menu.Count);
        Assert.Equal(original.ObjectiveCandidates, revived.ObjectiveCandidates);
        // byte-identity both ways: the belief layer is state, not survey
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
