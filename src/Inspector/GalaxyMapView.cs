using System.Linq;
using System.Text;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Inspector;

/// <summary>ASCII galaxy atlas (spec §9): the visual counterpart of stats.
/// Three zoom layers: galaxy (map, one glyph per cell), sector (32x40 hexes),
/// cell (8x10 hexes). Every glyph is emitted twice horizontally to compensate
/// for terminal fonts being ~2x taller than wide.</summary>
public static class GalaxyMapView
{
    private const string DensityRamp = " .:-=+*#%@";

#warning HEXMIGRATION: CellMap now walks the flat spiral-ordered cell list (no 2D grid rows/cols) instead of the placeholder square grid; a true staggered hex-lattice atlas render lands with the inspector rewrite (Task 10).
    public static string CellMap(GalaxySkeleton s, string layer)
    {
        var sb = new StringBuilder();
        foreach (var cell in s.Cells)
        {
            char glyph = CellChar(s, cell, layer);
            sb.Append(glyph).Append(glyph);
        }
        sb.AppendLine();
        sb.AppendLine(Legend(s, layer));
        return sb.ToString();
    }

    private static char CellChar(GalaxySkeleton s, RegionCell c, string layer) => layer switch
    {
        "polity" => c.IsVoid ? ' '
            : s.Polities.Any(p => !p.Extinct && p.CapitalCx == c.Q && p.CapitalCy == c.R) ? '*'
            : c.OwnerPolityId < 0 ? '.'
            : c.OwnerPolityId < 26 ? (char)('A' + c.OwnerPolityId)
            : (char)('a' + c.OwnerPolityId % 26),
        "zone" => c.IsVoid ? ' ' : c.WarScarred ? '!' : c.IsChokepoint ? '^'
            : c.Contested ? '?' : '.',
        "dev" => c.IsVoid ? ' ' : c.OwnerPolityId < 0 ? '.'
            : (char)('0' + System.Math.Min(5, c.DevelopmentTier)),
        "lean" => c.IsVoid ? ' ' : c.Lean switch
        {
            StellarLean.YoungBright => '+', StellarLean.OldDim => '-',
            StellarLean.RemnantGraveyard => 'x', _ => '.',
        },
        _ => DensityRamp[(int)(System.Math.Clamp(c.MeanDensity, 0, 0.9999) * 10)],
    };

    private static string Legend(GalaxySkeleton s, string layer) => layer switch
    {
        "polity" => string.Join("  ", s.Polities.Where(p => !p.Extinct).Select(p =>
            $"{(p.Id < 26 ? (char)('A' + p.Id) : (char)('a' + p.Id % 26))}={p.Name} "
            + $"({s.Cells.Count(c => c.OwnerPolityId == p.Id)} cells)"))
            + "   *=capital .=unclaimed",
        "zone" => "!=war-scarred ^=chokepoint ?=contested .=quiet",
        "dev" => "0-5=development .=unclaimed",
        "lean" => "+=young-bright -=old-dim x=remnant-graveyard .=balanced",
        _ => "density: ' " + DensityRamp + " ' low->high",
    };

#warning HEXMIGRATION: SectorMap bounds-checks against the placeholder square-grid radius (old SizeSectors sectorization); replaced by hex-native zoom navigation in its own task (Task 10).
    public static string SectorMap(GalaxyContext galaxy, int sx, int sy)
    {
        int gridSize = galaxy.Skeleton?.Config.GalaxyRadiusCells * 2 + 1 ?? 0;
        if (sx < 0 || sy < 0 || sx >= gridSize || sy >= gridSize)
            return "sector out of range";
        return HexMap(galaxy, sx * 32, sy * 40, 32, 40);
    }

#warning HEXMIGRATION: CellZoom bounds-checks against the placeholder square-grid radius; replaced by hex-native zoom navigation in its own task (Task 10).
    public static string CellZoom(GalaxyContext galaxy, int cx, int cy)
    {
        int gridSize = galaxy.Skeleton?.Config.GalaxyRadiusCells * 2 + 1 ?? 0;
        if (cx < 0 || cy < 0 || cx >= gridSize || cy >= gridSize)
            return "cell out of range";
        return HexMap(galaxy, cx * 8, cy * 10, 8, 10);
    }

    /// <summary>Hex-resolution render of any rectangular region (sector and cell zooms).</summary>
    private static string HexMap(GalaxyContext galaxy, int x0, int y0, int width, int height)
    {
        var sb = new StringBuilder();
        var skeleton = galaxy.Skeleton;
        for (int hy = y0; hy < y0 + height; hy++)
        {
            for (int hx = x0; hx < x0 + width; hx++)
            {
                var coord = new HexCoordinate(hx, hy);
                bool anchored = skeleton != null &&
                    skeleton.CellForHex(coord).Anchors.Any(a => a.Hex.Equals(coord));
                var system = Generator.Generate(galaxy, coord).System;
                char glyph = system == null ? '·'
                    : anchored ? '@'
                    : SystemIsSettled(system) ? 'o' : '*';
                sb.Append(glyph).Append(glyph);
            }
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
