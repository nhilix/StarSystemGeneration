using NUnit.Framework;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Atlas.Tests
{
    public class SmokeTests
    {
        [Test]
        public void CoreIsReachable_FromAtlasTestAssembly()
        {
            Assert.AreEqual(1, HexGrid.Distance(new HexCoordinate(0, 0), new HexCoordinate(1, 0)));
        }
    }
}
