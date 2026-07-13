# Slice ME kickoff — monetary equilibrium (the credit loop closes)

You are opening the monetary/credit-equilibrium slice (HANDOFF flag 1,
carried since the contract economy merged). Slice SH built the instrument
layer and ran the diagnosis; this slice designs and lands the fix. One
session, lighter protocol per /CLAUDE.md: scope nod → branch
`slice-me-monetary` (worktree!) → implement from the design tree → eyeball
→ merge decision.

## Reading list (in order)

1. `docs/superpowers/plans/2026-07-12-debt-diagnosis.md` — THE evidence
   base: the spiral is structural. Read the "inputs to the monetary
   slice" list last; those are inputs, not decisions.
2. `docs/SIMHEALTH.md` — the metric vocabulary and sweep runner you will
   measure the fix with; the "treasury spiral" pathology entry is the
   before-picture.
3. `docs/design/economy/markets.md` §Credit (and §Household income) —
   the living design this slice will amend in-branch.
4. `docs/TUNING.md` — Economy family (LoanRatePerYear/LoanTermYears are
   currently dead knobs; InitialCreditsPerPolity; LaborShare).
5. Code: `Phases.cs` AllocationPhase (the `max(Credits, Receipts)`
   budget at ~:369, `Borrow` at ~:590, `ServiceLoans` at ~:540),
   `PolityRecord` (the four points pools), `Planner`/`ProjectOps`
   (how pools actually get spent).

## The diagnosed mechanism (don't re-derive it)

- Allocation budgets `max(0, max(Credits, Receipts))` × six budget
  shares every epoch — the treasury can never accumulate; ensemble
  phase attribution: Allocation −32.3M vs Markets −5.6M.
- Pools accrue ~2× faster than the planner spends them; a third of the
  money supply parks there, another ~40% in household wealth.
- `Borrow` needs a lender at 2× a 1.2×-hole principal; the last
  qualifying lender vanishes at epoch 1–4 in every seed. Loans then
  never exist → LoanRate is inert.
- Money is CONSERVED (residual ~1e-8) — this is circulation, not leakage.

## Scope (design first, then implement — amend docs/design/ in-branch)

Design the equilibrium mechanism from the diagnosis inputs; the four
levers named there (bound the allocation base · drain idle pools ·
recirculate household wealth · a credit mechanism that can exist in
equilibrium) are candidates to weigh, not a checklist to implement.
Whatever lands must keep: time-not-ticks (rates per world-year),
conservation (P4 — new mints/sinks must enter the SIMHEALTH inventory
and the residual formula in the same commit), determinism, and the
tick-honesty discipline (P7).

**Mechanical acceptance (from the diagnosis, run before the eyeball):**
re-run the committed sweep
(`docs/superpowers/plans/2026-07-12-debt-diagnosis-experiment.json`) —
- `Polity.NegativeTreasuries` breathes: polities go under AND recover
  across the ensemble (deficit financing stays a feature);
- a live loan market past epoch 4 somewhere in every history's first
  half, `Money.LoanPrincipal` nonzero somewhere in the second half;
- `Economy.LoanRatePerYear` variants produce DIFFERENT histories
  (the dead knob comes alive);
- conservation residual stays ~0 (extend the mint/sink inventory if the
  design adds real mints — e.g. fiat issuance — rather than forcing it).

**Eyeball gate:** an updated dashboard from the re-run sweep telling the
after-story (the SH dashboard is the before), plus `ehealth` on a stepped
sim.

## Boundary (NOT this slice)

- Multi-hop actor runs over perceived books / retiring relay bids (the
  designed next economy slice — separate).
- The M4 carried debt (expedition purses valued at current ColonyCost)
  unless your design adds mid-run knob mutation anyway.
- K5/K6 atlas work — parallel track, take worktrees, never a shared
  checkout.
- No new UI; REPL + sweep + dashboard are the surfaces.

## Traps learned in SH (beyond the K4 ledger's environment list)

- Gitignored `src/Core/csc.rsp` + `unity/Packages/manifest.json` +
  `packages-lock.json` must be copied into a fresh worktree.
- `runs/` is gitignored and DISPOSABLE — anything worth keeping
  (experiment files, analysis scripts) gets committed under
  `docs/superpowers/plans/`; a review subagent may clean `runs/`.
- The health series is in-memory only: a loaded artifact starts blank —
  step it (or sweep fresh) before reading `ehealth`.
- Adding a metric = MetricRow field + registry entry + SIMHEALTH.md line;
  `MetricRegistryTests` enforces the rest. Never serialize metrics.
- The Allocation credit-leak comment at Phases.cs:381 (compound
  assignment vs in-call mutation) is a real pattern — treasury flows
  through the book can pay the payer; evaluate calls BEFORE `-=`.
