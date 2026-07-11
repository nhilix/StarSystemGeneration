using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice I task 3 — news pulses (perception-and-news.md §News
/// pulses): public events over the magnitude floor pulse from their hex at
/// Chronicle; Perception delivers each when its age covers the traffic-
/// derived delay. Arrival, not emission, updates belief; secret events
/// emit nothing.</summary>
public class NewsPulseTests
{
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

    [Fact]
    public void PublicEventsPulse_RegionalAndSecretDont()
    {
        var (state, _, pb) = DistantPairFixture();
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.PortEstablished, new[] { 1 }, pb.Hex, 1.0, 1.0,
            EventVisibility.Public, null));
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.FacilityBuilt, new[] { 1 }, pb.Hex, 1.0, 1.0,
            EventVisibility.Regional, null));
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.LaneOpened, new[] { 1 }, pb.Hex, 1.0, 1.0,
            EventVisibility.Secret, null));
        new ChroniclePhase().Run(state);
        var pulse = Assert.Single(state.Pulses);
        Assert.Equal(pb.Hex, pulse.Origin);
        Assert.Equal(100, pulse.EmitYear);
    }

    [Fact]
    public void SmallPublicEvents_StayBelowTheFloor()
    {
        var (state, _, pb) = DistantPairFixture();
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.PortEstablished, new[] { 1 }, pb.Hex,
            Magnitude: 0.1, Valence: 1.0, EventVisibility.Public, null));
        new ChroniclePhase().Run(state);
        Assert.Empty(state.Pulses);
    }

    [Fact]
    public void Arrival_NotEmission_DeliversTheWord()
    {
        var (state, _, pb) = DistantPairFixture();
        // word born at the far port (60 crawl-years from the observer)
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.PortEstablished, new[] { 1 }, pb.Hex, 1.0, 1.0,
            EventVisibility.Public, null));
        new ChroniclePhase().Run(state);
        var pulse = state.Pulses[0];

        new PerceptionPhase().Run(state);
        Assert.True(pulse.DeliveredTo(1), "the emitter hears its own news");
        Assert.False(pulse.DeliveredTo(0), "the word hasn't crossed the wilds");

        state.WorldYear += 25;
        new PerceptionPhase().Run(state);
        Assert.False(pulse.DeliveredTo(0));

        state.WorldYear += 50;                            // 75 ≥ 60: arrival
        new PerceptionPhase().Run(state);
        Assert.True(pulse.DeliveredTo(0));
        foreach (var (actorId, year) in pulse.Delivered)
            if (actorId == 0)
                Assert.Equal(175, year);                  // the journey shows
    }

    [Fact]
    public void StalePulses_AttenuateToRumor()
    {
        var (state, _, pb) = DistantPairFixture();
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.PortEstablished, new[] { 1 }, pb.Hex, 1.0, 1.0,
            EventVisibility.Public, null));
        new ChroniclePhase().Run(state);
        state.WorldYear += 200;   // past PulseMaxYears — the word is lost
        new PerceptionPhase().Run(state);
        Assert.False(state.Pulses[0].DeliveredTo(0));
    }

    [Fact]
    public void Pulses_Serialize_AndSurviveTheRoundTrip()
    {
        var (state, _, pb) = DistantPairFixture();
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.PortEstablished, new[] { 1 }, pb.Hex, 1.0, 1.0,
            EventVisibility.Public, null));
        new ChroniclePhase().Run(state);
        new PerceptionPhase().Run(state);
        string text = ArtifactSerializer.ToText(state);
        Assert.Contains("\nPULSE|0|", text);
        var loaded = ArtifactSerializer.Load(new StringReader(text));
        Assert.Single(loaded.Pulses);
        Assert.Equal(state.Pulses[0].Delivered, loaded.Pulses[0].Delivered);
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }
}
