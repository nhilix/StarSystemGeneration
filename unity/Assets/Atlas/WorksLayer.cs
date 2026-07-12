using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The works lens: the in-flight world — crane glyphs at
    /// construction sites (starving sites dimmed toward the ember),
    /// freight crates riding their routes (stalled ones loud red),
    /// expedition convoys as rockets in the field.</summary>
    public sealed class WorksLayer : GlyphLayerBase
    {
        protected override float ZOffset => -0.22f;

        public override void Show(AtlasReadModel model, EyeContext eye)
        {
            var sites = WorksLens.Sites(model, eye);
            var freight = WorksLens.Freight(model, eye);
            var convoys = WorksLens.Convoys(model, eye);
            var instances = new List<GlyphInstance>(
                sites.Count + freight.Count + convoys.Count);

            foreach (var s in sites)
            {
                // A starving site cools from amber toward ember red — the
                // starvation cascade as a color read.
                float fed = Mathf.Clamp01((float)s.FedFraction);
                var tint = new Rgba(
                    s.Color.R,
                    (byte)(s.Color.G * (0.45f + 0.55f * fed)),
                    (byte)(s.Color.B * (0.30f + 0.70f * fed)),
                    s.Color.A);
                instances.Add(new GlyphInstance(At(s.Hex),
                    0.9f * AtlasGeometry.HexStep, 15f, tint,
                    AtlasGlyph.WorkSite));
            }
            foreach (var f in freight)
                instances.Add(new GlyphInstance(At(f.Hex),
                    0.65f * AtlasGeometry.HexStep, f.Stalled ? 14f : 11f,
                    f.Color, AtlasGlyph.WorkFreight));
            foreach (var c in convoys)
                instances.Add(new GlyphInstance(At(c.Hex),
                    0.8f * AtlasGeometry.HexStep, 13f, c.Color,
                    AtlasGlyph.WorkConvoy));

            Apply(instances);
        }
    }
}
