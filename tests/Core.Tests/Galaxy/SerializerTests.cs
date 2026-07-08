using System;
using System.IO;
using System.Linq;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class SerializerTests
{
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, SizeSectors = 4 });

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
        Assert.Equal(original.Cells.Length, loaded.Cells.Length);
        Assert.Equal(original.Config.MasterSeed, loaded.Config.MasterSeed);
    }

    [Fact]
    public void SchemaVersionMismatch_Throws_NeverSilentlyRebuilds()
    {
        var text = SkeletonSerializer.ToText(Build());
        var tampered = text.Replace("STARGEN-SKELETON|1", "STARGEN-SKELETON|999");
        Assert.Throws<InvalidDataException>(() =>
            SkeletonSerializer.Load(new StringReader(tampered)));
    }

    [Fact]
    public void GoldenSnapshot_SmallGalaxyHeader()
    {
        // Golden guard against unintended drift (spec §10). If this fails because of an
        // INTENTIONAL generation change, update the literal and say so in the commit.
        var s = SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = 7, SizeSectors = 2 });
        var lines = SkeletonSerializer.ToText(s).Split('\n');
        Assert.Equal("STARGEN-SKELETON|1", lines[0].TrimEnd('\r'));
        // Golden facts recorded at implementation time — fill the two literals with the
        // observed values on first run, then they are frozen:
        Assert.Equal(2, s.Polities.Count);
        Assert.Equal(23, s.Events.Count);
    }
}
