using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Galaxy;

public class GalaxyEnumeratorTests
{
    [Fact]
    public void SpiralAt_MatchesHexGridSpiral()
    {
        var expected = HexGrid.Spiral(new HexCoordinate(0, 0), 9).ToList();
        for (int i = 0; i < expected.Count; i++)
            Assert.Equal(expected[i], GalaxyEnumerator.SpiralAt(i));
    }

    [Fact]
    public void SpiralIndexOf_InvertsSpiralAt()
    {
        for (int i = 0; i < 500; i++)
            Assert.Equal(i, GalaxyEnumerator.SpiralIndexOf(GalaxyEnumerator.SpiralAt(i)));
    }
}
