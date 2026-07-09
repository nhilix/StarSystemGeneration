using System;
using System.Linq;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Text;

namespace StarGen.Inspector;

public sealed class Repl
{
    private ulong _seed = 42;
    private int _spiralIndex;
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
                    Console.WriteLine("seed <n> | galaxy <seed> [radiusCells] | goto <q> <r> | next | prev | reroll");
                    Console.WriteLine("find <criterion> | stats <n> | map [layer] | cell <q> <r> | polity <id> | chronicle [polityId]");
                    Console.WriteLine("epoch <seed> [epochs] — step the new seven-phase frame, print the phase/event trace");
                    Console.WriteLine("gsave <path> | gload <path> | quit");
                    Console.WriteLine("map layers: density | polity | zone | dev | lean | trade | economy | war");
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
                    Console.WriteLine($"galaxy built in {sw.ElapsedMilliseconds} ms: {skeleton.Cells.Count} cells, "
                        + $"{living} living / {skeleton.Polities.Count - living} extinct polities, "
                        + $"{skeleton.Events.Count} events, {100.0 * claimed / nonVoid:F1}% of space claimed, "
                        + $"{chokepoints} chokepoints"
                        + $", {skeleton.Wars.Count} wars ({skeleton.Wars.Count(w => !w.Ended)} live)");
                    break;
                }
                case "cell" when parts.Length == 3 && _galaxy?.Skeleton is { } sk
                        && int.TryParse(parts[1], out var qcx) && int.TryParse(parts[2], out var qcy):
                {
                    var cellCoord = new HexCoordinate(qcx, qcy);
                    if (!sk.TryGetCell(cellCoord, out var cell))
                    { Console.WriteLine("cell out of range"); break; }
                    string owner = cell.OwnerPolityId >= 0 ? sk.Polities[cell.OwnerPolityId].Name : "unclaimed";
                    Console.WriteLine($"cell [{qcx},{qcy}] density {cell.MeanDensity:F2}"
                        + (cell.IsVoid ? " VOID" : "") + (cell.IsChokepoint ? " CHOKEPOINT" : "")
                        + $" · {cell.Lean} · metallicity {cell.Metallicity:F2}");
                    Console.WriteLine($"  owner: {owner} · dev {cell.DevelopmentTier}"
                        + (cell.WarScarred ? " · war-scarred" : ""));
                    Console.WriteLine($"  population {cell.Population:F1}"
                        + (cell.PopulationSpeciesId >= 0 ? $" ({sk.Species[cell.PopulationSpeciesId].Name})" : "")
                        + $" · throughput {cell.RouteThroughput:F1}"
                        + $" · value {Economy.SystemValue(cell.OwnerPolityId >= 0 ? sk.Species[sk.Polities[cell.OwnerPolityId].SpeciesId] : Economy.DisplayBaseline, cell):F1}");
                    foreach (var a in cell.Anchors)
                        Console.WriteLine($"  anchor: {a.Type} at [{a.Hex.Q:D4}-{a.Hex.R:D4}]"
                            + (a.SpeciesId >= 0 ? $" (species {sk.Species[a.SpeciesId].Name})" : ""));
                    foreach (var e in sk.Events)
                        if (e.Q == qcx && e.R == qcy)
                            Console.WriteLine("  " + ChronicleView.Describe(sk, e));
                    Console.WriteLine(GalaxyMapView.CellZoom(_galaxy, cellCoord));
                    break;
                }
                case "epoch" when parts.Length >= 2 && ulong.TryParse(parts[1], out var eseed):
                {
                    var econfig = new Core.Epoch.EpochSimConfig { MasterSeed = eseed };
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var epochs))
                        econfig.Sim.EpochCount = epochs;
                    var estate = Core.Epoch.StubGenesis.Seed(econfig);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    new Core.Epoch.EpochEngine().Run(estate);
                    sw.Stop();
                    Console.WriteLine(Core.Epoch.SimTraceView.Render(estate));
                    Console.WriteLine($"stepped in {sw.ElapsedMilliseconds} ms");
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
                    _spiralIndex = GalaxyEnumerator.SpiralIndexOf(new HexCoordinate(gx, gy)); Show(); break;
                case "next": Step(+1); Show(); break;
                case "prev": Step(-1); Show(); break;
                case "reroll":
                    _seed = (ulong)Guid.NewGuid().GetHashCode() * 2654435761UL;
                    Console.WriteLine($"seed = {_seed}"); Show(); break;
                case "find" when parts.Length == 2: Find(parts[1]); break;
                case "stats" when parts.Length == 2 && int.TryParse(parts[1], out var n):
                    Console.WriteLine(StatsReport.Build(_galaxy ?? GalaxyContext.Flatspace(_seed), _spiralIndex, n));
                    if (_galaxy?.Skeleton is { } statsSk) Console.WriteLine(EconomyReport.Build(statsSk));
                    break;
                case "map" when _galaxy?.Skeleton == null:
                    Console.WriteLine("build a galaxy first (galaxy <seed>)");
                    break;
                case "map":
                    Console.WriteLine(GalaxyMapView.CellMap(_galaxy!.Skeleton!,
                        parts.Length >= 2 ? parts[1] : "density"));
                    break;
                case "polity" when parts.Length == 2 && _galaxy?.Skeleton is { } skPol
                        && int.TryParse(parts[1], out var polityId):
                {
                    if (polityId < 0 || polityId >= skPol.Polities.Count)
                    { Console.WriteLine("no such polity"); break; }
                    var p = skPol.Polities[polityId];
                    var sp = skPol.Species[p.SpeciesId];
                    int cells = 0; double pop = 0;
                    foreach (var c in skPol.Cells)
                        if (c.OwnerPolityId == p.Id) { cells++; pop += c.Population; }
                    Console.WriteLine($"{p.Name} (id {p.Id}){(p.Extinct ? " EXTINCT" : "")}"
                        + $" · species {sp.Name} ({sp.Embodiment}) · capital [{p.CapitalQ},{p.CapitalR}]");
                    Console.WriteLine($"  {cells} cells · population {pop:F1} · tech tier {p.TechTier}"
                        + $" · stockpile {p.MilitaryStockpile:F1} · wealth {p.Wealth:F1}");
                    Console.WriteLine($"  balances: provisions {p.ProvisionsBalance:F1}"
                        + $" · ore {p.OreBalance:F1} · exotics {p.ExoticsBalance:F1}"
                        + $" (invested {p.ExoticsInvested:F1})"
                        + (p.BlockadeLoss > 0 ? $" · blockade loss {p.BlockadeLoss:F1}" : ""));
                    foreach (var w in skPol.Wars)
                    {
                        if (w.AttackerId != p.Id && w.DefenderId != p.Id) continue;
                        string other = skPol.Polities[w.AttackerId == p.Id ? w.DefenderId : w.AttackerId].Name;
                        double wear = w.AttackerId == p.Id ? w.AttackerWeariness : w.DefenderWeariness;
                        Console.WriteLine(w.Ended
                            ? $"  war vs {other}: {w.Goal}, ended epoch-started {w.StartEpoch} - {w.Outcome}"
                            : $"  war vs {other}: {w.Goal}, since epoch {w.StartEpoch}, weariness {wear:F2}");
                    }
                    break;
                }
                case "chronicle" when _galaxy?.Skeleton is { } skChr:
                {
                    int filter = parts.Length >= 2 && int.TryParse(parts[1], out var pf) ? pf : -1;
                    Console.WriteLine(ChronicleView.Build(skChr, filter));
                    break;
                }
                default:
                    Console.WriteLine("unrecognized — try 'help'"); break;
            }
        }
    }

    private void Step(int dir) => _spiralIndex = Math.Max(0, _spiralIndex + dir);

    private HexResult Gen(HexCoordinate c) =>
        Generator.Generate(_galaxy ?? GalaxyContext.Flatspace(_seed), c);

    private void Show() =>
        Console.WriteLine(SystemFormatter.Format(Gen(GalaxyEnumerator.SpiralAt(_spiralIndex))));

    private void Find(string criterion)
    {
        for (int i = 0; i < 50_000; i++)
        {
            Step(+1);
            var system = Gen(GalaxyEnumerator.SpiralAt(_spiralIndex)).System;
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
