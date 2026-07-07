using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using Xunit;

namespace StarGen.Core.Tests.Generation;

public class PresenceTests
{
    [Fact]
    public void EmptyHex_IsStable()
    {
        // find an empty hex, then re-check it several times
        for (int x = 0; x < 200; x++)
        {
            var coord = new HexCoordinate(x, 0);
            if (!Generator.Generate(1, coord).IsEmpty) continue;
            for (int i = 0; i < 5; i++)
                Assert.True(Generator.Generate(1, coord).IsEmpty);
            return;
        }
        Assert.Fail("No empty hex found in 200 tries — presence roll broken.");
    }

    [Fact]
    public void PresenceRate_IsNearDensity()
    {
        int present = 0;
        for (int x = 0; x < 100; x++)
            for (int y = 0; y < 40; y++)
                if (!Generator.Generate(7, new HexCoordinate(x, y)).IsEmpty) present++;
        Assert.InRange(present / 4000.0, 0.45, 0.55);
    }

    [Fact]
    public void NonEmptyHex_HasDesignation()
    {
        for (int x = 0; x < 200; x++)
        {
            var result = Generator.Generate(1, new HexCoordinate(x, 1));
            if (result.IsEmpty) continue;
            Assert.Equal(Designation.For(result.Coordinate), result.System!.Designation);
            return;
        }
        Assert.Fail("No system found in 200 tries.");
    }

    [Fact]
    public void Designation_Format() =>
        Assert.Equal("SGC 0012-0034", Designation.For(new HexCoordinate(12, 34)));
}
