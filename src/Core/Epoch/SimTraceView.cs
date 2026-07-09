using System.Text;
using static System.FormattableString;

namespace StarGen.Core.Epoch;

/// <summary>Deterministic text rendering of a stepped sim — the phase trace
/// plus the chronicle. The REPL prints it; the determinism gate byte-compares
/// it. Every interpolation renders invariant-culture (negative coordinates
/// would otherwise pick up culture negative signs); fixed iteration order; no
/// timing or environment.</summary>
public static class SimTraceView
{
    public static string Render(SimState state)
    {
        var sb = new StringBuilder();
        var sim = state.Config.Sim;
        sb.AppendLine(Invariant($"epoch frame — seed {state.Config.MasterSeed} · ")
            + Invariant($"{state.EpochIndex} epochs stepped × {sim.YearsPerEpoch}y = ")
            + Invariant($"{state.WorldYear} world-years"));

        sb.AppendLine(Invariant($"actors: {state.Actors.Count}"));
        foreach (var a in state.Actors)
            sb.AppendLine(Invariant($"  #{a.Id} {a.Name} ({a.Kind}) — seat ")
                + Invariant($"({a.Seat.Q},{a.Seat.R}), enters epoch {a.EntryEpoch} ")
                + Invariant($"(y{a.EntryEpoch * sim.YearsPerEpoch})")
                + (a.Entered ? "" : " [not yet entered]"));

        int lastEpoch = -1;
        foreach (var t in state.Trace)
        {
            if (t.Epoch != lastEpoch)
            {
                lastEpoch = t.Epoch;
                int y0 = t.Epoch * sim.YearsPerEpoch;
                sb.AppendLine();
                sb.AppendLine(Invariant($"epoch {t.Epoch} · y{y0}–y{y0 + sim.YearsPerEpoch}"));
            }
            sb.AppendLine(Invariant($"  {t.Phase,-10} {t.Note}"));
        }

        sb.AppendLine();
        sb.AppendLine(Invariant($"chronicle ({state.Log.Events.Count} events)"));
        foreach (var e in state.Log.Events)
            sb.AppendLine("  " + Describe(e));
        return sb.ToString();
    }

    public static string Describe(WorldEvent e)
    {
        string what = e.Payload switch
        {
            PolityEmergedPayload p => $"{p.PolityName} enters the galactic stage",
            _ => e.Type.ToString(),
        };
        string family = e.Family.ToString().ToLowerInvariant();
        string vis = e.Visibility.ToString().ToLowerInvariant();
        return Invariant($"y{e.WorldYear,-5} {family,-12} {what} ")
            + Invariant($"at ({e.Location.Q},{e.Location.R}) [{vis}]");
    }
}
