using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One drawable fleet: posture picks the authored glyph, the
/// owner tint colors it, hulls size it. Empty shells (zero hulls) leave
/// no mark — a fleet record without ships is bookkeeping, not presence.</summary>
public readonly record struct FleetMarker(
    int FleetId, HexCoordinate Hex, FleetPosture Posture, int OwnerActorId,
    int Hulls, Rgba Color);

/// <summary>The fleets lens — posture-differentiated marks at fleet hexes
/// (K2: the first authored sprite vocabulary; the lens speaks posture +
/// tint + size, the presentation's glyph atlas speaks shape).</summary>
public static class FleetLens
{
    public static IReadOnlyList<FleetMarker> Markers(AtlasReadModel model,
                                                     EyeContext eye)
    {
        var marks = new List<FleetMarker>();
        foreach (var fleet in model.State.Fleets)         // id order (P6)
        {
            if (fleet.TotalHulls <= 0) continue;
            var own = AtlasPalette.OwnerColor(fleet.OwnerActorId);
            // The same quarter-white nudge port markers use — glyphs must
            // read above the owner's own domain glow.
            marks.Add(new FleetMarker(
                fleet.Id, fleet.Hex, fleet.Posture, fleet.OwnerActorId,
                fleet.TotalHulls,
                new Rgba((byte)(own.R + (255 - own.R) / 4),
                         (byte)(own.G + (255 - own.G) / 4),
                         (byte)(own.B + (255 - own.B) / 4))));
        }
        return marks;
    }
}
