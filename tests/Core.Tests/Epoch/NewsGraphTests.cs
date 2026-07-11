using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice I task 1 — the news graph (narrative/perception-and-news.md,
/// fleets/ships-and-fleets.md §Information carriage): news speed per lane is
/// traffic-derived — busy lanes carry news fast, backwaters slowly, wilds
/// barely. The delay field is derived from live fleet state every epoch,
/// never persisted.</summary>
public class NewsGraphTests
{
    /// <summary>A hub with two spokes of equal length: one busy lane, one
    /// quiet lane, and a fourth port with no lane at all.</summary>
    private static (SimState State, Port Hub, Port Busy, Port Quiet, Port Wild)
        StarFixture()
    {
        var state = EpochTestKit.Seeded().State;
        var owner = state.Actors[0];
        owner.Entered = true;
        var hub = new Port(0, owner.Id, owner.Seat, tier: 2, foundedYear: 0);
        var busy = new Port(1, owner.Id,
            new HexCoordinate(owner.Seat.Q + 10, owner.Seat.R), 2, 0);
        var quiet = new Port(2, owner.Id,
            new HexCoordinate(owner.Seat.Q - 10, owner.Seat.R), 2, 0);
        var wild = new Port(3, owner.Id,
            new HexCoordinate(owner.Seat.Q, owner.Seat.R + 10), 2, 0);
        foreach (var p in new[] { hub, busy, quiet, wild })
        {
            state.Ports.Add(p);
            state.Markets.Add(new Market(p.Id, state.Config.Economy));
        }
        state.Lanes.Add(new Lane(0, 0, 1, builtYear: 0));
        state.Lanes.Add(new Lane(1, 0, 2, builtYear: 0));
        EpochTestKit.PostFreight(state, owner.Id, laneId: 0, hulls: 8);
        EpochTestKit.PostFreight(state, owner.Id, laneId: 1, hulls: 1);
        return (state, hub, busy, quiet, wild);
    }

    [Fact]
    public void LaneNewsSpeed_RisesWithPostedTraffic()
    {
        var (state, _, _, _, _) = StarFixture();
        double busySpeed = NewsOps.LaneNewsSpeed(state, state.Lanes[0]);
        double quietSpeed = NewsOps.LaneNewsSpeed(state, state.Lanes[1]);
        Assert.True(busySpeed > quietSpeed,
            $"busy lane should out-carry the backwater ({busySpeed} vs {quietSpeed})");
        Assert.True(quietSpeed >= state.Config.News.BaseLaneSpeedHexPerYear,
            "a standing lane never drops below the base carriage");
    }

    [Fact]
    public void DelayField_BusyLane_BeatsBackwater_BeatsWilds()
    {
        var (state, hub, busy, quiet, wild) = StarFixture();
        var delay = NewsOps.DelayFromHex(state, hub.Hex);
        Assert.Equal(0.0, delay[hub.Id], 6);
        Assert.True(delay[busy.Id] < delay[quiet.Id],
            "news along the busy spoke should arrive first");
        Assert.True(delay[quiet.Id] < delay[wild.Id],
            "any lane should beat the off-lane crawl");
        // the wilds crawl: 10 hexes at the off-lane speed
        Assert.Equal(10.0 / state.Config.News.OffLaneSpeedHexPerYear,
                     delay[wild.Id], 6);
    }

    [Fact]
    public void DelayField_RoutesAroundTheLongWay_WhenLanesCarryIt()
    {
        // hub—busy lane plus busy—far lane: two fast hops beat one crawl
        var (state, hub, busy, _, _) = StarFixture();
        var owner = state.Actors[0];
        var far = new Port(4, owner.Id,
            new HexCoordinate(busy.Hex.Q + 10, busy.Hex.R), 2, 0);
        state.Ports.Add(far);
        state.Markets.Add(new Market(far.Id, state.Config.Economy));
        state.Lanes.Add(new Lane(2, 1, 4, builtYear: 0));
        EpochTestKit.PostFreight(state, owner.Id, laneId: 2, hulls: 8);
        var delay = NewsOps.DelayFromHex(state, hub.Hex);
        double crawl = 20.0 / state.Config.News.OffLaneSpeedHexPerYear;
        Assert.True(delay[far.Id] < crawl,
            "two lane hops should beat the direct wilds crawl");
        Assert.True(delay[far.Id] >= delay[busy.Id],
            "the far port hears after the relay it routes through");
    }

    [Fact]
    public void DelayYears_ReadsAnActorsNearestEar()
    {
        var (state, hub, busy, _, _) = StarFixture();
        // a second polity owning only the busy-spoke port hears through it
        var other = state.Actors[1];
        other.Entered = true;
        busy.OwnerActorId = other.Id;
        var delay = NewsOps.DelayFromHex(state, hub.Hex);
        Assert.Equal(delay[busy.Id],
                     NewsOps.DelayYears(state, other.Id, delay, hub.Hex), 6);
        // the hub's owner hears instantly — the event is at its own port
        Assert.Equal(0.0,
                     NewsOps.DelayYears(state, state.Actors[0].Id, delay,
                                        hub.Hex), 6);
    }
}
