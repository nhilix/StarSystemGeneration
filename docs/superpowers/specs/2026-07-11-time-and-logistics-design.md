# Time & Logistics — durations, capability rates, and located goods

**Date**: 2026-07-11 · **Status**: approved by user, section by section
**Mandate**: HANDOFF next-up 0 ("durations design pass") + user extension in
session: polity capability as rates/yr, tick-aware intent planning, located
sources/sinks with delivery time.

## The problem

The epoch→fine-tick move broke every "completes within the generational tick"
hand-wave galaxy-wide. Today, in `AllocationPhase` and its neighbors:

- Facilities draw their whole `BuildCost` as a lump at groundbreaking; only
  output activation waits (`MarketEngine.IsActive` date arithmetic on the
  otherwise-unused `InfraDef.ConstructionYears`).
- Port tier raises, gate pairs, and hull lay-downs complete instantly, paid in
  credits or lump goods.
- Colony convoys depart and arrive in the same step.
- War mobilization, freight delivery: instant.
- There is **no in-flight work in `SimState` at all** — nothing that says
  "this consumes A/yr and completes in year Y."
- Intent sees point-in-time treasuries, never income/production *rates*, and
  cannot plan a schedule.
- `PolityRecord.ReserveQty` is a located-nowhere global pool — goods teleport
  from it to any site.

The governing principle (user, standing): **things take time, not ticks** —
durations are world-time state, never per-step or per-generation rate caps.
This pass also subsumes the lane-branch interim compromises (founding links'
no-goods-gate exception, instant gate raising) and is what makes FineTick P7
honesty automatic.

## Decisions taken in brainstorm

| Question | Decision |
|---|---|
| Scope | **Full sweep**: everything on HANDOFF's list (construction, convoy travel, shipyard queues, manufacturing, freight delivery, war mobilization) plus anything else discovered hand-waving time. Time is the backbone of moving from tick-based to continuous play. |
| Planning horizon | **Fixed ~GenerationYears horizon, replan every tick.** A 25y tick executes the whole plan before replanning; a 1y tick executes year 1 and revises. Fine ticks track coarse naturally; plans survive tick-rate changes. (Rejected: horizon = tick length — myopic at fine tick, coarse/fine diverge in kind. Rejected: horizon = max(tick, task length) — no forward schedule.) |
| Shortfall behavior | **Priority-ordered starvation.** High-priority tasks draw first; starved tasks progress by the fraction met (60% fed → 0.6 years delivered/yr); completion year slides; nothing aborts mechanically — the next replan cancels hopeless work, sunk goods stay sunk (residue, P1). |
| Actors | **Polities + corporations**, one task machinery. No two-speed world. |
| Plan concreteness | Entries are fully concrete this slice (sited, typed, scheduled); the schema carries a kind discriminator so program-style entries can join later. **Plan-action preferences vary by species, ideology, culture, government form, and ruler** — extending `Temperament.Compose`. |
| Located goods | **Stock has an address** (user directive, mid-design): draws are local-only; remote sourcing means shipments over the lane network that take transit time. |

## Approaches considered

- **A — Project ledger + capability brief + standing plan** (chosen): every
  in-flight piece of work is a record; Perception assembles perceived rates;
  Intent emits a prioritized schedule; Allocation executes mechanically.
- **B — Per-port build queues, no polity planner**: least machinery, but no
  polity-level capability view and no cross-goal scheduling. Fixes "instant,"
  not "unplanned." Rejected.
- **C — Full flow economy**: every commitment a continuous demand flow.
  Rewrites MarketEngine, hurts chronicle causality (P4), unobservable at 25y
  steps. Deferred — see Future passes (contract economy is its successor).

---

## 1. The Project record

New `state.Projects` collection (id = creation order, P6). One record per
piece of in-flight work:

- **Identity**: `Kind` (FacilityConstruction, PortRaise, GatePair, HullBatch,
  ColonyExpedition, Mobilization — extensible), `OwnerActorId` (gets the
  result), `FunderActorId` (whose treasury/stockpiles feed it; differs for
  corp-built gates on a host polity's port).
- **Anchor**: hex + port id. The site exists at groundbreaking — map residue
  from day one; a cancelled project leaves an abandoned-works ruin with a
  date and owner of record (P1, per assets-and-investment.md).
- **Rate contract**: `PerYearBasket` (good, qty/yr), `WagesPerYear` (credits/yr
  streamed to the site's segments), `YearsRequired` (double),
  `YearsDelivered` (double). **Conservation invariant**:
  `PerYearBasket × YearsRequired` equals the lump the old code consumed —
  `BuildCost / ConstructionYears` *is* the per-year rate; both catalog fields
  finally do their jobs; goods totals across a run are unchanged.
- **Duration sources**: construction kinds read
  `ConstructionYears × TierCostFactor`; travel kinds compute years from
  distance ÷ hull speed at spawn; mobilization is a doctrine-set readiness
  ramp. **No field on a project may mention epochs or ticks.**
- **Completion payload**: fires when `YearsDelivered ≥ YearsRequired` —
  facility activates (replacing `IsActive` date-check with delivered work),
  port tier increments, lane opens, hulls commission into reserve, colony
  founds, army stands ready. Stages a chronicle event at its world-year.
- **Progress rule**: fed fraction *f* of its year-scaled basket, a project
  advances `YearsDelivered += f × YearsPerEpoch` (capped at remaining need).
  *f* = **minimum across basket goods** — the scarcest input paces the work,
  like facility upkeep already does.
- **Conquest**: projects are site-anchored state; capture transfers them at
  current progress (mirrors facilities). The conqueror's replan keeps or
  cancels.

## 2. The capability brief (Perception)

Each entered polity (and active corporation) perceives its own economy as
**rates**, assembled in Perception into `PerceptionView`:

- **Generation/yr per good, per port**: what active facilities extract and
  process per world-year at current condition/labor/grades — `SupplyLands`'
  formula evaluated as a rate, not executed. Own-side facts read fresh.
- **Income/yr**: last step's realized receipts (tax, tariffs, facility
  revenue, gate fees) normalized by that step's year span. Deliberately
  *trailing*, not clairvoyant — a blockade shows up as shrinking perceived
  capability a step later (P3 at economy scale).
- **Committed rates**: sum of per-year baskets + wages of every in-flight
  project the actor funds, plus standing upkeep.
- **Free capability** = generation and income − commitments.
- **Located stock**: per-port stockpile levels (see §4b) — the brief is
  located, so the planner sees lead times.
- **Buildable candidates**: the siting-score × price-signal scan moves out of
  `AllocationPhase.BuildFacilities` into Perception as *perceived options*
  (joining `ColonyValuation.CandidatesFor`). P2 payoff: AI and future player
  see the same candidate list and ledger — this is the 4X economy screen.

Corporations get the same brief at their scope: receipts/yr, portfolio
output/yr, committed rates, candidate investments.

## 3. The standing plan (Intent)

`PolityPolicies`/`CorporatePolicies` gain a **Plan**: a prioritized schedule
of entries covering the fixed ~`GenerationYears` horizon, emitted fresh every
Intent from the capability brief. Entry = candidate reference (sited facility
type, port raise, gate pair, hull batch, mobilization) + **target start
year** + **priority class**, plus a kind discriminator reserved for future
program-style entries.

- **Scheduler** (deterministic): rank candidates by goal-category weight ×
  candidate score; pack into the horizon so summed per-year draws of
  in-flight + scheduled work never exceed perceived free capability —
  staggered start years fall out. A polity that can't afford a starport this
  year schedules it for year 12 when the fortress frees its rates. Fixed
  horizon ⇒ 1y and 25y ticks pack the same schedule; fine tick revises more
  often.
- **Personality**: goal-category weights (expansion / military / development /
  consolidation) composed per polity from species profile, ideology, culture,
  government form, ruler temperament — extending `Temperament.Compose`.
  Faction pressure bends the weights before the scheduler runs (as
  `PressedBudget` does today).
- **Continuity**: in-flight projects enter the scheduler as committed rates
  with their existing priority unless explicitly cancelled. Replanning
  revises the future, never silently restarts the present.
- **Acts stay acts** (controller contract intact): found-colony and
  declare-war remain discrete acts; what changes is what they *spawn* —
  Resolution creates a ColonyExpedition or Mobilization project instead of an
  instant result.
- **Budget weights survive** as the coarse envelope the plan must respect
  (and factions fight over); treasuries remain the funding pools for wages
  and administered values.

## 4. Allocation execution

Opening (loan service, tribute, budget split) and closing (upkeep, reserve
decay, fleet supply) stay. The greedy while-affordable loops —
`BuildFacilities`, `RaisePorts`, `BuildLanes`, `BuildFleets` — are **deleted**
and replaced by two mechanical passes:

- **Pass 1 — advance in-flight projects**, per funder, in (priority class,
  plan order, project id) order. Each draws `PerYearBasket × YearsPerEpoch`
  **locally only** (see §4b). Fraction met scales progress, the wage stream,
  and actual consumption — a starved project doesn't hoard. Priority-ordered
  draws against shared local inventory produce the starvation cascade with no
  extra machinery: the war front drinks first, the luxury starport slows.
  Completions fire payloads and stage events.
- **Pass 2 — break ground on due entries**: plan entries whose start year has
  arrived are checked against **truth** (Move 2: site valid and unoccupied,
  treasury covers administered value, slot budgets free) and become Project
  records. A truth failure skips without charge; the next replan sees why.

Knock-ons:

- **Construction demand becomes real market demand**: `AddConstructionPull`
  sums active projects' per-year baskets at each market — a build boom raises
  alloy prices for its duration, feeding the price signal that sites the next
  mine (P5).
- **Founding links subsumed** (closes the lane-branch compromise): the colony
  expedition's departure basket includes the founding gate pair's goods,
  drawn at the staging market. On arrival the port founds and the
  founding-link project starts with goods accounted for — conservation holds
  and the gate still takes its construction years to open.

## 4b. Located goods and shipping orders

**Stock has an address.** `PolityRecord.ReserveQty/Grade` dies. Stockpiles
live at ports (per-port, per-good, banked); depot-type facilities extend
capacity / cut decay — the controller contract's "stockpile targets →
depots/reserves" line gets its mechanism. Every unit of every good is at a
port market, in a located stockpile, or **in transit**.

**Shipments are records**: origin port, destination, basket, departure year,
**arrival year** = route over `LaneNetwork` ÷ lane speed (gate tier sets
speed), off-lane legs at slow crawl. In-transit goods are conserved state —
visible on the map as freight on the lane (P1), lost to piracy, stopped by
blockade or quarantine. Severing a lane severs the *supply line*: the
fortress under construction starves at the pace of its last delivery.

**Two channels fill a project site**, both taking time and competing for
freight capacity:

1. **Market channel** (mostly built): a project's per-year basket is standing
   demand at its local market; the existing `MoveFreight` routing gains
   transit time — routed goods become shipment records arriving in a future
   year. Most project supply flows this way: booms pull imports, prices
   signal, corps profit.
2. **Requisition channel** (new): state logistics. When the plan schedules a
   project, Allocation raises shipping orders from the polity's own located
   stockpiles toward the site — bypassing price (the state moving its own
   goods), never bypassing time, route, or capacity.

**Pass 1 draws are local-only**: a project consumes what is physically at its
site (local market + local stockpile + arrived shipments). Starvation causes
are chronicle-legible: the alloys are two years out, or at the bottom of the
sea with a pirate.

**Planner consequence**: the located brief exposes lead times — the scheduler
prefers sites near supply, pre-positions stock before remote groundbreaking,
and a sprawling polity pays a real coordination tax.

## 5. System-by-system sweep

| System | Becomes |
|---|---|
| Facility construction | Project; `BuildCost / ConstructionYears` as rate; `IsActive` date-check replaced by delivered work |
| Port tier raises | Project with a **real goods basket** (machinery + alloys + exotics per yr for N yrs — today pure credits, itself a hand-wave). New catalog entries: per-tier upgrade basket + years, in knob registry + TUNING.md |
| Gate pairs | One project spanning both ends, each end drawing locally (shipments cover shortfalls); lane opens when both ends complete. Instant gate raising closes |
| Shipyard queues | HullBatch projects anchored at a shipyard; per-year basket from ship recipe; build-years per hull size in catalog; shipyard tier caps concurrent batch work, excess entries queue; hulls commission into reserve on completion |
| Colony expeditions | Found-colony act spawns a travel project (distance ÷ hull speed); basket (incl. founding gate pair) loads at staging on departure; arrival founds the port; en-route expeditions are visible, interceptable state |
| War mobilization | Declaring war / doctrine shift spawns Mobilization projects: readiness ramps over years consuming military goods/yr at war priority; fronts fight at *current* readiness — early battles use the standing force. Fleet resupply draws located stock from home port. Full front supply-lines (interdictable convoys to the front) flagged for the contract-economy pass |
| Freight delivery | §4b shipments with transit years |
| Already honest — no change | Facility production/processing (rates), research conversion, condition decay/repair, news pulses, population growth/migration |

**Residual rule**: any instant completion discovered during implementation
either becomes a project kind or gets flagged in the ledger — no new silent
hand-waves.

## 6. Determinism, persistence, REPL, tests

- **Determinism**: scheduler, draws, routing are pure deterministic math over
  ordered state — no new rolls expected. Any future roll (e.g., piracy vs
  shipments) takes a fresh `RollChannel` (next free: 75) keyed (step, actor,
  channel). Fixed orders: projects (priority class, plan order, id),
  shipments (id), plan entries (position).
- **Serialization**: three new versioned blocks — Projects, Shipments,
  located Stockpiles (replacing `PolityRecord.ReserveQty/Grade`; tests
  migrate, no adapters — greenfield rule). Plans persist like other standing
  policies so a loaded artifact resumes mid-plan byte-identically. Golden
  re-freezes once at slice end (red window inside the slice).
- **Tick honesty**: the FineTick suite gains the P7 test — run coarse (25y)
  and fine (1y) from the same artifact; construction completions, colony
  foundings, hull commissionings land within honest bands of the same
  **world-years**, not step counts. Conservation extends to in-transit goods.
- **Other tests**: starvation semantics (60% feed → 0.6 yr/yr, ETA slides);
  priority cascade (war project drinks before starport at a shared market);
  requisition transit (goods leave origin at departure, exist only in the
  shipment, land at arrival); scheduler determinism + horizon packing (never
  over-commits free capability); conquest transfer of in-flight projects;
  hex-tier suite untouched.
- **REPL surface** (eyeball gate): `eprojects [polity]` — in-flight work with
  rates, fed-fraction, ETA year; `eplan <polity>` — the standing plan with
  start years and priorities; `efreight` — shipments in transit with routes
  and arrival years; `emap works` — construction-site and freight markers.
  The taste test: watch a polity plan a 25-year build-out, throttle a lane,
  read the starvation in the ETAs.
- **P1 evidence**: construction sites, freight on lanes, mobilizing armies,
  en-route expeditions are map residue *while in flight*; every completion
  and starvation cause is chronicle-explainable (P4); the capability ledger +
  plan + project list is the player-facing economy screen at polity scope
  (P2).

## Implementation sequencing

One spec, two staged sub-slices, each with its own green gate:

1. **Stage 1 — the project ledger**: Projects collection, capability brief,
   standing plan + scheduler, Allocation passes 1–2, system sweep conversions,
   with draws against local market + (interim) the existing polity reserve
   array.
2. **Stage 2 — located logistics**: per-port stockpiles replace the global
   reserve array, shipment records, transit-time freight in Markets, the
   requisition channel, located capability brief.

## Future passes (flagged, not designed here)

- **Contract economy** (user-flagged this session): evolve the market step
  from pooled per-market clearing into standing **buy/sell contracts**
  integrated with the logistics system. The price gradient between an open
  buy at the sink and an open sell at the source is the profit motive that
  freight-line corporations mechanically follow and fulfill. Requisitions and
  market pulls both become contracts; logistics stops being routed by the
  engine and starts being *fulfilled by actors* (P5). This pass lays its
  substrate: located stock, shipments, transit time.
- **Front supply lines**: war-side deepening of §4b — convoys to the front,
  interdictable; sequenced with the contract-economy pass.
- **Program-style plan entries**: "fortify the frontier" instantiated by
  Allocation; the entry schema already reserves the discriminator.

## Design-tree amendments this slice will make

- `frame/simulation-flow.md` — Allocation/Intent phase descriptions (plan
  execution, capability brief).
- `frame/controller-contract.md` — Plan added to polity/corporation policies.
- `economy/assets-and-investment.md` — §Construction rewritten to the project
  model.
- `substrate/infrastructure.md` — port upgrade baskets; ConstructionYears now
  load-bearing.
- `frame/time.md` — durations-as-world-time-state made explicit.
- `economy/markets.md` — construction pull, freight transit time.
- `fleets/ships-and-fleets.md` — hull batches, expedition travel.
- `interpolity/war.md` — mobilization ramp.
