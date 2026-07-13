using System;
using System.Text;
using StarGen.Core.Epoch;
using static System.FormattableString;

namespace StarGen.Inspector;

/// <summary>`ehealth` (slice SH): the macro health readout — the latest
/// metric row grouped by family, the negative-treasury roster with the
/// epoch each polity went under, and per-metric trends. The series is
/// in-memory only: a loaded artifact starts empty and accumulates as it
/// steps.</summary>
public static class HealthView
{
    public static string Render(SimState sim)
    {
        if (sim.Health.Rows.Count == 0)
            return "health series empty — the probe accumulates as the sim "
                + "steps (estep, or epoch <seed>); a loaded artifact starts blank";
        var sb = new StringBuilder();
        var row = sim.Health.Rows[^1];
        sb.AppendLine($"sim health — epoch {row.Epoch} · y{row.WorldYear} · "
            + $"{sim.Health.Rows.Count} rows in series");
        string family = "";
        foreach (var m in MetricRegistry.All)
        {
            string fam = m.Name[..m.Name.IndexOf('.')];
            if (fam != family)
            {
                family = fam;
                sb.AppendLine($"  [{family}]");
            }
            sb.AppendLine(Invariant($"    {m.Name,-32} {m.Get(row),14:G6}  {m.Doc}"));
        }

        // the debt roster: who is under water, and since when
        sb.AppendLine("  [debt roster]");
        bool any = false;
        foreach (var pr in sim.Polities)
        {
            var actor = sim.Actors[pr.ActorId];
            if (!actor.Entered || actor.Retired || pr.Credits >= 0) continue;
            any = true;
            int since = -1;
            foreach (var p in sim.Health.PolityRows)
                if (p.ActorId == pr.ActorId)
                {
                    if (p.Credits < 0) { if (since < 0) since = p.Epoch; }
                    else since = -1;   // recovered — the streak restarts
                }
            sb.AppendLine($"    #{pr.ActorId} {actor.Name,-24} "
                + Invariant($"{pr.Credits,12:F0} cr")
                + (since >= 0 ? $"  negative since epoch {since}" : ""));
        }
        if (!any) sb.AppendLine("    (every treasury above water)");
        return sb.ToString();
    }

    public static string RenderTrend(SimState sim, string metricName)
    {
        var metric = MetricRegistry.Find(metricName);
        if (metric == null)
        {
            var names = new StringBuilder("unknown metric — one of:\n");
            foreach (var m in MetricRegistry.All)
                names.AppendLine($"  {m.Name}");
            return names.ToString();
        }
        if (sim.Health.Rows.Count == 0)
            return "health series empty — step the sim to accumulate";
        var sb = new StringBuilder();
        sb.AppendLine($"{metric.Name} — {metric.Doc}");
        double min = double.MaxValue, max = double.MinValue;
        foreach (var r in sim.Health.Rows)
        {
            double v = metric.Get(r);
            if (v < min) min = v;
            if (v > max) max = v;
        }
        double span = max - min;
        foreach (var r in sim.Health.Rows)
        {
            double v = metric.Get(r);
            int bar = span <= 0 ? 0
                : (int)Math.Round((v - min) / span * 40);
            sb.AppendLine(Invariant($"  e{r.Epoch,3} y{r.WorldYear,5} {v,14:G6} ")
                + new string('#', bar));
        }
        return sb.ToString();
    }
}
