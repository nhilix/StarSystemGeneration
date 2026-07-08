using System;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Text;

namespace StarGen.Inspector;

public sealed class Repl
{
    private const int SectorWidth = 32;
    private ulong _seed = 42;
    private int _x, _y;
    private GalaxyContext? _galaxy;

    public void Run()
    {
        Console.WriteLine("StarGen inspector — 'help' for commands.");
        Show();
        while (true)
        {
            Console.Write("> ");
            var parts = (Console.ReadLine() ?? "quit")
                .Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            switch (parts[0].ToLowerInvariant())
            {
                case "quit" or "exit": return;
                case "help":
                    Console.WriteLine("seed <n> | galaxy <seed> [sectors] | goto <x> <y> | next | prev | reroll");
                    Console.WriteLine("find <criterion> | stats <n> | map [layer] | sector <sx> <sy> | cell <cx> <cy>");
                    Console.WriteLine("gsave <path> | gload <path> | quit");
                    Console.WriteLine("map layers: density | polity | zone | dev | lean");
                    Console.WriteLine("find criteria: overlay | <overlay-id> | settled | sapient");
                    break;
                case "seed" when parts.Length == 2 && ulong.TryParse(parts[1], out var s):
                    _seed = s;
                    _galaxy = null;
                    Console.WriteLine("seed set — back to flatspace (galaxy cleared)");
                    Show(); break;
                case "galaxy" when parts.Length >= 2 && ulong.TryParse(parts[1], out var gseed):
                {
                    int size = parts.Length >= 3 && int.TryParse(parts[2], out var sz) ? sz : 21;
                    var config = new GalaxyConfig { MasterSeed = gseed, GalaxyRadiusCells = size };
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var skeleton = SkeletonBuilder.Build(config);
                    sw.Stop();
                    _seed = gseed;
                    _galaxy = new GalaxyContext(config) { Skeleton = skeleton };
                    int nonVoid = 0, claimed = 0, chokepoints = 0;
                    foreach (var c in skeleton.Cells)
                    {
                        if (c.IsChokepoint) chokepoints++;
                        if (c.IsVoid) continue;
                        nonVoid++;
                        if (c.OwnerPolityId >= 0) claimed++;
                    }
                    int living = skeleton.Polities.Count(p => !p.Extinct);
                    Console.WriteLine($"galaxy built in {sw.ElapsedMilliseconds} ms: {skeleton.Cells.Length} cells, "
                        + $"{living} living / {skeleton.Polities.Count - living} extinct polities, "
                        + $"{skeleton.Events.Count} events, {100.0 * claimed / nonVoid:F1}% of space claimed, "
                        + $"{chokepoints} chokepoints");
                    break;
                }
                case "cell" when parts.Length == 3 && _galaxy?.Skeleton is { } sk
                        && int.TryParse(parts[1], out var qcx) && int.TryParse(parts[2], out var qcy):
                {
#warning HEXMIGRATION: 'cell' command bounds-checks against the placeholder square grid; replaced once the REPL addresses real hex cells.
                    if (qcx < 0 || qcy < 0 || qcx >= sk.GridSize || qcy >= sk.GridSize)
                    { Console.WriteLine("cell out of range"); break; }
                    var cell = sk.CellAt(qcx, qcy);
                    string owner = cell.OwnerPolityId >= 0 ? sk.Polities[cell.OwnerPolityId].Name : "unclaimed";
                    Console.WriteLine($"cell [{qcx},{qcy}] density {cell.MeanDensity:F2}"
                        + (cell.IsVoid ? " VOID" : "") + (cell.IsChokepoint ? " CHOKEPOINT" : "")
                        + $" · {cell.Lean} · metallicity {cell.Metallicity:F2}");
                    Console.WriteLine($"  owner: {owner} · dev {cell.DevelopmentTier}"
                        + (cell.WarScarred ? " · war-scarred" : ""));
                    foreach (var a in cell.Anchors)
                        Console.WriteLine($"  anchor: {a.Type} at [{a.Hex.Q:D4}-{a.Hex.R:D4}]"
                            + (a.SpeciesId >= 0 ? $" (species {sk.Species[a.SpeciesId].Name})" : ""));
                    foreach (var e in sk.Events)
                        if (e.Cx == qcx && e.Cy == qcy)
                            Console.WriteLine($"  epoch {e.Epoch}: {e.Type} by {sk.Polities[e.ActorPolityId].Name}"
                                + (e.TargetPolityId >= 0 ? $" vs {sk.Polities[e.TargetPolityId].Name}" : ""));
                    Console.WriteLine(GalaxyMapView.CellZoom(_galaxy, qcx, qcy));
                    break;
                }
                case "gsave" when parts.Length == 2 && _galaxy?.Skeleton != null:
                    System.IO.File.WriteAllText(parts[1], SkeletonSerializer.ToText(_galaxy.Skeleton));
                    Console.WriteLine($"saved to {parts[1]}");
                    break;
                case "gload" when parts.Length == 2:
                    try
                    {
                        using (var reader = new System.IO.StreamReader(parts[1]))
                        {
                            var skeleton = SkeletonSerializer.Load(reader);
                            _galaxy = new GalaxyContext(skeleton.Config) { Skeleton = skeleton };
                            _seed = skeleton.Config.MasterSeed;
                            Console.WriteLine($"loaded galaxy seed {_seed}, {skeleton.Polities.Count} polities");
                        }
                    }
                    catch (System.IO.InvalidDataException ex) { Console.WriteLine($"refused: {ex.Message}"); }
                    catch (System.IO.FileNotFoundException) { Console.WriteLine("file not found"); }
                    break;
                case "goto" when parts.Length == 3
                        && int.TryParse(parts[1], out var gx) && int.TryParse(parts[2], out var gy):
                    (_x, _y) = (Math.Max(0, gx), Math.Max(0, gy)); Show(); break;
                case "next": Step(+1); Show(); break;
                case "prev": Step(-1); Show(); break;
                case "reroll":
                    _seed = (ulong)Guid.NewGuid().GetHashCode() * 2654435761UL;
                    Console.WriteLine($"seed = {_seed}"); Show(); break;
                case "find" when parts.Length == 2: Find(parts[1]); break;
                case "stats" when parts.Length == 2 && int.TryParse(parts[1], out var n):
                    Console.WriteLine(StatsReport.Build(_galaxy ?? GalaxyContext.Flatspace(_seed), _x, _y, n, WalkWidth)); break;
                case "map" or "sector" when _galaxy?.Skeleton == null:
                    Console.WriteLine("build a galaxy first (galaxy <seed>)");
                    break;
                case "sector" when parts.Length == 3
                        && int.TryParse(parts[1], out var msx) && int.TryParse(parts[2], out var msy):
                    Console.WriteLine(GalaxyMapView.SectorMap(_galaxy!, msx, msy));
                    break;
                case "map":
                    Console.WriteLine(GalaxyMapView.CellMap(_galaxy!.Skeleton!,
                        parts.Length >= 2 ? parts[1] : "density"));
                    break;
                default:
                    Console.WriteLine("unrecognized — try 'help'"); break;
            }
        }
    }

    /// <summary>Row width for the linear hex walk: full galaxy width when a galaxy is
    /// loaded (find/stats would otherwise only ever sample the leftmost sector band),
    /// classic 32-hex sector width in flatspace.</summary>
#warning HEXMIGRATION: WalkWidth approximates the old rectangular WidthHexes from GalaxyRadiusCells so goto/next/prev/find/stats keep compiling; the linear x,y walk itself is replaced by a hex-native walk (e.g. HexGrid.Spiral) in its own task.
    private int WalkWidth => _galaxy != null ? _galaxy.Config.GalaxyRadiusCells * 2 + 1 : SectorWidth;

    private void Step(int dir)
    {
        int linear = _y * WalkWidth + _x + dir;
        if (linear < 0) linear = 0;
        (_x, _y) = (linear % WalkWidth, linear / WalkWidth);
    }

    private HexResult Gen(HexCoordinate c) =>
        Generator.Generate(_galaxy ?? GalaxyContext.Flatspace(_seed), c);

    private void Show() =>
        Console.WriteLine(SystemFormatter.Format(Gen(new HexCoordinate(_x, _y))));

    private void Find(string criterion)
    {
        for (int i = 0; i < 50_000; i++)
        {
            Step(+1);
            var system = Gen(new HexCoordinate(_x, _y)).System;
            if (system != null && Matches(system, criterion)) { Show(); return; }
        }
        Console.WriteLine($"no match for '{criterion}' within 50,000 hexes");
    }

    private static bool Matches(StarSystem s, string criterion) => criterion switch
    {
        "overlay" => s.OverlayId != null,
        "settled" => AnyBody(s, b => b.Settlement != Settlement.None),
        "sapient" => AnyBody(s, b => b.Biosphere == Biosphere.Sapient),
        _ => s.OverlayId == criterion,
    };

    private static bool AnyBody(StarSystem s, Func<Body, bool> pred)
    {
        foreach (var star in s.Stars)
            foreach (var slot in star.Slots)
            {
                if (slot.Body == null) continue;
                if (pred(slot.Body)) return true;
                foreach (var sat in slot.Body.Satellites)
                    if (pred(sat)) return true;
            }
        return false;
    }
}
