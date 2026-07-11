using NUnit.Framework;
using StarGen.AtlasView;

namespace StarGen.AtlasView.Tests
{
    /// <summary>The zoom continuum's thresholds (camera altitude over
    /// galaxy extent) and the continuous lattice fade curve.</summary>
    public class LodBandsTests
    {
        [Test]
        public void BandsStepDownAsTheCameraDescends()
        {
            const double extent = 100.0;
            Assert.AreEqual(LodBand.Galaxy, LodBands.BandFor(220, extent));
            Assert.AreEqual(LodBand.Galaxy, LodBands.BandFor(111, extent));
            Assert.AreEqual(LodBand.Domains, LodBands.BandFor(80, extent));
            Assert.AreEqual(LodBand.Region, LodBands.BandFor(20, extent));
            Assert.AreEqual(LodBand.Hex, LodBands.BandFor(6, extent));
        }

        [Test]
        public void ADegenerateExtentReadsGalaxy()
        {
            Assert.AreEqual(LodBand.Galaxy, LodBands.BandFor(10, 0));
        }

        [Test]
        public void TheLatticeFadesInContinuouslyTowardHex()
        {
            const double extent = 100.0;
            Assert.AreEqual(0f, LodBands.LatticeAlpha(120, extent));
            Assert.AreEqual(0f, LodBands.LatticeAlpha(40, extent));
            float mid = LodBands.LatticeAlpha(15, extent);
            float close = LodBands.LatticeAlpha(7, extent);
            Assert.Greater(mid, 0f);
            Assert.Greater(close, mid);
            Assert.LessOrEqual(close, 0.12f);
        }
    }
}
