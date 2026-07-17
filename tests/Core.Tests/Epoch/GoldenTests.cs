using System.IO;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Frozen at slice BF's end (re-frozen; earlier slice MC, originally
/// slice B): the reference artifact for seed 42, radius 12, default epoch
/// config. Slice BF moved it — the per-currency <c>Bank</c> now lends to the
/// state (a claim book), the polity services and retires that claim (the money
/// sink, <see cref="Currency.CumulativeFiatRetired"/>), and the CURRENCY/BANK
/// serializer lines carry the new fields (markets v6, banks v2). A diff here
/// means history changed for existing configs — deliberate changes regenerate
/// the golden in the same commit and say why.</summary>
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
