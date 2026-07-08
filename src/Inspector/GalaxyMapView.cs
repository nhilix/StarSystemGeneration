using System.Linq;
using System.Text;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Inspector;

/// <summary>ASCII galaxy atlas (spec §9): the visual counterpart of stats.</summary>
public static class GalaxyMapView
{
    private const string DensityRamp = " .:-=+*#%@";

    public static string CellMap(GalaxySkeleton s, string layer)
    {
        var sb = new StringBuilder();
        for (int cy = 0; cy < s.Config.CellsY; cy++)
        {
            for (int cx = 0; cx < s.Config.CellsX; cx++)
                sb.Append(CellChar(s, s.CellAt(cx, cy), layer));
            sb.AppendLine();
        }
        sb.AppendLine(Legend(s, layer));
        return sb.ToString();
    }

    private static char CellChar(GalaxySkeleton s, RegionCell c, string layer) => layer switch
    {
        "polity" => c.IsVoid ? ' '
            : s.Polities.Any(p => !p.Extinct && p.CapitalCx == c.Cx && p.CapitalCy == c.Cy) ? '*'
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

    public static string SectorMap(GalaxyContext galaxy, int sx, int sy)
    {
        if (sx < 0 || sy < 0 || sx >= galaxy.Config.SizeSectors || sy >= galaxy.Config.SizeSectors)
            return "sector out of range";
        var sb = new StringBuilder();
        var skeleton = galaxy.Skeleton;
        for (int hy = sy * 40; hy < sy * 40 + 40; hy++)
        {
            for (int hx = sx * 32; hx < sx * 32 + 32; hx++)
            {
                var coord = new HexCoordinate(hx, hy);
                bool anchored = skeleton != null &&
                    skeleton.CellForHex(coord).Anchors.Any(a => a.Hex.Equals(coord));
                var system = Generator.Generate(galaxy, coord).System;
                sb.Append(system == null ? '·'
                    : anchored ? '@'
                    : SystemIsSettled(system) ? 'o' : '*');
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
