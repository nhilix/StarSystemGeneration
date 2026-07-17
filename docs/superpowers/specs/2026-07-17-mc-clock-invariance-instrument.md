# Slice MC task 1 — the clock-invariance instrument, and the reconciliation

Branch `slice-mc-price-clock`, worktree `.worktrees/slice-mc`, base main `c7afaec`.
`dotnet test StarSystemGeneration.sln` green: **1102 passed** (1081 base + 21 new).

## TL;DR

The project now has a committed **P7 / clock-invariance sweep**, the sibling of
the 32-run conservation sweep. 20 seeds × {25y, 5y, 1y} × 2 variants, **5
seconds**, world-time constant by construction.

Its first use settled the disagreement it was built to settle:
**`2026-07-17-mc-baseline-on-main.md` is right. `2026-07-17-mc-seed99-and-budget-cap-refutation.md`
is wrong** — and its bug is identified, reproduced exactly, and pinned by a test.

## 1 — What was built

| Piece | Where |
|---|---|
| `ClockPlan` — span in, epoch count derived | `src/Core/Epoch/ClockPlan.cs` |
| Clock mode: `worldYears` + `clocks` | `src/Inspector/SweepRunner.cs` |
| `Economy.PolityReceipts`, `Economy.CorpReceipts`, `Settlement.Ports` | `src/Core/Epoch/Health/MetricRegistry.cs` |
| The committed experiment | `docs/superpowers/plans/2026-07-17-clock-invariance-experiment.json` |

```
dotnet run --project src/Inspector -c Release -- sweep \
  docs/superpowers/plans/2026-07-17-clock-invariance-experiment.json
→ runs/sweeps/clock-invariance/clock-divergence.csv
```

### Design call: the span is the input, the epoch count is derived

`SweepRunner` applied one global `epochs` to every variant while
`Sim.YearsPerEpoch` was a free-floating structural value, so the obvious clock
sweep would compare 40 world-years against 1000. Rather than add per-variant
epochs (which still lets the two drift apart), **`worldYears` is the input and
`ClockPlan.EpochsFor` derives each clock's epoch count**. There is no overload
that takes a step without a span. The mode refuses, before any run starts:

- `epochs` and `worldYears` together (mutually exclusive)
- `clocks` without `worldYears`, or `worldYears` without `clocks`
- a clock that cannot integrate the span exactly (250y at 7y/epoch)
- duplicate clocks (they would collide on output paths and halve the grid)

`Sim.YearsPerEpoch` is deliberately **not** registered in `KnobRegistry` — the
clock is identity, not calibration (KnobRegistry's own doc says so), and it lives
on the ESIM line. `ClockPlan` is the writer of that identity, not a new dial. No
new knob was added, so nothing can silently revert on reload.

### The nominal/real split is a column, not prose

Every prior round blurred this. `clock-divergence.csv` carries `frame` =
`nominal` | `real` and `agg` = `sum` | `final` per row. Receipts are a per-epoch
**flow** (`MarketsPhase` zeroes them every step) and integrate over rows;
`CumulativeFiatIssued` is already a running total and reads the final row.
Getting that backwards scales the answer by the epoch count — i.e. by the clock,
the very thing being measured.

## 2 — The instrument's self-checks

Four, three of them automatic and fatal:

1. **Span assertion, per run** — every run must end at exactly `worldYears`.
   A ratio across mismatched spans is the failure mode the instrument exists to
   rule out, so it is asserted, not assumed. Nonzero exit if violated.
2. **The null variant** — the coarsest clock is re-run and every reported column
   must match bit-for-bit. 25y vs 25y reports exactly 1.0×, or the sweep fails.
3. **Byte-identity across processes** — the whole sweep run twice, `diff -r`
   clean (P6).
4. **Dead-world controls** — seed 13 (1.04× receipts, 1.0× ports) and seed 777
   (1.31×, 1.0×) sit in the committed seed list. If a control ever diverges, the
   instrument is measuring itself.

One caveat worth naming: a row's `world_year` labels the **start** of the epoch
it summarizes (`Snapshot` lands before `WorldYear` advances), so the last row
reads `span − clock`, not `span`. A reader who treats it as the end-year sees a
fake clock-dependence in the time axis itself. Pinned by test.

## 3 — RECONCILIATION: which harness was wrong, and why

**The seed-99 investigation's harness applied the clock AFTER genesis.**

`EpochGenesis.Seed` bakes each actor's entry time as an epoch **index**:

```csharp
int entryEpoch = entryYear / Math.Max(1, config.Sim.YearsPerEpoch);   // EpochGenesis.cs
```

Seed genesis at the default 25y clock, then switch to 1y before `Run`, and every
staggered polity enters **~25× early in world-time**: an actor meant for
world-year 225 enters at world-year 9. Fine clocks get a fabricated, far larger
economy. Coarse clocks are untouched (25y genesis == 25y run), which is why
**every 25y row in both specs agrees**.

Emulating exactly that ordering reproduces all of the seed-99 spec's disputed
numbers **to the last decimal**:

| cell | baseline spec | seed-99 spec | trusted instrument | instrument, clock applied after genesis |
|---|---|---|---|---|
| 42 @1y Σ receipts | 191,189 | 959,830.6 | **191,189.0** | **959,830.6** ✓ |
| 42 @1y ports | 10 | 15 | **10** | **15** ✓ |
| 99 @1y Σ receipts | 47.3 | 704,071.5 | **47.3** | **704,071.5** ✓ |
| 99 @1y ports | 1 | 32 | **1** | **32** ✓ |
| 99 @5y Σ receipts | 91,787 | 445,520.8 | **91,787.0** | **445,520.8** ✓ |
| 11 receipts ratio | — | 264.1 | **0.006** | **264.1** ✓ |

**And it explains the 3-of-5 exact agreement** that made both agents trust their
frames. Entry epochs at 25y genesis:

| seed | entry epochs | verdict |
|---|---|---|
| 7, 2024, 13 | `[0, 20]` | all live origins at epoch **0** → the two frames **coincide** → exact agreement |
| 42 | `[0,9,9,9,12,15,15,15,20]` | staggered → corrupted |
| 99 | `[0,0,1,2,3,6,10,11,17,20]` | staggered → corrupted |
| 11 | `[0,0,0,2,3,9,10,11,12,13,20]` | staggered → corrupted |

The disagreeing seeds are **exactly** the seeds with a staggered entry schedule.
The seed-99 spec's own §1 trace reports "first expedition dispatches at
**world-year 8**" for seed 99 — that is `entryEpoch = 8` being read as year 8
instead of year 200. The fingerprint was in its own evidence.

Its appendix ruled out nondeterminism, static state, source drift, chaos, and
radius — all correctly. It could not have found this one: both frames are
perfectly deterministic and byte-identical on re-run. Determinism was never the
question; **the genesis ordering was**, and it is invisible to every check it ran.

### Consequences (see the ledger)

- The seed-99 spec's **"the bifurcation does not reproduce"** is itself the
  artifact. The bifurcation is **real**: 99 @1y is a dead world.
- Its **"direction is inconsistent across seeds — refuted"** is **not** refuted.
  6 of 20 seeds invert (ports ratio 0.08–0.33).
- Its **§5 budget-cap refutation was measured in the broken frame**. Diagnosis #4
  goes **back to open** — as does its wealth-levy lead (task 2's standing lead).
- The baseline spec's §1/§2/§3 reproduce cell-for-cell and stand.

### New design question, flagged not answered

**Genesis is clock-sensitive by construction.** `ClockPlan` documents the
ordering and the sweep gets it right, but nothing structurally prevents a caller
applying the clock after `Seed` — the exact bug that cost this project an entire
investigation. An engine-level guard (stamp the genesis clock on `SimState`,
refuse a stepped clock change) would close it. That is a design call and touches
whether changing the clock mid-run is an intended product capability
(`frame/time.md`: "the live game steps the same machine fine-grained"), so it is
**not** a task-1 call. Pinned meanwhile by
`ClockPlanTests.TheClockMustBeAppliedBeforeGenesis_EntryEpochsAreBakedThere`.

## 4 — The headline grid (mints-off, 250 world-years, radius 8)

Full data: `runs/sweeps/clock-invariance/clock-divergence.csv`.

| seed | Σ receipts 25y | Σ receipts 1y | **nominal ratio** | ports 25y→1y | **real ratio** |
|---|---|---|---|---|---|
| 42   | 15,606 | 191,189 | **12.3×** | 6 → 10 | 1.67× |
| 7    | 14,490 | 366,590 | **25.3×** | 3 → 10 | 3.33× |
| 13   | 43     | 45      | 1.04× (control) | 1 → 1 | 1.00× |
| 99   | 57,624 | **47** | **0.0008× — INVERTED** | 12 → **1** | **0.08×** |
| 2024 | 13,250 | 217,642 | **16.4×** | 3 → 10 | 3.33× |
| 1    | 16,354 | 196,581 | 12.0× | 3 → 10 | 3.33× |
| 2    | 2,677  | **45** | **0.017× — INVERTED** | 3 → **1** | **0.33×** |
| 3    | 28,610 | 289,459 | 10.1× | 6 → 10 | 1.67× |
| 5    | 2,429  | **12** | **0.005× — INVERTED** | 4 → **1** | **0.25×** |
| 8    | 13,463 | 247,620 | 18.4× | 5 → 10 | 2.00× |
| 11   | 24,774 | **148** | **0.006× — INVERTED** | 9 → **2** | **0.22×** |
| 17   | 18,131 | 266,363 | 14.7× | 5 → 10 | 2.00× |
| 23   | 19,970 | 351,654 | 17.6× | 3 → 10 | 3.33× |
| 55   | 13,370 | 245,349 | 18.4× | 4 → 10 | 2.50× |
| 77   | 14,646 | 225,351 | 15.4× | 5 → 10 | 2.00× |
| 101  | 2,339  | **36** | **0.015× — INVERTED** | 4 → **1** | **0.25×** |
| 123  | 13,169 | 281,773 | 21.4× | 5 → 10 | 2.00× |
| 404  | 12,959 | 318,522 | 24.6× | 4 → 9  | 2.25× |
| 777  | 38     | 50      | 1.31× (control) | 1 → 1 | 1.00× |
| 999  | 17,733 | 278,096 | 15.7× | 3 → 10 | 3.33× |

The `baseline` (mints-ON) variant tracks it closely — mints do not rescue the
collapses (99 @1y = 47.4 with mints on). Issuance remains a thermometer.

**The shape, stated but not diagnosed (task 2's job):**

1. **The premise holds and is bimodal.** 13 of 20 live seeds diverge **10–25×
   nominal** against **1.7–3.3× real**. 6 of 20 **collapse** at the fine clock
   (ports → 1–2, receipts → ~0). There is no single divergence number; there are
   two regimes, and the prior rounds each sampled one of them.
2. **Direction is NOT consistent.** The seed-99 spec's headline claim
   (20/20 ports ratio ≥ 1.0) was the artifact talking.
3. **An unexplained 10-port attractor.** Almost every non-collapsed seed lands on
   exactly 10 ports at 1y (10, 10, 10, 10, 10, 10, 10, 10, 9), where coarse
   spreads 3–12 and seed 3 reaches 18 at 5y. Not a hard cap — 25y exceeds it —
   but too sharp to be coincidence. Offered as a **lead with numbers**, not a
   finding. It is the first thing task 2 should look at, and it is exactly the
   kind of single-metric story this project has refuted four times, so it should
   be instrumented before anyone believes it.
