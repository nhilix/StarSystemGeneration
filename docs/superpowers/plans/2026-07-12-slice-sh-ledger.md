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

- [ ] S1 — `MetricRow` / `MoneyRow` / `PolityRow` records +
      `MetricsOps.Snapshot` / `MoneyRow(state, phase)` (TDD; pure, fixed
      iteration order)
- [ ] S2 — `MetricRegistry` (KnobRegistry pattern: Family.Name, doc,
      accessor; name-sorted) + `MetricRegistryTests`
- [ ] S3 — `SimState.Health` series + `EpochEngine.Step` hooks (money row
      per phase, full row + polity rows after Chronicle); purity gate:
      trace/artifact byte-identical to pre-slice
- [ ] S4 — money mint/sink inventory (code pass) + conservation-residual
      metric + test at default seed-42 config
- [ ] S5 — sweep runner: `Program.cs` arg dispatch + `SweepRunner.cs`
      (experiment JSON, KnobRegistry.Find resolution with unknown-name
      refusal, CSV writers, manifest.json); determinism test (same
      experiment → byte-identical CSVs); gitignore `runs/`
- [ ] S6 — REPL `ehealth` / `ehealth <metric>` / `ehealth save <file>`
- [ ] S7 — `docs/SIMHEALTH.md` (metric meanings, healthy ranges,
      mint/sink inventory, pathology entries) + TUNING.md cross-ref
- [ ] S8 — GATE: full suite green, no golden diffs, REPL surface works
- [ ] S9 — fresh-eyes whole-branch review subagent + one fix wave
- [ ] S10 — diagnosis: default-knob ensemble (~8 seeds × 40 epochs) via
      the sweep runner; debt-spiral characterization →
      `docs/superpowers/plans/2026-07-12-debt-diagnosis.md` + dashboard
      artifact (EYEBALL GATE)
- [ ] S11 — wrap-up: merge decision, HANDOFF, next kickoff prompt

## Decisions

- (running log — record deviations from spec here, flag to user)
