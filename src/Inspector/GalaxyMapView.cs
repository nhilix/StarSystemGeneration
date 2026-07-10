using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Inspector;

/// <summary>ASCII natural-raster atlas (spec §9): the visual counterpart of
/// stats. Two zoom layers: galaxy (map, one glyph per cell) and cell zoom (the
/// cell's 91 member hexes). Both render on an offset canvas — flat-top odd
/// columns drop a half line — with every glyph emitted twice horizontally to
/// compensate for terminal fonts being ~2x taller than wide. Political layers
/// (domains, lanes) render from the epoch sim's registries in EpochMapView.</summary>
public static class GalaxyMapView
{
    internal const string DensityRamp = " .:-=+*#%@";

    /// <summary>Shared offset-canvas renderer: one glyph per cell, doubled
    /// horizontally. The watch views drive it with working-state glyphs.</summary>
    internal static string RenderCells(GalaxySkeleton s, Func<RegionCell, char> glyph)
    {
        var offsets = s.Cells.Select(c => (cell: c, off: HexGrid.ToOffset(c.Coord))).ToList();
        int minCol = offsets.Min(t => t.off.Col), maxCol = offsets.Max(t => t.off.Col);
        int minRow = offsets.Min(t => t.off.Row), maxRow = offsets.Max(t => t.off.Row);
        int width = (maxCol - minCol + 1) * 2, height = (maxRow - minRow) * 2 + 2;
        var canvas = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) canvas[y, x] = ' ';

        foreach (var (cell, off) in offsets)
        {
            char g = glyph(cell);
            int col = off.Col - minCol, row = off.Row - minRow;
            int y = 2 * row + (off.Col & 1);          // odd columns drop half a line
            canvas[y, col * 2] = g;
            canvas[y, col * 2 + 1] = g;
        }

        var sb = new StringBuilder();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) sb.Append(canvas[y, x]);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    internal static char Ramp(double v) =>
        DensityRamp[(int)(System.Math.Clamp(v, 0, 0.9999) * 10)];

    public static string CellMap(GalaxySkeleton s, string layer) =>
        RenderCells(s, GlyphFor(s, layer)) + Legend(layer) + "\n";

    /// <summary>The natural-raster layers — every one a genesis residue
    /// read, never paint (slice F).</summary>
    private static Func<RegionCell, char> GlyphFor(GalaxySkeleton s, string layer)
    {
        switch (layer)
        {
            case "lean":
                return c => c.IsVoid ? ' ' : c.Lean switch
                {
                    StellarLean.YoungBright => '+', StellarLean.OldDim => '-',
                    StellarLean.RemnantGraveyard => 'x', _ => '.',
                };
            case "gas":
                return c => c.IsVoid && c.GasFraction <= 0 ? ' '
                    : Ramp(c.GasFraction / 0.5);
            case "metal":
                return c => c.IsVoid ? ' ' : Ramp(c.Metallicity);
            case "age":
                // stellar age: share of stellar+remnant mass past its youth
                return c => c.IsVoid ? ' ' : Ramp(c.CohortOld + c.CohortRemnant);
            case "minerals":
                return c => c.IsVoid ? ' ' : Ramp(c.MineralRichness);
            case "bio":
                return c => c.IsVoid ? ' '
                    : c.BiosphereRichness <= 0 ? '.' : Ramp(c.BiosphereRichness);
            case "emergence":
            {
                var chars = new Dictionary<HexCoordinate, char>();
                foreach (var wave in s.PrecursorWaves)
                    foreach (var site in wave.Sites)
                        if (site.Type == PrecursorSiteType.SterilizationScar)
                            chars[HexGrid.CellOf(site.Hex)] = 'x';
                foreach (var origin in s.Origins)
                    chars[origin.CellCoord] = origin.Era switch
                    {
                        OriginEra.Current => 'C',
                        OriginEra.Precursor => 'P',
                        _ => 'N',
                    };
                return c => chars.TryGetValue(c.Coord, out var g) ? g
                    : c.BiosphereRichness > 0 ? '·'
                    : c.IsVoid ? ' ' : '.';
            }
            case "features":
            {
                var chars = new Dictionary<HexCoordinate, char>();
                // low-priority first; later writes win
                foreach (var f in Ordered(s, GalacticFeatureType.AgnOutburst, 'a'))
                    chars[f.coord] = f.glyph;
                foreach (var f in Ordered(s, GalacticFeatureType.MergerStream, 'M'))
                    chars[f.coord] = f.glyph;
                foreach (var f in Ordered(s, GalacticFeatureType.DarkCloud, 'D'))
                    chars[f.coord] = f.glyph;
                foreach (var f in Ordered(s, GalacticFeatureType.EmissionNebula, 'E'))
                    chars[f.coord] = f.glyph;
                foreach (var f in Ordered(s, GalacticFeatureType.SupernovaRemnant, 'S'))
                    chars[f.coord] = f.glyph;
                foreach (var f in Ordered(s, GalacticFeatureType.GlobularCluster, 'G'))
                    chars[f.coord] = f.glyph;
                return c => chars.TryGetValue(c.Coord, out var g) ? g
                    : c.IsVoid ? ' ' : '.';
            }
            default:
                return c => Ramp(c.MeanDensity);
        }
    }

    private static IEnumerable<(HexCoordinate coord, char glyph)> Ordered(
        GalaxySkeleton s, GalacticFeatureType type, char glyph)
    {
        foreach (var feature in s.Features)
            if (feature.Type == type)
                foreach (var coord in feature.Cells)
                    yield return (coord, glyph);
    }

    private static string Legend(string layer) => layer switch
    {
        "lean" => "+=young-bright -=old-dim x=remnant-graveyard .=balanced",
        "gas" => "gas fraction: ' " + DensityRamp + " ' none->half the cell's mass",
        "metal" => "stellar metallicity: ' " + DensityRamp + " ' primordial->enriched",
        "age" => "stellar age (old+remnant share): ' " + DensityRamp + " ' young->burned out",
        "minerals" => "mineral richness: ' " + DensityRamp + " ' barren->supernova-forged",
        "bio" => "biosphere richness: ' " + DensityRamp + " ' (.=lifeless, blank=void)",
        "emergence" => "C=current homeworld P=precursor origin N=native x=sterilization scar ·=living biosphere",
        "features" => "G=globular S=supernova remnant E=emission nebula D=dark cloud M=merger stream a=AGN wave",
        _ => "density: ' " + DensityRamp + " ' low->high",
    };

    public static string CellZoom(GalaxyContext galaxy, HexCoordinate cellCoord)
    {
        var members = HexGrid.Spiral(HexGrid.CellCenter(cellCoord), HexGrid.CellRadius)
            .Select(h => (hex: h, off: HexGrid.ToOffset(h))).ToList();
        int minCol = members.Min(t => t.off.Col), minRow = members.Min(t => t.off.Row);
        int width = (members.Max(t => t.off.Col) - minCol + 1) * 2;
        int height = (members.Max(t => t.off.Row) - minRow) * 2 + 2;
        var canvas = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) canvas[y, x] = ' ';

        var skeleton = galaxy.Skeleton;
        foreach (var (hex, off) in members)
        {
            bool anchored = skeleton != null &&
                skeleton.CellForHex(hex).Anchors.Any(a => a.Hex.Equals(hex));
            var system = Generator.Generate(galaxy, hex).System;
            char glyph = system == null ? '·'
                : anchored ? '@'
                : SystemIsSettled(system) ? 'o' : '*';
            int col = off.Col - minCol, row = off.Row - minRow;
            int y = 2 * row + (off.Col & 1);
            canvas[y, col * 2] = glyph;
            canvas[y, col * 2 + 1] = glyph;
        }

        var sb = new StringBuilder();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) sb.Append(canvas[y, x]);
            sb.AppendLine();
        }
        sb.AppendLine("·=empty *=system o=settled @=anchored");
        return sb.ToString();
    }

    private static bool SystemIsSettled(StarSystem system)
    {
        foreach (var star in system.Stars)
            foreach (var slot in star.Slots)
            {
                if (slot.Body == null) continue;
                if (slot.Body.Settlement != Settlement.None) return true;
                foreach (var sat in slot.Body.Satellites)
                    if (sat.Settlement != Settlement.None) return true;
            }
        return false;
    }
}
