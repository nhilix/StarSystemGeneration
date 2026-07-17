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
/// reactivity is scaled by <see cref="EconomyKnobs.FxSensitivity"/>.
///
/// A bank's <b>unbacked claim book</b> — sovereign debt it holds beyond its hard
/// <see cref="Bank.Reserve"/> — weighs on its currency exactly like excess supply
/// (same units, same direction), so it enters the density as supply-equivalent
/// money, scaled by <see cref="EconomyKnobs.FxBackingSensitivity"/> (slice BF
/// design §5). This is what makes reserve depth <i>mean</i> something: a bank
/// whose claim dwarfs its reserve watches its own currency slide — endogenous
/// discipline, no gate on lending. <c>unbacked</c> is clamped at 0, so a
/// fully-backed bank is unaffected, and <see cref="Bank.Reserve"/> now offsets
/// the claim <i>directly</i> on top of its existing sequestration effect:
///
///     unbacked = max(0, ClaimOnState - Reserve)
///     effectiveMoney = Supply + FxBackingSensitivity * unbacked
///     density = effectiveMoney / max(Receipts, FxReceiptsFloor)
///     NumeraireRate = 1 / (1 + FxSensitivity * density)
///
/// The form is strictly positive and strictly decreasing in density, so it never
/// yields a zero or negative rate that would break <see cref="SimState.ConvertCurrency"/>.
/// A currency with no supply sits at 1.0 (matching a newly founded currency's
/// starting rate); FxSensitivity = 0 pins every rate at 1.0 (no reactivity).
/// FxBackingSensitivity = 0 (the default) drops the backing term entirely —
/// <c>effectiveMoney = Supply</c> exactly, byte-identical to CU-2 (design §5).</summary>
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
            // Unbacked sovereign debt is supply-equivalent money: it weighs on
            // the rate exactly like excess Supply, offset directly by the hard
            // reserve and clamped at 0 so a fully-backed bank is unaffected
            // (slice BF design §5). At FxBackingSensitivity = 0 (the default)
            // this term is exactly 0, so effectiveMoney == Supply — byte-
            // identical to CU-2. Every currency, Retired ones included, has a
            // bank founded 1:1 at FoundCurrency, so BankOf is safe registry-wide.
            var bank = state.BankOf(cur.Id);
            double unbacked = Math.Max(0.0, bank.ClaimOnState - bank.Reserve);
            double effectiveMoney = Math.Max(0.0, cur.Supply)
                                    + eco.FxBackingSensitivity * unbacked;
            double output = Math.Max(receipts, eco.FxReceiptsFloor);
            double density = effectiveMoney / output;
            cur.NumeraireRate = 1.0 / (1.0 + eco.FxSensitivity * density);
        }

        // Rates moved: every corporation's cached numeraire wallet total is now
        // stale against the new table and must be recomputed (handoff from the
        // data-model task). Id order keeps the refresh byte-identical run to run.
        foreach (var corp in state.Corporations)              // id order (P6)
            corp.RefreshNumeraire(state);
    }
}
