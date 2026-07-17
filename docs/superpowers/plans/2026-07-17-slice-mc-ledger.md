# Slice MC task ledger — clock-invariance instrument, then diagnosis

Branch `slice-mc-price-clock`, worktree `.worktrees/slice-mc`, base main `c7afaec`.
Kickoff `docs/superpowers/plans/2026-07-16-slice-mc-kickoff-prompt.md` (RE-AIMED —
read its header banner, not its original body).

## Why this slice changed shape (read this first)

MC was scoped to fix a P7 violation in `MarketEngine.DriftReferencePrices`. It is
not doing that, because **four successive diagnoses of the sim's clock-dependence
have each been refuted by instrumentation**:

| # | Diagnosis | Refuted by |
|---|---|---|
| 1 | `IssueSovereignCredit`'s cap is per-epoch | at 25y the cap **never binds — not once** |
| 2 | Mint feedback compounds | zeroing **both** mints still leaves receipts diverging 16× |
| 3 | `DriftReferencePrices` stock/flow mismatch | mean price level differs only **~1.9×**; can't explain 12–25× |
| 4 | The band-bid budget cap (`budget = seg.Wealth`) | neutralizing it: bind rate 36–53% → **0.0%**, divergence **61.5× → 61.5×** |

Every one was plausible. Every one was wrong. **And the measurements themselves
now disagree**: three readings of seed 42's Σ receipts @1y — 239,199 (pre-L2
investigation), ~191,189 (baseline agent), **959,831** (seed-99 agent) — while 3
of 5 seeds reproduce *to the last decimal* between the latter two, proving the
frames are otherwise identical. Only the two largest worlds (42, 99) disagree.

**Conclusion: the project is not short a diagnosis, it is short an instrument.**
It has a committed 32-run sweep as its *conservation* instrument (because CU-1
learned seed-42 unit tests lie) but **no clock-invariance instrument**. Every P7
claim in this codebase — all four refutations, both baselines, BF's §4a — was made
with a throwaway harness written fresh by whoever was investigating. Two of those
demonstrably disagree. A fifth diagnosis on an untrusted instrument is a coin flip.

**User call 2026-07-17: build the instrument first, then re-diagnose.**

## The trap the instrument must make impossible

`SweepRunner` applies **one global `epochs`** to every variant
(`SweepRunner.cs:146`, `econfig.Sim.EpochCount = epochs`), while `Sim.YearsPerEpoch`
is an ordinary per-variant knob. So the obvious way to write a clock sweep —
variants `{Sim.YearsPerEpoch: 25 | 5 | 1}` — silently compares **40 world-years
against 1000 world-years**. A clock comparison is only meaningful at a **constant
world-time span**: `epochs × YearsPerEpoch = const`. This is a prime suspect for
the harness disagreement above.

## Tasks

- [x] **1. The clock-invariance instrument (committed).** A P7 sweep — the
      clock-invariance sibling of the 32-run conservation sweep. Holds world-time
      **constant** by construction (per-variant epoch counts, or a `worldYears`
      span the runner derives epochs from — a design call). 20 seeds ×
      {25y, 5y, 1y}. Reports per seed, **nominal and real divergence separately**
      (Σ receipts / Σ fiat issued vs ports / population) — that distinction is the
      entire analytical frame and every prior round blurred it. Committed config +
      runner support, so no future P7 claim needs a throwaway harness. *Opus*
      (determinism + design judgment).
      **Gate — its first proof of correctness: reconcile the two disagreeing
      harnesses on seeds 42 and 99.** An instrument that cannot explain why the
      existing readings differ 5× is not yet trusted. Also: `dotnet test` green;
      deterministic (byte-identical run to run); runs in a few minutes.

- [ ] **2. Re-diagnose on the trusted instrument.** Only after task 1 is trusted.
      Establish what the divergence actually IS (magnitude, nominal vs real, which
      seeds) and hunt the cause with the four refutations already in hand.
      Standing lead, NOT yet diagnosed and resting on disputed numbers: seed 42's
      receipts appear dominated by the **wealth levy** (`Phases.cs:336`) at 166×
      while goods actually transacted diverge 13.1× — at fine, receipts *exceed*
      transacted value 4.4×. Note the levy formula IS correctly year-compounded
      (`1 − (1 − r)^spanYears`), so if it still diverges that much, the cause is
      elsewhere (what feeds `taxable`?). *Opus*.
      Gate: any claim carries the 20-seed grid, not a single seed.

- [ ] **3+. Scope the fix.** Cannot be written until task 2 lands. The remaining
      genuine open item from the last round: a modest, **direction-consistent**
      1.0–4.67× real-economy divergence (fine ≥ coarse in all 20 seeds) that
      nothing measured so far explains.

- [ ] **N. USER: eyeball · whole-branch fable review + fix wave · golden freeze ·
      merge decision.**

## Standing corrections to the record — ⚠ THEMSELVES CORRECTED (task 1)

The corrections below came from `2026-07-17-mc-seed99-and-budget-cap-refutation.md`.
**Task 1's instrument reconciled the two harnesses and that spec is the wrong one**
— its harness applied the clock AFTER `EpochGenesis.Seed`, so every staggered
polity entered ~25× early in world-time at the fine clocks. See the Log entry and
`2026-07-17-mc-clock-invariance-instrument.md`. Status now:

- ~~The seed-99 "real-economy bifurcation" does not reproduce~~ — **it DOES
  reproduce.** 99 @1y = 1 port / Σ receipts 47.3, exactly as the baseline said.
  The 32-port ignition is the artifact.
- ~~"Direction is inconsistent across seeds" refuted~~ — **not refuted.** 6 of 20
  seeds (2, 5, 11, 99, 101, and 42 on population) invert: ports ratio 0.08–0.33.
  The seed-99 spec's 20-seed table (1.0–4.67×, never below 1.0) is an artifact.
- The seed-99 spec's §5 **budget-cap refutation was measured in the broken frame**.
  Diagnosis #4 is therefore **un-refuted** — back to open. Same for its "wealth
  levy 166×" lead (task 2's standing lead), which must be re-measured.
- The baseline spec (`2026-07-17-mc-baseline-on-main.md`) §1/§2/§3 **reproduce
  cell-for-cell** on the trusted instrument. Its §5 seed-99 flag stands.
- The original investigation's "ask stock 165 vs 32 = 5.2×" re-measures at **1.4×**;
  its "fine sits at the 59–100 ceiling" is wrong (fine's median rel price is 1.000).
  (Not re-tested in task 1 — from the baseline spec, which is otherwise sound.)
- BF design §3's freeze of ME's issuance cap **stands** (investigation Finding 7 is
  the one finding that has survived every round).

## Log

- 2026-07-17 — baseline re-measured on main; premise held but defect site wrong
  (`fd01fd8`). Seed-99 investigation then refuted diagnosis #4 AND the baseline's
  own seed-42/99 rows. User call: build the instrument before diagnosing further.
  Ledger opened at this shape.
- 2026-07-17 — **task 1 DONE: the instrument exists and it settled the
  disagreement on its first use.** `ClockPlan` (Core) makes the span the input
  and derives every clock's epoch count; `SweepRunner` grows a clock mode
  (`worldYears` + `clocks`, mutually exclusive with `epochs`) that refuses a
  non-divisible span, duplicate clocks, and the epochs/worldYears mix; two new
  registry metrics (`Economy.PolityReceipts`, `Economy.CorpReceipts` — flows —
  and `Settlement.Ports`) make the nominal/real split a column. Committed config:
  `2026-07-17-clock-invariance-experiment.json`, 20 seeds × {25y, 5y, 1y} × 2
  variants in **5 seconds**. Self-checks: per-run span assertion, a built-in null
  variant (25y re-run → every ratio exactly 1.0), byte-identity across processes,
  dead controls 13/777 at 1.0× ports.
  **Reconciliation verdict: the baseline was right; the seed-99 investigation's
  harness was wrong.** It applied the clock AFTER `EpochGenesis.Seed`, but genesis
  bakes `EntryEpoch = entryYear / YearsPerEpoch`, so at 1y every staggered polity
  entered ~25× early in world-time. Emulating exactly that bug reproduces all four
  of its disputed numbers to the last decimal (42@1y 959,830.6 / 15 ports;
  99@1y 704,071.5 / 32 ports; 99@5y 445,520.8; 11 ratio 264.1) — and it explains
  why seeds 7/13/2024 agreed exactly: their origins all sit at entry epoch 0,
  where the two frames coincide. Pinned by
  `ClockPlanTests.TheClockMustBeAppliedBeforeGenesis_EntryEpochsAreBakedThere`.
  **New design question for task 2/3:** genesis is clock-sensitive by
  construction. `ClockPlan` documents the ordering but cannot enforce it — an
  engine-level guard would, and that is a design call, not a task-1 call.
  `dotnet test` 1102 passed / 0 failed (1081 base + 21 new).
