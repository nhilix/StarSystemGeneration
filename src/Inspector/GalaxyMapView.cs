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
    private const string DensityRamp = " .:-=+*#%@";

    public static string CellMap(GalaxySkeleton s, string layer)
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
            char glyph = CellChar(cell, layer);
            int col = off.Col - minCol, row = off.Row - minRow;
            int y = 2 * row + (off.Col & 1);          // odd columns drop half a line
            canvas[y, col * 2] = glyph;
            canvas[y, col * 2 + 1] = glyph;
        }

        var sb = new StringBuilder();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) sb.Append(canvas[y, x]);
            sb.AppendLine();
        }
        sb.AppendLine(Legend(layer));
        return sb.ToString();
    }

    private static char CellChar(RegionCell c, string layer) => layer switch
    {
        "lean" => c.IsVoid ? ' ' : c.Lean switch
        {
            StellarLean.YoungBright => '+', StellarLean.OldDim => '-',
            StellarLean.RemnantGraveyard => 'x', _ => '.',
        },
        _ => DensityRamp[(int)(System.Math.Clamp(c.MeanDensity, 0, 0.9999) * 10)],
    };

    private static string Legend(string layer) => layer switch
    {
        "lean" => "+=young-bright -=old-dim x=remnant-graveyard .=balanced",
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
