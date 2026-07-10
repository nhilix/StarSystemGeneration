using System.IO;
using System.Linq;
using StarGen.Core.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

public class ArtifactTests
{
    private static SimState Run(ulong seed = 42)
    {
        var state = EpochTestKit.Seeded(seed).State;
        new EpochEngine().Run(state);
        return state;
    }

    [Fact]
    public void Artifact_ByteIdentical_AcrossIndependentRuns()
    {
        Assert.Equal(ArtifactSerializer.ToText(Run()), ArtifactSerializer.ToText(Run()));
    }

    [Fact]
    public void Artifact_DifferentSeed_Diverges()
    {
        Assert.NotEqual(ArtifactSerializer.ToText(Run(42)), ArtifactSerializer.ToText(Run(43)));
    }

    [Fact]
    public void Artifact_LoadVsRebuild_Equivalence()
    {
        string a = ArtifactSerializer.ToText(Run());
        var loaded = ArtifactSerializer.Load(new StringReader(a));
        Assert.Equal(a, ArtifactSerializer.ToText(loaded));       // save ∘ load = identity
    }

    [Fact]
    public void Load_ReconstructsALiveState()
    {
        var built = Run();
        var loaded = ArtifactSerializer.Load(new StringReader(ArtifactSerializer.ToText(built)));
        Assert.Equal(built.EpochIndex, loaded.EpochIndex);
        Assert.Equal(built.WorldYear, loaded.WorldYear);
        Assert.Equal(built.Ports.Count, loaded.Ports.Count);
        Assert.Equal(built.Lanes.Count, loaded.Lanes.Count);
        Assert.Equal(built.Segments.Count, loaded.Segments.Count);
        Assert.Equal(built.Log.Events.Count, loaded.Log.Events.Count);
        Assert.Equal(built.Skeleton.Cells.Count, loaded.Skeleton.Cells.Count);
        // controllers reattach: the loaded state can keep stepping
        loaded.Config.Sim.EpochCount = loaded.EpochIndex + 1;
        new EpochEngine().Run(loaded);
        Assert.Equal(built.EpochIndex + 1, loaded.EpochIndex);
        // event payloads round-trip typed
        var e = built.Log.Events.First(e => e.Type == WorldEventType.PortEstablished);
        var l = loaded.Log.Events.First(x => x.Id == e.Id);
        Assert.Equal(((PortEstablishedPayload)e.Payload!).PolityName,
                     ((PortEstablishedPayload)l.Payload!).PolityName);
    }

    [Fact]
    public void Artifact_RefusesForeignAndVersionMismatch()
    {
        Assert.Throws<InvalidDataException>(
            () => ArtifactSerializer.Load(new StringReader("nonsense")));
        string bumped = ArtifactSerializer.ToText(Run())
            .Replace("LAYER|ports|1", "LAYER|ports|9");
        var ex = Assert.Throws<InvalidDataException>(
            () => ArtifactSerializer.Load(new StringReader(bumped)));
        Assert.Contains("ports", ex.Message);
    }

    [Fact]
    public void Artifact_RefusesTruncation()
    {
        string text = ArtifactSerializer.ToText(Run());
        // cut before the events layer (loses END and the tail layers)
        string truncated = text.Substring(0, text.IndexOf("LAYER|events|1"));
        Assert.Throws<InvalidDataException>(
            () => ArtifactSerializer.Load(new StringReader(truncated)));
        // even with END re-appended, missing layers must refuse
        Assert.Throws<InvalidDataException>(
            () => ArtifactSerializer.Load(new StringReader(truncated + "END\n")));
    }

    [Fact]
    public void Artifact_IsCultureInvariant()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.InvariantCulture;
            string invariant = ArtifactSerializer.ToText(Run());
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("sv-SE");
            string swedish = ArtifactSerializer.ToText(Run());
            Assert.Equal(invariant, swedish);
            // load under the flipped culture too
            Assert.Equal(invariant,
                ArtifactSerializer.ToText(ArtifactSerializer.Load(new StringReader(swedish))));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }
}
