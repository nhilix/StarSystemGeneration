using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class EventLogTests
{
    private static WorldEvent Append(EventLog log, int year, HexCoordinate at, params int[] actors) =>
        log.Append(year, ClockStratum.Generational, WorldEventType.PolityEmerged,
                   actors, at, magnitude: 1.0, valence: 0.5,
                   EventVisibility.Public, new PolityEmergedPayload("Test"));

    [Fact]
    public void Append_AssignsSequentialIds_InLogOrder()
    {
        var log = new EventLog();
        var a = Append(log, 0, new HexCoordinate(1, 2), 0);
        var b = Append(log, 25, new HexCoordinate(3, 4), 1);
        Assert.Equal(0, a.Id);
        Assert.Equal(1, b.Id);
        Assert.Equal(new[] { a, b }, log.Events);
    }

    [Fact]
    public void Event_CarriesTheFullGrammarTuple()
    {
        var log = new EventLog();
        var e = log.Append(150, ClockStratum.Generational, WorldEventType.PolityEmerged,
                           new[] { 3, 7 }, new HexCoordinate(-2, 5),
                           magnitude: 2.5, valence: -0.25,
                           EventVisibility.Regional, new PolityEmergedPayload("Veyra"));
        Assert.Equal(150, e.WorldYear);
        Assert.Equal(ClockStratum.Generational, e.Stratum);
        Assert.Equal(WorldEventType.PolityEmerged, e.Type);
        Assert.Equal(new[] { 3, 7 }, e.Actors);
        Assert.Equal(new HexCoordinate(-2, 5), e.Location);
        Assert.Equal(2.5, e.Magnitude);
        Assert.Equal(-0.25, e.Valence);
        Assert.Equal(EventVisibility.Regional, e.Visibility);
        Assert.Equal("Veyra", Assert.IsType<PolityEmergedPayload>(e.Payload).PolityName);
    }

    [Fact]
    public void TypeFamilies_CoverAllEight()
    {
        Assert.Equal(EventFamily.Political, WorldEventTypes.FamilyOf(WorldEventType.PolityEmerged));
        // the family enum is the design's eight, exactly
        Assert.Equal(new[]
        {
            EventFamily.Cosmic, EventFamily.Evolutionary, EventFamily.Economic,
            EventFamily.Political, EventFamily.Military, EventFamily.Diplomatic,
            EventFamily.Corporate, EventFamily.Character,
        }, System.Enum.GetValues<EventFamily>());
    }

    [Fact]
    public void PlaceView_ReturnsOnlyEventsAtThatHex_InLogOrder()
    {
        var log = new EventLog();
        var here = new HexCoordinate(4, -1);
        var a = Append(log, 0, here, 0);
        Append(log, 25, new HexCoordinate(9, 9), 1);
        var c = Append(log, 50, here, 2);
        Assert.Equal(new[] { a, c }, log.AtPlace(here).ToArray());
    }

    [Fact]
    public void ActorView_ReturnsEventsTheActorParticipatesIn()
    {
        var log = new EventLog();
        var a = Append(log, 0, new HexCoordinate(0, 0), 1, 2);
        Append(log, 25, new HexCoordinate(0, 0), 3);
        var c = Append(log, 50, new HexCoordinate(1, 1), 2);
        Assert.Equal(new[] { a, c }, log.ForActor(2).ToArray());
    }

    [Fact]
    public void Views_AreComputed_NotStoredState()
    {
        var log = new EventLog();
        var here = new HexCoordinate(0, 0);
        Assert.Empty(log.AtPlace(here));
        Append(log, 0, here, 0);
        Assert.Single(log.AtPlace(here));   // same view call sees the appended event
    }
}
