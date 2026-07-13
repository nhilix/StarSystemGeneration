# Sim-health harness — design (2026-07-12)

The observability slice that opens the tuning program. The sim can already
be *configured* (KnobRegistry → TUNING.md) but not *measured*: macro
pathologies like the all-polities-in-debt spiral (HANDOFF flag 1) were only
found by hand-probing a single seed at merge time. This slice builds the
instrument layer — deterministic per-epoch macro metrics, a headless
knob-sweep runner over multi-seed ensembles, and the read surfaces — and
proves it by diagnosing (not fixing) the debt spiral.

Decisions locked in brainstorm (2026-07-12):

- **Tooling first**, diagnosis rides on measurement, tuning comes after.
- **Isolation protocol = multi-seed ensembles**: an experiment is baseline
  vs variant knob sets run across the same N seeds; an effect must clear
  seed noise to register. Single-seed A/B is just an ensemble of one.
- **Read surface = CSV/JSON files + on-demand dashboard**; REPL gets a thin
  `ehealth`. Files are the durable substrate; charts are generated from
  them, never committed as build products.
- **Acceptance = tooling + first diagnosis** (the debt spiral,
  characterized). The monetary *fix* is explicitly out of scope — it is the
  already-flagged credit-equilibrium slice.

## 1. Core probe — `src/Core/Epoch/Health/`

- **`MetricRegistry`** — the KnobRegistry pattern applied to observation:
  every metric is a `Family.Name` entry with a one-line doc and an accessor
  over a `MetricRow`; name-sorted; `MetricRegistryTests` enforces naming,
  ordering, docs, and accessor round-trips, exactly as `KnobRegistryTests`
  does for dials. A metric must never exist outside the registry.
- **`MetricsOps.Snapshot(SimState) → MetricRow`** — pure read-only
  aggregation: fixed iteration order, invariant culture, no mutation, no
  hash rolls, no persistence. One row per epoch.
- **`MetricsOps.MoneyRow(SimState, phaseName) → MoneyRow`** — the narrow
  fast row: credit totals by holder class (polity treasuries / corp
  treasuries / segment wealth / order-book escrow) plus outstanding loan
  principal. Captured **after every phase**, this is the phase-attribution
  instrument: which of the seven phases drains the treasuries.
- **`SimState.Health`** — an in-memory `MetricSeries` (epoch rows, phase
  money rows, per-polity rows). **Never serialized**: zero artifact-format
  churn, zero golden churn. A loaded artifact starts an empty series and
  accumulates from whatever epoch it steps from. `EpochEngine.Step` appends
  a `MoneyRow` after each phase and the full `MetricRow` + per-polity rows
  after Chronicle. Always on — the probe is cheap (linear scans of
  registries the phases already iterate) and an always-on probe cannot
  cause config divergence.

## 2. Metric catalog v1

Monetary (the debt-spiral instruments):

- Credit totals by holder class (the `MoneyRow` classes), and their sum —
  the money supply.
- **Conservation residual**: Δ(money supply) this epoch minus known mints
  plus known sinks. Implementation starts with a mint/sink inventory pass
  over the codebase (entry grants via `InitialCreditsPerPolity`, colony
  founding wealth, and whatever else turns up); the inventory is documented
  in SIMHEALTH.md. A nonzero residual is a leak or an unknown mint — the
  goods-conservation twin (HANDOFF flag 2) can follow the same pattern
  later.
- Negative-treasury count · min/median/max polity credits · loans
  issued/defaulted per epoch · outstanding principal.

Real economy:

- Per-good median reference price and dispersion (galaxy-wide, over ports
  that trade the good) · order-book fill volume · famine count · total
  population · mean SoL · project completions and abandons.

Actors and conflict:

- Live polity / corp counts · total hulls · mean fleet readiness · live
  wars · mean pair tension · graduations and coups per epoch.

Per-polity narrow table (each epoch, per entered polity): credits,
population, SoL, legitimacy. Distribution questions ("who is negative,
since when") stay answerable without re-running.

The catalog will grow during diagnosis; the registry discipline (and
SIMHEALTH.md) is the contract, not this exact list. Adding a metric is a
registry entry + doc line, never a serialization change.

## 3. Docs — `docs/SIMHEALTH.md`

TUNING.md's observability twin, same three-surface discipline (registry ·
REPL `ehealth` · this doc). Per metric: what it measures, what healthy
looks like at default knobs, and known pathologies. The debt spiral is its
first pathology entry. The money mint/sink inventory lives here.

## 4. Sweep runner — headless Inspector mode

`src/Inspector/Program.cs` dispatches: no args → REPL (unchanged);
`sweep <experiment.json>` → headless runner (new `SweepRunner.cs`).

Experiment file (JSON):

```json
{
  "name": "loan-rate-probe",
  "seeds": [42, 7, 1001, 31337, 555, 2718, 8128, 9091],
  "epochs": 40,
  "baseline": { "Economy.LoanRatePerYear": 0.02 },
  "variants": {
    "cheap-credit": { "Economy.LoanRatePerYear": 0.005 },
    "dear-credit":  { "Economy.LoanRatePerYear": 0.05 }
  }
}
```

- `baseline` overrides apply to every run; each variant layers its deltas
  on top. The baseline itself always runs as variant `baseline`. Knob names
  resolve through `KnobRegistry.Find` — unknown names refuse to run, same
  contract as artifact loading.
- Output: `runs/sweeps/<name>/<variant>/<seed>.csv` (epoch metric rows),
  `<seed>.polities.csv` (per-polity narrow table), `<seed>.phases.csv`
  (per-phase money rows), and one `manifest.json` stamping the full
  resolved knob set per variant plus seed list and epoch count.
- `runs/` is gitignored. CSVs are invariant-culture and byte-identical for
  the same experiment file (the determinism gate extends to the harness).
- Runs execute sequentially in manifest order; a 40-epoch history is fast
  enough that parallelism isn't worth nondeterministic file timestamps.

## 5. REPL surface

- `ehealth` — latest `MetricRow` (grouped by family) plus the
  negative-treasury roster with epoch-crossed-zero.
- `ehealth <metric>` — that metric's stepped-history trend as a column.
- `ehealth save <file>` — CSV export of the live session's series.

## 6. Dashboard (on demand, not a build product)

Claude generates an HTML chart artifact from a sweep directory on request:
small multiples per metric family, baseline-vs-variant overlays with
ensemble bands (min/max across seeds, median line). The CSVs are the
interface; the dashboard has no code footprint in the repo.

## 7. Gates & testing

- `MetricRegistryTests` — naming, ordering, docs, accessors.
- Probe purity: stepping with the probe (it is always on) produces a
  byte-identical `SimTraceView` render and artifact to pre-slice — the
  probe observably never perturbs. **No golden changes at all this slice.**
- Conservation residual ≈ 0 (tolerance for double accumulation) at default
  config over a full seed-42 run — or, if a genuine leak is found, the test
  freezes the *known* residual and the leak is documented in SIMHEALTH.md
  and flagged in the diagnosis report (fixing it may belong to the monetary
  slice).
- Sweep determinism: same experiment file twice → byte-identical CSVs.
- Hex-tier suite untouched; `dotnet test` green throughout.

## 8. The diagnosis (the slice's acceptance)

Run a default-knob ensemble (~8 seeds × 40 epochs). Characterize the debt
spiral:

- When do treasuries cross zero (per seed, per polity — is it universal or
  seed-personality)?
- Which phase drains them (the per-phase money rows)?
- What does total money supply do — and does the conservation residual
  expose unknown mints/leaks?
- How does `Phases.Borrow`'s lender requirement (2× principal) interact
  with the distribution — when does the last lender vanish?

Findings land in `docs/superpowers/plans/2026-07-12-debt-diagnosis.md`
with the supporting sweep name. **The eyeball gate is the dashboard
telling this story.** No knob values change this slice; proposed monetary
mechanics or golden values go into the diagnosis report as inputs to the
next slice's design.

## Out of scope

- Any fix to the credit loop (lender-of-last-resort, mints, debt relief) —
  the flagged monetary slice designs from this slice's diagnosis.
- Goods-conservation sweep (same pattern, later).
- Artifact persistence of metrics / timeline integration — stable metrics
  can graduate to an artifact layer once the vocabulary settles.
- Atlas HEALTH lens (post-K6 candidate; reads the same `MetricsOps`).

## Slice logistics

Branch `slice-sh-sim-health` from main. Ledger
`docs/superpowers/plans/2026-07-12-slice-sh-ledger.md`. Standard gates:
scope nod (done — this brainstorm), REPL/dashboard eyeball on the
diagnosis, merge decision.
