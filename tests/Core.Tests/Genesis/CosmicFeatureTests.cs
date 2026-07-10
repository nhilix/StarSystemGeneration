using System;
using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Genesis;
using Xunit;

namespace StarGen.Core.Tests.Genesis;

/// <summary>Discrete cosmic features: sparse, identified, dated, bounded —
/// and they interact with the field stack (ledgered injections, starbursts,
/// sterilization) rather than bypassing it. The deep-time chronicle carries
/// them in the standard event grammar with true deep-time world-years.</summary>
public class CosmicFeatureTests
{
    private static GalaxySkeleton Run(ulong seed = 42, int radius = 8,
                                      Action<GalaxyConfig>? tune = null)
    {
        var config = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius };
        tune?.Invoke(config);
        var skeleton = new GalaxySkeleton(config);
        CosmicSim.Run(skeleton);
        return skeleton;
    }

    [Fact]
    public void Features_AreBounded_Dated_AndNamed_AcrossSeeds()
    {
        foreach (ulong seed in new ulong[] { 1, 42, 7777 })
        {
            var s = Run(seed);
            Assert.InRange(s.Features.Count, 3, 40);
            foreach (var f in s.Features)
            {
                Assert.False(string.IsNullOrWhiteSpace(f.Name));
                Assert.InRange(f.DateGyr, -CosmicSim.SpanGyr, 0.0);
                Assert.NotEmpty(f.Cells);
                Assert.Equal(f.Id, s.Features.IndexOf(f));
            }
        }
    }

    [Fact]
    public void Mergers_LeaveStreams_WithMultiCellTrails()
    {
        var s = Run();
        var streams = s.Features
            .Where(f => f.Type == GalacticFeatureType.MergerStream).ToList();
        Assert.NotEmpty(streams);
        Assert.All(streams, f => Assert.True(f.Cells.Count >= 2,
            $"a merger trail should cross cells ({f.Cells.Count})"));
        Assert.All(streams, f => Assert.True(f.DateGyr < -2.0,
            "mergers arrive in deep time, not yesterday"));
    }

    [Fact]
    public void Globulars_AreSingleCell_MetalPoor_GasStarved()
    {
        var config = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 8 };
        var skeleton = new GalaxySkeleton(config);
        var state = CosmicSim.Run(skeleton);

        var globulars = skeleton.Features
            .Where(f => f.Type == GalacticFeatureType.GlobularCluster).ToList();
        Assert.NotEmpty(globulars);
        foreach (var g in globulars)
        {
            var cell = skeleton.CellAt(Assert.Single(g.Cells));
            int i = cell.SpiralIndex;
            Assert.True(state.StarsOld[i] > 0, "globulars are ancient stars");
            Assert.True(state.StarZ(i) < CosmicSim.LifeViableZFloor,
                $"life never starts in a globular (Z {state.StarZ(i):F4})");
            double meanGas = state.Gas.Sum() / state.CellCount;
            Assert.True(state.Gas[i] < meanGas,
                "globular cells hold less gas than the disc");
        }
    }

    [Fact]
    public void Agn_Sterilizes_TheInnerDisc_ThenGoesQuiet()
    {
        // stock activity may or may not fire on a given seed; crank it up
        var s = Run(tune: c => c.Cosmic.AgnActivity = 2.0);
        var skeleton = s;
        var outbursts = s.Features
            .Where(f => f.Type == GalacticFeatureType.AgnOutburst).ToList();
        Assert.NotEmpty(outbursts);
        foreach (var o in outbursts)
        {
            Assert.Contains(o.Cells, c => HexGrid.Distance(c,
                new Core.Model.HexCoordinate(0, 0)) == 0);
            Assert.True(o.DateGyr <= -CosmicSim.SpanGyr * 0.10,
                $"the nucleus is quiescent at present day ({o.DateGyr:F2})");
        }
    }

    [Fact]
    public void AgnActivityZero_MeansAQuietNucleus()
    {
        var s = Run(tune: c => c.Cosmic.AgnActivity = 0.0);
        Assert.DoesNotContain(s.Features,
            f => f.Type == GalacticFeatureType.AgnOutburst);
    }

    [Fact]
    public void Nebulae_EmergeAtFinalization_FromRealGas()
    {
        var config = new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 8 };
        var skeleton = new GalaxySkeleton(config);
        var state = CosmicSim.Run(skeleton);
        var nebulae = skeleton.Features.Where(f =>
            f.Type is GalacticFeatureType.EmissionNebula
                   or GalacticFeatureType.DarkCloud).ToList();
        Assert.NotEmpty(nebulae);
        double meanGas = state.Gas.Sum() / state.CellCount;
        foreach (var nebula in nebulae)
            foreach (var coord in nebula.Cells)
                Assert.True(state.Gas[skeleton.CellAt(coord).SpiralIndex] > meanGas,
                    "nebula cells are the high-gas cells");
    }

    [Fact]
    public void DeepTimeChronicle_CarriesTheCosmicStratum()
    {
        var s = Run();
        Assert.NotEmpty(s.DeepTimeEvents);
        long previousYear = long.MinValue;
        foreach (var e in s.DeepTimeEvents)
        {
            Assert.Equal(ClockStratum.Cosmic, e.Stratum);
            Assert.Equal(EventFamily.Cosmic, e.Family);
            Assert.InRange(e.WorldYear, -15_000_000_000L, 0L);
            Assert.True(e.WorldYear >= previousYear, "chronicle reads in time order");
            previousYear = e.WorldYear;
        }
        Assert.Contains(s.DeepTimeEvents,
            e => e.Type == WorldEventType.DwarfGalaxyMerged);
    }

    [Fact]
    public void FeatureRegistry_IsDeterministic()
    {
        var a = Run();
        var b = Run();
        Assert.Equal(a.Features.Count, b.Features.Count);
        for (int i = 0; i < a.Features.Count; i++)
        {
            Assert.Equal(a.Features[i].Type, b.Features[i].Type);
            Assert.Equal(a.Features[i].Name, b.Features[i].Name);
            Assert.Equal(a.Features[i].DateGyr, b.Features[i].DateGyr);
            Assert.Equal(a.Features[i].Cells, b.Features[i].Cells);
        }
        Assert.Equal(a.DeepTimeEvents.Count, b.DeepTimeEvents.Count);
    }

    [Fact]
    public void MergerCountKnob_ScalesTheStreams()
    {
        var many = Run(tune: c => c.Cosmic.MergerCount = 6.0);
        var none = Run(tune: c => c.Cosmic.MergerCount = 0.0);
        int manyStreams = many.Features
            .Count(f => f.Type == GalacticFeatureType.MergerStream);
        int noneStreams = none.Features
            .Count(f => f.Type == GalacticFeatureType.MergerStream);
        Assert.Equal(0, noneStreams);
        Assert.True(manyStreams >= 3, $"MergerCount 6 should land several ({manyStreams})");
    }
}
