using System;
using System.IO;
using System.Linq;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class SerializerTests
{
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = 8 });

    [Fact]
    public void SameConfig_ByteIdenticalSerialization()
    {
        Assert.Equal(SkeletonSerializer.ToText(Build()), SkeletonSerializer.ToText(Build()));
    }

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var original = Build();
        var loaded = SkeletonSerializer.Load(new StringReader(SkeletonSerializer.ToText(original)));
        Assert.Equal(SkeletonSerializer.ToText(original), SkeletonSerializer.ToText(loaded));
        Assert.Equal(original.Polities.Count, loaded.Polities.Count);
        Assert.Equal(original.Events.Count, loaded.Events.Count);
        Assert.Equal(original.Cells.Count, loaded.Cells.Count);
        Assert.Equal(original.Config.MasterSeed, loaded.Config.MasterSeed);
    }

    [Fact]
    public void SchemaVersionMismatch_Throws_NeverSilentlyRebuilds()
    {
        var text = SkeletonSerializer.ToText(Build());
        var tampered = text.Replace("STARGEN-SKELETON|3", "STARGEN-SKELETON|999");
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader(tampered)));
    }

    [Fact]
    public void Load_RecordBeforeConfig_Throws()
    {
        var text = "STARGEN-SKELETON|3\nANCHOR|0|0|1|0|0|-1\nEND\n";
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader(text)));
    }

    [Fact]
    public void Load_TruncatedCellLine_Throws()
    {
        var text = SkeletonSerializer.ToText(Build());
        var lines = text.Split('\n');
        var cellLineIndex = Array.FindIndex(lines, l => l.StartsWith("CELL|"));
        Assert.True(cellLineIndex >= 0, "fixture must contain a CELL line");
        var fields = lines[cellLineIndex].Split('|');
        lines[cellLineIndex] = string.Join("|", fields.Take(3));
        var tampered = string.Join("\n", lines);
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader(tampered)));
    }

    [Fact]
    public void Load_NonNumericSchemaVersion_Throws()
    {
        var text = "STARGEN-SKELETON|abc\nEND\n";
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader(text)));
    }

    [Fact]
    public void GoldenSnapshot_SmallGalaxyHeader()
    {
        // Golden guard against unintended drift (spec §10). If this fails because of an
        // INTENTIONAL generation change, update the literal and say so in the commit.
        var s = SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = 7, GalaxyRadiusCells = 3 });
        var lines = SkeletonSerializer.ToText(s).Split('\n');
        Assert.Equal("STARGEN-SKELETON|3", lines[0].TrimEnd('\r'));
        // Golden facts recorded at implementation time — fill the two literals with the
        // observed values on first run, then they are frozen:
        Assert.Equal(2, s.Polities.Count);
        // ECONMIGRATION: re-freeze in serializer-v4 task
        Assert.Equal(30, s.Events.Count);
    }

    [Fact]
    public void RoundTrip_PreservesNewConfigFields()
    {
        var s = SkeletonBuilder.Build(new GalaxyConfig
        {
            MasterSeed = 11, GalaxyRadiusCells = 3,
            ArmStrength = 0.6, CoreRadius = 0.25, DiscFalloff = 0.7,
            MineralAnchorMultiplier = 2.0, PrecursorAnchorMultiplier = 0.5,
        });
        string text = SkeletonSerializer.ToText(s);
        var loaded = SkeletonSerializer.Load(new StringReader(text));
        Assert.Equal(0.6, loaded.Config.ArmStrength);
        Assert.Equal(0.25, loaded.Config.CoreRadius);
        Assert.Equal(0.7, loaded.Config.DiscFalloff);
        Assert.Equal(2.0, loaded.Config.MineralAnchorMultiplier);
        Assert.Equal(0.5, loaded.Config.PrecursorAnchorMultiplier);
        Assert.Equal(text, SkeletonSerializer.ToText(loaded));
    }

    [Fact]
    public void Load_RejectsSchemaV2()
    {
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader("STARGEN-SKELETON|2\nEND\n")));
    }
}
