using NUnit.Framework;
using StarGen.Atlas;
using StarGen.Core.Galaxy;
using UnityEngine;

namespace StarGen.Atlas.Tests
{
    public class LayerPaletteTests
    {
        private static GalaxySkeleton Skeleton() =>
            SkeletonBuilder.Build(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 });

        [Test]
        public void DensityLayer_IsGrayscale_ScalingWithDensity()
        {
            var s = Skeleton();
            var cell = new RegionCell { MeanDensity = 0.5 };
            var c = LayerPalette.CellColor(s, cell, AtlasLayer.Density);
            Assert.AreEqual(c.r, c.g);
            Assert.AreEqual(c.g, c.b);
            var brighter = LayerPalette.CellColor(s, new RegionCell { MeanDensity = 0.9 }, AtlasLayer.Density);
            Assert.Greater(brighter.r, c.r);
        }

        [Test]
        public void PolityLayer_VoidBlack_UnclaimedGray_CapitalWhite_OwnersDistinct()
        {
            var s = Skeleton();
            var voidCell = new RegionCell { IsVoid = true };
            Assert.AreEqual(10, LayerPalette.CellColor(s, voidCell, AtlasLayer.Polity).r);
            var unclaimed = new RegionCell { OwnerPolityId = -1 };
            Assert.AreEqual(40, LayerPalette.CellColor(s, unclaimed, AtlasLayer.Polity).r);
            var polity = s.Polities[0];
            var capital = s.CellAt(polity.CapitalCoord);
            var capColor = LayerPalette.CellColor(s, capital, AtlasLayer.Polity);
            Assert.AreEqual(255, capColor.r);
            Assert.AreEqual(255, capColor.g);
            Assert.AreEqual(255, capColor.b);
            var owned0 = new RegionCell { OwnerPolityId = 0, DevelopmentTier = 2, Q = 99, R = 99 };
            var owned1 = new RegionCell { OwnerPolityId = 1, DevelopmentTier = 2, Q = 99, R = 99 };
            Assert.AreNotEqual(LayerPalette.CellColor(s, owned0, AtlasLayer.Polity),
                               LayerPalette.CellColor(s, owned1, AtlasLayer.Polity));
        }

        [Test]
        public void HexStates_AllDistinct_AndHighlightBrightens()
        {
            var states = new[] { HexState.Void, HexState.Empty, HexState.System, HexState.Settled, HexState.Anchored };
            for (int i = 0; i < states.Length; i++)
                for (int j = i + 1; j < states.Length; j++)
                    Assert.AreNotEqual(LayerPalette.HexColor(states[i]), LayerPalette.HexColor(states[j]));
            var baseColor = LayerPalette.HexColor(HexState.System);
            var hi = LayerPalette.Highlight(baseColor);
            Assert.Greater(hi.r, baseColor.r);
        }

        [Test]
        public void EveryLayer_ProducesAColor_ForEveryRealCell()
        {
            var s = Skeleton();
            foreach (var cell in s.Cells)
                foreach (AtlasLayer layer in System.Enum.GetValues(typeof(AtlasLayer)))
                    LayerPalette.CellColor(s, cell, layer);   // must not throw
            Assert.Pass();
        }
    }
}
