using System.Collections.Generic;
using System.Linq;
using System.Text;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Substrate = StarGen.Core.Substrate;

namespace StarGen.Inspector;

/// <summary>ASCII political atlas over the epoch sim's registries — the
/// two-plane model made visible (space-and-travel.md P1: empires as
/// port-domain glows with organic borders, lanes as literal highways, the
/// wilds visibly dark). One glyph per raster cell, same offset/double-width
/// canvas idiom as GalaxyMapView; every quantity derives from the port
/// registry at render time — nothing here is stored state.</summary>
public static class EpochMapView
{
    public static string Render(SimState state, string layer = "domains",
                                Substrate.GoodId good = Substrate.GoodId.Provisions)
    {
        var sk = state.Skeleton;
        var offsets = sk.Cells.Select(c => (cell: c, off: HexGrid.ToOffset(c.Coord))).ToList();
        int minCol = offsets.Min(t => t.off.Col);
        int minRow = offsets.Min(t => t.off.Row);
        int width = (offsets.Max(t => t.off.Col) - minCol + 1) * 2;
        int height = (offsets.Max(t => t.off.Row) - minRow) * 2 + 2;
        var canvas = new char[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) canvas[y, x] = ' ';

        var portCells = new HashSet<HexCoordinate>();
        foreach (var p in state.Ports) portCells.Add(HexGrid.CellOf(p.Hex));
        var laneCells = layer == "lanes" ? LaneCells(state) : null;

        var owners = new List<int>();
        foreach (var (cell, off) in offsets)
        {
            char glyph;
            if (cell.IsVoid) glyph = ' ';
            else if (layer == "lanes")
                glyph = portCells.Contains(cell.Coord) ? '*'
                    : laneCells!.Contains(cell.Coord) ? '+' : '.';
            else if (layer == "price")
                glyph = PriceGlyph(state, cell, good);
            else
            {
                PortDomains.OwnersAt(sk, state.Config, state.Ports,
                                     HexGrid.CellCenter(cell.Coord), owners);
                glyph = owners.Count switch
                {
                    0 => '.',
                    1 => OwnerLetter(owners[0]),
                    _ => '?',
                };
            }
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
        sb.AppendLine(layer switch
        {
            "lanes" => "*=port cell +=lane .=off-network (wilds dark)",
            "price" => $"{Substrate.Goods.Get(good).Name} vs founding price: "
                       + "_ glut · - cheap · = par · + dear · * scarce · # spike "
                       + "· ! famine-grade · .=wilds",
            _ => "letter=domain ?=contested overlap .=wilds " + Legend(state),
        });
        return sb.ToString();
    }

    /// <summary>Per-good price shading at the nearest servicing port —
    /// spikes at blockades, gluts at cut-off producers (market-geography.md:
    /// the most legible economic layer).</summary>
    private static char PriceGlyph(SimState state, RegionCell cell,
                                   Substrate.GoodId good)
    {
        var center = HexGrid.CellCenter(cell.Coord);
        int best = -1, bestDist = int.MaxValue;
        foreach (var p in state.Ports)
        {
            if (!PortDomains.Services(state.Skeleton, state.Config, p, center))
                continue;
            int dist = HexGrid.Distance(p.Hex, center);
            if (dist < bestDist) { bestDist = dist; best = p.Id; }
        }
        if (best < 0) return '.';
        double ratio = state.Markets[best].Price[(int)good]
                       / Market.InitialPrice(state.Config.Economy, good);
        return ratio switch
        {
            < 0.25 => '_',
            < 0.6 => '-',
            < 1.5 => '=',
            < 3.0 => '+',
            < 8.0 => '*',
            < 30.0 => '#',
            _ => '!',
        };
    }

    private static char OwnerLetter(int actorId) =>
        actorId % 52 < 26 ? (char)('A' + actorId % 26) : (char)('a' + actorId % 26);

    private static string Legend(SimState state)
    {
        var parts = new List<string>();
        foreach (var a in state.Actors)
        {
            if (!a.Entered) continue;
            int ports = state.Ports.Count(p => p.OwnerActorId == a.Id);
            parts.Add($"{OwnerLetter(a.Id)}={a.Name}({ports})");
        }
        return string.Join(" ", parts);
    }

    /// <summary>Cells crossed by each lane's hex line (cube lerp + round).</summary>
    private static HashSet<HexCoordinate> LaneCells(SimState state)
    {
        var cells = new HashSet<HexCoordinate>();
        foreach (var lane in state.Lanes)
        {
            var a = state.Ports[lane.PortAId].Hex;
            var b = state.Ports[lane.PortBId].Hex;
            int n = HexGrid.Distance(a, b);
            for (int i = 0; i <= n; i++)
            {
                double t = n == 0 ? 0.0 : (double)i / n;
                cells.Add(HexGrid.CellOf(HexGrid.Round(
                    a.Q + (b.Q - a.Q) * t, a.R + (b.R - a.R) * t)));
            }
        }
        return cells;
    }
}
