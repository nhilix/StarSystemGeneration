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
        var laneCells = layer == "lanes" ? LaneCells(state, live: true) : null;
        var deadLaneCells = layer == "lanes" ? LaneCells(state, live: false) : null;
        var traffic = layer == "traffic" ? TrafficCells(state) : null;
        var warCells = layer == "war" ? WarStationCells(state) : null;

        var owners = new List<int>();
        foreach (var (cell, off) in offsets)
        {
            char glyph;
            if (cell.IsVoid) glyph = ' ';
            else if (layer == "lanes")
                glyph = portCells.Contains(cell.Coord) ? '*'
                    : laneCells!.Contains(cell.Coord) ? '+'
                    : deadLaneCells!.Contains(cell.Coord) ? '~' : '.';
            else if (layer == "price")
                glyph = PriceGlyph(state, cell, good);
            else if (layer == "traffic")
                glyph = portCells.Contains(cell.Coord) ? '*'
                    : traffic!.TryGetValue(cell.Coord, out double trips)
                        ? TrafficGlyph(trips) : '.';
            else if (layer == "war")
            {
                // borders flaring: belligerent domains keep their letter,
                // the peaceful fade, war fleets on station burn
                PortDomains.OwnersAt(sk, state.Config, state.Ports,
                                     HexGrid.CellCenter(cell.Coord), owners);
                bool warFleetHere = warCells!.Contains(cell.Coord);
                glyph = warFleetHere ? '!'
                    : owners.Count switch
                    {
                        0 => '.',
                        1 => WarOps.AtWar(state, owners[0])
                            ? OwnerLetter(owners[0]) : ',',
                        _ => '?',
                    };
            }
            else if (layer == "tension")
            {
                // the pressure gauge shaded: a domain cell shows its
                // owner's hottest live relation as a digit
                PortDomains.OwnersAt(sk, state.Config, state.Ports,
                                     HexGrid.CellCenter(cell.Coord), owners);
                glyph = owners.Count switch
                {
                    0 => '.',
                    1 => TensionGlyph(state, owners[0]),
                    _ => '?',
                };
            }
            else if (layer == "plague")
            {
                // contagion made visible: infected port cells burn, immune
                // ones carry the scar, quarantined approaches close
                glyph = PlagueGlyph(state, cell, portCells, owners);
            }
            else if (layer == "tech")
            {
                // the tech gap made visible: each domain cell shows its
                // owner's Astrogation tier — whose ports reach farther
                PortDomains.OwnersAt(sk, state.Config, state.Ports,
                                     HexGrid.CellCenter(cell.Coord), owners);
                glyph = owners.Count switch
                {
                    0 => '.',
                    1 => (char)('0' + System.Math.Min(9,
                        Tech.Tier(state, owners[0], TechDomain.Astrogation))),
                    _ => '?',
                };
            }
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
            "war" => "!=war fleet on station letter=belligerent domain "
                     + ",=at peace ?=contested .=wilds",
            "tension" => "owner's hottest live relation per domain cell: "
                         + "digit=tension×9 ?=contested .=wilds "
                         + "(the war-pressure gauge)",
            "tech" => "owner's Astrogation tier per domain cell: digit=tier "
                      + "?=contested .=wilds (leaders' ports reach farther)",
            "plague" => "!=infected port o=immune (recovered) x=quarantined "
                        + "approach letter=healthy domain .=wilds",
            "lanes" => "*=port cell +=lane ~=dead lane (gate down) .=off-network (wilds dark)",
            "traffic" => "posted trips/year: *=port · ,=lane no hulls · "
                         + "- <0.5 · = <2 · + <5 · # 5+ · .=wilds "
                         + "(news rides this — busy lanes carry it fast)",
            "price" => $"{Substrate.Goods.Get(good).Name} vs founding price: "
                       + "_ glut · - cheap · = par · + dear · * scarce · # spike "
                       + "· ! famine-grade · .=wilds",
            _ => "letter=domain ?=contested overlap .=wilds " + Legend(state),
        });
        return sb.ToString();
    }

    /// <summary>The contagion layer: infected port cells '!', immune 'o',
    /// cells crossed by a quarantined lane 'x', healthy domains keep their
    /// letter, wilds stay dark.</summary>
    private static char PlagueGlyph(SimState state, RegionCell cell,
        HashSet<HexCoordinate> portCells, List<int> owners)
    {
        if (portCells.Contains(cell.Coord))
            foreach (var port in state.Ports)
            {
                if (!HexGrid.CellOf(port.Hex).Equals(cell.Coord)) continue;
                if (PlagueOps.Afflicted(state, port.Id)) return '!';
                foreach (var plague in state.Plagues)
                    if (plague.ImmuneUntil.TryGetValue(port.Id, out long lapse)
                        && lapse >= state.WorldYear) return 'o';
            }
        foreach (var lane in state.Lanes)
            if (lane.QuarantinedUntil >= state.WorldYear
                && LaneCrosses(state, lane, cell.Coord)) return 'x';
        PortDomains.OwnersAt(state.Skeleton, state.Config, state.Ports,
                             HexGrid.CellCenter(cell.Coord), owners);
        return owners.Count switch
        {
            0 => '.', 1 => OwnerLetter(owners[0]), _ => '?',
        };
    }

    private static bool LaneCrosses(SimState state, Lane lane,
                                    HexCoordinate cellCoord)
    {
        var a = state.Ports[lane.PortAId].Hex;
        var b = state.Ports[lane.PortBId].Hex;
        int n = HexGrid.Distance(a, b);
        for (int i = 0; i <= n; i++)
        {
            double t = n == 0 ? 0.0 : (double)i / n;
            if (HexGrid.CellOf(HexGrid.Round(
                    a.Q + (b.Q - a.Q) * t, a.R + (b.R - a.R) * t))
                .Equals(cellCoord)) return true;
        }
        return false;
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

    /// <summary>The owner's hottest live relation, 0–9 — where the powder is.</summary>
    private static char TensionGlyph(SimState state, int ownerId)
    {
        double hottest = 0;
        foreach (var rel in state.Relations)
            if (rel.Involves(ownerId) && RelationsOps.BothLive(state, rel)
                && rel.Tension > hottest)
                hottest = rel.Tension;
        return (char)('0' + (int)System.Math.Round(hottest * 9));
    }

    /// <summary>Cells where war fleets stand — blockades and expeditions
    /// on active-war stations.</summary>
    private static HashSet<HexCoordinate> WarStationCells(SimState state)
    {
        var cells = new HashSet<HexCoordinate>();
        foreach (var fleet in state.Fleets)
            if (fleet.TotalHulls > 0
                && fleet.Posture is FleetPosture.Blockade
                    or FleetPosture.Expedition
                && WarOps.AtWar(state, fleet.OwnerActorId))
                cells.Add(HexGrid.CellOf(fleet.Hex));
        return cells;
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

    /// <summary>Traffic band glyph for a lane cell — a lane exists but reads
    /// blank when no hulls are posted on it (nothing moves, no news).</summary>
    private static char TrafficGlyph(double tripsPerYear) => tripsPerYear switch
    {
        <= 0 => ',',   // a lane exists, but no hulls: nothing moves, no news
        < 0.5 => '-',
        < 2.0 => '=',
        < 5.0 => '+',
        _ => '#',
    };

    /// <summary>Max posted traffic per lane cell — derived from fleet state
    /// at render time (FleetOps.TrafficPerYear, the slice-I news-speed data).</summary>
    private static Dictionary<HexCoordinate, double> TrafficCells(SimState state)
    {
        var cells = new Dictionary<HexCoordinate, double>();
        foreach (var lane in state.Lanes)
        {
            double trips = FleetOps.TrafficPerYear(state, lane);
            var a = state.Ports[lane.PortAId].Hex;
            var b = state.Ports[lane.PortBId].Hex;
            int n = HexGrid.Distance(a, b);
            for (int i = 0; i <= n; i++)
            {
                double t = n == 0 ? 0.0 : (double)i / n;
                var cell = HexGrid.CellOf(HexGrid.Round(
                    a.Q + (b.Q - a.Q) * t, a.R + (b.R - a.R) * t));
                cells.TryGetValue(cell, out double held);
                cells[cell] = System.Math.Max(held, trips);
            }
        }
        return cells;
    }

    /// <summary>Cells crossed by each lane's hex line (cube lerp + round) —
    /// live and dead lanes render as separate strokes (a downed gate is a
    /// visible wound, lane-economics spec §2).</summary>
    private static HashSet<HexCoordinate> LaneCells(SimState state, bool live)
    {
        var cells = new HashSet<HexCoordinate>();
        foreach (var lane in state.Lanes)
        {
            if (LaneMath.IsLive(state, lane) != live) continue;
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
