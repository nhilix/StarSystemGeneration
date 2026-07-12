using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The POI lens: typed authored glyphs at anchored hexes,
    /// magnitude-sized, dormant precursor sites brightened (a live
    /// remnant, not an inert ruin).</summary>
    public sealed class PoiLayer : GlyphLayerBase
    {
        protected override float ZOffset => -0.18f;

        public override void Show(AtlasReadModel model, EyeContext eye)
        {
            var marks = PoiLens.Marks(model, eye);
            var instances = new List<GlyphInstance>(marks.Count);
            foreach (var m in marks)
            {
                float px = 12f + Mathf.Min(8f, (float)m.Magnitude * 0.25f);
                float world = 0.8f * AtlasGeometry.HexStep;
                var tint = m.Dormant
                    ? new StarGen.Core.Atlas.Rgba(
                        (byte)Mathf.Min(255, m.Color.R + 40),
                        (byte)Mathf.Min(255, m.Color.G + 40),
                        (byte)Mathf.Min(255, m.Color.B + 40), m.Color.A)
                    : m.Color;
                instances.Add(new GlyphInstance(At(m.Hex), world, px,
                    tint, AtlasGlyphs.Of(m.Type)));
            }
            Apply(instances);
        }
    }
}
