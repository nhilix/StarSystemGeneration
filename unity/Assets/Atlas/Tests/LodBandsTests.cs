using NUnit.Framework;
using StarGen.AtlasView;

namespace StarGen.AtlasView.Tests
{
    /// <summary>The zoom continuum's band table: thresholds are fractions
    /// of the galaxy extent, and styling steps down monotonically as the
    /// camera closes in.</summary>
    public class LodBandsTests
    {
        [Test]
        public void BandsStepDownAsTheCameraClosesIn()
        {
            const double extent = 100.0;
            Assert.AreEqual(LodBand.Galaxy, LodBands.BandFor(100, extent));
            Assert.AreEqual(LodBand.Galaxy, LodBands.BandFor(56, extent));
            Assert.AreEqual(LodBand.Domains, LodBands.BandFor(40, extent));
            Assert.AreEqual(LodBand.Region, LodBands.BandFor(12, extent));
            Assert.AreEqual(LodBand.Hex, LodBands.BandFor(3, extent));
        }

        [Test]
        public void ADegenerateExtentReadsGalaxy()
        {
            Assert.AreEqual(LodBand.Galaxy, LodBands.BandFor(10, 0));
        }

        [Test]
        public void LaneAndPortStylingNarrowWithTheBands()
        {
            Assert.Greater(LodBands.LaneWidth(LodBand.Galaxy),
                           LodBands.LaneWidth(LodBand.Domains));
            Assert.Greater(LodBands.LaneWidth(LodBand.Domains),
                           LodBands.LaneWidth(LodBand.Region));
            Assert.Greater(LodBands.LaneWidth(LodBand.Region),
                           LodBands.LaneWidth(LodBand.Hex));
            Assert.Greater(LodBands.PortScale(LodBand.Galaxy),
                           LodBands.PortScale(LodBand.Hex));
        }
    }
}
