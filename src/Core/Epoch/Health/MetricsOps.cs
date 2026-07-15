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
    double CourierEscrow, double ExpeditionPurses,
    double LoanPrincipal)
{
    /// <summary>The money supply: every holder class summed.</summary>
    public double Supply => PolityCredits + PolityPools + CorpCredits
        + SegmentWealth + FactionWealth + OrderEscrow
        + CourierEscrow + ExpeditionPurses;
}

/// <summary>One epoch's macro snapshot — a pure function of the state at
/// capture (levels and counts only; per-epoch deltas are the reader's
/// derivative, so the row never needs cross-step memory).</summary>
public sealed record MetricRow(
    int Epoch, int WorldYear, MoneyRow Money,
    int LivePolities, int NegativeTreasuries,
    double MinPolityCredits, double MedianPolityCredits,
    double MaxPolityCredits,
    double Population, double MeanSoL,
    int EndowedEntries, double ConservationResidual,
    double CumulativeFiatIssued, double CumulativeSteadyIssuance,
    int SettledHexes);

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
        // couriers hold their fee from post to delivery/refund; live-only
        // registry, but a retired record's escrow is already paid out
        double courier = 0;
        foreach (var c in state.Couriers)
            if (c.Status == CourierStatus.Open
                || c.Status == CourierStatus.InTransit)
                courier += c.FeeEscrow;
        // an expedition carries its founding purse from the act's
        // expansion-point charge to the settlers' pockets at landfall —
        // a cancelled expedition's purse is lost with the convoy (a
        // DESIGNED sink the residual will print; SIMHEALTH.md carries it)
        double purses = 0;
        foreach (var p in state.Projects)
            if (p.Kind == ProjectKind.ColonyExpedition && p.InFlight)
                purses += state.Config.Expansion.ColonyCost;
        double principal = 0;
        foreach (var l in state.Loans)
            if (!l.Closed) principal += l.Principal;
        return new MoneyRow(state.EpochIndex, phase, polity, pools, corp,
            segment, faction, escrow, courier, purses, principal);
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

        // conservation (spec §2, widened by monetary-equilibrium design §5 and
        // Part B): there are now THREE declared mints — the one-time entry
        // endowment, reactive/backstop sovereign issuance (CumulativeFiatIssued),
        // and the always-on steady issuance channel (CumulativeSteadyIssuance) —
        // so the supply delta minus the epoch's endowments AND both issuance
        // deltas must be zero. All are diffed as levels against the previous row;
        // every other new flow (pool decay, wealth levy) is a symmetric transfer
        // between existing holders, not a mint. A fresh series (a loaded artifact)
        // has no baseline: its first residual is defined 0.
        int endowed = 0;
        foreach (var e in state.Log.Events)
            if (e.Type == WorldEventType.PolityEmerged) endowed++;
        var money = Money(state, "epoch");
        double residual = 0;
        if (state.Health.Rows.Count > 0)
        {
            var prev = state.Health.Rows[state.Health.Rows.Count - 1];
            double endowment = state.Config.Economy.InitialCreditsPerPolity
                + state.Config.Expansion.HomeworldSegmentSize
                  * state.Config.Economy.InitialWealthPerPop;
            residual = money.Supply - prev.Money.Supply
                - (endowed - prev.EndowedEntries) * endowment
                - (state.CumulativeFiatIssued - prev.CumulativeFiatIssued)
                - (state.CumulativeSteadyIssuance - prev.CumulativeSteadyIssuance);
        }

        return new MetricRow(state.EpochIndex, state.WorldYear, money,
            credits.Count, negative, min, median, max,
            pop, pop <= 0 ? 0.0 : sol / pop,
            endowed, residual, state.CumulativeFiatIssued,
            state.CumulativeSteadyIssuance, state.SettledSystems.Count);
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
