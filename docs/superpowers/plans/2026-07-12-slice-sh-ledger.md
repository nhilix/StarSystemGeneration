# Slice SH ledger — sim-health harness (2026-07-12)

Spec: `docs/superpowers/specs/2026-07-12-sim-health-harness-design.md`.
Branch `slice-sh-sim-health` (worktree `.claude/worktrees/slice-sh-sim-health`,
branched from local main cb85495). Baseline: 832/832 green.

Money holder classes discovered in the state-model pass (the MoneyRow
vocabulary — all conserved credit stores):

- Polity `Credits` (can be negative — deficit financing)
- Polity investment pools: `ExpansionPoints` + `DevelopmentPoints` +
  `MilitaryPoints` + `ReservePoints` (drawn from Credits in Allocation —
  conserved, so they're money parked outside the treasury line)
- Corporation `Credits`
- Segment `Wealth`
- Faction `Wealth` (war chests; capitalize corps at graduation, P4)
- Buy-order `EscrowCredits` (the book's held money)
- (Loan principal tracked beside these as a claim, not a holder)

Early spiral suspect (to be quantified, not fixed, in S10):
`AllocationPhase` budgets `allocatable = max(0, max(Credits, Receipts))` —
a positive-balance polity budgets its whole treasury per epoch; a negative
one still budgets full receipts, and `Credits -= allocatable × Σshares`
digs deeper. Interacts with `Phases.Borrow`'s 2×-principal lender gate.

## Tasks

- [x] S1 — `MetricRow` / `MoneyRow` / `PolityRow` records +
      `MetricsOps.Snapshot` / `Money(state, phase)` / `PolityRows` (123d474)
- [x] S2 — `MetricRegistry` + `MetricRegistryTests` (af6b7fb)
- [x] S3 — `SimState.Health` series + `EpochEngine.Step` hooks; purity
      witnessed by goldens/determinism staying green (845/845)
- [x] S4 — mint/sink inventory + conservation residual (7117580):
      entry endowment is the ONLY mint; ExpeditionPurses + CourierEscrow
      join the holder classes; residual ZERO across seed-42 defaults
- [x] S5 — sweep runner (52a4821): MetricCsv in Core (testable), JSON
      shell in Inspector; smoke sweep byte-identical across reruns
- [x] S6 — REPL ehealth ×3 verbs (66d022b); 12-epoch probe already shows
      the spiral: treasuries negative from epoch 1, pools+households hold
      the supply
- [x] S7 — SIMHEALTH.md + TUNING.md cross-ref (92db73c)
- [x] S8 — GATE: 852/852, zero golden diffs, REPL surface exercised
- [x] S10 — diagnosis (ran before S9, see Decisions): 32-history
      ensemble; findings in 2026-07-12-debt-diagnosis.md (a50165f);
      dashboard artifact published (EYEBALL GATE — pending user);
      experiment JSON committed beside the doc
- [x] S9 — fresh-eyes review: 0 critical / 2 high / 6 medium / 7 low;
      conservation inventory independently verified clean; ONE fix wave
      applied (H1 ehealth-save crash, H2 reserved 'baseline' variant,
      M1/M2/L3 SIMHEALTH corrections, M3 JSON refusal, M5 manifest
      stamps applied values, M6 artifact-based purity witness, L1
      invariant culture, L2 manifest LF, L5 name sanitation, L7 usage
      line). DEFERRED: M4 purse-at-current-ColonyCost (needs a Project
      field = serialization churn; documented in SIMHEALTH.md as a
      limit, carried to the mid-run-knob slice); L4 O(events²) snapshot
      scan (trivial at 40 epochs); L6 dot-less-name guard (registry test
      pins the convention).
- [x] S11 — wrap-up: eyeball + merge accepted 2026-07-12 (dashboard
      artifact 7f691c48); merged to main locally; HANDOFF updated; the
      monetary-equilibrium kickoff prompt chained.

## Decisions

- S10 diagnosis runs BEFORE the S9 fresh-eyes review (ledger-order
  deviation): probe gaps the diagnosis exposes should be fixed inside the
  review's one wave, not after it.
- Diagnosis ensemble carries three credit-relevant variants beyond
  baseline (flush-start ×10 initial credits, lean-labor 0.2, cheap-credit
  0.005/yr) — inputs to the monetary slice's design, no knob changes land
  this slice.
- MetricCsv lives in Core (testable, deterministic strings); the
  Inspector keeps only JSON parsing + file I/O.
