using System;
using System.Linq;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Genesis;
using Xunit;

namespace StarGen.Core.Tests.Genesis;

/// <summary>The coarse civ-arc sim: every deep-time origin waves, arcs are
/// bounded and dated, ruins have real geography (lane trees on the raster),
/// endings are cause-typed with the right residue, and the living residue is
/// causal — scars depress downstream life, transcendence seeds machine
/// descendants into the current era.</summary>
public class PrecursorArcTests
{
    private static GalaxySkeleton Built(ulong seed = 42, int radius = 8,
                                        Action<GalaxyConfig>? tune = null)
    {
        var config = new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = radius };
        tune?.Invoke(config);
        return SkeletonBuilder.Build(config);
    }

    [Fact]
    public void Registry_IsDeterministic()
    {
        var a = Built();
        var b = Built();
        Assert.Equal(a.PrecursorWaves.Count, b.PrecursorWaves.Count);
        for (int i = 0; i < a.PrecursorWaves.Count; i++)
        {
            var wa = a.PrecursorWaves[i]; var wb = b.PrecursorWaves[i];
            Assert.Equal(wa.Name, wb.Name);
            Assert.Equal(wa.Class, wb.Class);
            Assert.Equal(wa.EndCause, wb.EndCause);
            Assert.Equal(wa.Cells, wb.Cells);
            Assert.Equal(wa.Sites.Count, wb.Sites.Count);
        }
    }

    [Fact]
    public void EveryDeepTimeOrigin_Waves_AndEndsInDeepTime()
    {
        foreach (ulong seed in new ulong[] { 7, 42, 99 })
        {
            var s = Built(seed);
            int precursors = s.Origins.Count(o => o.Era == OriginEra.Precursor);
            Assert.Equal(precursors, s.PrecursorWaves.Count);
            long gapYears = (long)(-EvolutionSim.PrecursorGapGyr * 1e9);
            long stepSlack = (long)(EvolutionSim.GyrPerStep * 1e9);
            foreach (var w in s.PrecursorWaves)
            {
                Assert.True(w.FellYear > w.RoseYear, $"{w.Name} must rise before it falls");
                Assert.True(w.FellYear <= gapYears + stepSlack,
                    $"{w.Name} fell at {w.FellYear / 1e9:F2} Gyr — precursors end in deep time");
                Assert.False(string.IsNullOrWhiteSpace(w.Name));
            }
        }
    }

    [Fact]
    public void GrandWaves_AreFew_AndBigger()
    {
        var s = Built(42, 12);
        var grand = s.PrecursorWaves.Where(w => w.Class == VigorClass.Grand).ToList();
        Assert.InRange(grand.Count, 1,
            (int)Math.Round(s.Config.Evolution.GrandWaveLimit));
        double grandMean = grand.Average(w => (double)w.Cells.Count);
        double pocketMean = s.PrecursorWaves
            .Where(w => w.Class == VigorClass.Pocket)
            .Average(w => (double)w.Cells.Count);
        Assert.True(grandMean > pocketMean,
            $"grand extent ({grandMean:F1}) should dwarf pockets ({pocketMean:F1})");
    }

    [Fact]
    public void RuinsHaveRealGeography_LaneTreesOnTraversableTerrain()
    {
        var s = Built(42, 12);
        foreach (var w in s.PrecursorWaves)
        {
            Assert.Equal(w.Cells.Count, w.PortHexes.Count);
            Assert.Equal(w.Cells.Count - 1, w.Lanes.Count);   // expansion tree
            Assert.Equal(w.Cells.Count, w.Cells.Distinct().Count());
            foreach (var (a, b) in w.Lanes)
            {
                Assert.InRange(a, 0, w.Cells.Count - 1);
                Assert.InRange(b, 0, w.Cells.Count - 1);
                Assert.Equal(1, HexGrid.Distance(w.Cells[a], w.Cells[b]));
            }
            for (int k = 0; k < w.Cells.Count; k++)
                Assert.Equal(w.Cells[k], HexGrid.CellOf(w.PortHexes[k]));
        }
    }

    [Fact]
    public void Sites_CarryTheResidueVocabulary()
    {
        var s = Built(42, 12);
        foreach (var w in s.PrecursorWaves)
        {
            Assert.NotEmpty(w.Sites);
            Assert.Single(w.Sites.Where(x => x.Type == PrecursorSiteType.Capital));
            bool sterilizing = w.EndCause is WaveEndCause.War or WaveEndCause.Plague;
            if (!sterilizing)
                Assert.DoesNotContain(w.Sites,
                    x => x.Type == PrecursorSiteType.SterilizationScar);
            if (w.EndCause == WaveEndCause.Transcendence && w.Cells.Count > 1)
                Assert.Contains(w.Sites,
                    x => x.Type == PrecursorSiteType.Megastructure);
        }
        // dormant remnants exist but stay a minority
        int sites = s.PrecursorWaves.Sum(w => w.Sites.Count);
        int dormant = s.PrecursorWaves.Sum(w => w.Sites.Count(x => x.Dormant));
        Assert.True(dormant > 0, "some sites stay live");
        Assert.True(dormant < sites / 4, $"dormancy is rare ({dormant}/{sites})");
    }

    [Fact]
    public void SterilizationScars_DepressDownstreamLife()
    {
        var s = Built(42, 12);
        var scarCells = s.PrecursorWaves
            .SelectMany(w => w.Sites)
            .Where(x => x.Type == PrecursorSiteType.SterilizationScar)
            .Select(x => s.CellForHex(x.Hex))
            .Distinct().ToList();
        Assert.NotEmpty(scarCells);
        double scarred = scarCells.Average(c => c.BiosphereRichness);
        double living = s.Cells.Where(c => !c.IsVoid && c.LifeViableStep >= 0
                                           && !scarCells.Contains(c))
            .Average(c => c.BiosphereRichness);
        Assert.True(scarred < living,
            $"scarred cells ({scarred:F3}) should lag unscarred viable cells ({living:F3})");
    }

    [Fact]
    public void MachineDescendants_EnterTheCurrentEra_AtTheCapital()
    {
        // transcendence is rolled; sweep seeds until one seeds a descendant
        foreach (ulong seed in new ulong[] { 7, 42, 99, 1234 })
        {
            var s = Built(seed, 12);
            foreach (var w in s.PrecursorWaves.Where(w => w.DescendantOriginId >= 0))
            {
                var origin = s.Origins[w.DescendantOriginId];
                Assert.Equal(OriginEra.Current, origin.Era);
                Assert.Equal(w.Id, origin.DescendantOfWaveId);
                Assert.Equal(w.CapitalHex, origin.Hex);
                Assert.Equal(WaveEndCause.Transcendence, w.EndCause);
                Assert.True(origin.SpaceflightYear >= 0);
                return;   // found and verified one — enough
            }
        }
        Assert.Fail("no machine descendant across four seeds — the channel is dead");
    }

    [Fact]
    public void Chronicle_CarriesRiseFallAndContact()
    {
        var s = Built(42, 12);
        var evo = s.DeepTimeEvents;
        Assert.Equal(s.PrecursorWaves.Count,
            evo.Count(e => e.Type == WorldEventType.PrecursorWaveRose));
        Assert.Equal(s.PrecursorWaves.Count,
            evo.Count(e => e.Type == WorldEventType.PrecursorWaveFell));
        // battlefields imply contact events
        if (s.PrecursorWaves.Any(w => w.Sites.Any(
                x => x.Type == PrecursorSiteType.Battlefield)))
            Assert.Contains(evo, e => e.Type == WorldEventType.PrecursorContact);
    }

    [Fact]
    public void ZeroDomainBudget_MeansCapitalOnlyPockets()
    {
        var s = Built(tune: c => c.Evolution.DomainBudgetFraction = 0.0);
        Assert.All(s.PrecursorWaves, w => Assert.Single(w.Cells));
    }
}
