using System.Linq;
using StarGen.Core.Model;
using Xunit;
using static StarGen.Core.Tests.Generation.BodyGeneratorTests;

namespace StarGen.Core.Tests.Generation;

public class SatelliteTests
{
    [Fact]
    public void Satellites_ExistSomewhere() =>
        Assert.Contains(SampleBodies(13, 800), b => b.Satellites.Count > 0);

    [Fact]
    public void SatelliteRules_Hold()
    {
        foreach (var body in SampleBodies(13, 800))
        {
            if (body.Kind == BodyKind.PlanetoidBelt || (body.Kind != BodyKind.GasGiant && body.Size < 4))
                Assert.Empty(body.Satellites);

            foreach (var sat in body.Satellites)
            {
                Assert.True(sat.Kind == BodyKind.RockyWorld || sat.Kind == BodyKind.IceWorld);
                Assert.True(sat.Size < body.Size || body.Kind == BodyKind.GasGiant);
                Assert.True(sat.Size >= 1);
                Assert.Empty(sat.Satellites); // no recursion
            }
        }
    }

    [Fact]
    public void GasGiants_CanHaveUpToFour()
    {
        var counts = SampleBodies(13, 3000)
            .Where(b => b.Kind == BodyKind.GasGiant)
            .Select(b => b.Satellites.Count).ToList();
        Assert.True(counts.Max() >= 3, "large moon families should occur in a big sample");
        Assert.True(counts.Max() <= 4);
    }
}
