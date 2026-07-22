using System.Collections.Generic;
using StarGen.Core.Atlas;

namespace StarGen.AtlasView
{
    /// <summary>AC1.3 — the domain's worked skeleton. A small owner-tinted dot
    /// at each worked (satellite) hex, dimmer and smaller than a port dot, so a
    /// domain stops reading as a uniform glow and shows the hexes it actually
    /// farms. Rides the domains lens (LensRail gates visibility); outpost hexes
    /// are drawn by <see cref="OutpostLayer"/>, not doubled here.</summary>
    public sealed class DomainInteriorLayer : DotMarkLayer
    {
        // Behind the outpost marks (-0.13) and the port dots (-0.15): the
        // faintest, farthest tier of the domain read.
        protected override float Z => -0.11f;

        public override void Show(AtlasReadModel model, EyeContext eye)
        {
            var worked = DomainInteriorMarks.Build(model, eye).Worked;
            var marks = new List<DotMark>(worked.Count);
            foreach (var w in worked)
            {
                var color = AtlasPalette.OwnerColor(w.OwnerActorId);
                // subordinate: a small dot, a screen floor well under the
                // port's (2 + 1.4·tier)·2 px, faint so it dusts the glow
                // rather than competing with the keystone.
                marks.Add(new DotMark(w.Hex,
                    0.14f * AtlasGeometry.HexStep, 4.5f, color, 0.55f));
            }
            Render(marks);
        }
    }
}
