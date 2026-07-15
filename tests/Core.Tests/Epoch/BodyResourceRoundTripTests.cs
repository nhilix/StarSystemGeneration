using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Dedicated round-trip coverage for the `bodyresources` artifact
/// layer (v1) — isolates the layer's own serialize/reload correctness from
/// the broader determinism suites, which only exercise it incidentally
/// through a full economy run.</summary>
public class BodyResourceRoundTripTests
{
    [Fact]
    public void BodyStocks_RoundTripByteIdentical()
    {
        var (_, state) = EpochTestKit.Seeded();
        state.BodyResources[(new HexCoordinate(2, -1), new BodyRef(0, 3))]
            = new Stock(GoodId.Ore, 1234.5, 0.42);
        state.BodyResources[(new HexCoordinate(2, -1), new BodyRef(1, 0))]
            = new Stock(GoodId.Exotics, 7.0, 0.8);

        var text1 = ArtifactSerializer.ToText(state);
        var reloaded = ArtifactSerializer.Load(new StringReader(text1));
        var text2 = ArtifactSerializer.ToText(reloaded);

        Assert.Equal(text1, text2);
        var s = reloaded.BodyResources[(new HexCoordinate(2, -1),
                                        new BodyRef(0, 3))];
        Assert.Equal(GoodId.Ore, s.Good);
        Assert.Equal(1234.5, s.Quantity, 6);
        Assert.Equal(0.42, s.Grade, 6);

        var s2 = reloaded.BodyResources[(new HexCoordinate(2, -1),
                                         new BodyRef(1, 0))];
        Assert.Equal(GoodId.Exotics, s2.Good);
        Assert.Equal(7.0, s2.Quantity, 6);
        Assert.Equal(0.8, s2.Grade, 6);
    }

    [Fact]
    public void BodyStocks_SerializedInSortedOrder()
    {
        // Insert out of (q, r, star, slot) order — the writer must still
        // emit BODYRESOURCE lines sorted, not in dictionary insertion order
        // (P6 fixed-iteration-order discipline).
        var (_, state) = EpochTestKit.Seeded();
        state.BodyResources[(new HexCoordinate(5, 5), new BodyRef(2, 1))]
            = new Stock(GoodId.Ore, 10.0, 0.5);
        state.BodyResources[(new HexCoordinate(0, 0), new BodyRef(1, 0))]
            = new Stock(GoodId.Volatiles, 20.0, 0.5);
        state.BodyResources[(new HexCoordinate(0, 0), new BodyRef(0, 9))]
            = new Stock(GoodId.Exotics, 30.0, 0.5);
        state.BodyResources[(new HexCoordinate(-3, 2), new BodyRef(0, 0))]
            = new Stock(GoodId.Organics, 40.0, 0.5);

        var text = ArtifactSerializer.ToText(state);
        var lines = text.Split('\n');
        int prevIndex = -1;
        foreach (var line in lines)
        {
            if (!line.StartsWith("BODYRESOURCE|")) continue;
            var f = line.Split('|');
            int q = int.Parse(f[1]);
            int r = int.Parse(f[2]);
            int star = int.Parse(f[3]);
            int slot = int.Parse(f[4]);
            int idx = System.Array.IndexOf(
                new[] { (-3, 2, 0, 0), (0, 0, 0, 9), (0, 0, 1, 0), (5, 5, 2, 1) },
                (q, r, star, slot));
            Assert.True(idx >= 0, $"unexpected key in output: {line}");
            Assert.True(idx > prevIndex,
                $"BODYRESOURCE lines out of (q,r,star,slot) sorted order at: {line}");
            prevIndex = idx;
        }
    }

    [Fact]
    public void Load_RejectsArtifactMissingTheBodyResourcesLayer()
    {
        // ArtifactSerializer.Load enforces that every layer in the Layers
        // table (including the appended "bodyresources") plus the END
        // sentinel are present — this is a general, pre-existing discipline
        // (line ~1698: "truncated epoch artifact: every layer and the END
        // sentinel are required"), not something new here. Per the
        // greenfield/no-old-golden-preservation policy (CLAUDE.md), an
        // artifact predating this layer is simply not a supported input —
        // it is expected to throw, not silently load with an empty
        // BodyResources dictionary. This test pins that discipline so a
        // future change doesn't accidentally loosen it for this layer.
        var (_, state) = EpochTestKit.Seeded();
        var text = ArtifactSerializer.ToText(state);

        var lines = new System.Collections.Generic.List<string>(text.Split('\n'));
        lines.RemoveAll(l => l.StartsWith("LAYER|bodyresources")
            || l.StartsWith("BODYRESOURCE|"));
        var stripped = string.Join("\n", lines);

        Assert.Throws<InvalidDataException>(
            () => ArtifactSerializer.Load(new StringReader(stripped)));
    }
}
