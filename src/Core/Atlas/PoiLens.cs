using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One anchored point of interest: type picks the authored glyph,
/// magnitude sizes it, dormancy (precursor sites) brightens it — a live
/// remnant, not an inert ruin.</summary>
public readonly record struct PoiMark(
    int PoiId, PoiType Type, HexCoordinate Hex, double Magnitude,
    bool Dormant, Rgba Color);

/// <summary>The POI lens — debris with a name at its hex
/// (chronicle-and-poi.md). Live anchors only: depleted POIs stay in the
/// registry as history but no longer pin their hex (PoiCompiler.LiveAt
/// parity).</summary>
public static class PoiLens
{
    // The narrative gold family (§8 dot #D8B46F), shaded per type so the
    // glyph set reads at a glance even before the sprite resolves.
    private static readonly Rgba Battlefield = new(205, 115, 85);   // wreckage rust
    private static readonly Rgba Ruins = new(190, 178, 148);        // dead-city bone
    private static readonly Rgba RuinedCapital = new(175, 135, 195);// fallen purple
    private static readonly Rgba Memorial = new(150, 172, 210);     // mourning blue
    private static readonly Rgba Precursor = new(216, 180, 111);    // deep-time gold

    public static IReadOnlyList<PoiMark> Marks(AtlasReadModel model,
                                               EyeContext eye)
    {
        var marks = new List<PoiMark>();
        foreach (var poi in model.State.Pois)             // id order (P6)
        {
            if (poi.Depleted) continue;
            marks.Add(new PoiMark(poi.Id, poi.Type, poi.Hex, poi.Magnitude,
                                  poi.Dormant, ColorOf(poi.Type)));
        }
        return marks;
    }

    public static Rgba ColorOf(PoiType type) => type switch
    {
        PoiType.Battlefield => Battlefield,
        PoiType.Ruins => Ruins,
        PoiType.RuinedCapital => RuinedCapital,
        PoiType.Memorial => Memorial,
        _ => Precursor,
    };
}
