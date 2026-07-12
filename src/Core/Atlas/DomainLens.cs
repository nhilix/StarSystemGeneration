using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>The domains lens — territory as the union of port service
/// areas, derived from the port registry at query time (P4/P5: never
/// stored). Overlap is meaningful: ≥2 distinct owners is a contested-
/// influence zone and reads as its own shade. An overlay lens: wilds are
/// transparent so the nature base shows through — visibly dark.</summary>
public static class DomainLens
{
    private const byte GlowAlpha = 150;
    private const byte ContestedAlpha = 200;

    /// <summary>Distinct owner actor ids servicing the hex, ascending, into
    /// the caller-owned list (cleared first — no per-query allocation in
    /// render loops). God-truth today; the controller eye will read
    /// believed extents when the play tier lands (reserved seam).</summary>
    public static void OwnersAt(AtlasReadModel model, EyeContext eye,
                                HexCoordinate hex, List<int> into) =>
        PortDomains.OwnersAt(model.Skeleton, model.State.Config,
                             model.State.Ports, hex, into);

    /// <summary>Per-raster-cell domain shades, parallel to model.Cells,
    /// sampled at each cell's center hex: transparent wilds, the owner's
    /// golden-ratio glow, a blended-and-brightened contested shade.</summary>
    public static IReadOnlyList<Rgba> CellShades(AtlasReadModel model, EyeContext eye)
    {
        var shades = new Rgba[model.Cells.Count];
        var owners = new List<int>();
        for (int i = 0; i < shades.Length; i++)
        {
            OwnersAt(model, eye, HexGrid.CellCenter(model.Cells[i].Coord), owners);
            shades[i] = Shade(owners);
        }
        return shades;
    }

    // Overlap (Venn) relationship shades — what the intersection of two
    // polities' regions says about the pair.
    public static readonly Rgba WarShade = new(225, 70, 60, 255);
    public static readonly Rgba TensionShade = new(235, 150, 60, 255);
    public static readonly Rgba WarmShade = new(80, 210, 160, 255);
    public static readonly Rgba NeutralShade = new(125, 145, 185, 255);

    /// <summary>Distinct port owners, ascending — the field shader's slot
    /// order (deterministic: registry-derived, id-sorted).</summary>
    public static IReadOnlyList<int> PolitySlots(AtlasReadModel model, EyeContext eye)
    {
        var slots = new List<int>();
        foreach (var port in model.State.Ports)
        {
            int at = slots.BinarySearch(port.OwnerActorId);
            if (at < 0) slots.Insert(~at, port.OwnerActorId);
        }
        return slots;
    }

    /// <summary>The shade of the region where two polities' domains
    /// overlap: an active war overrides everything, then loaded tension,
    /// then real warmth; strangers and lukewarm pairs read neutral.
    /// Symmetric by construction.</summary>
    public static Rgba OverlapShade(AtlasReadModel model, EyeContext eye,
                                    int actorA, int actorB)
    {
        if (WarOps.ActiveWarBetween(model.State, actorA, actorB) != null)
            return WarShade;
        var rel = model.State.RelationOf(actorA, actorB);
        if (rel == null) return NeutralShade;
        if (rel.Tension >= 0.4) return TensionShade;
        if (rel.Warmth >= 0.3) return WarmShade;
        return NeutralShade;
    }

    /// <summary>Per-hex domain shades for an arbitrary hex list — the map
    /// surface samples service radii at hex resolution, which is where the
    /// organic borders come from.</summary>
    public static IReadOnlyList<Rgba> HexShades(AtlasReadModel model, EyeContext eye,
                                                IReadOnlyList<HexCoordinate> hexes)
    {
        var shades = new Rgba[hexes.Count];
        var owners = new List<int>();
        for (int i = 0; i < shades.Length; i++)
        {
            OwnersAt(model, eye, hexes[i], owners);
            shades[i] = Shade(owners);
        }
        return shades;
    }

    private static Rgba Shade(List<int> owners)
    {
        if (owners.Count == 0) return AtlasPalette.Clear;
        if (owners.Count == 1)
        {
            var own = AtlasPalette.OwnerColor(owners[0]);
            return new Rgba(own.R, own.G, own.B, GlowAlpha);
        }
        // Contested: the owners' colors blended, lifted toward white so the
        // overlap visibly outshines either claim.
        int r = 0, g = 0, b = 0;
        foreach (int id in owners)
        {
            var c = AtlasPalette.OwnerColor(id);
            r += c.R; g += c.G; b += c.B;
        }
        r /= owners.Count; g /= owners.Count; b /= owners.Count;
        return new Rgba(
            (byte)(r + (255 - r) / 3),
            (byte)(g + (255 - g) / 3),
            (byte)(b + (255 - b) / 3),
            ContestedAlpha);
    }
}
