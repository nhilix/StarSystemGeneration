using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

/// <summary>The derived seeding (slice F): density/lean/metallicity are
/// cosmic-sim residue, homeworlds and species derive from the emergence
/// schedule, precursor-site anchors from the wave registry, mineral anchors
/// from the simulated richness field. The painted passes are gone; these
/// tests replaced the stub tests (the one legitimate replacement zone).</summary>
public class SeedingPassTests
{
    // small GalaxyRadiusCells keeps builds fast while big enough for structure.
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = 8 });

    [Fact]
    public void Build_IsDeterministic()
    {
        var a = Build();
        var b = Build();
        for (int i = 0; i < a.Cells.Count; i++)
        {
            Assert.Equal(a.Cells[i].MeanDensity, b.Cells[i].MeanDensity);
            Assert.Equal(a.Cells[i].IsVoid, b.Cells[i].IsVoid);
            Assert.Equal(a.Cells[i].IsChokepoint, b.Cells[i].IsChokepoint);
            Assert.Equal(a.Cells[i].Metallicity, b.Cells[i].Metallicity);
            Assert.Equal(a.Cells[i].Anchors.Count, b.Cells[i].Anchors.Count);
        }
        Assert.Equal(a.Species.Count, b.Species.Count);
    }

    [Fact]
    public void DensitySummary_HasStructure()
    {
        var s = Build();
        Assert.Contains(s.Cells, c => c.IsVoid);                       // rim/void cells exist
        Assert.Contains(s.Cells, c => c.MeanDensity > 0.5);            // dense cells exist
        Assert.All(s.Cells, c => Assert.InRange(c.MeanDensity, 0.0, 1.0));
    }

    [Fact]
    public void Chokepoints_AreNonVoid_AndScarcerThanOrdinaryCells()
    {
        var s = Build();
        var chokepoints = s.Cells.Where(c => c.IsChokepoint).ToList();
        Assert.All(chokepoints, c => Assert.False(c.IsVoid));
        Assert.True(chokepoints.Count < s.Cells.Count(c => !c.IsVoid) / 2,
            "chokepoints should be a minority of traversable cells");
    }

    [Fact]
    public void StellarPopulation_AllLeansOccur_AndBalancedDominates()
    {
        var s = Build();
        var counts = s.Cells.GroupBy(c => c.Lean).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(counts.TryGetValue(StellarLean.Balanced, out var balanced) && balanced > s.Cells.Count / 3,
            "Balanced should be the most common lean");
        Assert.Contains(StellarLean.YoungBright, counts.Keys);
        Assert.Contains(StellarLean.OldDim, counts.Keys);
        if (counts.TryGetValue(StellarLean.RemnantGraveyard, out var graveyards))
            Assert.True(graveyards < s.Cells.Count / 4, "graveyards stay a minority");
    }

    [Fact]
    public void Metallicity_IsBounded_AndVaries()
    {
        var s = Build();
        Assert.All(s.Cells, c => Assert.InRange(c.Metallicity, 0.0, 1.0));
        Assert.True(s.Cells.Select(c => System.Math.Round(c.Metallicity, 2)).Distinct().Count() > 10,
            "metallicity should vary across cells");
    }

    [Fact]
    public void Anchors_ArePlaced_OnePerHex_InsideTheirCell()
    {
        var s = Build();
        var all = s.Cells.SelectMany(c => c.Anchors.Select(a => (c, a))).ToList();
        Assert.True(all.Count(x => x.a.Type == AnchorType.MineralRich) > 3, "mineral anchors should exist");
        Assert.Contains(all, x => x.a.Type == AnchorType.PrecursorSite);
        Assert.Contains(all, x => x.a.Type == AnchorType.Homeworld);
        var hexes = all.Select(x => x.a.Hex).ToList();
        Assert.Equal(hexes.Count, hexes.Distinct().Count());
        foreach (var (c, a) in all)
            Assert.True(HexGrid.Distance(a.Hex, HexGrid.CellCenter(c.Coord)) <= HexGrid.CellRadius,
                $"anchor at {a.Hex} outside cell {c.Coord}");
    }

    [Fact]
    public void MineralAnchors_FollowTheSimulatedRichness()
    {
        var s = Build();
        var richCells = s.Cells.Where(c => !c.IsVoid && c.MineralRichness > 0.6).ToList();
        var poorCells = s.Cells.Where(c => !c.IsVoid && c.MineralRichness < 0.2).ToList();
        double richRate = richCells.Count(c => c.Anchors.Any(a => a.Type == AnchorType.MineralRich)) / (double)richCells.Count;
        double poorRate = poorCells.Count(c => c.Anchors.Any(a => a.Type == AnchorType.MineralRich)) / (double)poorCells.Count;
        Assert.True(richRate > poorRate,
            $"mineral-rich cells ({richRate:F2}) should out-anchor poor ({poorRate:F2})");
    }

    [Fact]
    public void PrecursorAnchors_ComeFromTheWaveRegistry()
    {
        var s = Build();
        var siteHexes = s.PrecursorWaves.SelectMany(w => w.Sites)
            .Where(x => x.Type != PrecursorSiteType.SterilizationScar)
            .Select(x => x.Hex).ToHashSet();
        var anchors = s.Cells.SelectMany(c => c.Anchors)
            .Where(a => a.Type == AnchorType.PrecursorSite).ToList();
        Assert.NotEmpty(anchors);
        Assert.All(anchors, a => Assert.Contains(a.Hex, siteHexes));
    }

    [Fact]
    public void Homeworlds_MatchTheEmergenceSchedule()
    {
        var s = Build();
        var current = s.Origins.Where(o => o.Era == OriginEra.Current).ToList();
        var homeworlds = s.Cells.SelectMany(c => c.Anchors)
            .Where(a => a.Type == AnchorType.Homeworld).ToList();
        Assert.Equal(current.Count, homeworlds.Count);
        Assert.Equal(current.Count, s.Species.Count);
        foreach (var species in s.Species)
        {
            var anchor = Assert.Single(homeworlds.Where(a => a.SpeciesId == species.Id));
            var cell = s.CellForHex(anchor.Hex);
            Assert.False(cell.IsVoid, "homeworlds seed on traversable cells");
            Assert.False(string.IsNullOrEmpty(species.Name));
            Assert.InRange(species.Cohesion, 0.0, 1.0);
            if (species.Embodiment == Embodiment.Hive)
                Assert.True(species.Cohesion >= 0.75);
        }
    }

    [Fact]
    public void MachineSpecies_DescendFromPrecursorCapitals()
    {
        // sweep seeds: transcendence descendants are rolled
        foreach (ulong seed in new ulong[] { 7, 42, 99, 1234 })
        {
            var s = SkeletonBuilder.Build(new GalaxyConfig
            { MasterSeed = seed, GalaxyRadiusCells = 12 });
            var currents = s.Origins.Where(o => o.Era == OriginEra.Current).ToList();
            for (int i = 0; i < currents.Count; i++)
            {
                if (currents[i].DescendantOfWaveId < 0)
                {
                    Assert.NotEqual(Embodiment.Machine, s.Species[i].Embodiment);
                    continue;
                }
                Assert.Equal(Embodiment.Machine, s.Species[i].Embodiment);
                Assert.Equal(s.PrecursorWaves[currents[i].DescendantOfWaveId].CapitalHex,
                    currents[i].Hex);
                return;   // verified one descendant end to end
            }
        }
        Assert.Fail("no machine descendant across four seeds");
    }

    [Fact]
    public void AnchorMultipliers_ScaleAnchorCounts()
    {
        static GalaxySkeleton BuildWith(double mineral, double precursor) =>
            SkeletonBuilder.Build(new GalaxyConfig
            {
                MasterSeed = 99, GalaxyRadiusCells = 8,
                MineralAnchorMultiplier = mineral,
                PrecursorAnchorMultiplier = precursor,
            });

        static int Count(GalaxySkeleton s, AnchorType type) =>
            s.Cells.Sum(c => c.Anchors.Count(a => a.Type == type));

        var stock = BuildWith(1.0, 1.0);
        var none = BuildWith(0.0, 0.0);
        var sampled = BuildWith(1.0, 0.3);

        Assert.Equal(0, Count(none, AnchorType.MineralRich));
        Assert.Equal(0, Count(none, AnchorType.PrecursorSite));
        // the precursor multiplier samples the causal site list â€” it can
        // thin it, never invent sites
        Assert.True(Count(sampled, AnchorType.PrecursorSite)
                    < Count(stock, AnchorType.PrecursorSite));
        Assert.True(Count(stock, AnchorType.MineralRich) > 0);
    }

    [Fact]
    public void BuildShape_MatchesFullBuildDensities_AndSkipsSeeding()
    {
        var config = new GalaxyConfig { MasterSeed = 5, GalaxyRadiusCells = 8 };
        var shape = SkeletonBuilder.BuildShape(config);
        var full = SkeletonBuilder.Build(config);

        Assert.Equal(full.Cells.Count, shape.Cells.Count);
        for (int i = 0; i < full.Cells.Count; i++)
        {
            Assert.Equal(full.Cells[i].MeanDensity, shape.Cells[i].MeanDensity);
            Assert.Equal(full.Cells[i].IsVoid, shape.Cells[i].IsVoid);
        }
        Assert.Empty(shape.Species);
        Assert.Empty(shape.Origins);
        Assert.All(shape.Cells, c => Assert.Empty(c.Anchors));
    }
}

