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

- [x] **3. Land the entry fix as `Actor.EntryYear`.** *Opus* (spans genesis /
      phases / natives / serialization, changes serialized state, moves the
      product's default numbers). Replace the ambiguous `EntryEpoch` outright —
      no multiplier swap, no shim. User call taken with it: **fix the 25y
      truncation too**, accepting that default-clock numbers move.
      Gate: verified on the committed instrument, not a throwaway harness.
      **DONE** — see the Log entry below. `live_polities` exactly equal 5y vs 1y
      on 20/20, collapse regime gone, ports ≥ 1.0 on 20/20; 1103 passed, golden
      the only red (expected window).

- [ ] **4+. The residual nominal divergence — UNSETTLED, and NOT a fix task.**
      Survives the entry fix at 5.5–190× nominal against a real economy at
      2.0–4.3× ports. Task 2's judgment stands: the next step is a
      **measurement, not a fix** — receipts vs goods actually transacted per
      world-year, to settle whether Σ receipts (a gross flow, booked once per
      clearing, so 25× more clearings churn the same conserved money 25× more
      times) is even the right metric. Resolve "is the metric wrong" before
      pointing a slice at a sixth mechanism; five have died already.

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
- 2026-07-17 — **task 2 PARTIAL: the collapse regime is solved; the nominal
  divergence is not.** Root cause of the collapse regime, the direction
  inconsistency and the `live_polities` clock-dependence: **the entry gate
  mis-multiplies units.** `Phases.cs:1605` reads `EntryEpoch × GenerationYears`
  while `EpochGenesis.cs:66` writes `EntryEpoch = entryYear ÷ YearsPerEpoch`.
  Equal only at the default 25y clock, so it is invisible at coarse and stretches
  the whole emergence schedule 25× at 1y — the fine clock admits ~25× fewer
  polities (window 500y vs a 250y run). `NativeOps.cs:90` is the same gate's
  second victim (natives never enter at fine clocks). Removal test: one token,
  a **no-op at 25y** (coarse byte-identical), and `live_polities` goes to
  **exactly 1.000× on 20/20 seeds**, all 5 collapse seeds recover (99: 0.0008×
  → 8.8×, ports 12→30), ports direction ≥ 1.0 in 20/20. It also explains why the
  seed-99 harness was accidentally RIGHT about direction: applying the clock
  after genesis baked `EntryEpoch` at 25y, which cancels this bug exactly.
  **Two defects were cancelling: `FineTickTests` (the only automated P7 net) is
  green on main only because the entry bug drags the fine economy back down —
  it goes red under the fix, legitimately.** `EntryEpoch` is irredeemably
  ambiguous (its unit depends on the genesis clock); the fix shape is
  `Actor.EntryYear`, verified by a second probe that keeps `FineTickTests` green
  and matches the 1y column cell-for-cell. Re-tested on the corrected frame:
  **diagnosis #4 (budget cap) REFUTED again** (full removal moves 42 by 18.06 →
  18.06; ~0–15% elsewhere), and the **wealth-levy lead REFUTED as root cause** —
  it is a tail amplifier only (11: 171→11.7, 55: 129→11.6, but median holds ~14×
  and some seeds worsen). Tested as a sweep variant with zero code change; the
  instrument paid for itself. **Residual UNSETTLED**: nominal 4.8–171× vs real
  1.0–4.0×. Also ruled out: per-step money leak (per-currency residual ≤ 4e-9),
  freight/wages/production year-scaling (all correct), cross-currency
  non-commensurability (seed 7 is single-currency and still 25×). My own
  replacement hypothesis (nominal = superlinear in the port divergence) was
  **killed on the grid** (r = 0.286, exponents scatter 0.82–7.42). Trap logged:
  `Money.Supply`/`Money.SegmentWealth` are non-commensurable across currencies
  and look exactly like a step-scaled leak — read `MetricsOps.cs:6–37` first.
  Next step is a **measurement, not a fix**: receipts vs goods actually
  transacted per world-year, to settle whether Σ receipts (a gross flow) is even
  the right metric. Branch left **pristine** (all probes reverted); full findings
  in the task-2 report. Task 2 stays unticked: the entry defect is settled, the
  slice's headline nominal divergence is not.
- 2026-07-17 — **task 3 DONE: the entry fix is landed as `Actor.EntryYear`**
  (`44fc431`) — the slice's first actual fix, after five dead diagnoses.
  `EntryEpoch` is **gone** (greenfield, no shim): genesis writes the world-year
  directly, `Phases` and `NativeOps` compare it against `state.WorldYear`, no
  multiplier anywhere. Full site audit turned up **nine** sites, not the four
  named in the brief: `Actor` (field + ctor param), `EpochGenesis:66` (write),
  `Phases:1605` (gate), `NativeOps:90` (gate's second victim — now
  `state.WorldYear`, not `EpochIndex`), `ArtifactSerializer` (write + read +
  layer tuple), `SimTraceView:32` (rendered "enters epoch N (yM)" — now just
  "enters y{EntryYear}"), plus test-side `EpochGenesisTests`, `GenesisShapeTests`,
  `EpochEngineTests`, `AllocationTests`, `CurrencyArtifactTests` and five
  positional `entryEpoch: 0` helpers. Serializer `actors` **9 → 10**: field 6
  keeps its position and width but changes UNIT, so only the version distinguishes
  them.
  **Genesis is now clock-INVARIANT** — that division was its only clock read. So
  the ordering hazard `ClockPlanTests.TheClockMustBeAppliedBeforeGenesis` pinned
  is gone by construction, and that test (which asked in its own docstring to be
  retired if genesis ever stopped being clock-sensitive) is now
  `GenesisIsClockInvariant_TheEntryScheduleIsACalendarNotAnIndex` — same fixture,
  opposite assertion. **Task 2's §9.5 "engine-level genesis-clock guard is now
  urgent" is thereby moot**: there is no longer a genesis clock to stamp.
  **Instrument verdict** (committed sweep, self-checks green, null variant exactly
  1.0, byte-identical across processes): the **collapse regime is gone** (2, 5, 11,
  99, 101 — seed 99 ports 12→1 becomes 10→30, receipts 0.0008× → 10.85×); **ports
  ratio ≥ 1.0 on 20/20** (was 6 inverted); dead controls 13/777 still exactly 1.0×.
  The **1y column reproduces task 2's probe 1 cell-for-cell** (ports 42→13, 99→30,
  2→6, 5→6, 11→18, 101→5) — independent corroboration of the diagnosis.
  ⚠ **The brief's headline signature — `live_polities` 1.000× on 20/20 — is NOT
  reproduced, and CANNOT be**, because it was probe 1's signature and probe 1 was
  a deliberate no-op at 25y. This fix takes the approved 25y truncation change, so
  the 25y column moves by design. The correct invariance statement for this fix is
  **`live_polities` exactly equal at 5y vs 1y on 20/20** — the clocks that can
  actually resolve an entry date agree perfectly. 25y differs on 6 seeds for an
  honest reason: a 25y step only admits at 25-year boundaries and the last one in a
  250y run is year 225, so a polity scheduled for 226–249 no longer sneaks in early.
  **What 25y moved by** (the approved product change): `live_polities` down on
  **6/20** (42: 4→1; 8/11/101/123/404: −1), never up. Ports down on **9/20** (−1
  to −3), never up. Σ receipts **unchanged on 9/20** — exactly, to 6 s.f., incl.
  seeds 42 and 7 — and down on 11/20 (median ≈ −7%, worst seed 2 at −74.6%).
  Why receipts barely move while polity counts drop hard: the truncation's extra
  polities were mostly **phantom final-step entries** — admitted at year 225,
  founding a port and trading for zero time. The economic content of the fix at 25y
  is the *timing* shift (entry now rounds UP, up to 24 years later), which is what
  the 11 moved seeds show.
  **`FineTickTests` stays GREEN** (task 2's probe-2 prediction, confirmed) and its
  bands are **re-tightened, never widened**: provisions **0.85 → 0.6** (its value
  before three widenings, each rationalized by a "confirmed: no per-tick-vs-per-year
  formula defect" claim that was **false** — there was one, and each widening
  absorbed it), population **0.5 → 0.25**, ports **0.5 → 0.35**. Measured worst
  spreads: 0.41 / 0.14 / 0.23. Hulls left at 0.5 (worst 0.36) — not sized for a dead
  bug, so not touched. Its **blind spot is now documented in the test**: it forks
  after a coarse prologue and changes the clock mid-run, so genesis always ran at
  25y and it structurally cannot see an entry-schedule defect — which is exactly why
  it missed this one for four slices.
  Three fixture re-tunes, each with a **cause**, not a band-widening: `TreatyTests`
  first clean relation 13 → **14** and `HandoverTests` prologue 10 → **11** (both
  the same one-epoch entry slip; the other Handover tests keep the 10-epoch
  prologue), and `TradePact_OpensCrossBorderLanes` 24 → **25** — a 22..28 sweep
  shows 24 is the *only* hole in the range at the same pair and distance, i.e.
  fixture luck, so it was moved onto the plateau rather than left on the knife-edge.
  `dotnet test` **1103 passed / 1 failed** — the failure is `GoldenTests` only, the
  expected mid-slice red window (**not** regenerated; it re-freezes once at slice
  end).
