using System;
using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>Credit totals by holder class at one instant (sim-health spec
/// §1) — the money supply decomposed into every conserved store it can sit
/// in. Captured after every phase, it attributes treasury motion to the
/// phase that moved it. Every column except <c>CorpCredits</c> is a raw
/// NOMINAL sum in each holder's own native currency — a polity mints its
/// own currency (<see cref="Currency"/>), and Segment/Faction wealth and
/// escrow resolve to their owning polity's currency at point of use
/// (<see cref="SupplyOps.WalkNative"/>), so PolityCredits, PolityPools,
/// SegmentWealth, FactionWealth, OrderEscrow, CourierEscrow,
/// ExpeditionPurses, and LoanPrincipal each add together amounts that may
/// be denominated in different currencies: informative as a trend line,
/// but NOT commensurable across currencies and NOT an invariant (a
/// legitimate swing in one weak currency's nominal magnitude moves
/// LoanPrincipal/SegmentWealth with no real leak — this nearly produced a
/// false lead in a real investigation). Only CorpCredits is
/// numeraire-converted (<see cref="Corporation.Credits"/>) and meaningful
/// as a single galaxy-wide number; the real per-currency invariant is
/// <see cref="MetricRow.ConservationResidual"/>, not this row. LoanPrincipal
/// rides beside the classes as a claim, not a holder: loans move credits
/// between ledgers, the principal is memory.</summary>
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

/// <summary>One currency's ending state and conservation residual at an epoch
/// (currency-and-FX design, slice CU-1 task 9): its walked <see cref="Currency.
/// Supply"/> and the four cumulative counters the residual nets — sovereign and
/// steady mints, plus the paired converted-in/out transfer tallies. The
/// residual is that currency's own supply delta minus its mints and net of its
/// conversions; nonzero means a per-currency leak. Defined 0 on a currency's
/// first observed epoch (no prior baseline — the same convention the whole-sim
/// first row uses), which also absorbs the one-time founding endowment/seed.
/// <see cref="Reserve"/> is the currency's <see cref="Bank.Reserve"/> — money
/// SEQUESTERED out of the walked (circulating-only) <see cref="Currency.Supply"/>
/// by conversion spread (slice CU-2 bank-actor); the residual balances against
/// <c>Supply + Reserve</c> so a nonzero reserve is not read as a leak, and the
/// baseline row carries it so the delta stays correct.</summary>
public sealed record CurrencyResidualRow(
    int CurrencyId, double Supply,
    double CumulativeFiatIssued, double CumulativeSteadyIssuance,
    double CumulativeConvertedIn, double CumulativeConvertedOut,
    double Residual, double Reserve);

/// <summary>One epoch's macro snapshot — a pure function of the state at
/// capture (levels and counts only; per-epoch deltas are the reader's
/// derivative, so the row never needs cross-step memory).
/// <see cref="ConservationResidual"/> is the worst (max-absolute) per-currency
/// residual across <see cref="Currencies"/> — the galaxy roll-up of the real
/// invariant, which is now per-currency, not the single lump supply number.</summary>
public sealed record MetricRow(
    int Epoch, int WorldYear, MoneyRow Money,
    int LivePolities, int NegativeTreasuries,
    double MinPolityCredits, double MedianPolityCredits,
    double MaxPolityCredits,
    double Population, double MeanSoL,
    int EndowedEntries, double ConservationResidual,
    double CumulativeFiatIssued, double CumulativeSteadyIssuance,
    IReadOnlyList<CurrencyResidualRow> Currencies,
    int SettledHexes, double BodyStockRemaining);

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

        int endowed = 0;
        foreach (var e in state.Log.Events)
            if (e.Type == WorldEventType.PolityEmerged) endowed++;
        var money = Money(state, "epoch");

        // Per-currency conservation (currency-and-FX design, "Conservation &
        // determinism"): each Currency.Supply grows only through its own
        // declared mints — sovereign (CumulativeFiatIssued) and steady
        // (CumulativeSteadyIssuance) issuance into that currency — and every
        // cross-currency conversion is a TRANSFER, not a mint: it lowers the
        // source's supply (CumulativeConvertedOut) and raises the destination's
        // (CumulativeConvertedIn), so the residual nets those pairs out. All
        // counters diff as levels against the same currency's row last epoch.
        // A currency with no prior baseline (its first observed epoch) is
        // defined 0 — the same convention the whole-sim first row uses, which
        // also absorbs the one-time founding endowment (emergence) or seed
        // transfer (graduation/federation) that lands in that first epoch.
        // Requires SupplyOps.Recompute to have written Currency.Supply first
        // (the epoch engine runs it immediately before this snapshot).
        var prev = state.Health.Rows.Count > 0
            ? state.Health.Rows[state.Health.Rows.Count - 1] : null;
        var currencyRows = new List<CurrencyResidualRow>(state.Currencies.Count);
        double worstResidual = 0.0;
        foreach (var cur in state.Currencies)                 // id order (P6)
        {
            CurrencyResidualRow? baseline = null;
            if (prev != null)
                foreach (var row in prev.Currencies)
                    if (row.CurrencyId == cur.Id) { baseline = row; break; }

            // The bank reserve is money sequestered OUT of the walked Supply
            // (SupplyOps stays circulating-only), so the residual balances
            // against Supply + Reserve — the SAME bank SettleConversion writes.
            // Otherwise a nonzero reserve reads as a false leak.
            double reserve = state.BankOf(cur.Id).Reserve;
            double residual = 0.0;
            if (baseline != null)
                residual = (cur.Supply + reserve) - (baseline.Supply + baseline.Reserve)
                    - (cur.CumulativeFiatIssued - baseline.CumulativeFiatIssued)
                    - (cur.CumulativeSteadyIssuance - baseline.CumulativeSteadyIssuance)
                    - (cur.CumulativeConvertedIn - baseline.CumulativeConvertedIn)
                    + (cur.CumulativeConvertedOut - baseline.CumulativeConvertedOut);

            currencyRows.Add(new CurrencyResidualRow(cur.Id, cur.Supply,
                cur.CumulativeFiatIssued, cur.CumulativeSteadyIssuance,
                cur.CumulativeConvertedIn, cur.CumulativeConvertedOut, residual,
                reserve));
            double abs = residual < 0 ? -residual : residual;
            if (abs > worstResidual) worstResidual = abs;
        }

        double bodyStock = 0;
        foreach (var s in state.BodyResources.Values) bodyStock += s.Quantity;

        return new MetricRow(state.EpochIndex, state.WorldYear, money,
            credits.Count, negative, min, median, max,
            pop, pop <= 0 ? 0.0 : sol / pop,
            endowed, worstResidual, state.CumulativeFiatIssued,
            state.CumulativeSteadyIssuance, currencyRows,
            state.SettledSystems.Count, bodyStock);
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
