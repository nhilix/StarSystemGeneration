using System;
using NUnit.Framework;
using StarGen.Core.Atlas;
using StarGen.Core.Substrate;

namespace StarGen.AtlasView.Tests
{
    /// <summary>K3's no-drift contract, Unity side: every GlyphKey the
    /// Core legend emits must name a real AtlasGlyph cell — the legend and
    /// the layers share one vocabulary (the K2 eyeball ask, kept honest).</summary>
    public class LegendDriftTests
    {
        private static readonly string[] RailKeys =
        {
            "domains", "war", "tension", "lanes", "traffic", "trade",
            "fleets", "works", "price", "tech", "plague", "news", "pois",
            "ports", "nature",
        };

        [Test]
        public void EveryLegendGlyphKeyNamesAnAtlasCell()
        {
            foreach (var key in RailKeys)
                foreach (var entry in LegendQuery.For(key, GoodId.Provisions))
                {
                    if (entry.GlyphKey == null) continue;
                    Assert.IsTrue(
                        Enum.TryParse<AtlasGlyph>(entry.GlyphKey, out _),
                        $"legend '{key}' names unknown glyph "
                        + $"'{entry.GlyphKey}'");
                }
        }

        [Test]
        public void EveryRailLensKeyYieldsEntries()
        {
            foreach (var key in RailKeys)
                Assert.IsNotEmpty(LegendQuery.For(key, GoodId.Provisions),
                                  $"no legend for '{key}'");
        }
    }
}
