using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

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
        }
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
        // RemnantGraveyard is rare (~12%) — at 256 cells it should appear but stay a small minority
        if (counts.TryGetValue(StellarLean.RemnantGraveyard, out var graveyards))
            Assert.True(graveyards < s.Cells.Count / 4);
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
        Assert.True(all.Count(x => x.a.Type == AnchorType.MineralRich) > 5, "mineral anchors should exist");
        Assert.Contains(all, x => x.a.Type == AnchorType.PrecursorSite);
        var hexes = all.Select(x => x.a.Hex).ToList();
        Assert.Equal(hexes.Count, hexes.Distinct().Count());
        foreach (var (c, a) in all)
            Assert.True(HexGrid.Distance(a.Hex, HexGrid.CellCenter(c.Coord)) <= HexGrid.CellRadius,
                $"anchor at {a.Hex} outside cell {c.Coord}");
    }

    [Fact]
    public void MineralAnchors_FollowMetallicity()
    {
        var s = Build();
        var richCells = s.Cells.Where(c => !c.IsVoid && c.Metallicity > 0.6).ToList();
        var poorCells = s.Cells.Where(c => !c.IsVoid && c.Metallicity < 0.4).ToList();
        double richRate = richCells.Count(c => c.Anchors.Any(a => a.Type == AnchorType.MineralRich)) / (double)richCells.Count;
        double poorRate = poorCells.Count(c => c.Anchors.Any(a => a.Type == AnchorType.MineralRich)) / (double)poorCells.Count;
        Assert.True(richRate > poorRate, $"metal-rich cells ({richRate:F2}) should out-anchor metal-poor ({poorRate:F2})");
    }

    [Fact]
    public void Homeworlds_CountAndSpacing()
    {
        var s = Build();
        var homeCells = s.Cells.Where(c =>
            c.Anchors.Any(a => a.Type == AnchorType.Homeworld)).ToList();
        int expected = System.Math.Max(2, (int)System.Math.Round(
            s.Config.HomeworldRatePerCell * s.Cells.Count));
        Assert.InRange(homeCells.Count, 2, expected);   // spacing may reject a few below target
        Assert.True(homeCells.Count >= expected / 2, $"got {homeCells.Count}, want >= {expected / 2}");
        foreach (var a in homeCells)
            foreach (var b in homeCells)
                if (!ReferenceEquals(a, b))
                    Assert.True(HexGrid.Distance(a.Coord, b.Coord) >= 2,
                        "homeworld cells must not be adjacent on the cell lattice");
    }

    [Fact]
    public void Homeworlds_HaveSpeciesTaggedAnchors()
    {
        var s = Build();
        Assert.NotEmpty(s.Species);
        foreach (var species in s.Species)
        {
            Assert.False(string.IsNullOrEmpty(species.Name));
            Assert.InRange(species.Cohesion, 0.0, 1.0);
            if (species.Embodiment == Embodiment.Hive) Assert.True(species.Cohesion >= 0.75);
            // seeding guarantees exactly one homeworld anchor per species — the
            // hook EpochGenesis seeds its polity actors from
            var homeworldCells = s.Cells.Where(c => c.Anchors.Any(a =>
                a.Type == AnchorType.Homeworld && a.SpeciesId == species.Id)).ToList();
            var home = Assert.Single(homeworldCells);
            Assert.False(home.IsVoid, "homeworlds seed on traversable cells");
        }
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
        var rich = BuildWith(3.0, 3.0);

        Assert.Equal(0, Count(none, AnchorType.MineralRich));
        Assert.Equal(0, Count(none, AnchorType.PrecursorSite));
        // Fixed seed: a larger multiplier only raises thresholds against the
        // same rolls, so rich anchors are a strict superset of stock's.
        Assert.True(Count(rich, AnchorType.MineralRich) > Count(stock, AnchorType.MineralRich));
        Assert.True(Count(rich, AnchorType.PrecursorSite) > Count(stock, AnchorType.PrecursorSite));
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
        Assert.All(shape.Cells, c => Assert.Empty(c.Anchors));
    }
}
