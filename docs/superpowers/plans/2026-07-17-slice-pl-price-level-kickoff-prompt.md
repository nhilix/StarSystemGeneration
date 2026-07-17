# Slice PL kickoff — the clock-dependent nominal price level

You are opening a slice that Slice MC **characterized but deliberately did not
fix**. MC made the sim's *real* economy clock-invariant (the polity-entry unit
bug). What survives is purely **nominal**: across clocks, the same goods change
hands at a wildly different price level. Read first, brainstorm, do NOT skip to a
fix — this problem has an unusually deep graveyard behind it (six diagnoses tried
across the MC slice, five refuted by instrumentation and one metric criticism
refuted).

## The one-line finding

On the committed clock-invariance instrument (20 seeds, mints-off, 250 world-years):

| quantity | median 25y→1y | frame |
|---|---|---|
| Σ receipts | 16.2× | nominal |
| Σ goods **value** | 16.6× | nominal |
| Σ goods **UNITS** | **1.5×** | **real** |
| ports | 3.3× | real |

**The same goods change hands ~1.5× more, at ~10.7× the value per unit.** Real
throughput is nearly clock-invariant (tighter than ports). The entire surviving
divergence is a **nominal price level** that depends on the clock. That is this
slice's target.

## Read first, in order

1. `docs/superpowers/plans/2026-07-17-slice-mc-ledger.md` — MC's full arc,
   including the "why this slice changed shape" table of five refuted diagnoses.
2. `docs/superpowers/specs/2026-07-17-mc-clock-invariance-instrument.md` — **your
   instrument.** Do not write a throwaway harness; a throwaway harness invalidated
   an entire MC investigation. Extend the committed one — the next investigator
   inherits whatever you add.
3. The MC residual measurement:
   `C:/…/scratchpad/bf/mc-residual-metric.md` if still present, else its
   conclusions are in the MC ledger log and the HANDOFF MC section.
4. Code:
   - `src/Core/Epoch/MarketEngine.cs` `DriftReferencePrices` (~:1132) — the
     price-drift pass. **Demoted, NOT cleared** (see the sharp lead below).
   - `src/Core/Epoch/OrderOps.cs` `SettleSale` — the single clearing chokepoint;
     where `GoodsValueCleared` (nominal) and `GoodsTransacted` (real units) are
     both booked. The realized clearing price lives here.
   - `src/Core/Epoch/Health/MetricRegistry.cs` — the two MC metrics
     (`Economy.GoodsTransacted`, `Economy.GoodsValueCleared`) you'll build on.

## THE sharp lead — re-measure demoted diagnosis #3

Price drift (`DriftReferencePrices`) was **demoted** during MC on a measurement
that it moves the mean price level only ~1.9× between clocks — too small to explain
12–25×. **But that was an *unweighted reference* price, measured pre-instrument.**
The thing that actually diverges ~10.7× is the **volume-weighted realized clearing
price** (value cleared ÷ units cleared) — a *different statistic*. #3's refutation
should NOT be treated as load-bearing until you re-measure with the right one.

First task, before any design: extend the instrument to report the volume-weighted
realized clearing price per clock (and ideally per good — the residual is "price OR
mix or both", and per-good columns separate them). Then ask: is the divergence in
the *reference* price (drift), the *realized* price (clearing/bid mechanics), or
the *basket mix* (which goods trade)? Only then design.

## The open design questions — weigh them in the brainstorm

- **Is a clock-dependent nominal price level even a defect?** A price is nominal;
  if real throughput is clock-invariant (it is), does it matter that prices sit at
  a different level at a different clock? It matters iff the price level feeds a
  real decision through a non-linear path (issuance caps read receipts; budgets
  read wealth; FX reads supply/output). Establish whether it actually bites before
  fixing it. "It's nominal, leave it" is a legitimate possible outcome.
- **If it bites: where?** Drift formula, clearing/bid mechanics, or basket mix.
  The MC residual note points at realized price, not reference price — follow the
  measurement.
- **The `DriftReferencePrices` stock/flow mismatch is still real on inspection**
  (demand normalized `/StepFraction`, supply a raw stock) even though it was only
  an ~11% term. Does fixing it help, or is it a red herring the way the budget cap
  was (a real defect that barely moves the outcome)? Measure before committing.

## What is REFUTED — do not re-tread (the graveyard)

All on the trusted instrument or by removal test:
1. Issuance cap per-epoch — never binds at 25y, not once.
2. Mint feedback compounds — zeroing both mints still leaves 16×.
3. `DriftReferencePrices` — demoted to ~11% on *reference* price (but re-measure
   on *realized* price — that's this slice's lead, above).
4. Band-bid budget cap — neutralizing it: 61.5× → 61.5×.
5. Receipts churn / "wrong metric" — churn multiple 1.02× against a clock ratio of
   25; it's a rigidly fixed fraction of trade value, double-counts nothing.
6. The polity-entry unit bug — **fixed by MC** (this is what made real throughput
   clock-invariant; it is why only the nominal residual is left).

## Boundary — NOT this session

- The polity-entry fix (MC) is done and merged; do not reopen it.
- If the brainstorm concludes the nominal price level does NOT bite any real
  decision, the right deliverable may be a **documented non-defect + a regression
  guard**, not a code change. That is a real outcome, not a failure.

## Traps carried

- **Five diagnoses died in MC, and one whole investigation was invalidated by its
  own harness.** Do not deliver a plausible story. Instrument on the committed
  sweep, measure on the 20-seed grid, and try to KILL your own hypothesis (a
  removal test is what finally worked — a cause must not survive its own removal).
  "Unsettled" is acceptable; a sixth plausible story is not.
- **`Money.Supply` / `Money.SegmentWealth` are non-commensurable across
  currencies** and look exactly like a step-scaled leak but are not
  (`MetricsOps.cs:6–37` — read the doc comment; it has burned multiple
  investigations, MC's included). Use seed 7 (single-currency at every clock) as
  the currency-confound control.
- **`FineTickTests` is the only automated P7 net, and MC documented its structural
  blind spot** — it forks after a 25y prologue and changes the clock mid-run, so
  genesis-schedule defects are invisible to it. If this slice touches anything
  genesis-adjacent, that blind spot is back; lean on the committed instrument, not
  FineTick.
- Slice-session workflow (scope nod · REPL/instrument eyeball · merge decision;
  task ledger; subagent-driven-development; one fable whole-branch review + fix
  wave; kickoff-prompt chaining). **Dispatch subagents synchronously**
  (`run_in_background: false`) — background dispatch silently did nothing in the
  BF/MC sessions. `git log main` before merge-out (main moves under you — L2, DX,
  and MC all landed mid-session on other people's clocks).
