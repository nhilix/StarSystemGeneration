# Sim Economy Slice — Budgets, Stockpiles, Commodities, Trade, and Persistent War

Status: **draft — awaiting user review**
Date: 2026-07-08

## 1. Overview

Implements stages 2 and 3 of the epoch simulation's staging plan
(`docs/superpowers/specs/2026-07-07-regional-generation-design.md` §7.9):
**budget allocation + military stockpile** (stage 2) and **commodities, flows,
value, blockades** (stage 3), plus the parts of the parent spec those stages
presuppose — threshold-based tech tiers (§7.1), per-cell population (§7 State),
and the persistent war model with goals, weariness, and termination (§7.3,
minus vassalage).

Slice 1 shipped a deliberately minimal stage-1 loop: flat expansion budgets,
coin-flip development, and instant one-cell skirmish wars resolved by summed
development tiers. This slice replaces that loop with the parent spec's real
epoch structure — **income → allocation → action → resolution** — and hangs the
stage-2/3 mechanics off it. Stages 4–6 (relations ladder, news, POI compiler)
later append phase content to this frame without another rewrite.

Everything new surfaces in the inspector REPL (map layers, `polity`,
`chronicle`, `stats`, `cell`), honoring the parent spec's map-legibility rule.
Unity atlas parity is deliberately deferred to the next atlas batch.

## 2. Scope

**In:** phase-pipeline `EpochSim` rewrite; four-way budget allocation;
military stockpile; per-cell species-tagged population; three commodities with
per-cell production, pathed intra-polity flows, opportunistic cross-polity
trade, and per-cell route throughput; blockades as path severance; system
value; threshold-based tech tiers; persistent wars with deficit-driven goals,
fronts, weariness, victory/white-peace termination; serializer schema v4;
REPL layers/commands; invariant + shape-band + golden tests.

**Out (deferred):** vassalage (stage 4 — it is a relations state); the
relations matrix and ladder (stage 4); news/stances (stage 5); event→POI
compiler and world-state handoff finalization (stage 6); Unity atlas layer
parity (next atlas batch); non-deficit war causes (§11); hex-level highway
rendering (own follow-up spec).

## 3. Architecture: the Phase Pipeline

`EpochSim.Run` becomes a thin orchestrator. Each epoch executes four global
phases in order; every phase iterates in fixed deterministic order (cells by
`SpiralIndex`, polities by id, wars by id). All rolls stay on skeleton
`RollChannel`s keyed by (epoch, cell/polity/war id) — the stateless hash
discipline is unchanged.

| Phase | Does |
|---|---|
| **1. Income** | Per-cell production; intra-polity surplus→deficit flows over the connectivity graph; cross-polity trade; throughput accumulation; shortage effects (famine, stalls, decay). |
| **2. Allocation** | Wealth income splits expansion / development / military / war upkeep (temperament-weighted, war-overridden); stockpile growth/decay; development spending; exotics→tech investment. |
| **3. Action** | Expansion spends its budget on frontier cells (stage-1 species-affinity cost model unchanged); war declarations create `War` objects with goals. |
| **4. Resolution** | Active wars contest their fronts; cells flip by relative strength; weariness accrues; termination check; capital-loss relocation and extinction (carried from stage 1). |

Stage 5's news phase will slot in before allocation (the parent spec's loop
order: news arrival precedes decisions).

**File layout** (`src/Core/Galaxy/Sim/`, new folder): `IncomePhase.cs`,
`AllocationPhase.cs`, `ActionPhase.cs`, `ResolutionPhase.cs`, plus
`Economy.cs` — the pure functions (production potentials, consumption, flow
routing, system value, tech ladder) with no skeleton mutation, unit-testable
in isolation. `EpochSim.cs` remains the entry point and orchestrator; its
stage-1 `Expand/Develop/War` bodies are absorbed into the phases, not kept
alongside. `Affinity` stays public-internal where it is; both expansion cost
and provisions production read it.

## 4. Data Model and Schema (v3 → v4)

**`Polity` gains:**

| Field | Type | Meaning |
|---|---|---|
| `MilitaryStockpile` | double | grows with military spend, decays without; war resolution input |
| `TechTier` | int | multiplies war strength; raises development ceiling |
| `ExoticsInvested` | double | cumulative; crosses thresholds → `TechTier` |
| `Wealth` | double | carried balance; allocation's budget pool |
| `ProvisionsBalance`, `OreBalance`, `ExoticsBalance` | double | last epoch's net per good; war goals and shortage effects read these |

Per-epoch budget splits are transient — only their effects persist.

**`RegionCell` gains:**

| Field | Type | Meaning |
|---|---|---|
| `Population` | double | grows with development + provisions surplus; shrinks under famine and war scarring |
| `PopulationSpeciesId` | int | −1 when empty; single-species per cell this slice (conquest composition is stage 4) |
| `RouteThroughput` | double | last-epoch snapshot of flow magnitude transiting the cell; trade zone tags and system value read it |

`Contested` (present since slice 1, unused) becomes live: set while any
active war's front includes the cell, cleared at termination. Production
*potentials* are **not** persisted — they are pure functions of existing cell
fields (lean, metallicity, anchors, density) computed in `Economy.cs`.

**`War`** — new registry object, persisted list on the skeleton, ordered by id:

- `Id`, `AttackerId`, `DefenderId`, `StartEpoch`
- `Goal` enum: `Ore`, `Exotics`, `Chokepoint`, `Punitive`
- goal cell coordinates (the initial target cluster, ≤3 cells)
- per-side `Weariness`, per-side committed stockpile this epoch (transient)
- `Ended` flag, `Outcome` enum: `AttackerVictory`, `DefenderVictory`, `WhitePeace`

Ended wars are retained forever (the chronicle references them), mirroring
extinct-polity retention. One live war per polity *pair*; a polity may fight
several wars concurrently and pays upkeep per war.

**`GalaxyEvent`:** new types `WarEnded`, `TechAdvance`, `Famine`,
`TradeBlocked`; one new field `Detail` (int, default 0) carrying type-specific
payload (war-goal type on `WarStarted`, outcome on `WarEnded`, tier reached on
`TechAdvance`). Existing types/records untouched.

**Serializer:** schema v4. New per-polity and per-cell fields append to their
records; wars serialize as a new section between polities and events. No v3
loader (pre-release; consistent with the v2→v3 precedent). Version-literal
fixtures updated; goldens re-frozen under the red-window discipline (§9).

## 5. Economy Mechanics

Exact constants are tuned during acceptance against the shape bands (§9); this
section pins forms and invariants.

**Production** (per owned cell, per epoch):

- *Provisions* = cell habitability **through the owner's embodiment**
  (reusing `EpochSim.Affinity` — aquatics farm bright-star cells) ×
  development × population factor.
- *Ore* = metallicity × development; mineral anchors add a large flat bonus.
- *Exotics* = near-zero baseline; precursor-site anchors contribute outsized
  amounts.

**Consumption:** population consumes provisions (lithic/machine embodiments at
a steep discount, per parent §7.1); development upkeep consumes ore; tech
investment consumes exotics (via allocation).

**Flows and throughput:** per polity, surplus cells route to deficit cells
along shortest connectivity-graph paths (BFS over traversable edges,
deterministic tie-break by spiral index). Paths may not transit **blockaded**
cells: contested cells, or cells owned by a polity at war with the flow's
owner. Ordering per good: intra-polity deficits fill first; remaining
polity-level surplus then offers to graph-adjacent non-belligerent polities,
where matched complementary surpluses convert to wealth for both sides (the
hook stage 4 upgrades into trade-relation strengthening); surplus that can
reach no deficit or partner over unblockaded paths is **lost** — a blockade
hurts even when production is intact. When blockade-induced loss for a polity
exceeds a magnitude floor in an epoch, a `TradeBlocked` event fires (actor:
the blockaded polity; location: its capital cell).
Every transited cell accumulates the epoch's flow magnitude into
`RouteThroughput` (snapshot semantics: reset each income phase).

**Shortage effects** (applied in income, read all epoch):

| Deficit | Effect |
|---|---|
| Provisions | population shrinks (famine; `Famine` event above a magnitude floor), population growth stalls |
| Ore | development spending stalls; stockpile decay steepens |
| Exotics | tech investment stops (stagnation) |

**System value** = production potential + `RouteThroughput` + chokepoint
bonus. A pure `Economy.cs` function; war-goal selection maximizes it within
goal type, and the inspector exposes it.

**Wealth and income:** polity income = development sum + trade wealth, into
`Wealth`, which allocation splits. **Nothing goes negative** — deficits
manifest only as shortage effects, never negative balances (invariant test).

**Tech:** allocation routes the exotics balance into `ExoticsInvested`.
Thresholds form a geometric ladder (base × 3^tier, base a knob); crossing
rate scales with Industry. `TechTier` multiplies effective war strength and
raises the development-tier ceiling (stage 1's flat cap of 5 becomes a
function of tier).

**Population dynamics:** seeded at homeworld claim (homeworld cells start
populated; expansion-claimed cells start near-empty and grow), grows
logistically with development and provisions surplus, shrinks under famine
and war scarring. Species id = claiming polity's species this slice.

## 6. War Model (parent §7.3, minus vassalage)

**Declaration (action phase):** militancy-gated roll as in stage 1, further
gated: no new war against a polity you already fight. Goal selection is
deficit-driven — the declarer's worst commodity deficit picks the goal type
(ore war, exotics grab); with no meaningful deficit, chokepoint seizure or
punitive blockade by system value. Goal = target cell cluster (the picked
border cell + adjacent same-owner cells, capped at 3). `WarStarted.Detail`
carries the goal type — history annotations inherit it ("the Ore War of
epoch 6").

**Fronts and fighting (resolution phase):** the front = goal cells plus any
cells either side has taken from the other in this war; front cells are
`Contested` while the war lives. Each epoch both sides commit a fraction of
stockpile per active war; effective strength = committed stockpile ×
(1 + tech advantage) × militancy factor. Per contested cell, a seeded roll
weighted by relative strength decides flip / hold / stalemate. Cells
contested across multiple epochs accumulate `WarScarred`. Capital falls
(relocation + `LostCapital`) and extinction (flagged retention) carry over
from stage 1 unchanged.

**Weariness and termination:** each side accrues weariness per epoch at war —
base rate (knob) steepened by active shortages and by cells lost in this war.
A side breaks when weariness crosses its cohesion-scaled threshold *or* its
stockpile is depleted. One side breaks → the other is victor: annexes the
war-goal cells plus holds prior takings. Both break in the same epoch →
**white peace**: front freezes as-is; contested cells demilitarize carrying
`WarScarred`. Either way: `War.Ended` + outcome set, `WarEnded` event
(outcome in `Detail`), and the victor emerges with a drained stockpile —
the "exhausted victors are prime schism candidates" seed stage 4 reads.

**Upkeep coupling (allocation phase):** at war, allocation forcibly shifts
toward military and pays upkeep per active war; unpayable upkeep accelerates
stockpile decay. Long wars genuinely exhaust both sides.

**Termination invariant:** weariness accrues monotonically while at war and
thresholds are finite, so every war terminates (or is live at the final
epoch — the war-zone source, per parent §7.8).

## 7. Config Knobs (GalaxyConfig additions)

Seeded defaults, artifact-stamped like the existing knobs; deliberately few:

| Knob | Governs |
|---|---|
| `WarWearinessRate` | base weariness accrual per epoch at war |
| `StockpileDecayRate` | fractional stockpile decay per epoch without upkeep |
| `TechThresholdBase` | first tech tier's exotics cost (geometric ladder above) |
| `TradeIncomeWeight` | trade wealth vs development in polity income |
| `ProvisionsPerPop` | consumption rate — the famine dial |

## 8. Inspector Surface (REPL only this slice)

- **Map layers** (existing toggle/legend machinery): `trade` — throughput
  shading; `economy` — dominant production per cell as P/O/E glyphs with
  anchors highlighted; `war` — active fronts/contested plus war-scarred
  history.
- **`polity <id>`**: adds tech tier, stockpile, wealth, per-good balances,
  last-epoch budget split, population sum, active wars with goal type and
  weariness bars.
- **`chronicle`**: renders the new event types with existing polity/cell
  filtering; `WarEnded` prints outcome, `TechAdvance` the tier, `Famine` the
  magnitude.
- **`stats`**: economy aggregates — total production per good, famine count,
  wars started/ended, white-peace ratio, mean tech tier, throughput
  distribution summary.
- **`cell <q> <r>`**: adds population + species, throughput, production
  potentials, system value.

## 9. Testing Strategy

- **Determinism:** same config → byte-identical v4 serialization;
  load-vs-rebuild equivalence within the code version.
- **Sim invariants:** no negative or NaN budgets, balances, population,
  stockpile, weariness; every war ends before the epoch cap or is live at
  final epoch; ended wars and extinct polities retained; throughput
  accumulates only on cells lying on computed paths; a constructed
  two-polity blockade scenario delivers strictly less than its unblockaded
  twin; anchored pre-commitments still honored by hex output.
- **Unit tests (`Economy.cs`):** production forms per embodiment; BFS routing
  including blockade detours and loss; system value composition; tech ladder
  crossings; weariness monotonicity.
- **Shape acceptance bands** (reference config, the institutionalized `stats`
  lesson): polity survival count, claimed % (reopens the deferred 73.5%
  tuning conversation — budget rework changes expansion pressure; band set
  during acceptance), famine rarity, war-termination mix (both victories and
  white peaces occur — both code paths exercised at reference scale), tech
  tier spread.
- **Goldens:** red-window discipline (hex-geometry precedent) — goldens and
  version-literal fixtures break early in the branch, re-freeze exactly once
  in the final task (header v4, new polity/event counts).
- **Flatspace regression:** the Phase 1 suite (109/109 baseline) stays green
  throughout; no editor needed — all gates are `dotnet test`.
- **Live acceptance (REPL eyeball):** `map` trade/economy/war layers look
  like an economy — throughput concentrates on corridors and chokepoints,
  famines cluster in blockaded or void-poor territory; `chronicle` reads as
  a coherent wars-and-trade history; `polity` panels tell consistent stories
  (warring polities show drained stockpiles and shifted budgets).

## 10. Deferred / Follow-Up Work

- **Vassalage** (parent §7.3) — a relations state; lands with the stage-4
  relations ladder.
- **Non-deficit war causes** *(user note, this brainstorm)* — wars should not
  all be economically rational: ideological/belief-driven conflicts between
  polities with incompatible ideology seeds, and rare random spark events
  ("shot heard round the world") escalating tensions into war. Expand with
  stage 4/5 (stances and news machinery are the natural home).
- **Unity atlas parity** — trade/economy/war layers and enriched panels in
  the Unity atlas; rides the next atlas batch with its live MCP acceptance.
- **Cross-polity trade → relations** — the opportunistic-trade wealth hook
  becomes the trade→alliance ladder's mechanical basis in stage 4.
- **Route-reach tech multiplier** (parent §7.1) — meaningful once routes have
  reach limits; revisit with highways or stage 4.
- **Migration/population flows between cells** — population is cell-local
  this slice; movement is future sim-tier material.

## 11. Relationship to Prior Docs

Implements `2026-07-07-regional-generation-design.md` §7.9 stages 2–3 (with
the §7.1/§7.3 machinery those stages presuppose); no amendments to the parent
spec required. DESIGN.md roadmap: this is regional-generation progress, not a
numbered phase item; the slice list in DESIGN.md §4's regional paragraph
should note slice 2 (sim economy) when merged.
