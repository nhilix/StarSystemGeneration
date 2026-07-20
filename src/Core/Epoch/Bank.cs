namespace StarGen.Core.Epoch;

/// <summary>One Currency's central bank — a plain state record (NOT an actor,
/// no controller, not an <see cref="ICreditLedger"/>): founded 1:1 alongside
/// its <see cref="Currency"/> at <see cref="SimState.FoundCurrency"/>, the
/// same single chokepoint every currency mints through, and keyed by currency
/// id in the dense-parallel <see cref="SimState.Banks"/> registry (slice CU-2
/// bank-actor design). This task only declares the record; reserve dynamics
/// (spread intake, reserve funding) are wired by later tasks — for now
/// <see cref="Reserve"/> starts and stays at 0 for every newly founded bank.</summary>
public sealed class Bank
{
    public int CurrencyId { get; }
    public double Reserve { get; set; }
    /// <summary>Running total of spread income the bank has ever taken in —
    /// a level across the whole sim, never reset per epoch (later task).</summary>
    public double CumulativeSpreadIntake { get; set; }
    /// <summary>Running total the bank has ever funded out of its reserve —
    /// a level across the whole sim, never reset per epoch (later task).</summary>
    public double CumulativeReserveFunded { get; set; }
    /// <summary>The bank's outstanding claim against its own polity (slice BF
    /// bank-flow design) — <b>NOT money</b>. Mirrors the reasoning already
    /// documented at <see cref="MoneyRow.LoanPrincipal"/>: "LoanPrincipal
    /// rides beside the classes as a claim, not a holder: loans move credits
    /// between ledgers, the principal is memory." <see cref="ClaimOnState"/>
    /// never enters <see cref="Currency.Supply"/>, is never walked by
    /// <see cref="SupplyOps"/>, and never appears on the conservation
    /// residual's balance side. Grown by <see cref="LendToState"/>; shrunk by
    /// the servicing pass (later task).</summary>
    public double ClaimOnState { get; set; }
    /// <summary>Running total ever lent to the polity's state — a level
    /// across the whole sim, never reset per epoch. Unlike
    /// <see cref="ClaimOnState"/>, repayment never draws it down.</summary>
    public double CumulativeLentToState { get; set; }
    /// <summary>Running total of claim principal ever destroyed on
    /// repayment — a level across the whole sim, never reset per epoch
    /// (later task). The bank-side mirror of <see cref="Currency.CumulativeFiatRetired"/>.</summary>
    public double CumulativeRetired { get; set; }

    public Bank(int currencyId)
    {
        CurrencyId = currencyId;
    }

    /// <summary>The bounded-share sibling of BF's <c>unbacked</c> FX signal
    /// (<c>FxOps.cs</c>: <c>unbacked = max(0, ClaimOnState − Reserve)</c>) —
    /// where that measures un-backing as unbounded supply-equivalent money,
    /// this is a bounded [0,1] share of how much of the claim book is
    /// reserve-backed (slice CU-4 monetary-federation design §2). 1.0 is a
    /// pure saver (<see cref="ClaimOnState"/> zero); 0.0 is a pure debtor
    /// (<see cref="Reserve"/> zero); 0.5 is exactly backed. A fresh bank
    /// (both zero) guards to 0.0 — credibility is accumulated, not granted.
    /// A pure computed property: no state, no allocation.</summary>
    public double BackedShare =>
        (Reserve + ClaimOnState) <= 0 ? 0.0
                                      : Reserve / (Reserve + ClaimOnState);

    /// <summary>Book this bank as the creditor of a sovereign mint (slice BF
    /// bank-flow design §3) — the CLAIM half only. The money creation itself
    /// stays where it has always been, at the
    /// <c>AllocationPhase.IssueSovereignCredit</c> chokepoint: same trigger,
    /// same <see cref="EconomyKnobs.SovereignIssuanceRate"/> cap, same
    /// magnitude, same <see cref="Currency.CumulativeFiatIssued"/> counter.
    /// What this adds is a creditor beside it, moving the bank from ~0.1% of
    /// deficit funding (the FX spread alone) to ~100% without touching a single
    /// receipt site (design §1). Deliberately NOT gated on
    /// <see cref="Reserve"/>: the backstop is absolute and a polity is never cut
    /// off for want of reserve — the monetary-equilibrium slice's debt-spiral
    /// cure rests on that lender-of-last-resort floor, and a reserve gate
    /// re-lights precisely the spiral ME was built to cure (design §3, §5).
    /// Backing discipline is endogenous instead, via the FX coupling (§5).
    /// <paramref name="amount"/> is the amount minted; the caller has already
    /// established it is positive.</summary>
    public void LendToState(double amount)
    {
        ClaimOnState += amount;
        CumulativeLentToState += amount;
    }
}
