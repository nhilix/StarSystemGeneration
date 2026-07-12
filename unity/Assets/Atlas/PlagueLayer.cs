using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The plague lens's port marks: infected ports burn under
    /// the biohazard glyph, recovered ones carry the regeneration scar.
    /// Quarantined approaches are the LaneLayer's QuarantineOnly mode —
    /// the rail wires the two together.</summary>
    public sealed class PlagueLayer : GlyphLayerBase
    {
        protected override float ZOffset => -0.25f;
        protected override int QueueBias => 110;

        public override void Show(AtlasReadModel model, EyeContext eye)
        {
            var marks = PlagueLens.Marks(model, eye);
            var instances = new List<GlyphInstance>(marks.Count);
            foreach (var m in marks)
                instances.Add(new GlyphInstance(At(m.Hex),
                    0.85f * AtlasGeometry.HexStep,
                    m.Status == PortPlagueStatus.Infected ? 16f : 13f,
                    m.Color, m.Status == PortPlagueStatus.Infected
                        ? AtlasGlyph.PlagueInfected : AtlasGlyph.PlagueImmune));
            Apply(instances);
        }
    }
}
