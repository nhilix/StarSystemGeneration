# Slice BF task ledger — the bank as monetary authority

> ## ⛔ PARKED 2026-07-16 — blocked on the market-clock slice
>
> **Status: tasks 1–5b complete and committed; tasks 6–13 NOT started.** The
> branch is sound and its tests are honest — do not treat this as abandoned work.
>
> **Why parked (user decision):** task 5b's clock-invariance probe surfaced a
> pre-existing P7 violation that BF did not cause and cannot fix. Root-caused in
> `docs/superpowers/specs/2026-07-16-market-clock-dependence-investigation.md`:
> `MarketEngine.DriftReferencePrices` normalizes demand per generation but takes
> supply as the raw resting-ask stock, so a 25y clock and a 1y clock sit in
> **opposite saturated price regimes** (nominal receipts/yr diverge 68×). It is
> upstream of every monetary channel, so **no BF tuning or sweep claim is
> meaningful until it is fixed** — which makes tasks 9 (sweep + backing
> activation) and 10 (eyeball) unrunnable, not merely inconvenient.
>
> **Known-red on this branch, all expected and understood:**
> - `GoldenTests.ReferenceArtifact_MatchesTheFrozenGolden` — the standing
>   mid-slice golden red window (task 12 re-freezes, once, at slice end).
> - 6 conservation residuals, every one **negative** — the task 6 gap exactly
>   (the residual does not yet subtract `CumulativeFiatRetired`). Task 6 is the
>   resume point and closes them.
> - `FineTickTests.FineTick_ProjectCompletions_LandOnWorldYears_NotSteps` — the
>   market-clock defect, amplified by BF's servicing (2.9× → 7.3×). **Only the
>   market-clock slice can green this.** Do NOT widen its band; see below.
>
> **Resume at task 6** once the market-clock slice lands, then re-baseline: the
> fix will move every price and receipt, so BF's knob defaults (§9) must be
> re-examined before task 9's sweep rather than assumed.
>
> **§3's freeze of ME's issuance cap is CORRECT and stands.** An earlier
> diagnosis blamed the issuance cap; the investigation refuted it (at 25y the cap
> never binds — not once). Do not amend §3.

Branch `slice-bf-bank-flow` (worktree `.worktrees/slice-cu3` — directory name is a
stale artifact of the CU-3 pivot, `git worktree move` blocked by an open handle;
retry at wrap-up). Base: main `768a8e4`. Design:
`docs/superpowers/specs/2026-07-16-bf-bank-flow-design.md`.

Slice sequenced ahead of CU-3 by user decision (design §8). CU-3's kickoff prompt
(`2026-07-16-slice-cu3-kickoff-prompt.md`) stays on disk and is re-chained at
wrap-up.

## Model routing (CLAUDE.md "Model usage")

Escalate to Opus when a task touches conservation/determinism invariants, spans
subsystems, or is a design judgment call. Most of this slice is money movement, so
the escalation bar is met more often than usual — noted per task.

## Tasks

- [x] **1. Data model** — `Bank.ClaimOnState`, `.CumulativeLentToState`,
      `.CumulativeRetired`; `Currency.CumulativeFiatRetired`. Fields + XML docs
      only, all default 0. No behavior. *Sonnet* (mechanical).
      Gate: `dotnet test` green, byte-identity unchanged (all fields 0).

- [x] **2. Serialization** — extend `BANK` (`ArtifactSerializer.cs:254`/`:1284`)
      and `CURRENCY` (`:254`/`:1267`) lines; bump the version tuple; round-trip
      test. *Sonnet* (mechanical, but the dense id-order guards are load-bearing).
      Gate: round-trip + `LoadThenContinue` determinism tests green.

- [x] **3. Knobs** — `SovereignClaimInterestRate` (0.02), `ClaimServicingShare`
      (0.25), `FxBackingSensitivity` (**0.0** — CU-2-identical landing).
      **All three registered in `KnobRegistry.cs`** (CU-2 review finding 1: an
      unregistered knob silently reverts on reload). Parallel with 1–2. *Sonnet*.
      Gate: knob-registration test; a reload preserves each.

- [x] **4. LendToState** — `IssueSovereignCredit` → `Bank.LendToState`; books
      `ClaimOnState += m` alongside the **unchanged** `Supply`/`CumulativeFiatIssued`
      motion. ME's cap and floor untouched. `FundDeficit` stage 1 unchanged.
      *Opus* (conservation invariant + money creation).
      Gate: `dotnet test`; issuance magnitudes byte-identical to CU-2 (only the
      claim is new).

- [x] **5. ServiceSovereignClaim** — the servicing pass, ordered **after**
      `FundDeficit` in Allocation. Budget computed ONCE before mutation (design
      §4 — the `Phases.cs:444` compound-assignment trap). Surplus-only; interest
      never capitalizes. *Opus* (the money sink + both hard rules).
      Gate: servicing never drives `Credits` negative; no capitalization on an
      insolvent polity; residual holds across a repayment epoch.

- [x] **5b. Servicing is per world-year** (design **§4a**, the amendment the
      Task 5 probe forced — commit `3983ec2`). Knob renames to the repo's
      `PerYear` convention: `ClaimServicingSharePerYear` (**0.01**, compounds:
      `share = 1 − (1 − s)^years`) and `SovereignClaimInterestRatePerYear`
      (**0.001**, **LINEAR** in years — rule 2 forbids compounding). Follows
      `DecayIdlePools` (`Phases.cs:632`). Both hard rules stay structural — no
      clamp, floor, or ceiling added. *Opus* (P7 invariant + money).
      Gate: `FineTickTests.FineTick_ProjectCompletions_LandOnWorldYears_NotSteps`
      green. **⚠ NOT met — see the log; the residual cause is a separate,
      pre-existing clock-dependence in issuance, not in servicing.**

- [x] **6. Residual** — add `CumulativeFiatRetired` to `CurrencyResidualRow` AND
      the baseline carry (`MetricsOps.cs:195` is a **delta** form). Design §6
      names this the single easiest way to get the slice wrong. *Opus*.
      Gate: residual ~0 across repayment epochs; **32-run sweep** (first run).

- [x] **7. FX backing term** — `unbacked = max(0, ClaimOnState − Reserve)`;
      `effectiveMoney = Supply + FxBackingSensitivity × unbacked` into
      `FxOps.cs:59`. *Opus* (determinism formula + design judgment).
      Gate: `FxBackingSensitivity = 0` reproduces CU-2 **byte-identically**.

- [ ] **8. REPL surface** — claim book, backing ratio, retired-to-date on the
      currency line. *Sonnet*.
      Gate: the REPL surface works (piped via bash `printf`, not PowerShell).

- [ ] **9. Sweep + backing activation** — run the 32-run committed sweep; then
      raise `FxBackingSensitivity` off 0 to a defensible value and re-run.
      **The eyeball needs a non-zero run** — at 0 the FX coupling is inert.
      *Opus* (tuning claim ⇒ ensemble bar, SIMHEALTH.md).
      Gate: worst per-currency residual ~1e-9 abs / ~1e-16 relative.

- [ ] **10. USER: REPL eyeball acceptance** (the taste gate).

- [ ] **11. Whole-branch fresh-eyes review** — *fable*, pinned. Then one fix wave.

- [ ] **12. Golden freeze** — once, at slice end (red-window inside the slice).

- [ ] **13. USER: merge decision** → merge to main locally (`git log main` first —
      L2 is in flight and may have moved main) · update `docs/HANDOFF.md` ·
      **re-chain CU-3's kickoff** (amend it for the claim-book merge questions
      design §8 raises) · sync Trello · push only on say-so.

## Gates (all mandatory, all mechanical)

`dotnet test` green · hex-tier suite never breaks · determinism byte-identity ·
32-run sweep residual within tolerance · goldens frozen once at slice end · the
REPL surface works.

## Log

- 2026-07-16 — brainstorm complete; sequencing decision taken (BF before CU-3,
  user overrode the CU-3-first recommendation); design approved and committed
  `298f20f`; ledger opened.
- 2026-07-16 — Task 1 (data model) done: `Bank.ClaimOnState`,
  `.CumulativeLentToState`, `.CumulativeRetired`; `Currency.CumulativeFiatRetired`.
  Fields + XML docs only, all default 0, no write sites — functional no-op.
  `dotnet test` green (1047 passed).
- 2026-07-16 — Task 2 (serialization) done: `BANK` gains `ClaimOnState`,
  `CumulativeLentToState`, `CumulativeRetired` (banks v1 → v2); `CURRENCY` gains
  `CumulativeFiatRetired` trailing after `Retired` (markets v5 → v6). Round-trip
  tests extended with distinct nonzero values in both `BankArtifactTests` and
  `CurrencyArtifactTests` (field-order mix-ups fail loudly). `dotnet test`:
  1046 passed, 1 expected red (`GoldenTests.ReferenceArtifact_MatchesTheFrozenGolden`
  — pure `LAYER|markets|5` → `|6` format-version diff, goldens re-freeze at Task
  12). `FineTickTests`/`TimeMachineTests` `LoadThenContinue` stayed green.
- 2026-07-16 — Task 3 (knobs) done: `SovereignClaimInterestRate` (0.02),
  `ClaimServicingShare` (0.25), `FxBackingSensitivity` (0.0) added to
  `EconomyKnobs` under a new "Bank monetary authority (slice BF)" section
  comment, each carrying a FLAGGED-for-tuning XML doc in the `ConversionSpread`
  style; registered in `KnobRegistry.cs`, name-sorted into the existing
  `Economy.*` block (`Names_AreUnique_Sorted_AndDocumented` and
  `EveryKnob_RoundTripsThroughItsAccessors` cover registration and round-trip
  generically — no new per-knob tests needed). Nothing reads the knobs yet.
  `dotnet test`: 1046 passed, 1 expected red (the same frozen-golden diff,
  now additionally showing the new `KNOB|Economy.ClaimServicingShare|...`
  line — no new failures). `FxBackingSensitivity = 0.0` keeps every other
  behavior byte-identical.
- 2026-07-16 — Task 4 (LendToState) done: `Bank.LendToState(amount)` books the
  CLAIM half only (`ClaimOnState += m`, `CumulativeLentToState += m`); the money
  creation stays at the `IssueSovereignCredit` chokepoint, untouched. The call
  sits inside the existing `if (pr.CurrencyId >= 0)` per-currency-mirror guard,
  so the pre-genesis path (no currency ⇒ no bank) still mints with no creditor
  and cannot throw — the same fall-through `FundDeficit` stage 1 already takes.
  No reserve gate, no reserve term in the cap (design §3 — ME's floor).
  `FundDeficit` stage 1 untouched. New `SovereignLendingTests` (7 cases): claim
  matches the mint, claim accumulates across epochs, an empty bank still lends in
  full, a reserve-funded deficit books NO claim, a zero cap books no claim, the
  pre-genesis polity mints with no claim and no crash, and the per-currency
  residual stays 0 (the claim is not money).
  **Byte-identity evidence**: dumped the full seed-42 reference artifact with and
  without the source change (temp harness, since removed) and diffed — the ONLY
  differing lines in the whole artifact are `BANK` lines, and only in the
  `ClaimOnState`/`CumulativeLentToState` columns. Every `CURRENCY` line
  (`CumulativeFiatIssued` included), every treasury, every metric: identical.
  Issuance magnitudes provably unmoved. `dotnet test`: 1053 passed, 1 expected
  red (the same Task-2/3 frozen-golden format diff) — no new failures.
- 2026-07-16 — Task 5 (ServiceSovereignClaim) done: the money sink lands.
  A new private `AllocationPhase.ServiceSovereignClaim` runs in its OWN pass
  after the `FundDeficit` loop (actor-id order, P6) — a separate loop, not a
  tail call, so co-tenants of one currency all draw on the reserve before any
  of them pays into it. Budget computed ONCE before any `Credits` mutation
  (the `Phases.cs:444` trap). Both hard rules are properties of the
  arithmetic, NOT clamps: `budget = Credits × share ≤ Credits` and
  `interest + principal ≤ budget` ⇒ `Credits` provably still positive (no
  floor guard exists because none is reachable); unpaid interest is dropped by
  the `Math.Min` against the budget and never touches `ClaimOnState`, so there
  is no compounding term anywhere — no ceiling, no default, no write-off.
  New `SovereignClaimServicingTests` (13 cases) covering both rules, the
  compound-assignment trap (pinned numerically: 210, not the 200 a re-read
  would give), full repayment stopping at zero, pre-genesis, and both
  conservation halves.
  **Real-flow evidence** (temp probe, since removed — seed 42, 10-epoch
  prologue + 8×25y): the sink fires at scale, not just in fixtures —
  `CumulativeFiatRetired` 1343.6, claim book 6084.3 against 7427.9 issued
  (claim == issued − retired to the digit), and the reserve reaches 186.2 vs
  32.5 on task-4 HEAD: interest is the reserve's first real-economy inflow,
  the actual closure of design §1.
  `dotnet test`: 1059 passed, 8 red. **5 are the Task-6 gap, exactly as the
  design predicted** (`ConservationTests` ×2, `ShapeAcceptanceTests
  FortyEpochs_CreditsConserveToTheMint` ×3 seeds, `GraduationTests
  Schism_..._Conserved`): every one is a NEGATIVE residual of ~25–41 (money
  destroyed, no counter moved) because the delta form does not yet subtract
  `CumulativeFiatRetired`. Task 6 wires it; `MetricsOps.cs` deliberately
  untouched here. 1 is the standing frozen-golden diff.
  **⚠ The 8th is a genuine NEW failure and a DESIGN gap, not an implementation
  bug** — `FineTickTests.FineTick_ProjectCompletions_LandOnWorldYears_NotSteps`.
  Design §4's formula (and Task 3's knob docs, "per epoch"/"in one epoch") make
  servicing a fraction of a treasury STOCK charged once per epoch, so its
  per-world-year intensity scales as 1/`YearsPerEpoch` — the sim's outcome now
  depends on the clock. Probe evidence at the same world-year 450: on task-4
  HEAD total treasury is clock-invariant (4514.8 coarse @25y vs 4515.6 fine
  @1y, 0.02% apart); WITH this pass it diverges 4× (6879.8 vs 1704.4). Every
  other stock-fraction op in the sim is year-scaled (`DecayIdlePools`'
  `(1 − PoolIdleDecayPerYear)^years`, `StockpileDecayPerYear`,
  `ConditionDecayPerYear`) — this is the P7 / "time, not ticks" discipline the
  design did not consider. Implemented design-faithfully and NOT deviated
  (the design is the spec); the fix is a §4 amendment — year-scale the budget
  as `1 − (1 − share)^years` and the interest as a per-year rate, which
  preserves both hard rules structurally (the factor stays in [0,1], so
  `budget ≤ Credits`; interest stays `Math.Min`-capped by budget) but changes
  knob semantics. **Flagged to the user for a design decision.**
- 2026-07-16 — Task 5b (§4a year-scaling) done: `ClaimServicingShare` →
  `ClaimServicingSharePerYear` (0.25 → **0.01**, compounded
  `1 − (1 − s)^years` per `DecayIdlePools`' precedent) and
  `SovereignClaimInterestRate` → `SovereignClaimInterestRatePerYear`
  (0.02 → **0.001**, **linear** `claim × r × years` — rule 2 forbids
  compounding, so each world-year accrues on the same principal). Renamed in
  `EpochSimConfig.cs`, **re-registered in `KnobRegistry.cs`** (name-sorted; a
  missed registration silently reverts on reload), XML docs restated as
  per-world-year. Both hard rules still structural: `share < 1` for ANY epoch
  length keeps `budget ≤ Credits`; `interest + principal ≤ budget`. No clamp,
  floor, ceiling, or write-off added.
  `dotnet test`: **1062 passed, 8 red — all expected, no new failures.**
  1 standing frozen-golden diff (goldens NOT regenerated, Task 12 re-freezes);
  6 conservation residuals, every one **negative** (−20.2 … −34.8), the Task 6
  gap (the residual does not yet subtract `CumulativeFiatRetired`) —
  `MetricsOps.cs` untouched. The canary
  `PrincipalRepayment_ResidualIsExactlyTheNotYetWiredRetirement` left for Task 6
  to flip.
  **Clock-invariance evidence** (isolated servicing harness, steady real income
  of 100 credits/world-year, 200 world-years, claim 20 000):
  treasury 7713.57 @25y/epoch vs 8707.58 @1y/epoch (**11.4%**), principal
  retired 9915.82 vs 8967.82 (9.6%), claim 10084.18 vs 11032.18. Pre-amendment
  the same harness settles to ~10 000 vs ~400 — a **~25× clock-dependence**. The
  residual ~12% is irreducible, not a defect: it is the discretization gap of any
  per-year-compounded share against income arriving between draws (steady state
  solves to 8750 @25y vs 9900 @1y — the observed figures exactly), and
  `DecayIdlePools` shares it. Three new tests pin this permanently: the share's
  compounding and the interest's linearity each **exactly**, plus the history-level
  band at 20%.
  **⚠ `FineTick_ProjectCompletions_LandOnWorldYears_NotSteps` did NOT go green,
  and §4a cannot green it.** Task 5's log attributed it to servicing; that is
  half right. Probe at world-year 450 with servicing DISABLED from genesis
  reproduces Task 5's pre-servicing figures exactly (treasury 4514.8 @25y vs
  4515.6 @1y) — but issuance is *already* clock-dependent there: 6259 issued
  @25y vs **18104** @1y (2.9×), because `IssueSovereignCredit`'s cap
  (`SovereignIssuanceRate × Receipts`, `Phases.cs:715`) is applied once per
  EPOCH, so a 1-year clock gets 25× the borrowing opportunities. Servicing does
  not cause this; it *amplifies* it by creating recurring deficits (issued 7225
  @25y vs 52614 @1y). Built units: 7 coarse / 9 fine with servicing off (band
  passes); 2 coarse / 21 fine with it on — the coarse side starves because it
  cannot re-borrow, and the test's `coarse×0.5 … coarse×2` band is brittle at
  such small counts. §4a made the servicing arithmetic itself clock-invariant
  (4× → ~12%); the remaining failure is **the per-epoch issuance cap**, which
  design §3 explicitly freezes ("ME's cap and floor are untouched") — so fixing
  it is a design decision, not this task's. **Flagged to the user.**
- 2026-07-17 — Task 6 (residual wiring) done, on **main folded in** (post-MC:
  branch now carries L2, CU-2, DX, MC). Pure observability wiring in
  `MetricsOps.cs` — no money moved, no serializer/version change.
  `CurrencyResidualRow` gains a `CumulativeFiatRetired` field (grouped with the
  other cumulative counters, before `Residual`/`Reserve`; the single
  construction site updated in step), the delta form gains
  `+ (cur.CumulativeFiatRetired − baseline.CumulativeFiatRetired)` — the one
  SUBTRACTIVE counter enters with a PLUS sign to cancel the Supply drop
  (`(−p) + (p) = 0`), and the baseline row carries it, exactly as `Reserve` did
  in CU-2. Doc comment extended to name the retirement term. The canary
  `PrincipalRepayment_ResidualIsExactlyTheNotYetWiredRetirement` FLIPPED (not
  deleted) to `PrincipalRepayment_ResidualIsZero_RetirementWired`, now asserting
  `residual == 0` (tol 6) — green.
  `dotnet test`: **1126 passed, 1 red** — ONLY the standing frozen-golden diff
  (`GoldenTests.ReferenceArtifact_MatchesTheFrozenGolden`, re-frozen at Task 12,
  NOT regenerated). All 6 Task-6-gap conservation tests now green.
  **32-run sweep** (first run since the money sink landed AND since MC changed
  the polity entry schedule): worst per-currency `Money.ConservationResidual`
  = **1.81e-07 abs** at Supply 5.43e+08 (cheap-credit/31337 epoch 33), worst
  **relative 2.15e-15** (cheap-credit/9091 epoch 33) — a few ULPs, FP noise, not
  a leak. Conservation holds across the merged reality, not just seed 42.
- 2026-07-17 — Task 7 (FX backing term) done: the unbacked claim book now weighs
  on its currency's FX rate (design §5). `FxOps.RecomputeRates` (`FxOps.cs:55-71`)
  reads `state.BankOf(cur.Id)` per currency (id order, P6 — safe registry-wide
  since every currency, Retired included, is founded 1:1 with a bank) and forms
  `unbacked = max(0, ClaimOnState − Reserve)`,
  `effectiveMoney = max(0, Supply) + FxBackingSensitivity × unbacked` before the
  unchanged density/rate step. Reserve now offsets the claim DIRECTLY on top of
  its sequestration effect; `unbacked` clamps at 0 so a fully-backed bank is
  inert. Class doc-comment extended with the backing block and the §5 cite. NO
  gate on lending — discipline is endogenous via FX (ME's floor stays absolute).
  **Byte-identity at the default `FxBackingSensitivity = 0`**: at 0 the term is
  `Supply + 0.0 × unbacked ≡ Supply` bit-for-bit, so every rate reproduces CU-2.
  Proven by the full suite, NOT assumed: `dotnet test` = **1129 passed, 1 red**
  — the red is ONLY the standing frozen-golden diff
  (`ReferenceArtifact_MatchesTheFrozenGolden`, first mismatch at the
  `ClaimServicingSharePerYear` knob line from task 5b, unrelated to FX; goldens
  re-freeze at Task 12, NOT regenerated). Task 6 ended 1126 passed / same 1 red;
  +3 new tests ⇒ 1129, and NO currency/FX/conservation test flipped (the 130-test
  Currency|Fx|Conservation|Sovereign|Bank filter is all-green). Every existing
  numeric FX assertion (0.5, 0.2, …) still holds unchanged. Three new tests in
  `FxRateTests`: the term BITES when the knob > 0 (unbacked claim ⇒ strictly lower
  rate, pinned at 1/3.25), a fully-backed bank is unaffected even at backing 5.0
  (unbacked clamps to 0), and an empty claim book at the default knob is
  BitConverter-identical to the pure supply/output rate. Harness `AddCurrency`
  now founds the 1:1 bank alongside the currency (mirrors `FoundCurrency`) so the
  pass can read `BankOf` — bank starts empty, rate unchanged. Scope held: knob
  stayed at 0 (task 9 activates), no touch to `Bank`/`Currency`/serializer/
  `Phases`/`MetricsOps`.
