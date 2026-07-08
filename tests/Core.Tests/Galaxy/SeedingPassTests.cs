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
}
