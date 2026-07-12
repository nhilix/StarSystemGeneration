using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The war lens's stations: blockade interdiction marks at
    /// port approaches, expedition marks in the field — burning hot
    /// regardless of owner (WarLens.Stations). The belligerent-domain
    /// accent rides the DomainFieldLayer's War accent, not this layer.</summary>
    public sealed class WarLayer : GlyphLayerBase
    {
        protected override float ZOffset => -0.24f;
        protected override int QueueBias => 120;

        public override void Show(AtlasReadModel model, EyeContext eye)
        {
            var stations = WarLens.Stations(model, eye);
            var instances = new List<GlyphInstance>(stations.Count);
            foreach (var s in stations)
                instances.Add(new GlyphInstance(At(s.Hex),
                    0.9f * AtlasGeometry.HexStep,
                    s.Posture == FleetPosture.Blockade ? 16f : 14f,
                    s.Color, AtlasGlyphs.Of(s.Posture)));
            Apply(instances);
        }
    }
}
