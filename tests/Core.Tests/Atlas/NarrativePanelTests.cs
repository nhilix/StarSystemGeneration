using System;
using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>K3: the Belief/News/Stances tabs and Chronicle/Eras panels —
/// NarrativeView parity: belief-vs-truth rows, live pulse liveness
/// (age ≤ PulseMaxYears and undelivered somewhere), the ±0.3 stance
/// verdicts, era-annotated chronicle with headers at boundaries.</summary>
public class NarrativePanelTests
{
    private static readonly Lazy<SimState> Ran = new(() =>
    {
        var state = EpochTestKit.Seeded().State;
        new EpochEngine().Run(state);
        return state;
    });

    [Fact]
    public void BeliefRowsSitBesideTheTruth()
    {
        var state = Ran.Value;
        foreach (var observer in state.Actors)
        {
            if (observer.Beliefs.Polities.Count == 0) continue;
            var rows = BeliefPanel.Rows(new AtlasReadModel(state),
                EyeContext.God(state.WorldYear), observer.Id);
            Assert.Equal(observer.Beliefs.Polities.Count, rows.Count);
            foreach (var row in rows)
            {
                var b = observer.Beliefs.Polities[row.SubjectId];
                Assert.Equal(b.Strength, row.BelievedStrength);
                Assert.Equal(FleetOps.WarStrength(state, row.SubjectId),
                             row.TruthStrength);
                Assert.Equal(state.WorldYear - b.HeardYear, row.StaleYears);
                Assert.Equal(b.Menu.Count, row.MenuCount);
            }
            return;
        }
        throw new InvalidOperationException("nobody believes anything");
    }

    [Fact]
    public void WarBeliefRowsCoverTheActiveWars()
    {
        var state = Ran.Value;
        foreach (var observer in state.Actors)
        {
            int expected = 0;
            foreach (var wb in observer.Beliefs.Wars.Values)
                if (state.Wars[wb.WarId].Active) expected++;
            var rows = BeliefPanel.WarRows(new AtlasReadModel(state),
                EyeContext.God(state.WorldYear), observer.Id);
            Assert.Equal(expected, rows.Count);
            foreach (var row in rows)
            {
                var war = state.Wars[row.WarId];
                double truth = war.OnAttackerSide(observer.Id)
                    ? war.AttackerExhaustion : war.DefenderExhaustion;
                Assert.Equal(truth, row.TruthExhaustion);
            }
        }
    }

    [Fact]
    public void PulseLivenessMatchesTheRenderer()
    {
        var state = Ran.Value;
        int entered = 0;
        foreach (var a in state.Actors)
            if (a.Entered && a.Kind == ActorKind.Polity) entered++;
        int expectedLive = 0;
        foreach (var p in state.Pulses)
            if (state.WorldYear - p.EmitYear
                    <= state.Config.News.PulseMaxYears
                && p.Delivered.Count < entered) expectedLive++;
        var rows = NewsPanel.LivePulses(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear));
        Assert.Equal(expectedLive, rows.Count);
        // newest-first, the renderer's reading order
        for (int i = 1; i < rows.Count; i++)
            Assert.True(rows[i - 1].Id > rows[i].Id);
        if (rows.Count > 0)
            Assert.Equal(entered, rows[0].EnteredCount);
    }

    [Fact]
    public void APulseJourneyListsItsDeliveries()
    {
        var state = Ran.Value;
        foreach (var p in state.Pulses)
        {
            if (p.Delivered.Count == 0) continue;
            var card = NewsPanel.Journey(new AtlasReadModel(state),
                EyeContext.God(state.WorldYear), p.Id)!;
            Assert.Equal(p.Delivered.Count, card.Deliveries.Count);
            Assert.Equal(p.Delivered[0].Year - p.EmitYear,
                         card.Deliveries[0].TransitYears);
            Assert.False(string.IsNullOrEmpty(card.EventText));
            return;
        }
        throw new InvalidOperationException("no delivered pulse in the run");
    }

    [Fact]
    public void StanceVerdictsBreakAtPointThree()
    {
        Assert.Equal(StanceVerdict.Monster, StancesPanel.VerdictOf(-0.3));
        Assert.Equal(StanceVerdict.Neutral, StancesPanel.VerdictOf(-0.29));
        Assert.Equal(StanceVerdict.Neutral, StancesPanel.VerdictOf(0.29));
        Assert.Equal(StanceVerdict.Hero, StancesPanel.VerdictOf(0.3));
    }

    [Fact]
    public void StanceRowsReadTheBeliefState()
    {
        var state = Ran.Value;
        var rows = StancesPanel.Rows(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear));
        int expected = 0;
        foreach (var a in state.Actors)
            expected += a.Beliefs.Stances.Count;
        Assert.Equal(expected, rows.Count);
    }

    [Fact]
    public void TheChronicleStitchesEraHeadersAtBoundaries()
    {
        var state = Ran.Value;
        var eras = EraQueries.Eras(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear));
        Assert.Equal(EraDetector.Detect(state).Count, eras.Count);
        var lines = ChronicleQueries.Annotated(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear), state.Log.Events);
        Assert.Equal(state.Log.Events.Count, lines.Count);
        int headers = 0;
        string? last = null;
        foreach (var line in lines)
        {
            Assert.False(string.IsNullOrEmpty(line.Text));
            if (line.EraHeader != null && line.EraHeader != last)
            { headers++; last = line.EraHeader; }
        }
        if (eras.Count > 0)
            Assert.True(headers > 0, "no era header stitched into the run");
    }

    [Fact]
    public void TheDeepChronicleIsStrataFiltered()
    {
        var state = Ran.Value;
        var deep = ChronicleQueries.DeepTime(new AtlasReadModel(state),
            EyeContext.God(state.WorldYear));
        int expected = 0;
        foreach (var e in state.Log.Events)
            if (e.Stratum is ClockStratum.Cosmic or ClockStratum.Evolutionary)
                expected++;
        Assert.Equal(expected, deep.Count);
    }

    [Fact]
    public void ThePlaceChronicleUsesTheLogIndex()
    {
        var state = Ran.Value;
        foreach (var poi in state.Pois)
        {
            var lines = ChronicleQueries.AtPlace(new AtlasReadModel(state),
                EyeContext.God(state.WorldYear), poi.Hex);
            int expected = 0;
            foreach (var _ in state.Log.AtPlace(poi.Hex)) expected++;
            Assert.Equal(expected, lines.Count);
            return;
        }
    }
}
