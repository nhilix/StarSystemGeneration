using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice I task 7 — the incremental POI compiler
/// (chronicle-and-poi.md §The POI compiler): residue anchors POIs inside
/// Chronicle every epoch — battlefields from wreckage concentrations, ruins
/// from dead cities, fallen capitals, memorials, precursor sites charted by
/// reach. One live anchor per hex by magnitude; POIs decay as consumed.</summary>
public class PoiCompilerTests
{
    private static (SimState State, Port Home) Fixture()
    {
        var state = EpochTestKit.Seeded().State;
        // pin the canvas: the real skeleton carries precursor sites that
        // would chart themselves into these fixtures
        state.Skeleton.PrecursorWaves.Clear();
        var a0 = state.Actors[0];
        a0.Entered = true;
        var home = new Port(0, a0.Id, a0.Seat, tier: 2, foundedYear: 0);
        state.Ports.Add(home);
        state.Markets.Add(new Market(0, state.Config.Economy));
        int sp = state.PolityOf(0).SpeciesId;
        state.Segments.Add(new PopulationSegment(0, 0, sp, sp, 3.0));
        state.WorldYear = 100;
        return (state, home);
    }

    private static void WreckHulls(SimState state, int actorId,
                                   HexCoordinate hex, int hulls)
    {
        var design = DesignRegistry.Current(state, actorId,
                ShipRole.Line, ShipSize.Medium)
            ?? DesignRegistry.Register(state, actorId,
                ShipRole.Line, ShipSize.Medium, grade: 0.5);
        var fleet = new FleetRecord(state.Fleets.Count, actorId, hex);
        fleet.AddHulls(design.Id, hulls, 0.5);
        state.Fleets.Add(fleet);
        state.PolityOf(actorId).HullsBuilt += hulls;
        FleetOps.Wreck(state, fleet, hulls, quiet: true);
    }

    [Fact]
    public void WreckageConcentration_AnchorsABattlefield()
    {
        var (state, home) = Fixture();
        var field = new HexCoordinate(home.Hex.Q + 5, home.Hex.R);
        WreckHulls(state, 0, field, 6);
        new ChroniclePhase().Run(state);
        var poi = Assert.Single(state.Pois);
        Assert.Equal(PoiType.Battlefield, poi.Type);
        Assert.Equal(field, poi.Hex);
        Assert.Equal(6.0, poi.Magnitude, 6);
        Assert.Contains(0, poi.ParticipantActorIds);
        Assert.Contains(state.Log.Events,
            e => e.Type == WorldEventType.BattlefieldMarked);
    }

    [Fact]
    public void SkirmishWrecks_StayBelowTheFloor()
    {
        var (state, home) = Fixture();
        WreckHulls(state, 0, new HexCoordinate(home.Hex.Q + 5, home.Hex.R), 2);
        new ChroniclePhase().Run(state);
        Assert.Empty(state.Pois);
    }

    [Fact]
    public void TheField_Grows_WhileWarsGrindTheSameGround()
    {
        var (state, home) = Fixture();
        var field = new HexCoordinate(home.Hex.Q + 5, home.Hex.R);
        WreckHulls(state, 0, field, 6);
        new ChroniclePhase().Run(state);
        WreckHulls(state, 0, field, 4);
        new ChroniclePhase().Run(state);
        var poi = Assert.Single(state.Pois);   // one anchor, grown
        Assert.Equal(10.0, poi.Magnitude, 6);
    }

    [Fact]
    public void ADeadCity_AnchorsRuins_AndRevivesOutOfThem()
    {
        var (state, home) = Fixture();
        var a1 = state.Actors[1];
        a1.Entered = true;
        var colony = new Port(1, a1.Id,
            new HexCoordinate(home.Hex.Q + 12, home.Hex.R), 1,
            foundedYear: 0);
        state.Ports.Add(colony);
        state.Markets.Add(new Market(1, state.Config.Economy));
        // empty for 100 years (well past the grace window): a dead city
        new ChroniclePhase().Run(state);
        var poi = Assert.Single(state.Pois,
            p => p.Type == PoiType.Ruins && p.SubjectId == colony.Id);
        Assert.False(poi.Depleted);

        // settlers return: the ruins come back to life
        int sp = state.PolityOf(1).SpeciesId;
        var settlers = new PopulationSegment(1, colony.Id, sp, sp, 2.0);
        state.Segments.Add(settlers);
        new ChroniclePhase().Run(state);
        Assert.True(poi.Depleted);

        // and when they leave again, the grace window restarts from the
        // last populated year — no same-epoch re-ruining (review fix 2)
        settlers.Size = 0;
        state.WorldYear += 25;
        new ChroniclePhase().Run(state);
        Assert.Single(state.Pois, p => p.Type == PoiType.Ruins);
        state.WorldYear += 50;                            // grace expires
        new ChroniclePhase().Run(state);
        Assert.Equal(2, state.Pois.FindAll(
            p => p.Type == PoiType.Ruins).Count);
    }

    [Fact]
    public void AnAnnexedPolity_LeavesARuinedCapital()
    {
        var (state, _) = Fixture();
        state.Actors[1].Entered = true;
        var war = new War(0, "The Last War", 0, 1, CasusBelli.BorderIncident,
                          -1, WarDemand.Annihilation, 50);
        state.Wars.Add(war);
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.PeaceSettled, new[] { 0, 1 },
            state.Actors[1].Seat, 3.0, -0.9, EventVisibility.Public,
            new PeaceSettledPayload(0, "The Last War",
                (int)WarOutcome.Annexed, WinnerId: 0, "A", "B", 0, 0)));
        new ChroniclePhase().Run(state);
        var poi = Assert.Single(state.Pois,
            p => p.Type == PoiType.RuinedCapital);
        Assert.Equal(state.Actors[1].Seat, poi.Hex);
        Assert.Equal(1, poi.SubjectId);
        Assert.Contains(state.Log.Events,
            e => e.Type == WorldEventType.CapitalRuined);
    }

    [Fact]
    public void OneAnchorPerHex_BiggerMagnitudeWins()
    {
        var (state, home) = Fixture();
        var hex = new HexCoordinate(home.Hex.Q + 5, home.Hex.R);
        // a deep famine memorializes the hex...
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.FamineStruck, new[] { 0 }, hex, 0.9, -1.0,
            EventVisibility.Regional, new FamineStruckPayload(0, 0.9)));
        new ChroniclePhase().Run(state);
        Assert.Single(state.Pois);
        // ...then a great battle over the same ground supersedes it
        WreckHulls(state, 0, hex, 8);
        new ChroniclePhase().Run(state);
        Assert.Equal(2, state.Pois.Count);
        Assert.True(state.Pois[0].Depleted, "the memorial is superseded");
        Assert.Equal(PoiType.Battlefield, state.Pois[1].Type);
        Assert.Same(state.Pois[1], PoiCompiler.LiveAt(state, hex));
    }

    [Fact]
    public void PrecursorSites_Chart_WhenExpansionReachesThem()
    {
        var (state, home) = Fixture();
        var wave = new StarGen.Core.Galaxy.PrecursorWave
        { Id = 0, Name = "Vanished Ones" };
        var near = new StarGen.Core.Galaxy.PrecursorSite
        {
            Id = 0, WaveId = 0,
            Type = StarGen.Core.Galaxy.PrecursorSiteType.Capital,
            Hex = new HexCoordinate(home.Hex.Q + 3, home.Hex.R),
            Dormant = true,
        };
        var far = new StarGen.Core.Galaxy.PrecursorSite
        {
            Id = 1, WaveId = 0,
            Type = StarGen.Core.Galaxy.PrecursorSiteType.Ruins,
            Hex = new HexCoordinate(home.Hex.Q + 40, home.Hex.R),
        };
        wave.Sites.Add(near);
        wave.Sites.Add(far);
        state.Skeleton.PrecursorWaves.Add(wave);
        new ChroniclePhase().Run(state);
        var poi = Assert.Single(state.Pois);
        Assert.Equal(PoiType.PrecursorSite, poi.Type);
        Assert.Equal(near.Hex, poi.Hex);
        Assert.True(poi.Dormant);
        // charted once — the next epoch doesn't re-chart
        new ChroniclePhase().Run(state);
        Assert.Single(state.Pois);
    }

    [Fact]
    public void Pois_Serialize_AndSurviveTheRoundTrip()
    {
        var (state, home) = Fixture();
        WreckHulls(state, 0, new HexCoordinate(home.Hex.Q + 5, home.Hex.R), 6);
        new ChroniclePhase().Run(state);
        state.Pois[0].HullsSalvaged = 2;
        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("\nPOI|0|", text);
        var loaded = ArtifactSerializer.Load(new StringReader(text));
        Assert.Single(loaded.Pois);
        Assert.Equal(state.Pois[0].Magnitude, loaded.Pois[0].Magnitude, 9);
        Assert.Equal(2, loaded.Pois[0].HullsSalvaged);
        Assert.Equal(state.Pois[0].SourceEventIds, loaded.Pois[0].SourceEventIds);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
