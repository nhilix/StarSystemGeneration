# Session Handoff — 2026-07-12 (Slice SH, Sim-health harness — MERGED)

State: `slice-sh-sim-health` merged to `main` locally at 2926928 (not
pushed — push on say-so). Gates at merge: **852/852 dotnet · ZERO golden
diffs** (the probe is provably read-only — goldens are the witness) ·
fresh-eyes whole-branch review (0 critical / 2 high / 6 medium / 7 low,
one fix wave) · user eyeball (dashboard artifact) + merge accepted
2026-07-12. ProjectSettings churn stays uncommitted.

## Slice SH — the sim-health harness (closed)

Spec `docs/superpowers/specs/2026-07-12-sim-health-harness-design.md`.
Ledger `docs/superpowers/plans/2026-07-12-slice-sh-ledger.md` (S1–S11,
review verdict, deferrals). Doc surface: **`docs/SIMHEALTH.md`** —
TUNING.md's observability twin.

- **The probe** (`src/Core/Epoch/Health/`): `MetricRegistry` (KnobRegistry
  pattern over observation), `MetricsOps` (pure aggregation), always-on
  engine hooks — a `MoneyRow` per phase (phase attribution of treasury
  motion), full `MetricRow` + per-polity rows per epoch, into
  `SimState.Health` (in-memory ONLY, never serialized — zero format churn;
  loaded artifacts start blank and accumulate as stepped).
- **Conservation**: the entry endowment is the sim's ONLY mint
  (InitialCreditsPerPolity + HomeworldSegmentSize×InitialWealthPerPop per
  PolityEmerged). Holder classes include courier fee escrow and in-flight
  expedition purses. `Money.ConservationResidual` ≈ 1e-8 across 32
  histories; `ConservationTests` freezes it.
- **Sweep runner**: `dotnet run --project src/Inspector -- sweep
  <experiment.json>` — baseline + knob variants × seed ensemble, LF CSVs
  byte-identical per experiment, manifest stamps APPLIED knob values,
  unknown names/reserved 'baseline' refuse. `runs/` gitignored and
  disposable; experiments worth keeping are committed (the diagnosis one
  is at `docs/superpowers/plans/2026-07-12-debt-diagnosis-experiment.json`).
- **REPL**: `ehealth` (snapshot + debt roster with negative-since) ·
  `ehealth <metric>` (trend) · `ehealth save <base>` (3 CSVs).

## The treasury-spiral diagnosis (the slice's product)

`docs/superpowers/plans/2026-07-12-debt-diagnosis.md` + dashboard
artifact (claude.ai/code/artifact/7f691c48-7b98-4816-be32-80ba7854e8a1).
Headline: **structural, universal, conserved**. Allocation budgets
`max(Credits, Receipts)` every epoch (ensemble drain −32.3M vs −5.6M
Markets); the supply parks in unspent pools + household wealth; the
2×-lender gate kills the credit market at epoch 1–4 in every seed, so
**`Economy.LoanRatePerYear` is currently a dead knob** (cheap-credit
variant byte-identical to baseline); 10× starting credits only delays the
dive ~3 epochs. Fix levers + mechanical acceptance criteria are in the
report — the fix slice re-runs the same committed sweep.

## Carried / flagged

1. **Monetary equilibrium — the fix slice, kickoff ready**:
   `docs/superpowers/plans/2026-07-12-slice-me-kickoff-prompt.md`.
2. **SH deferrals**: expedition purses valued at CURRENT ColonyCost
   (needs a serialized Project field — rides whichever slice adds mid-run
   knob mutation; documented in SIMHEALTH.md); O(events²) snapshot scan
   over the event log (trivial at 40 epochs).
3. **CE carried debt** (ledger C17): relay bids until the multi-hop
   trader slice; courier allocation fee-blind; capital-goods chains
   anemic; RepriceAsks re-anchoring.
4. Timeline branch switch-back UI · unbounded keyframe memory (K4).
5. Per-lens readability deep-dives — backlog.
6. Menu F1–F4 stubs; NEW GALAXY → atlas seed handoff (post-K).

## Worktree / environment traps

K4 ledger's list still applies (manifest/csc.rsp copies, CRLF goldens,
PowerShell stdin mangling — bash `printf`, Write tool over Set-Content).
New from SH: `runs/` is disposable (a review subagent may clean it — never
keep the only copy of anything there); the health series is in-memory
(step before `ehealth`); `python3` exists for CSV analysis.

## Next up

1. **Slice K5 (System stage & closeout)** — if not already in flight:
   `docs/superpowers/plans/2026-07-12-slice-k5-kickoff-prompt.md`.
2. **Slice ME (Monetary equilibrium)** — parallel-safe with K5 (worktrees):
   `docs/superpowers/plans/2026-07-12-slice-me-kickoff-prompt.md`.
3. **Slice K6 (The economy surfaces)** — AFTER K5:
   `docs/superpowers/plans/2026-07-12-slice-k6-kickoff-prompt.md`.
4. **Multi-hop actor runs** over perceived books (retires relay bids) —
   unscheduled; now measurable with the sweep harness when it comes.
5. User read-through of the design specs — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings
stays uncommitted; parallel slices take worktrees; every new `src/Core`
file gets a two-line `.meta` with a fresh guid; the design is the spec —
deviations amend `docs/design/` in-branch, flagged. New: tuning
conclusions clear the ensemble bar (SIMHEALTH.md) before landing in
TUNING.md.
