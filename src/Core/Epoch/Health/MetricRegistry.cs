using System;
using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>One macro observation: dotted name, one-line doc, and an
/// accessor over a snapshot row.</summary>
public sealed record MetricDef(
    string Name, string Doc, Func<MetricRow, double> Get);

/// <summary>The single index of every sim-health metric — name-sorted,
/// fully documented, driving the sweep CSV columns, the REPL `ehealth`
/// command, and docs/SIMHEALTH.md (which carries the what-healthy-looks-like
/// prose). A metric must never exist outside this table; MetricRegistryTests
/// enforces order, uniqueness, docs, and accessor sanity. KnobRegistry's
/// observability twin (sim-health spec §1).</summary>
public static class MetricRegistry
{
    private static MetricDef M(string name, string doc,
                               Func<MetricRow, double> get) =>
        new(name, doc, get);

    private static readonly MetricDef[] Table =
    {
        // ---- Economy (nominal flows — the clock-invariance headline) ----
        M("Economy.CorpReceipts",
          "credits booked by corporations THIS epoch (a flow, zeroed each step)",
          r => r.CorpReceipts),
        M("Economy.GoodsTransacted",
          "goods UNITS actually cleared THIS epoch across every port (a flow, "
          + "zeroed each step) — real throughput, no currency in it, so it is "
          + "the honest twin of the gross Receipts flow",
          r => r.GoodsTransacted),
        M("Economy.GoodsValueCleared",
          "gross value of goods cleared THIS epoch (a flow, zeroed each step) — "
          + "NOMINAL and summed across each port's own currency, so it carries "
          + "the MoneyRow non-commensurability trap; trustworthy in a "
          + "single-currency world, else read Economy.GoodsTransacted",
          r => r.GoodsValueCleared),
        M("Economy.PolityReceipts",
          "credits booked by polity treasuries THIS epoch (a flow, zeroed each "
          + "step) — integrate the column for a run's Σ receipts",
          r => r.PolityReceipts),

        // ---- Extraction (body resource stocks — locality) ----
        M("Extraction.BodyStockRemaining",
          "total remaining depletable body-resource stock (falls as bodies deplete)",
          r => r.BodyStockRemaining),

        // ---- Money (the holder classes — conserved credit stores) ----
        M("Money.ConservationResidual",
          "worst per-currency supply delta minus known mints and net of "
          + "conversions — nonzero means an unknown leak in some currency",
          r => r.ConservationResidual),
        M("Money.CorpCredits", "corporation treasuries summed",
          r => r.Money.CorpCredits),
        M("Money.CourierEscrow",
          "courier fees in flight (post → delivery/refund)",
          r => r.Money.CourierEscrow),
        M("Money.CumulativeFiatIssued",
          "running total minted by bounded sovereign issuance (second mint)",
          r => r.CumulativeFiatIssued),
        M("Money.CumulativeSteadyIssuance",
          "running total minted by the always-on steady issuance channel (third mint)",
          r => r.CumulativeSteadyIssuance),
        M("Money.ExpeditionPurses",
          "founding purses aboard in-flight colony expeditions",
          r => r.Money.ExpeditionPurses),
        M("Money.FactionWealth", "faction war chests summed",
          r => r.Money.FactionWealth),
        M("Money.LoanPrincipal",
          "outstanding principal across open loans (a claim, not a holder)",
          r => r.Money.LoanPrincipal),
        M("Money.OrderEscrow", "credits held by resting buy orders",
          r => r.Money.OrderEscrow),
        M("Money.PolityCredits",
          "polity treasuries summed (negative = deficit financing)",
          r => r.Money.PolityCredits),
        M("Money.PolityPools",
          "polity investment pools summed (expansion+development+military+reserve)",
          r => r.Money.PolityPools),
        M("Money.SegmentWealth", "household (segment) wealth summed",
          r => r.Money.SegmentWealth),
        M("Money.Supply", "the money supply: every holder class summed",
          r => r.Money.Supply),

        // ---- Polity (the treasury distribution) ----
        M("Polity.Emerged",
          "cumulative endowed entries (the PolityEmerged chronicle count)",
          r => r.EndowedEntries),
        M("Polity.Live", "entered, unretired polities",
          r => r.LivePolities),
        M("Polity.MaxCredits", "richest live polity's treasury",
          r => r.MaxPolityCredits),
        M("Polity.MedianCredits", "median live polity treasury",
          r => r.MedianPolityCredits),
        M("Polity.MinCredits", "poorest live polity's treasury",
          r => r.MinPolityCredits),
        M("Polity.NegativeTreasuries",
          "live polities with a negative credit balance",
          r => r.NegativeTreasuries),

        // ---- Segment (people) ----
        M("Segment.MeanSoL", "population-weighted mean standard of living",
          r => r.MeanSoL),
        M("Segment.Population", "total population across segments",
          r => r.Population),

        // ---- Settlement (locality — frozen hex-tier state) ----
        M("Settlement.Ports",
          "ports in the galaxy (the real economy's extent — colonies land here)",
          r => r.Ports),
        M("Settlement.SettledHexes",
          "committed hexes with a frozen system (grows with visitation, no eviction)",
          r => r.SettledHexes),
    };

    public static IReadOnlyList<MetricDef> All => Table;

    /// <summary>The metric behind a name, or null (the sweep and REPL
    /// resolve through this — unknown names refuse, like knobs).</summary>
    public static MetricDef? Find(string name)
    {
        foreach (var m in Table)
            if (string.Equals(m.Name, name, StringComparison.Ordinal))
                return m;
        return null;
    }
}
