using System.IO;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice J — the delta boundary (narrative/handoff.md §The delta
/// boundary): everything the live game mutates records as deltas against
/// the artifact plus the continuing event log; the procedural baseline
/// stays pure. A save is: GalaxyConfig + the artifact + the deltas + the
/// log's continuation.</summary>
public class DeltaTests
{
    private static SimState Prologue(ulong seed = 42)
    {
        var state = EpochTestKit.Seeded(seed).State;
        state.Config.Sim.EpochCount = 10;
        new EpochEngine().Run(state);
        return state;
    }

    private static void Continue(SimState state, int steps, int years)
    {
        state.Config.Sim.YearsPerEpoch = years;
        var engine = new EpochEngine();
        for (int i = 0; i < steps; i++) engine.Step(state);
    }

    [Fact]
    public void Delta_RoundTrips_ByteIdentically()
    {
        // the gate: artifact + deltas + log continuation ≡ live state
        var state = Prologue();
        string baseText = ArtifactSerializer.ToText(state);
        Continue(state, 4, years: 25);
        string liveText = ArtifactSerializer.ToText(state);

        string delta = DeltaSerializer.Diff(baseText, liveText);
        Assert.Equal(liveText, DeltaSerializer.Apply(baseText, delta));
    }

    [Fact]
    public void Delta_RoundTrips_AtFineTick()
    {
        var state = Prologue();
        string baseText = ArtifactSerializer.ToText(state);
        Continue(state, 20, years: 1);
        string liveText = ArtifactSerializer.ToText(state);

        string delta = DeltaSerializer.Diff(baseText, liveText);
        Assert.Equal(liveText, DeltaSerializer.Apply(baseText, delta));
        // and the applied text is a loadable artifact
        using var reader = new StringReader(
            DeltaSerializer.Apply(baseText, delta));
        var loaded = ArtifactSerializer.Load(reader);
        Assert.Equal(state.WorldYear, loaded.WorldYear);
    }

    [Fact]
    public void TheProceduralBaseline_StaysPure()
    {
        // genesis strata never re-record: the delta carries no raster,
        // species, features, origins, or precursor sections
        var state = Prologue();
        string baseText = ArtifactSerializer.ToText(state);
        Continue(state, 4, years: 25);
        string delta = DeltaSerializer.Diff(baseText,
                                            ArtifactSerializer.ToText(state));
        foreach (var layer in new[] { "raster", "species", "features",
                                      "origins", "precursors" })
            Assert.DoesNotContain($"DLAYER|{layer}|", delta);
    }

    [Fact]
    public void TheLog_NeverCloses_ItAppends()
    {
        // the events section rides as a continuation, not a copy: the
        // delta carries exactly the events logged after the base save
        var state = Prologue();
        string baseText = ArtifactSerializer.ToText(state);
        int eventsAtBase = state.Log.Events.Count;
        Continue(state, 4, years: 25);
        string delta = DeltaSerializer.Diff(baseText,
                                            ArtifactSerializer.ToText(state));
        int continuation = 0;
        bool inEvents = false;
        foreach (var line in delta.Split('\n'))
        {
            if (line.StartsWith("DLAYER|"))
                inEvents = line.StartsWith("DLAYER|events|");
            else if (inEvents && line.StartsWith("EVENT|")) continuation++;
        }
        Assert.Equal(state.Log.Events.Count - eventsAtBase, continuation);
    }

    [Fact]
    public void AForeignBase_IsRefused()
    {
        var state = Prologue();
        string baseText = ArtifactSerializer.ToText(state);
        Continue(state, 2, years: 25);
        string delta = DeltaSerializer.Diff(baseText,
                                            ArtifactSerializer.ToText(state));
        string otherBase = ArtifactSerializer.ToText(Prologue(7));
        Assert.Throws<InvalidDataException>(
            () => DeltaSerializer.Apply(otherBase, delta));
    }

    [Fact]
    public void AnUnchangedWorld_IsAnEmptyDelta()
    {
        var state = Prologue();
        string baseText = ArtifactSerializer.ToText(state);
        string delta = DeltaSerializer.Diff(baseText, baseText);
        Assert.DoesNotContain("DLAYER|", delta);
        Assert.Equal(baseText, DeltaSerializer.Apply(baseText, delta));
    }
}
