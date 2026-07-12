using System;
using System.Collections.Generic;
using NUnit.Framework;
using StarGen.AtlasView;
using StarGen.Core.Epoch;
using UnityEngine;

namespace StarGen.AtlasView.Tests
{
    /// <summary>The authored glyph atlas contract: every glyph resolves a
    /// distinct in-bounds UV cell (enum order IS the PNG layout), every
    /// posture and POI type maps somewhere, and the atlas asset loads.</summary>
    public class GlyphAtlasTests
    {
        [Test]
        public void EveryGlyphHasItsOwnCell()
        {
            var seen = new HashSet<Vector4>();
            foreach (AtlasGlyph glyph in Enum.GetValues(typeof(AtlasGlyph)))
            {
                var rect = AtlasGlyphs.UvRect(glyph);
                Assert.IsTrue(seen.Add(rect), $"{glyph} shares a cell");
                Assert.That(rect.x, Is.InRange(0f, 1f));
                Assert.That(rect.y, Is.InRange(0f, 1f));
                Assert.That(rect.z, Is.InRange(0f, 1f));
                Assert.That(rect.w, Is.InRange(0f, 1f));
                Assert.Less(rect.x, rect.z, $"{glyph} rect is degenerate");
                Assert.Less(rect.y, rect.w, $"{glyph} rect is degenerate");
            }
        }

        [Test]
        public void TheFirstGlyphSitsAtTheImageTopLeft()
        {
            // PNG row 0 is the image top; Unity v runs bottom-up.
            var rect = AtlasGlyphs.UvRect(AtlasGlyph.FleetPosted);
            Assert.AreEqual(0f, rect.x, 1e-5f);
            Assert.AreEqual(1f, rect.w, 1e-5f);
        }

        [Test]
        public void EveryPostureAndPoiTypeResolvesAGlyph()
        {
            foreach (FleetPosture posture in Enum.GetValues(typeof(FleetPosture)))
                Assert.DoesNotThrow(() => AtlasGlyphs.Of(posture));
            foreach (PoiType type in Enum.GetValues(typeof(PoiType)))
                Assert.DoesNotThrow(() => AtlasGlyphs.Of(type));
            Assert.AreEqual(AtlasGlyph.FleetBlockade,
                            AtlasGlyphs.Of(FleetPosture.Blockade));
            Assert.AreEqual(AtlasGlyph.PoiBattlefield,
                            AtlasGlyphs.Of(PoiType.Battlefield));
        }

        [Test]
        public void TheAtlasAssetLoads()
        {
            Assert.IsNotNull(AtlasGlyphs.Atlas,
                "Resources/AtlasGlyphs.png missing or not imported");
            Assert.AreEqual(512, AtlasGlyphs.Atlas.width);
            // 4×5: a stale 4×4 PNG would pass width and misalign every
            // row past the first — the height IS the layout contract.
            Assert.AreEqual(640, AtlasGlyphs.Atlas.height);
        }

        [Test]
        public void GlyphsResolveTowardRegion()
        {
            const double extent = 100.0;
            Assert.AreEqual(0f, LodBands.GlyphFade(120, extent));
            float domains = LodBands.GlyphFade(50, extent);
            float region = LodBands.GlyphFade(20, extent);
            Assert.Greater(region, domains);
            Assert.AreEqual(1f, LodBands.GlyphFade(10, extent), 1e-4f);
        }
    }
}
