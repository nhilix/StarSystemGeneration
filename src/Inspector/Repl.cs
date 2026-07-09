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
    private Core.Epoch.SimState? _sim;

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
                    Console.WriteLine("find <criterion> | stats <n> | map [layer] | cell <q> <r>");
                    Console.WriteLine("epoch <seed> [epochs] [radiusCells] — run the seven-phase frame, print the phase/event trace");
                    Console.WriteLine("emap [domains|lanes] — port-domain / lane-network map of the stepped sim");
                    Console.WriteLine("chronicle [actorId] — the event log, optionally one actor's biography view");
                    Console.WriteLine("esave <path> | eload <path> — the layer-sectioned world-state artifact");
                    Console.WriteLine("goods — the 17-good catalog, grade bands, demand profiles");
                    Console.WriteLine("infra [q r] — the facility catalog + potentials/siting for sample cells (or a galaxy cell)");
                    Console.WriteLine("map layers: density | lean");
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
                    int nonVoid = 0, chokepoints = 0, homeworlds = 0;
                    foreach (var c in skeleton.Cells)
                    {
                        if (c.IsChokepoint) chokepoints++;
                        foreach (var a in c.Anchors)
                            if (a.Type == AnchorType.Homeworld) homeworlds++;
                        if (!c.IsVoid) nonVoid++;
                    }
                    Console.WriteLine($"galaxy built in {sw.ElapsedMilliseconds} ms: {skeleton.Cells.Count} cells "
                        + $"({nonVoid} non-void), {chokepoints} chokepoints, "
                        + $"{homeworlds} homeworld anchors, {skeleton.Species.Count} species");
                    break;
                }
                case "cell" when parts.Length == 3 && _galaxy?.Skeleton is { } sk
                        && int.TryParse(parts[1], out var qcx) && int.TryParse(parts[2], out var qcy):
                {
                    var cellCoord = new HexCoordinate(qcx, qcy);
                    if (!sk.TryGetCell(cellCoord, out var cell))
                    { Console.WriteLine("cell out of range"); break; }
                    Console.WriteLine($"cell [{qcx},{qcy}] density {cell.MeanDensity:F2}"
                        + (cell.IsVoid ? " VOID" : "") + (cell.IsChokepoint ? " CHOKEPOINT" : "")
                        + $" · {cell.Lean} · metallicity {cell.Metallicity:F2}");
                    foreach (var a in cell.Anchors)
                        Console.WriteLine($"  anchor: {a.Type} at [{a.Hex.Q:D4}-{a.Hex.R:D4}]"
                            + (a.SpeciesId >= 0 ? $" (species {sk.Species[a.SpeciesId].Name})" : ""));
                    Console.WriteLine(GalaxyMapView.CellZoom(_galaxy, cellCoord));
                    break;
                }
                case "epoch" when parts.Length >= 2 && ulong.TryParse(parts[1], out var eseed):
                {
                    var econfig = new Core.Epoch.EpochSimConfig { MasterSeed = eseed };
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var epochs))
                        econfig.Sim.EpochCount = epochs;
                    int eradius = parts.Length >= 4 && int.TryParse(parts[3], out var er) ? er : 21;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var eskeleton = SkeletonBuilder.Build(
                        new GalaxyConfig { MasterSeed = eseed, GalaxyRadiusCells = eradius });
                    var estate = Core.Epoch.EpochGenesis.Seed(eskeleton, econfig);
                    new Core.Epoch.EpochEngine().Run(estate);
                    sw.Stop();
                    _sim = estate;
                    _seed = eseed;
                    _galaxy = new GalaxyContext(eskeleton.Config) { Skeleton = eskeleton };
                    Console.WriteLine(Core.Epoch.SimTraceView.Render(estate));
                    Console.WriteLine($"stepped in {sw.ElapsedMilliseconds} ms");
                    break;
                }
                case "emap" when _sim == null:
                    Console.WriteLine("run a sim first (epoch <seed>) or eload an artifact");
                    break;
                case "emap":
                    Console.WriteLine(EpochMapView.Render(_sim!,
                        parts.Length >= 2 ? parts[1] : "domains"));
                    break;
                case "chronicle" when _sim == null:
                    Console.WriteLine("run a sim first (epoch <seed>) or eload an artifact");
                    break;
                case "chronicle":
                {
                    int filter = parts.Length >= 2 && int.TryParse(parts[1], out var cf) ? cf : -1;
                    var events = filter >= 0 ? _sim!.Log.ForActor(filter) : _sim!.Log.Events;
                    int shown = 0;
                    foreach (var e in events)
                    {
                        Console.WriteLine("  " + Core.Epoch.SimTraceView.Describe(e));
                        shown++;
                    }
                    if (shown == 0) Console.WriteLine("  (no events)");
                    break;
                }
                case "esave" when parts.Length == 2 && _sim != null:
                    System.IO.File.WriteAllText(parts[1],
                        Core.Epoch.ArtifactSerializer.ToText(_sim));
                    Console.WriteLine($"artifact saved to {parts[1]}");
                    break;
                case "esave":
                    Console.WriteLine("run a sim first (epoch <seed>), then: esave <path>");
                    break;
                case "eload" when parts.Length == 2:
                    try
                    {
                        using (var reader = new System.IO.StreamReader(parts[1]))
                        {
                            var loaded = Core.Epoch.ArtifactSerializer.Load(reader);
                            _sim = loaded;
                            _seed = loaded.Skeleton.Config.MasterSeed;
                            _galaxy = new GalaxyContext(loaded.Skeleton.Config)
                            { Skeleton = loaded.Skeleton };
                            Console.WriteLine($"artifact loaded: seed {_seed}, "
                                + $"epoch {loaded.EpochIndex} (y{loaded.WorldYear}), "
                                + $"{loaded.Ports.Count} ports, {loaded.Lanes.Count} lanes, "
                                + $"{loaded.Log.Events.Count} events");
                        }
                    }
                    catch (System.IO.InvalidDataException ex) { Console.WriteLine($"refused: {ex.Message}"); }
                    catch (System.IO.FileNotFoundException) { Console.WriteLine("file not found"); }
                    break;
                case "goods":
                    Console.WriteLine(Core.Substrate.SubstrateView.RenderGoods());
                    break;
                case "infra" when parts.Length == 3 && _galaxy?.Skeleton is { } isk
                        && int.TryParse(parts[1], out var iq) && int.TryParse(parts[2], out var ir):
                {
                    if (!isk.TryGetCell(new HexCoordinate(iq, ir), out var icell))
                    { Console.WriteLine("cell out of range"); break; }
                    var fields = new Core.Substrate.CellFields(
                        icell.MeanDensity, icell.Lean, icell.Metallicity,
                        icell.Anchors.Any(a => a.Type == AnchorType.MineralRich),
                        icell.Anchors.Any(a => a.Type == AnchorType.PrecursorSite));
                    // connectivity/port context are epoch-sim state — neutral
                    // wilds here; workforce from the homeworld anchor if present
                    var site = new Core.Substrate.CellSite(fields, Connectivity: 0.3,
                        IsPortHeart: false, PortTier: 0,
                        DevelopmentTier: 0, IsChokepoint: icell.IsChokepoint);
                    var homeAnchor = icell.Anchors.FirstOrDefault(a => a.Type == AnchorType.Homeworld);
                    var workforce = homeAnchor != null && homeAnchor.SpeciesId >= 0
                        ? isk.Species[homeAnchor.SpeciesId].Embodiment
                        : Embodiment.TerranAnalog;
                    Console.WriteLine(Core.Substrate.SubstrateView.RenderSite(
                        FormattableString.Invariant($"cell [{iq},{ir}]"), fields, site, workforce));
                    break;
                }
                case "infra" when parts.Length == 3:
                    Console.WriteLine("no galaxy loaded (or coords unparseable) — build one with: galaxy <seed>");
                    break;
                case "infra":
                    Console.WriteLine(Core.Substrate.SubstrateView.RenderInfra());
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
                    break;
                case "map" when _galaxy?.Skeleton == null:
                    Console.WriteLine("build a galaxy first (galaxy <seed>)");
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
