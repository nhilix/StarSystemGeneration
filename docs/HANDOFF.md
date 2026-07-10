# Session Handoff — 2026-07-09 (Slice D: Segments & markets — merged)

State: `main`, pushed (carries slices A+C+B+D). Tests 286/286 green —
hex-tier suite untouched at 100%. ProjectSettings churn remains uncommitted
as always.

## What this session did: Slice D of the epoch-sim rebuild, merged

The economy animates — the first slice where goods actually move. Ledger
(the full task/decision/surprise record):
`docs/superpowers/plans/2026-07-09-slice-d-ledger.md`.

- **Market per port** (`src/Core/Epoch/Market.cs`): per-good price,
  inventory (qty + mean grade), last-cleared, black book. Market index ==
  port id, created with every port.
- **The market step** (`MarketEngine.cs`, run by MarketsPhase in fixed
  order): supply lands (terrain-graded extraction, recipe processing on
  working capital, margin-gated, price-throttled) → demand assembles
  (population bands × per-capita knobs, wealth-backed; industrial inputs +
  upkeep; construction pull; procurement deficits; re-export term) →
  freight (reserve release, lane arbitrage within the fleet-capacity stub,
  procurement — absorption-capped, legality/tariffs, realized-net gated) →
  price drift (rate-limited, **import-parity capped over lanes**: severed
  lanes lift the cap, which is the blockade spike) → clearing (band
  priority, famine/SoL consequences) → pool distribution (tax → port
  polity, labor share → households, rest → suppliers). Credits conserve
  **exactly to the entry mint** across whole histories (shape gate).
- **Segments deepened** (`PopulationSegment.cs`): culture (registry, id ==
  species id until splits) + 4-axis ideology + SoL + wealth +
  LastSubsistence (cross-step state — serialized). Interior: growth =
  f(SoL, subsistence, embodiment) against shared port caps, famine shrink,
  migration over lanes (refugees ×8, off-lane escape, diasporas, wealth
  travels), ideology drift.
- **Allocation** (`Phases.cs`): budget weights split epoch *receipts*
  (deficit-financed development); lanes/tiers/facility construction pay
  construction wages (treasury spending is somebody's income — money is
  never destroyed); facility siting = C's scores × price signal ÷
  same-type saturation, costed at administered base prices, drawing market
  + banked polity reserves; upkeep pro-rata with condition drifting toward
  the met fraction; reserve decay by perishability; simple credit (borrow
  from whoever holds surplus, defaults seize collateral).
- **Colonial viability** (user-driven eyeball waves): expeditions ship the
  equipment matched to the site (food-security premium 1.25×, farm seeds
  alongside extraction); the genesis AI holds expansion below realm
  subsistence 0.8; colonies found with the expedition cost in settler
  pockets.
- **Knob discipline (new, hard)**: every calibration constant lives in a
  config knob family, indexed by `KnobRegistry` (~75 dials, name → doc →
  get/set), serialized as name-sorted `KNOB|Family.Name|value` lines
  (config layer v3 — the format never reshapes again), dumped by the REPL
  `knobs [filter]` command, documented with consequences in
  **`docs/TUNING.md`**. KnobRegistryTests enforce it.
- **Artifact**: 12 layers — config v3, actors v2 (policies + credits),
  segments v2 (identity layers), appended markets layer
  (CULTURE/MARKET/RESERVE/LOAN). Gates: byte-identity, save∘load identity,
  **load-then-continue == straight-run byte identity**, id==index
  validation, version/unknown-knob refusal. Golden:
  `tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt` (regenerated
  deliberately per history-changing commit through the slice).
- **Events**: economic 202–206 (FamineStruck, FacilityBuilt, LoanIssued,
  LoanDefaulted, MigrationWave) — next economic free: **207**; military
  400s untouched. `RollChannel` next free: **41** (D's economy is
  roll-free).
- **REPL**: `market <portId>` · `emap price [good]` · `lanecut <a> <b>` ·
  `estep [n]` · `knobs [filter]`. Eyeball-accepted 2026-07-09 after three
  fix waves (colonial viability · mid-chain industrialization · knob
  centralization).
- **Design-doc amendment** (in-branch, flagged): `economy/markets.md`
  market-step order — freight moves before the price drift so the drift
  reads realized supply; an import-fed port prices its arrivals, a
  blockaded one their absence.

## Next up

1. **Slice E (Fleets)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-09-slice-e-kickoff-prompt.md`
   (complete: the D handoff is baked in — the fleet-capacity stub location,
   the waiting shipyard/armaments economy, knob discipline, next-free
   values, macro-economics lessons).
2. **Parked calibration question**: catalog machinery-upkeep coefficients —
   upkeep is the economy's dominant sink (100 farms out-eat the foundry
   base). Tracked in the D ledger + TUNING.md; E's fleet upkeep lands on
   the same markets, so E likely absorbs the calibration pass.
3. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · REPL eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings stays
uncommitted; bash printf for REPL piping; parallel slices never share a
checkout — take a `git worktree` each; **every calibration constant goes in
KnobRegistry + TUNING.md** (never a bare const). Older carried minors: see
`git show a1f5843~40:docs/HANDOFF.md`.
