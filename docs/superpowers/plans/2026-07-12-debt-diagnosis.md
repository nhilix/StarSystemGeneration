# The treasury spiral — diagnosis (slice SH, 2026-07-12)

The first finding of the sim-health harness (HANDOFF flag 1: all 15
seed-42 polities negative at the CE merge, worst ≈ −402k). Evidence:
sweep `debt-diagnosis` — 8 seeds × 40 epochs × radius 21, baseline plus
three credit-relevant variants (32 histories; output in
`runs/sweeps/debt-diagnosis`, byte-reproducible from the committed
experiment `2026-07-12-debt-diagnosis-experiment.json` beside this doc).

## Findings

**1. The spiral is universal — a mechanism, not seed personality.**
Every seed ends with essentially its whole roster negative (54/54, 50/50,
57/57, 54/54, 72/72, 62/62, 68/68, 56/56 at baseline; the counts exceed
the entered polities because graduations keep minting states — every new
state joins the spiral). Median first-negative epoch 18–23 at radius 21;
small dense galaxies (radius 8) go under as early as epoch 1.

**2. Money is conserved; the treasuries drain into pools and households.**
Max |conservation residual| across all 32 histories: 1.3e-8 (double
noise). Nothing leaks. At year 1000 (baseline, per seed): treasuries sum
to −2.9M…−7.1M while the polities' own **investment pools hold
+1.1M…+2.9M unspent** and **household wealth holds +1.7M…+4.1M**. The
supply the endowments minted sits almost entirely in pools and household
wealth; the treasury line finances all of it by unbounded deficit.

**3. Allocation is the drain — quantitatively.**
Ensemble treasury motion by phase (Δ polity credits summed over all
baseline histories): **Allocation −32.3M** · Markets −5.6M ·
Resolution −0.4M · Interior +0.5M · Perception/Intent/Chronicle 0.
The mechanism (`AllocationPhase`, Phases.cs:369):

```
allocatable = max(0, max(Credits, Receipts))
pools      += allocatable × Σ(budget shares)
Credits    -= allocatable × Σ(...)  (+ appeasement + research)
```

A polity with a positive balance budgets its **entire treasury** every
epoch; one with a negative balance still budgets its full receipts. The
treasury can never accumulate: it spends max(what it has, what it earned)
by construction, while only ~10% of downstream market flow returns as
tax. A one-way ratchet into deficit — deficit financing was the intent,
but nothing ever finances the deficit (see 4), and nothing ever bounds it.

**4. The credit market dies at epoch 1–4 and never recovers.**
`Phases.Borrow` needs a lender holding 2× the principal (principal itself
1.2× the hole). Last epoch any seed had a qualifying lender: e1 (most
seeds), eNone/e1–e4 across variants. After that, zero loans for the rest
of history in every history. Corollary — **`Economy.LoanRatePerYear` is a
dead knob**: the cheap-credit variant (0.005 vs 0.02) is **byte-identical
to baseline** in all 8 seeds. Interest never prices anything because
loans never exist.

**5. It is structural, not liquidity.** flush-start
(`InitialCreditsPerPolity` 5000, 10×) delays the median first-negative
epoch by ~3 and keeps a lender alive until e3–4 — then the identical
spiral. lean-labor (`LaborShare` 0.2) makes households poorer
(wealth −45%) and pools richer, flips the Markets phase positive for
treasuries (+4.6M), and the roster STILL ends all-negative — Allocation
just drains harder (−37.8M). No calibration of these dials exits the
spiral; the exit is mechanical.

**6. Collateral damage already visible.**
`Corporate.NationalizeWealthFactor` never triggers against indebted
hosts (documented in TUNING.md); end-of-history mean SoL sits at
0.32–0.45 (mediocre-poor everywhere); and every "richest polity"
readout is negative, so any mechanic gated on treasury surplus
(reserves, lending, seizure) is dead galaxy-wide.

## What the monetary slice should weigh (inputs, not decisions)

- **Bound the allocation base**: budget from receipts (or a capped
  multiple of them), never from the raw balance; or bound the deficit
  (allocation shrinks as debt/income grows — an austerity curve).
- **Drain the pools**: points accrue ~2× faster than the planner spends
  them; unspent pools should decay back to the treasury (or the planner
  should bid them down) instead of parking a third of the money supply.
- **Recirculate household wealth**: consumption only partially returns
  to state-taxable flows; wealth accumulates unboundedly (no wealth tax,
  no luxury drain scaled to stock).
- **A credit mechanism that can exist in equilibrium**: the 2×-lender
  gate assumes a surplus economy that the allocation rule makes
  impossible; corp/segment lenders, bond issuance against future
  receipts, or a softer gate are all candidates once the drain is fixed.
- Re-run this exact sweep after the fix: acceptance is
  `Polity.NegativeTreasuries` breathing (rising and *recovering*) across
  the ensemble, a live loan market past epoch 4, and
  `Money.LoanPrincipal` nonzero somewhere in the second half of history.

## Method note

Every number above is one `sweep` + one script
(`analyze_debt.py`, archived in the sweep dir) over the CSVs. The
dashboard renders the same series; the eyeball gate is the dashboard.
