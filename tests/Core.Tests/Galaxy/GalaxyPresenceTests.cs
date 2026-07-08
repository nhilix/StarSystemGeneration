using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class GalaxyPresenceTests
{
    [Fact]
    public void Flatspace_MatchesLegacySignature_Exactly()
    {
        var flat = GalaxyContext.Flatspace(17);
        for (int x = 0; x < 300; x++)
        {
            var coord = new HexCoordinate(x, 5);
            var viaLegacy = Generator.Generate(17, coord);
            var viaContext = Generator.Generate(flat, coord);
            Assert.Equal(viaLegacy.IsEmpty, viaContext.IsEmpty);
            Assert.Equal(viaLegacy.System?.Designation, viaContext.System?.Designation);
        }
    }

    [Fact]
    public void ShapedGalaxy_CornersEmpty_CoreDense()
    {
        var galaxy = new GalaxyContext(new GalaxyConfig { MasterSeed = 42 });
        // far beyond the rim: always empty
        Assert.True(Generator.Generate(galaxy, new HexCoordinate(400, 0)).IsEmpty);
        // galactic core (origin-centered): far denser than flat 50%
        int present = 0, total = 0;
        for (int dx = -10; dx <= 10; dx++)
            for (int dy = -10; dy <= 10; dy++)
            {
                total++;
                if (!Generator.Generate(galaxy, new HexCoordinate(dx, dy)).IsEmpty) present++;
            }
        Assert.True(present / (double)total > 0.6, $"core presence {present}/{total} should exceed 60%");
    }

    [Fact]
    public void ShapedGalaxy_IsDeterministic()
    {
        var a = new GalaxyContext(new GalaxyConfig { MasterSeed = 99 });
        var b = new GalaxyContext(new GalaxyConfig { MasterSeed = 99 });
        for (int i = 0; i < 200; i++)
        {
            var coord = new HexCoordinate(100 + i, 200);
            Assert.Equal(Generator.Generate(a, coord).IsEmpty, Generator.Generate(b, coord).IsEmpty);
        }
    }
}
