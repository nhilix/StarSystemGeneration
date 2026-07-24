using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>AC4.3 — the chronicle/news copy layer (SimTraceView.Describe is
/// the SOLE formatter; NarrativeView.RenderNews/RenderChronicle and the
/// atlas NewsPanel/NarrativePanels all call it, never duplicate it).</summary>
public class SimTraceViewTests
{
    private static WorldEvent CargoSeizedEvent(double units) => new(
        Id: 0, WorldYear: 100, ClockStratum.Generational,
        WorldEventType.CargoSeized, Actors: new[] { 1, 0 },
        Location: new HexCoordinate(0, 0), Magnitude: units, Valence: -0.6,
        EventVisibility.Regional, new CargoSeizedPayload(5, 1, units));

    /// <summary>Before this fix the format string was `{Units:0}` — a real
    /// sub-1-unit prize (an off-lane crawl's small cargo, or the tail of a
    /// larger one) rounded to a misleading "0 units taken as prize". Now
    /// renders 2dp, so the actual haul is legible.</summary>
    [Fact]
    public void ASubOneUnitPrizeStillReadsAsNonZero()
    {
        var text = SimTraceView.Describe(CargoSeizedEvent(0.1610256132239097));
        Assert.Contains("0.16 units taken as prize", text);
        Assert.DoesNotContain("0 units taken as prize", text);
    }

    /// <summary>CargoSeizedPayload(ShipmentId, InterdictorActorId, Units)
    /// carries nothing that tells a war-interdiction seizure (on-lane) apart
    /// from an off-lane smuggling detection — both ShipmentOps.Sail call
    /// sites construct the identical payload shape. The copy must not
    /// overclaim a specific mechanism ("on a contested lane" was always
    /// false for the off-lane half) — this documents the neutral wording
    /// as the deliberate fix, not an oversight.</summary>
    [Fact]
    public void TheCopyNamesNoSpecificMechanism_PayloadCannotDistinguishOnVsOffLane()
    {
        var text = SimTraceView.Describe(CargoSeizedEvent(12.0));
        Assert.DoesNotContain("contested lane", text);
        Assert.DoesNotContain("off-lane", text);
        Assert.Contains("a convoy is seized — 12.00 units taken as prize",
            text);
    }
}
