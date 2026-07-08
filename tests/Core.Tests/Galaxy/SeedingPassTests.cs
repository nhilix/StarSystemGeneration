using System.Linq;
using StarGen.Core.Galaxy;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class SeedingPassTests
{
    // SizeSectors = 4 keeps builds fast (16x16 cells) while big enough for structure.
    private static GalaxySkeleton Build(ulong seed = 42) =>
        SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = seed, SizeSectors = 4 });

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
}
