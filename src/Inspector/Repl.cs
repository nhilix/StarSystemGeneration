using System;
using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Text;

namespace StarGen.Inspector;

public sealed class Repl
{
    private const int SectorWidth = 32;
    private ulong _seed = 42;
    private int _x, _y;

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
                    Console.WriteLine("seed <n> | goto <x> <y> | next | prev | reroll | find <criterion> | stats <n> | quit");
                    Console.WriteLine("find criteria: overlay | <overlay-id> | settled | sapient");
                    break;
                case "seed" when parts.Length == 2 && ulong.TryParse(parts[1], out var s):
                    _seed = s; Show(); break;
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
                    Console.WriteLine(StatsReport.Build(_seed, _x, _y, n, SectorWidth)); break;
                default:
                    Console.WriteLine("unrecognized — try 'help'"); break;
            }
        }
    }

    private void Step(int dir)
    {
        int linear = _y * SectorWidth + _x + dir;
        if (linear < 0) linear = 0;
        (_x, _y) = (linear % SectorWidth, linear / SectorWidth);
    }

    private void Show() =>
        Console.WriteLine(SystemFormatter.Format(
            Generator.Generate(_seed, new HexCoordinate(_x, _y))));

    private void Find(string criterion)
    {
        for (int i = 0; i < 50_000; i++)
        {
            Step(+1);
            var system = Generator.Generate(_seed, new HexCoordinate(_x, _y)).System;
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
