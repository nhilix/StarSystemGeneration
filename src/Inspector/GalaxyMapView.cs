using System.Linq;
using System.Text;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Inspector;

/// <summary>ASCII galaxy atlas (spec §9): the visual counterpart of stats.
/// Two zoom layers: galaxy (map, one glyph per cell) and cell zoom (the cell's
/// 91 member hexes). Both render on an offset canvas — flat-top odd columns
/// drop a half line — with every glyph emitted twice horizontally to compensate
/// for terminal fonts being ~2x taller than wide.</summary>
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

        // Trade shading is relative to this map's busiest cell — absolute flow
        // magnitudes are tuning-dependent and would render near-uniform.
        double maxThroughput = layer == "trade"
            ? System.Math.Max(1e-9, s.Cells.Max(c => c.RouteThroughput)) : 0;

        foreach (var (cell, off) in offsets)
        {
            char glyph = CellChar(s, cell, layer, maxThroughput);
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
        sb.AppendLine(Legend(s, layer));
        return sb.ToString();
    }

    private static char CellChar(GalaxySkeleton s, RegionCell c, string layer, double maxThroughput = 1.0) => layer switch
    {
        "polity" => c.IsVoid ? ' '
            : s.Polities.Any(p => !p.Extinct && p.CapitalQ == c.Q && p.CapitalR == c.R) ? '*'
            : c.OwnerPolityId < 0 ? '.'
            : c.OwnerPolityId < 26 ? (char)('A' + c.OwnerPolityId)
            : (char)('a' + c.OwnerPolityId % 26),
        "zone" => c.IsVoid ? ' ' : c.WarScarred ? '!' : c.IsChokepoint ? '^'
            : c.Contested ? '?' : '.',
        "dev" => c.IsVoid ? ' ' : c.OwnerPolityId < 0 ? '.'
            : (char)('0' + System.Math.Min(9, c.DevelopmentTier)),
        "lean" => c.IsVoid ? ' ' : c.Lean switch
        {
            StellarLean.YoungBright => '+', StellarLean.OldDim => '-',
            StellarLean.RemnantGraveyard => 'x', _ => '.',
        },
        "trade" => c.IsVoid ? ' '
            : c.RouteThroughput <= 0 ? '.'
            : DensityRamp[System.Math.Clamp(
                (int)(c.RouteThroughput / maxThroughput * 9.0), 1, 9)],
        "economy" => c.IsVoid ? ' ' : EconomyChar(s, c),
        "war" => c.IsVoid ? ' ' : c.Contested ? '!' : c.WarScarred ? 'x'
            : c.IsChokepoint ? '^' : '.',
        _ => DensityRamp[(int)(System.Math.Clamp(c.MeanDensity, 0, 0.9999) * 10)],
    };

    private static char EconomyChar(GalaxySkeleton s, RegionCell c)
    {
        var species = c.OwnerPolityId >= 0
            ? s.Species[s.Polities[c.OwnerPolityId].SpeciesId]
            : Economy.DisplayBaseline;
        double p = Economy.ProvisionsPotential(species, c);
        double o = Economy.OrePotential(c);
        double e = Economy.ExoticsPotential(c);
        bool anchored = c.Anchors.Count > 0;
        char glyph = p >= o && p >= e ? 'p' : o >= e ? 'o' : 'e';
        return anchored ? char.ToUpperInvariant(glyph) : glyph;
    }

    private static string Legend(GalaxySkeleton s, string layer) => layer switch
    {
        "polity" => string.Join("  ", s.Polities.Where(p => !p.Extinct).Select(p =>
            $"{(p.Id < 26 ? (char)('A' + p.Id) : (char)('a' + p.Id % 26))}={p.Name} "
            + $"({s.Cells.Count(c => c.OwnerPolityId == p.Id)} cells)"))
            + "   *=capital .=unclaimed",
        "zone" => "!=war-scarred ^=chokepoint ?=contested .=quiet",
        "dev" => "0-9=development .=unclaimed",
        "lean" => "+=young-bright -=old-dim x=remnant-graveyard .=balanced",
        "trade" => "route throughput (relative to busiest cell): .=none "
            + DensityRamp.Substring(1) + " low->high",
        "economy" => "p/o/e=dominant production (P/O/E=anchored) provisions/ore/exotics",
        "war" => "!=contested front x=war-scarred ^=chokepoint .=quiet",
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
