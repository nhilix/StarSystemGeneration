# Sim health — the macro observation reference

TUNING.md's observability twin. Every macro metric lives in one index:
`MetricRegistry` (`src/Core/Epoch/Health/MetricRegistry.cs`), which drives
three surfaces so they can never drift apart:

- **The sweep CSVs**: the metrics header IS the registry (name-sorted);
  adding a metric is a registry entry, never a serialization change.
- **The REPL**: `ehealth` prints every metric with its live value and
  one-line doc; `ehealth <metric>` renders its trend; `ehealth save <base>`
  exports the session's series.
- **This document**: what each family means, what healthy looks like at
  default knobs, and the known pathologies.

The probe (`MetricsOps`) is always on: `EpochEngine.Step` appends a money
row after every phase and a full metric row + per-polity rows after
Chronicle, into `SimState.Health`. Pure read-only aggregation — it can
never perturb (the goldens are the witness). The series is **never
serialized**: a loaded artifact starts blank and accumulates as it steps.

## Running experiments

`dotnet run --project src/Inspector -- sweep experiment.json` — a baseline
and named knob-variants across a seed ensemble (sim-health spec §4):

```json
{
  "name": "loan-rate-probe",
  "seeds": [42, 7, 1001],
  "epochs": 40,
  "radius": 21,
  "baseline": {},
  "variants": { "dear-credit": { "Economy.LoanRatePerYear": 0.05 } }
}
```

Knob names resolve through `KnobRegistry` then `GalaxyKnobRegistry`;
unknown names refuse to run. Output lands in
`runs/sweeps/<name>/<variant>/<seed>[.polities|.phases].csv` plus a
`manifest.json` stamping the full resolved knob set — byte-identical for
the same experiment file. **The isolation protocol is the ensemble**: an
effect must clear seed personality (5–16 polities per seed) to count;
never conclude from one galaxy.

## The money vocabulary (the holder classes)

Money is conserved (P4): **three declared mints** exist — the one-time entry
endowment (`Economy.InitialCreditsPerPolity` +
`Expansion.HomeworldSegmentSize × Economy.InitialWealthPerPop` per
`PolityEmerged` event); bounded reactive **sovereign issuance**, the second
mint (`Economy.SovereignIssuanceRate` × the epoch's own receipts, run last in
`AllocationPhase`'s per-polity loop once every bill is paid — the backstop that
covers a residual shortfall); and the always-on **steady issuance** channel,
the third mint (`Economy.SteadyIssuanceRate` × every polity's own receipts every
epoch, minted into Credits and flowed through the allocation base so it funds
real investment — the growth-linked monetary expansion, not an emergency patch;
monetary-
equilibrium design §5). Everything else moves credits between these
classes:

| Class | Where it sits |
|---|---|
| `Money.PolityCredits` | Treasury lines (`PolityRecord.Credits`) — negative = deficit financing |
| `Money.PolityPools` | Allocated budget waiting to be spent (expansion/development/military/reserve points) |
| `Money.CorpCredits` | Corporation books |
| `Money.SegmentWealth` | Household wealth (wages accumulate here; consumption and the wealth levy spend it) |
| `Money.FactionWealth` | Faction war chests (appeasement in, corp capitalization out) |
| `Money.OrderEscrow` | Credits held by resting buy orders (drawn whole at post) |
| `Money.CourierEscrow` | Courier fees in flight (post → delivery/refund) |
| `Money.ExpeditionPurses` | Founding purses aboard in-flight colony expeditions (`ColonyCost` each) |
| `Money.CumulativeFiatIssued` | Not a holder — running total minted by bounded reactive sovereign issuance, the second mint the residual formula nets out |
| `Money.CumulativeSteadyIssuance` | Not a holder — running total minted by the always-on steady issuance channel, the third mint the residual formula nets out |

`Money.LoanPrincipal` rides beside them as a *claim*, not a holder — loans
move credits between ledgers; the principal is memory.

**`Money.ConservationResidual`** is the leak detector: this epoch's supply
delta minus the epoch's endowment mint, its reactive sovereign-issuance mint,
and its steady-issuance mint (the three declared mints). Anything beyond double-accumulation noise is
an unknown mint or leak — find it before trusting anything else the run
says. `ConservationTests` freezes the residual within 1e-6 of zero
(relative to supply) across the full seed-42 default history; in practice
it sits at ~1e-8.

Known **designed sinks and limits**:

- A **cancelled colony expedition** loses its purse with the convoy
  (`SpendTreasury`'s expedition case never refunds) — prints as negative
  residual when it fires. It didn't fire on seed 42 × 40 epochs at
  defaults; if a sweep prints residual dips, chase this first.
- Couriers do **not** leak: every retirement path (delivery, loss,
  expiry) pays out or refunds the fee before the record retires. The
  HANDOFF's stalled-InTransit flag is about *locked* escrow on a dead
  lane, which the `CourierEscrow` class keeps counting — held, not lost.
- **Cargo seizures do not sink goods.** Piracy (channel 75), war
  interdiction (76), and off-lane smuggling **detection** (78) each take an
  in-transit shipment's cargo and re-post it on a captor's port book via
  `PostSupply` — a conserved redistribution of goods (P4), never a mint or a
  sink; no money moves. A captor with nowhere to land a prize (a portless
  interdictor or patrol owner) simply takes nothing, and the cargo sails on.
  These touch goods, not credits, so they leave `ConservationResidual`
  (a money detector) untouched.
- Expedition purses are valued at the **current** `Expansion.ColonyCost`;
  changing that knob while expeditions are in flight mis-books the
  residual by Δcost × in-flight count until they land. Latent today (no
  path mutates knobs mid-run — the sim's own refund/landfall code shares
  the assumption); stamp the purse on the `Project` when a mid-run knob
  surface arrives.

## Phase attribution (`<seed>.phases.csv`)

Holder-class totals after every phase. Read a treasury mystery by
differencing consecutive rows inside one epoch: Markets moves sale
proceeds and taxes; Allocation moves treasury → pools → sellers/factions
and services loans; Resolution founds and fights; **Interior** runs the
entry loop, migration, and demographics. Entry endowments land in
Interior, so epoch-0 rows through Resolution read all zero.

## Healthy vs pathological at defaults

What a healthy seed looks like (calibration prose, not assertions):

- `Money.ConservationResidual` ≈ 0 always.
- `Segment.MeanSoL` drifting up from 0.5 toward 0.6+ as trade thickens;
  a galaxy-wide slide below ~0.3 means the economy is starving people.
- `Polity.NegativeTreasuries` occasionally nonzero (deficit financing is
  intentional) but **recovering**; the pathological signature is the whole
  roster negative and monotonically deepening.

### Pathology: the treasury spiral (resolved — slice ME)

**Resolved (slice ME):** the circulation trap diagnosed below is fixed by
`docs/superpowers/specs/2026-07-13-monetary-equilibrium-design.md`.
`AllocationPhase` budgets off `Receipts` alone — it no longer reads
`Credits` into the allocation base, so a positive treasury stops being
swept to zero every epoch. A standing `Operations` budget share stays
liquid in `Credits` as the margin that pays upkeep, loan service, and
tribute. Idle Expansion/Development/Military pools and household wealth
above a per-capita floor recirculate instead of parking forever. And a
bounded reactive sovereign mint (`Money.CumulativeFiatIssued`,
`Economy.SovereignIssuanceRate`) covers the true end-of-epoch shortfall up
to a receipts-scaled cap — the second declared mint, netted out of
`Money.ConservationResidual` above. A separate always-on **steady issuance**
channel (`Money.CumulativeSteadyIssuance`, `Economy.SteadyIssuanceRate`) mints
a small fraction of every polity's receipts each epoch into the allocation
base — the third declared mint, growth-linked rather than reactive, so the
money supply expands in step with real output instead of only being patched
during shortfalls. `Phases.Borrow`'s lender search now also scans corporation
books, and a borrower-side debt-to-income gate (`Economy.MaxDebtToIncomeRatio`)
refuses new credit to a polity whose open-loan principal already exceeds a
bounded multiple of its trailing income — a credit-score check that keeps loan
principal from stacking to absurd multiples of a small economy. See
`docs/design/economy/markets.md` §Credit for the amended spec. The diagnosis
below is kept as the evidence record it was built from.

Seed 42, radius 8, defaults: every entered polity's treasury crosses zero
in **epoch 1** and deepens monotonically (−41.6k summed by epoch 11)
while `Money.PolityPools` (+19.6k) and `Money.SegmentWealth` (+22.8k)
hold nearly all of the supply. No leak — a circulation trap:

1. `AllocationPhase` budgets `allocatable = max(0, max(Credits,
   Receipts))` and spends it into pools/appeasement/research — a
   positive-balance polity budgets its **whole treasury** each epoch, a
   negative one keeps budgeting full receipts (deficit financing by
   design), so the treasury line ratchets downward.
2. `Economy.LaborShare` (0.4) moves sale value into segment wealth, and
   household spending only partially recirculates to state-taxable flows —
   wealth pools in households while `Segment.MeanSoL` can still starve.
3. `Phases.Borrow` requires a lender holding **2× the principal** (which
   is itself 1.2× the hole); once every treasury is negative no lender
   exists, and the polity-to-polity credit market is permanently dead.

Diagnosis: `docs/superpowers/plans/2026-07-12-debt-diagnosis.md`. The fix
is out of scope for slice SH (the flagged monetary/credit-equilibrium
pass).

## Extraction (locality — body resource stocks)

`Extraction.BodyStockRemaining` sums `SimState.BodyResources[...].Quantity`
across every depletable body — the remaining stock behind every Mine or
ExcavationSite facility currently drawing from a body deposit.

**Healthy shape**: rises when new deposits are rolled and groundbroken
(a fresh Mine/ExcavationSite starts pulling from a body no prior facility
had touched), falls monotonically between founding waves as active
facilities dig their bodies out. A mature, mine-heavy galaxy trends down
between founding waves; a galaxy still expanding into fresh systems shows
a sawtooth (rises at each wave, falls as it's worked).

**Known open question**: no eviction or relocation is proposed yet for a
body that runs dry — a facility sitting on a depleted deposit is not
currently reassigned or torn down. This metric exists to give the
depletion rate evidence before any relocation mechanic is designed: if a
long sweep shows most stock exhausted early with facilities idling on
dead bodies, that's the signal to design relocation, not a bug here.

## Settlement (locality — frozen hex-tier state)

`Settlement.SettledHexes` counts `SimState.SettledSystems` — hexes whose
full star system has been committed (frozen, memoized) because something
touched them: construction, a population act, an off-lane visit
(`SystemRegistry.Commit`, locality design). It is the metric that makes
hex-tier growth visible at the macro level.

**Healthy shape**: monotonic non-decreasing (commits are permanent —
`SystemRegistry` never evicts), rising as more of the galaxy gets
developed or visited, and bounded above by the galaxy's total hex count.
A flat line all run means nothing outside the seeded homeworlds ever got
touched; a curve that outpaces `Segment.Population`/`Polity.Live` growth
by a lot may mean visitation (not just settlement) is driving commits
faster than expected.

**Known open question**: no eviction is proposed for committed systems —
memory growth here is unbounded by design (locality design §7) and is
meant to be caught by evidence, not assumed safe. If a long sweep shows
`Settlement.SettledHexes` growing without bound relative to the galaxy's
actual developed footprint, that is the signal to revisit eviction, not
a bug in this metric.

`Settlement.Outposts` counts **living** (non-graduated) `SimState.Outposts`
records — settlements founded when a population segment elects to relocate
to a worked satellite hex within a port's domain (domain-hex-expansion
design §3, Stage 2: "pop follows work"). It rises as domains fill in and
falls by one every time an outpost graduates (Stage 3) — the same
settlement then counted by `Settlement.GraduatedPorts`/`Settlement.Ports`
instead, so the pair reads as a handoff rather than a double-count.

**Healthy shape**: rises and falls as a domain's interior fills in
(permanently subordinate density, never promoted) while its frontier
outposts occasionally graduate into a new port. Graduation is intentionally
uncommon (design §4 "On rarity") — do not expect this column to fall often.

`Settlement.GraduatedPorts` counts `SimState.Outposts` records with
`Graduated == true` — outposts promoted into real starports through Stage
3's infill graduation (domain-hex-expansion design §4), the densification
counterpart to an expedition-founded port. Cumulative: an outpost's
`Graduated` flag never resets, so this column never falls, and every
increment pairs with a `Settlement.Outposts` decrement and a
`Settlement.Ports` increment in the same epoch.

**Healthy shape**: monotonic non-decreasing, and — given the map's
currently flat/sparse economic geography (design's Forward roadmap) —
expected to stay small (rare graduations) relative to `Settlement.Outposts`
and `Settlement.Ports` for the foreseeable term. A flat zero line all run
is not itself unhealthy; it means no outpost has yet cleared the frontier
gate, which is by design the common case.

## Adding a metric

1. Extend `MetricRow` (or `MoneyRow`) in `MetricsOps.cs` — levels and
   counts only, a pure function of current state (deltas are the reader's
   derivative).
2. Add the registry entry (name-sorted) with a one-line doc.
3. Document it here: meaning, healthy range, what moves it.

`MetricRegistryTests` enforces the discipline. A metric must never exist
outside the registry.
