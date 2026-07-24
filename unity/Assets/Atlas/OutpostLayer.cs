using System.Collections.Generic;
using StarGen.Core.Atlas;

namespace StarGen.AtlasView
{
    /// <summary>AC1.4 — outpost marks, the subordinate sibling of PortLayer.
    /// A named owner-tinted dot at each live outpost hex: smaller than a port
    /// dot, no service ring, no market affordance. Always on (like the ports it
    /// answers to), selectable through SelectionModel, named on hover. Graduated
    /// outposts are omitted upstream — they are ports now and carry a port dot.</summary>
    public sealed class OutpostLayer : DotMarkLayer
    {
        // In front of the worked dust (-0.11), behind the port dots (-0.15).
        protected override float Z => -0.13f;

        public override void Show(AtlasReadModel model, EyeContext eye)
        {
            var outposts = DomainInteriorMarks.Build(model, eye).Outposts;
            var marks = new List<DotMark>(outposts.Count);
            foreach (var o in outposts)
            {
                var own = AtlasPalette.OwnerColor(o.OwnerActorId);
                // a nudge above its own glow (PortLens' quarter-lift), but
                // sized under a tier-1 port dot so the keystone still leads.
                var bright = new Rgba(
                    (byte)(own.R + (255 - own.R) / 4),
                    (byte)(own.G + (255 - own.G) / 4),
                    (byte)(own.B + (255 - own.B) / 4));
                marks.Add(new DotMark(o.Hex,
                    0.22f * AtlasGeometry.HexStep, 5.5f, bright, 0.9f));
            }
            Render(marks);
        }
    }
}
