using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Frozen at slice B's end (red-window closed at eyeball
/// acceptance): the reference artifact for seed 42, radius 12, default
/// epoch config. A diff here means history changed for existing configs —
/// deliberate changes regenerate the golden in the same commit and say why.</summary>
public class GoldenTests
{
    [Fact]
    public void ReferenceArtifact_MatchesTheFrozenGolden()
    {
        var gc = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 12 };
        var state = EpochGenesis.Seed(SkeletonBuilder.Build(gc),
                                      new EpochSimConfig { MasterSeed = 42 });
        new EpochEngine().Run(state);

        string golden = File.ReadAllText(Path.Combine(System.AppContext.BaseDirectory,
                "Goldens", "slice-b-artifact-seed42.txt"))
            .Replace("\r\n", "\n");   // survive checkout newline translation
        Assert.Equal(golden, ArtifactSerializer.ToText(state));
    }
}
