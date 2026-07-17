# Slice MC kickoff — P7-clean nominal price formation

You are opening a slice that was **discovered, not planned**. Slice BF's
clock-invariance probe surfaced a structural P7 violation in the market engine;
BF is parked behind it by user decision. This slice's job: make nominal price
formation clock-invariant, so that a 25-year step produces what twenty-five
1-year steps produce.

This is a **market-engine design slice with economy-wide blast radius**, not an
arithmetic patch. Read first, brainstorm, do NOT skip to a fix — the obvious fix
has already been tried and measured, and it does not work (see below).

## Read first, in order

1. `docs/superpowers/specs/2026-07-16-market-clock-dependence-investigation.md`
   — **the whole thing.** This is a completed root-cause investigation with
   instrumented evidence, a multi-seed sweep, a control seed, and two refuted
   hypotheses. It is the reason this slice exists. Do not re-derive it; do not
   re-run the bisection. Findings 5 (the located defect), 6 (the tested-and-
   rejected fix), and 7 (why the LOLR floor is not in tension) are load-bearing.
2. `docs/HANDOFF.md` §"Slice BF — the bank as monetary authority (PARKED)".
3. `docs/superpowers/plans/2026-07-16-slice-bf-ledger.md` — the parked ledger,
   for what BF resumes into and what it needs from you.
4. Code — the real surface:
   - `src/Core/Epoch/MarketEngine.cs:1130–1167` `DriftReferencePrices` — the
     defect itself.
   - `Phases.cs:632–643` `DecayIdlePools` — the P7 precedent whose shape does
     NOT transfer here (Finding 6 explains why: it acts on a stock it alone
     depletes, with no cross-actor equilibrium; price formation has neither
     property).
   - `tests/Core.Tests/.../FineTickTests.cs` — `FineTick_ProjectCompletions_
     LandOnWorldYears_NotSteps`, currently red on the BF branch, plus a
     **docstring that is now known to be wrong** (see "Correct the record").

## The defect, in one block

```
demand = (unfilledBids + SignalDemand) / StepFraction   // FLOW, normalized to a generation
supply = unsoldAsks                                     // STOCK, raw, NOT normalized
factor = (demand/supply)^PriceDriftExponent             // applied ONCE PER STEP
factor = clamp(factor, 1/cap, cap)                      // cap = exp(PriceDriftMaxPerYear × years)
```

Two defects compose:

1. **Stock/flow normalization mismatch.** Demand is a flow normalized to a
   generation; supply is the raw resting-ask stock, whose size scales with step
   length (measured: 165 at 25y vs 32 at 1y for good 0). The ratio fed to the
   drift is therefore not span-invariant.
2. **Clamp saturation in opposite directions.** The clamp is itself P7-clean
   (`exp(0.04×1)^25 == exp(0.04×25)` exactly) — but both clocks sit saturated
   against it in **opposite** directions. Coarse dumps 25y of supply into one
   clearing → permanent glut → prices pinned (`max = 1.000` across all markets).
   Fine trickles → frequent stock-outs (`supply = 0` → `demand/eps` ≈ 1e9 →
   clamped to `cap` every step) → prices at the ceiling (`max = 59–100`).

The two clocks are not sampling one economy at two resolutions. They are in
**different price regimes** — one structurally glutted, one structurally starved.
Nominal receipts follow (68× divergence per world-year), and every monetary
channel follows receipts. The real economy diverges only 2–3× (pop 13→14, ports
6→14), which is the tell: this is nominal, not real.

## The open design questions — weigh them in the brainstorm

- **Flow-vs-flow, or a normalized ask window?** Finding 6 names two candidate
  directions: make the drift read a *cleared-vs-posted over the step* ratio
  (flow vs flow), or normalize the ask stock to a comparable window. Neither is
  designed. Weigh them; there may be better.
- **What does supply batching mean at 25y?** The deeper question under the
  defect: a 25-year step landing 25 years of production into one clearing may be
  wrong *modelling*, not just wrong arithmetic. Is the fix in the drift formula,
  or in how asks rest/expire across a step?
- **The clamp.** It is P7-clean in isolation but is where both regimes get
  pinned. Does a correct ratio make saturation rare, or does the clamp itself
  need rethinking?
- **Stock-outs.** `supply = 0 → demand/eps ≈ 1e9` is its own pathology
  independent of clock. Worth fixing here or separately?

## What is already TESTED and REFUTED — do not re-tread

- **"The issuance cap is applied per epoch."** Refuted: at 25y the cap **never
  binds — not once** (`capBound = 0`). BF design §3's freeze of ME's cap is
  correct and **stands**; do not touch the cap.
- **"Mint feedback compounds."** Refuted: zeroing *both* mints structurally
  removes the loop, and receipts still diverge 16×.
- **The naive P7 fix** (drop `/StepFraction`, compound `factor^years`) — the
  formula that is *correct on paper*. Implemented and measured: divergence
  improves 16× → 5× but **does not converge and inverts** (fine undershoots;
  fiat 1512/1241/**114**), and the fine side still hits the ceiling. The reason
  is structural: `unsoldAsks` is step-length-dependent, so no reformulation *of
  the drift alone* makes its input span-invariant.

## Boundary — NOT this session

- **Slice BF** stays parked. Do not resume it, do not touch `Bank`/servicing/
  claims. Your deliverable unblocks it; BF's own session resumes at its task 6.
- **The LOLR floor / ME's cap.** Finding 7: the fix is upstream and
  monetary-policy-agnostic, so it needs no touch to cap, floor, reserve, or
  trigger. The backstop stays absolute. If your design finds itself reaching for
  the cap, re-read Finding 1.

## Correct the record (part of this slice)

`FineTickTests`' docstring asserts *"confirmed: no per-tick-vs-per-year formula
defect ... legitimate economy-trajectory drift"* — and its band has been widened
**three times (0.6→0.85)** absorbing this exact defect under that explanation.
Finding 5 contradicts it directly. There *is* a per-tick-vs-per-year formula
defect. Correct the docstring, and re-tighten the bands to what the fixed engine
actually supports rather than leaving them sized for the bug.

## Traps

- **The blast radius is the whole economy.** Every price, every price-sensitive
  golden, the FineTick bands, and ME's monetary operating point. Budget for it.
- **The converse risk, flagged by the investigation (Finding 7):** a fix that
  raises coarse prices to fine levels raises coarse `Receipts` ~16×, hence ME's
  issuance cap ~16× — a large, real change to the monetary regime's operating
  point. **That** is what needs ensemble validation per the slice-SH bar. Do not
  let a green clock test hide a moved operating point.
- **Multi-seed or it isn't real.** The investigation swept seeds 42/7/99/2024
  (16×/24×/60×/23×) and used seed 13 (a dead world, 1.0×) as a control. Any
  convergence claim needs the same treatment — a single seed proves nothing here.
  The 32-run committed sweep
  (`docs/superpowers/plans/2026-07-12-debt-diagnosis-experiment.json`) is the
  instrument.
- **`StepFraction`, `PriceDriftExponent`, `PriceDriftMaxPerYear`** are the live
  knobs. Register anything new in `KnobRegistry.cs` — an unregistered knob
  silently reverts on reload, breaking determinism AND blocking the sweep.
- Slice-session workflow (scope nod · REPL eyeball · merge decision; task ledger;
  subagent-driven-development; one whole-branch fable review + fix wave;
  kickoff-prompt chaining). `git log main` before merge-out — L2 and BF are both
  in flight in their own worktrees.
- **Subagent dispatch note:** in the BF session, subagents dispatched to the
  background silently did nothing. Dispatch them synchronously
  (`run_in_background: false`) and verify with `git log` rather than trusting the
  spawn message.
