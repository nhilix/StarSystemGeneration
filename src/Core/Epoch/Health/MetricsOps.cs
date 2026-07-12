using System;
using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>Credit totals by holder class at one instant (sim-health spec
/// §1) — the money supply decomposed into every conserved store it can sit
/// in. Captured after every phase, it attributes treasury motion to the
/// phase that moved it. LoanPrincipal rides beside the classes as a claim,
/// not a holder: loans move credits between ledgers, the principal is memory.</summary>
public sealed record MoneyRow(
    int Epoch, string Phase,
    double PolityCredits, double PolityPools, double CorpCredits,
    double SegmentWealth, double FactionWealth, double OrderEscrow,
    double LoanPrincipal)
{
    /// <summary>The money supply: every holder class summed.</summary>
    public double Supply => PolityCredits + PolityPools + CorpCredits
        + SegmentWealth + FactionWealth + OrderEscrow;
}

/// <summary>One epoch's macro snapshot — a pure function of the state at
/// capture (levels and counts only; per-epoch deltas are the reader's
/// derivative, so the row never needs cross-step memory).</summary>
public sealed record MetricRow(
    int Epoch, int WorldYear, MoneyRow Money,
    int LivePolities, int NegativeTreasuries,
    double MinPolityCredits, double MedianPolityCredits,
    double MaxPolityCredits,
    double Population, double MeanSoL);

/// <summary>One entered polity's narrow per-epoch row — the distribution
/// behind the galaxy medians ("who is negative, since when").</summary>
public sealed record PolityRow(
    int Epoch, int ActorId, double Credits, double Pools,
    double Population, double MeanSoL, double Legitimacy);

/// <summary>The sim-health probe (sim-health spec §1): pure read-only
/// aggregation over the registries — fixed iteration order, no mutation,
/// no hash rolls, nothing persisted. KnobRegistry configures the sim;
/// this observes it.</summary>
public static class MetricsOps
{
    /// <summary>The narrow fast row — credit totals by holder class.</summary>
    public static MoneyRow Money(SimState state, string phase)
    {
        double polity = 0, pools = 0;
        foreach (var pr in state.Polities)
        {
            polity += pr.Credits;
            pools += pr.ExpansionPoints + pr.DevelopmentPoints
                + pr.MilitaryPoints + pr.ReservePoints;
        }
        double corp = 0;
        foreach (var c in state.Corporations) corp += c.Credits;
        double segment = 0;
        foreach (var s in state.Segments) segment += s.Wealth;
        double faction = 0;
        foreach (var f in state.Factions) faction += f.Wealth;
        double escrow = 0;
        foreach (var o in state.Orders) escrow += o.EscrowCredits;
        double principal = 0;
        foreach (var l in state.Loans)
            if (!l.Closed) principal += l.Principal;
        return new MoneyRow(state.EpochIndex, phase, polity, pools, corp,
            segment, faction, escrow, principal);
    }

    /// <summary>The full macro row — one per epoch, after Chronicle.</summary>
    public static MetricRow Snapshot(SimState state)
    {
        var credits = new List<double>();
        int negative = 0;
        double min = 0, max = 0;
        foreach (var pr in state.Polities)
        {
            if (!state.Actors[pr.ActorId].Entered
                || state.Actors[pr.ActorId].Retired) continue;
            if (pr.Credits < 0) negative++;
            if (credits.Count == 0) { min = pr.Credits; max = pr.Credits; }
            else
            {
                if (pr.Credits < min) min = pr.Credits;
                if (pr.Credits > max) max = pr.Credits;
            }
            credits.Add(pr.Credits);
        }
        credits.Sort();
        double median = credits.Count == 0 ? 0.0
            : credits.Count % 2 == 1 ? credits[credits.Count / 2]
            : (credits[credits.Count / 2 - 1] + credits[credits.Count / 2]) / 2.0;

        double pop = 0, sol = 0;
        foreach (var s in state.Segments)
        {
            pop += s.Size;
            sol += s.SoL * s.Size;
        }
        return new MetricRow(state.EpochIndex, state.WorldYear,
            Money(state, "epoch"),
            credits.Count, negative, min, median, max,
            pop, pop <= 0 ? 0.0 : sol / pop);
    }

    /// <summary>Per-entered-polity narrow rows, actor-id order (P6).</summary>
    public static List<PolityRow> PolityRows(SimState state)
    {
        var rows = new List<PolityRow>();
        foreach (var pr in state.Polities)
        {
            if (!state.Actors[pr.ActorId].Entered
                || state.Actors[pr.ActorId].Retired) continue;
            double pop = 0, sol = 0;
            foreach (var s in state.Segments)
            {
                if (state.Ports[s.PortId].OwnerActorId != pr.ActorId) continue;
                pop += s.Size;
                sol += s.SoL * s.Size;
            }
            rows.Add(new PolityRow(state.EpochIndex, pr.ActorId,
                pr.Credits,
                pr.ExpansionPoints + pr.DevelopmentPoints
                    + pr.MilitaryPoints + pr.ReservePoints,
                pop, pop <= 0 ? 0.0 : sol / pop,
                pr.Interior?.Legitimacy ?? 0.0));
        }
        return rows;
    }
}
