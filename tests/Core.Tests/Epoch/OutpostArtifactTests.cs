using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice DX task 2.1 — the state-model foundation: PopulationSegment
/// gains a serialized Hex (segments layer v4), SimState gains an Outposts
/// registry with its own outposts layer. No behaviour yet; these tests pin the
/// round-trip byte-identity the whole stage builds on (the BodyResources
/// lesson: a layer that lags the state breaks reload).</summary>
public class OutpostArtifactTests
{
    private static SimState Run(ulong seed = 42)
    {
        var state = EpochTestKit.Seeded(seed).State;
        new EpochEngine().Run(state);
        return state;
    }

    [Fact]
    public void SegmentHex_AndOutpost_RoundTripByteIdentical()
    {
        var built = Run();
        Assert.NotEmpty(built.Segments);

        // relocate a segment to a NON-default hex (not its port hex) so the
        // round-trip actually exercises the new Hex field, not the default.
        var seg = built.Segments[0];
        var moved = new HexCoordinate(seg.Hex.Q + 3, seg.Hex.R - 2);
        seg.Hex = moved;

        // found a lightweight outpost with a spaced name to exercise the
        // Name() free-text escaping through the pipe-delimited line format.
        built.Outposts.Add(new Outpost(0, "Rusthaven Landing", moved,
            ParentPortId: seg.PortId, FoundingYear: built.WorldYear)
        { Graduated = true });

        string first = ArtifactSerializer.ToText(built);
        var loaded = ArtifactSerializer.Load(new StringReader(first));

        // segment Hex survived exactly
        Assert.Equal(moved, loaded.Segments[0].Hex);

        // outpost survived, every field
        Assert.Single(loaded.Outposts);
        var o = loaded.Outposts[0];
        Assert.Equal(0, o.Id);
        Assert.Equal("Rusthaven Landing", o.Name);
        Assert.Equal(moved, o.Hex);
        Assert.Equal(seg.PortId, o.ParentPortId);
        Assert.Equal((long)built.WorldYear, o.FoundingYear);
        Assert.True(o.Graduated);

        // save ∘ load = identity, byte-for-byte
        Assert.Equal(first, ArtifactSerializer.ToText(loaded));
    }

    [Fact]
    public void SegmentHex_DefaultsToPortHex_AfterFullRun()
    {
        // no behaviour moves a segment's hex this task, and PortId is get-only,
        // so every segment must still sit at its administering port's hex — the
        // invariant Task 2.4 staffing depends on.
        var built = Run();
        Assert.NotEmpty(built.Segments);
        foreach (var s in built.Segments)
            Assert.Equal(built.Ports[s.PortId].Hex, s.Hex);
    }

    [Fact]
    public void Load_RefusesStaleSegmentsVersion()
    {
        string bumped = ArtifactSerializer.ToText(Run())
            .Replace("LAYER|segments|4", "LAYER|segments|3");
        var ex = Assert.Throws<InvalidDataException>(
            () => ArtifactSerializer.Load(new StringReader(bumped)));
        Assert.Contains("segments", ex.Message);
    }
}
