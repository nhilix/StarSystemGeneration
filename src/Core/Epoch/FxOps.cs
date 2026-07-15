using System;
using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>The FX-rate pass (currency-and-FX design, slice CU-1). Once per
/// epoch, at the very start — before Markets, Borrow, ServiceLoans, PayTribute,
/// and every conversion — it recomputes every <see cref="Currency.NumeraireRate"/>
/// from the state left at the END of the prior epoch, so the whole epoch about
/// to run converts money against one frozen rate table with no phase-ordering
/// ambiguity (design, "FX rate").
///
/// The rate is a quantity-theory money-per-output density: a currency carrying
/// more <see cref="Currency.Supply"/> relative to its issuing polity's real
/// output (<see cref="PolityRecord.Receipts"/>) is weaker. Receipts are floored
/// by <see cref="EconomyKnobs.FxReceiptsFloor"/> so a freshly split polity with
/// near-zero receipts neither divides by zero nor blows its rate up, and the
/// reactivity is scaled by <see cref="EconomyKnobs.FxSensitivity"/>:
///
///     density = Supply / max(Receipts, FxReceiptsFloor)
///     NumeraireRate = 1 / (1 + FxSensitivity * density)
///
/// The form is strictly positive and strictly decreasing in density, so it never
/// yields a zero or negative rate that would break <see cref="SimState.ConvertCurrency"/>.
/// A currency with no supply sits at 1.0 (matching a newly founded currency's
/// starting rate); FxSensitivity = 0 pins every rate at 1.0 (no reactivity).</summary>
public static class FxOps
{
    /// <summary>Recompute every currency's numeraire rate, then refresh each
    /// corporation's cached wallet total against the new table (its
    /// numeraire-denominated <see cref="Corporation.Credits"/> would otherwise go
    /// stale). Deterministic: currencies walk in registry (id) order, polity
    /// receipts resolve through a single scan, corporations refresh in id order —
    /// no hash rolls, no floating iteration order (design "Conservation &amp;
    /// determinism": the FX rate is a pure formula). A no-op before genesis wires
    /// real currencies (empty registry), by construction.</summary>
    public static void RecomputeRates(SimState state)
    {
        var eco = state.Config.Economy;

        // The issuing polity's real output, keyed by the currency it currently
        // mints. A living polity's CurrencyId is the authoritative "who mints
        // this now" link — equal to Currency.FoundingPolityId under the genesis
        // 1:1 rule, but robust when a founder has been absorbed (its currency
        // lingers Retired with dangling holdings and simply finds no receipts
        // here, floored to the minimum). One scan, so it stays deterministic.
        var receiptsByCurrency = new Dictionary<int, double>();
        foreach (var pr in state.Polities)                    // actor-id order (P6)
        {
            if (pr.CurrencyId < 0) continue;
            receiptsByCurrency.TryGetValue(pr.CurrencyId, out double sum);
            receiptsByCurrency[pr.CurrencyId] = sum + pr.Receipts;
        }

        foreach (var cur in state.Currencies)                 // id order (P6)
        {
            receiptsByCurrency.TryGetValue(cur.Id, out double receipts);
            double output = Math.Max(receipts, eco.FxReceiptsFloor);
            double density = Math.Max(0.0, cur.Supply) / output;
            cur.NumeraireRate = 1.0 / (1.0 + eco.FxSensitivity * density);
        }

        // Rates moved: every corporation's cached numeraire wallet total is now
        // stale against the new table and must be recomputed (handoff from the
        // data-model task). Id order keeps the refresh byte-identical run to run.
        foreach (var corp in state.Corporations)              // id order (P6)
            corp.RefreshNumeraire(state);
    }
}
