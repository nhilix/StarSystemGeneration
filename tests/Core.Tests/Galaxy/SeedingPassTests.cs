using System.Linq;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class SeedingPassTests
{
    // small GalaxyRadiusCells keeps builds fast while big enough for structure.
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, GalaxyRadiusCells = 4 });

    [Fact]
    public void Build_IsDeterministic()
    {
        var a = Build();
        var b = Build();
        for (int i = 0; i < a.Cells.Length; i++)
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
        Assert.True(counts.TryGetValue(StellarLean.Balanced, out var balanced) && balanced > s.Cells.Length / 3,
            "Balanced should be the most common lean");
        Assert.Contains(StellarLean.YoungBright, counts.Keys);
        Assert.Contains(StellarLean.OldDim, counts.Keys);
        // RemnantGraveyard is rare (~12%) — at 256 cells it should appear but stay a small minority
        if (counts.TryGetValue(StellarLean.RemnantGraveyard, out var graveyards))
            Assert.True(graveyards < s.Cells.Length / 4);
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
        // one anchor per hex, and each anchor's hex lies inside its cell
        var hexes = all.Select(x => x.a.Hex).ToList();
        Assert.Equal(hexes.Count, hexes.Distinct().Count());
        foreach (var (c, a) in all)
        {
            Assert.InRange(a.Hex.Q, c.Cx * 8, c.Cx * 8 + 7);
            Assert.InRange(a.Hex.R, c.Cy * 10, c.Cy * 10 + 9);
        }
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
        int expected = System.Math.Max(2, (int)System.Math.Round(
            s.Config.HomeworldRatePerCell * s.Cells.Length));
        Assert.InRange(s.Polities.Count, 2, expected);   // spacing may reject a few below target
        Assert.True(s.Polities.Count >= expected / 2, $"got {s.Polities.Count}, want >= {expected / 2}");
        var capitals = s.Polities.Select(p => (p.CapitalCx, p.CapitalCy)).ToList();
        foreach (var a in capitals)
            foreach (var b in capitals)
                if (a != b)
                    Assert.True(System.Math.Max(System.Math.Abs(a.CapitalCx - b.CapitalCx),
                                                System.Math.Abs(a.CapitalCy - b.CapitalCy)) >= 2,
                        "capitals must not be adjacent");
    }

    [Fact]
    public void Homeworlds_HaveSpeciesAnchorsAndOwnership()
    {
        var s = Build();
        foreach (var polity in s.Polities)
        {
            var species = s.Species.Single(sp => sp.Id == polity.SpeciesId);
            Assert.False(string.IsNullOrEmpty(species.Name));
            Assert.InRange(species.Cohesion, 0.0, 1.0);
            if (species.Embodiment == Embodiment.Hive) Assert.True(species.Cohesion >= 0.75);
            var cell = s.CellAt(polity.CapitalCx, polity.CapitalCy);
            Assert.Equal(polity.Id, cell.OwnerPolityId);
            Assert.InRange(cell.DevelopmentTier, 2, 5);   // seeding sets to 2; epoch sim can increase
            Assert.Contains(cell.Anchors, a => a.Type == AnchorType.Homeworld && a.SpeciesId == species.Id);
        }
    }
}
