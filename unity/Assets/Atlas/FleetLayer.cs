using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The fleets lens: posture-differentiated authored glyphs at
    /// fleet hexes, owner-tinted, hull-count nudging the size (FleetLens
    /// speaks data; the atlas cell speaks shape).</summary>
    public sealed class FleetLayer : GlyphLayerBase
    {
        protected override float ZOffset => -0.20f;

        public override void Show(AtlasReadModel model, EyeContext eye)
        {
            var markers = FleetLens.Markers(model, eye);
            var instances = new List<GlyphInstance>(markers.Count);
            foreach (var m in markers)
            {
                float px = 13f + Mathf.Min(7f, m.Hulls * 0.5f);
                float world = 0.85f * AtlasGeometry.HexStep;
                instances.Add(new GlyphInstance(At(m.Hex), world, px,
                    m.Color, AtlasGlyphs.Of(m.Posture)));
            }
            Apply(instances);
        }
    }
}
