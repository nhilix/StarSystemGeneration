using System.IO;
using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Genesis;

/// <summary>Genesis outputs are P6-persisted (unlike the hex tier): raster
/// v2 carries the residue fields, and the features / origins / precursors
/// layers restore the registries byte-exactly. Load never re-runs genesis —
/// the artifact IS the physical and living galaxy.</summary>
public class GenesisArtifactTests
{
    private static SimState Seeded()
    {
        var gc = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 8 };
        return EpochGenesis.Seed(SkeletonBuilder.Build(gc),
                                 new EpochSimConfig { MasterSeed = 42 });
    }

    [Fact]
    public void GenesisLayers_RoundTripByteExactly()
    {
        var state = Seeded();
        string text = ArtifactSerializer.ToText(state);
        var loaded = ArtifactSerializer.Load(new StringReader(text));
        Assert.Equal(text, ArtifactSerializer.ToText(loaded));
    }

    [Fact]
    public void LoadedSkeleton_CarriesTheFullResidue_WithoutRerunningGenesis()
    {
        var state = Seeded();
        var loaded = ArtifactSerializer.Load(
            new StringReader(ArtifactSerializer.ToText(state)));
        var a = state.Skeleton;
        var b = loaded.Skeleton;

        for (int i = 0; i < a.Cells.Count; i++)
        {
            Assert.Equal(a.Cells[i].GasFraction, b.Cells[i].GasFraction);
            Assert.Equal(a.Cells[i].CohortRemnant, b.Cells[i].CohortRemnant);
            Assert.Equal(a.Cells[i].MineralRichness, b.Cells[i].MineralRichness);
            Assert.Equal(a.Cells[i].SfActivity, b.Cells[i].SfActivity);
            Assert.Equal(a.Cells[i].LifeViableStep, b.Cells[i].LifeViableStep);
            Assert.Equal(a.Cells[i].LastSterilizedStep, b.Cells[i].LastSterilizedStep);
            Assert.Equal(a.Cells[i].BiosphereRichness, b.Cells[i].BiosphereRichness);
            Assert.Equal(a.Cells[i].BiosphereAgeGyr, b.Cells[i].BiosphereAgeGyr);
        }

        Assert.Equal(a.Features.Count, b.Features.Count);
        for (int i = 0; i < a.Features.Count; i++)
        {
            Assert.Equal(a.Features[i].Type, b.Features[i].Type);
            Assert.Equal(a.Features[i].Name, b.Features[i].Name);
            Assert.Equal(a.Features[i].DateGyr, b.Features[i].DateGyr);
            Assert.Equal(a.Features[i].Cells, b.Features[i].Cells);
        }

        Assert.Equal(a.Origins.Count, b.Origins.Count);
        for (int i = 0; i < a.Origins.Count; i++)
        {
            Assert.Equal(a.Origins[i].SpaceflightYear, b.Origins[i].SpaceflightYear);
            Assert.Equal(a.Origins[i].Era, b.Origins[i].Era);
            Assert.Equal(a.Origins[i].Hex, b.Origins[i].Hex);
            Assert.Equal(a.Origins[i].DescendantOfWaveId, b.Origins[i].DescendantOfWaveId);
        }

        Assert.Equal(a.PrecursorWaves.Count, b.PrecursorWaves.Count);
        for (int i = 0; i < a.PrecursorWaves.Count; i++)
        {
            var wa = a.PrecursorWaves[i]; var wb = b.PrecursorWaves[i];
            Assert.Equal(wa.Name, wb.Name);
            Assert.Equal(wa.Class, wb.Class);
            Assert.Equal(wa.EndCause, wb.EndCause);
            Assert.Equal(wa.Cells, wb.Cells);
            Assert.Equal(wa.PortHexes, wb.PortHexes);
            Assert.Equal(wa.Lanes, wb.Lanes);
            Assert.Equal(wa.Sites.Count, wb.Sites.Count);
            for (int k = 0; k < wa.Sites.Count; k++)
            {
                Assert.Equal(wa.Sites[k].Type, wb.Sites[k].Type);
                Assert.Equal(wa.Sites[k].Hex, wb.Sites[k].Hex);
                Assert.Equal(wa.Sites[k].Dormant, wb.Sites[k].Dormant);
                Assert.Equal(wa.Sites[k].OtherWaveId, wb.Sites[k].OtherWaveId);
            }
        }
    }

    [Fact]
    public void GenesisLayerVersions_RefuseMismatches()
    {
        var state = Seeded();
        string text = ArtifactSerializer.ToText(state);
        foreach (var layer in new[] { "raster|2", "features|1", "origins|2", "precursors|1" })
        {
            var name = layer.Split('|')[0];
            string tampered = text.Replace($"LAYER|{layer}",
                $"LAYER|{name}|9");
            Assert.Throws<InvalidDataException>(() =>
                ArtifactSerializer.Load(new StringReader(tampered)));
        }
    }

    [Fact]
    public void LoadThenContinue_EqualsTheStraightRun_WithGenesisState()
    {
        var straight = Seeded();
        var engine = new EpochEngine();
        for (int i = 0; i < 6; i++) engine.Step(straight);

        var resumed = Seeded();
        for (int i = 0; i < 3; i++) engine.Step(resumed);
        var reloaded = ArtifactSerializer.Load(
            new StringReader(ArtifactSerializer.ToText(resumed)));
        for (int i = 0; i < 3; i++) engine.Step(reloaded);

        Assert.Equal(ArtifactSerializer.ToText(straight),
                     ArtifactSerializer.ToText(reloaded));
    }
}
