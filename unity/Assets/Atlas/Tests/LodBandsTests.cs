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
            Assert.AreEqual(LodBand.System, LodBands.BandFor(4.5, extent));
        }

        [Test]
        public void ATinyGalaxyKeepsItsHexBand()
        {
            // extent 30: Hex starts at 4.2 — the System floor must duck
            // under it, not swallow it.
            const double extent = 30.0;
            Assert.AreEqual(LodBand.Hex, LodBands.BandFor(3.0, extent));
            Assert.AreEqual(LodBand.System, LodBands.BandFor(2.0, extent));
        }

        [Test]
        public void TheCrossfadeDissolvesTheMapIntoTheStage()
        {
            const double extent = 100.0;
            // above the window: map full, stage off
            Assert.AreEqual(1f, LodBands.MapFade(20, extent));
            Assert.AreEqual(0f, LodBands.StageFade(20, extent));
            // inside the window: complementary, monotone
            float mapHigh = LodBands.MapFade(9, extent);
            float mapLow = LodBands.MapFade(6, extent);
            Assert.Greater(mapHigh, mapLow);
            Assert.Greater(mapLow, 0f);
            Assert.AreEqual(1f, mapHigh + LodBands.StageFade(9, extent), 1e-4);
            // at and below the floor: map gone, stage full
            Assert.AreEqual(0f, LodBands.MapFade(5, extent));
            Assert.AreEqual(1f, LodBands.StageFade(3, extent));
        }

        [Test]
        public void EveryMapCurveDiesInsideTheSystemBand()
        {
            const double extent = 100.0;
            Assert.AreEqual(0f, LodBands.LaneFade(4, extent));
            Assert.AreEqual(0f, LodBands.GlyphFade(4, extent));
            Assert.AreEqual(0f, LodBands.LatticeAlpha(4, extent));
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
            // 10 sits below the lattice's fade-in knee but above the K5
            // hex→orbit crossfade window (which kills every map curve)
            float mid = LodBands.LatticeAlpha(15, extent);
            float close = LodBands.LatticeAlpha(10, extent);
            Assert.Greater(mid, 0f);
            Assert.Greater(close, mid);
            Assert.LessOrEqual(close, 0.12f);
        }
    }
}
