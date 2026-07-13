using System.Text;
using static System.FormattableString;

namespace StarGen.Core.Epoch;

/// <summary>Deterministic CSV rendering of the health series — the sweep
/// runner's file substance (sim-health spec §4). Invariant culture, R
/// round-trip formatting, '\n' line ends, byte-identical for the same
/// history. The metrics header IS the registry: adding a metric is a
/// registry entry, never a serialization change.</summary>
public static class MetricCsv
{
    public static string RenderMetrics(MetricSeries health)
    {
        var sb = new StringBuilder();
        sb.Append("epoch,world_year");
        foreach (var m in MetricRegistry.All)
            sb.Append(',').Append(m.Name);
        sb.Append('\n');
        foreach (var row in health.Rows)
        {
            sb.Append(Invariant($"{row.Epoch},{row.WorldYear}"));
            foreach (var m in MetricRegistry.All)
                sb.Append(',').Append(Invariant($"{m.Get(row):R}"));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public static string RenderPhases(MetricSeries health)
    {
        var sb = new StringBuilder();
        sb.Append("epoch,phase,polity_credits,polity_pools,corp_credits,")
          .Append("segment_wealth,faction_wealth,order_escrow,")
          .Append("courier_escrow,expedition_purses,loan_principal,supply")
          .Append('\n');
        foreach (var r in health.MoneyRows)
            sb.Append(Invariant($"{r.Epoch},{r.Phase},{r.PolityCredits:R},"))
              .Append(Invariant($"{r.PolityPools:R},{r.CorpCredits:R},"))
              .Append(Invariant($"{r.SegmentWealth:R},{r.FactionWealth:R},"))
              .Append(Invariant($"{r.OrderEscrow:R},{r.CourierEscrow:R},"))
              .Append(Invariant($"{r.ExpeditionPurses:R},"))
              .Append(Invariant($"{r.LoanPrincipal:R},{r.Supply:R}"))
              .Append('\n');
        return sb.ToString();
    }

    public static string RenderPolities(MetricSeries health)
    {
        var sb = new StringBuilder();
        sb.Append("epoch,actor_id,credits,pools,population,mean_sol,")
          .Append("legitimacy").Append('\n');
        foreach (var r in health.PolityRows)
            sb.Append(Invariant($"{r.Epoch},{r.ActorId},{r.Credits:R},"))
              .Append(Invariant($"{r.Pools:R},{r.Population:R},"))
              .Append(Invariant($"{r.MeanSoL:R},{r.Legitimacy:R}"))
              .Append('\n');
        return sb.ToString();
    }
}
