# Slice T2 — located logistics: task ledger

Branch `slice-t2-located-logistics` off main `0702f86`. Spec: `docs/superpowers/specs/2026-07-11-time-and-logistics-design.md` §4b (+ §5 freight row, §6). Kickoff: `docs/superpowers/plans/2026-07-11-time-stage2-kickoff-prompt.md`. Scope nod given 2026-07-11 (no worktree — no parallel session).

Golden red-window is OPEN inside the slice; goldens re-freeze ONCE after the
final review's fix wave. Hex-tier suite never breaks. Determinism ×2 at gates.

## Grounding decisions (from the code read, before task 1)

- **Stockpile home**: per-port stock lives on `Port` (`StockQty[]`/`StockGrade[]`,
  goods-indexed) — owner is implicitly `Port.OwnerActorId`, so conquest,
  federation absorption, and schism move stock *by moving the port*: no
  transfer bookkeeping at all. Serialized as `STOCK` lines beside `MARKET`
  (markets layer version bump); `RESERVE` lines die with
  `PolityRecord.ReserveQty/Grade`.
- **Shipment**: new record + `SimState.Shipments` (id order, P6): origin port,
  dest port, owner actor, channel (Freight/Requisition), per-good qty+grade,
  departure year, arrival year (double), lane route (ids). Trailing versioned
  `shipments` layer modeled on the projects layer.
- **Transit math**: per-lane leg years = hexes / (FreightHexesPerYearBase ×
  `LaneMath.TransitSpeed`); off-lane legs at OffLaneFreightHexesPerYear crawl.
  Both knobs registered + TUNING.md.
- **Arrivals** process at Markets open (before SupplyLands): freight-channel
  deposits into the destination market; requisitions into the port stockpile.
  A severed/quarantined lane on the remaining route stalls the shipment
  (arrival slides by the blocked span); piracy loss (if a roll is warranted)
  takes RollChannel 75 keyed (step, actor, channel).
- **Per-end gate draws**: GatePair keeps ONE project; Feed draws half the
  basket at each end's market+stockpile; fed fraction = min across both ends
  (a half-built highway opens no lane).
- **Founding cadence normalization**: no new state — the controller reads the
  owner's most recent ColonyExpedition project `StartedYear` from
  `state.Projects` and holds fire within FoundingCadenceYears (world-time).
- **Hull slot floor**: replace `Max(1, tier·rate·span)` with a persistent
  fractional-throughput accumulator (serialized), consumed when slots emit.

## Tasks

- [x] **T1 — located stockpile substrate.** COMPLETE. Suite 663/664 ×(golden
  = the sanctioned red window; re-freeze at T12). All reserve sites migrated
  (MarketEngine target-demand/release/procure per port; FleetOps home-port
  stock; WarConduct defender-port larder; Federation/Graduation blocks
  DELETED — stock moves with the port; ProjectOps Feed local-only with
  owner-gated stock draw; RESERVE lines dead, STOCK lines live, markets v2).
  `PolityRecord.ReserveQty/Grade` deleted. Knobs: DepotDecayFactor,
  StockCapPerPortTier/PerDepotTier (+TUNING.md).
  **T7's hull-slot fix pulled forward**: located yard stock fed the fine-tick
  slot-floor inflation (fine completed 10 batch records vs coarse 1 — root-
  caused via a main-worktree A/B diagnostic: coarse was identical on main;
  fine batches newly complete because per-port larders protect the yard's
  components from remote drains). Fixed with a STATELESS world-time slot
  clock in Planner (floor(rate·year) telescopes exactly across tick sizes —
  no persistent accumulator needed); FineTick completions test amended to
  count UNITS (hulls per batch), since batch-record granularity is cadence-
  dependent by design. Coarse 5 units vs fine 4 — honest. T7 still owes the
  founding-cadence normalization + band tightening review. `Port.StockQty/StockGrade`;
  Depot mechanism (capacity per port tier + depot tiers; decay cut); per-port
  `DecayReserves` at Allocation close; migrate ALL reserve sites:
  `MarketEngine` stockpile-target demand (per port) / `ReleaseReserves` (port
  → own market) / `Procure` (buy into local port stock); `FleetOps.DrawUpkeep`
  (home-port stock); `WarConduct.SiegeThreshold` (defender port stock);
  `FederationOps` merge (stock stays put — delete reserve merge);
  `GraduationOps` schism (seceding ports carry stock — delete split);
  serializer STOCK write/load + RESERVE delete; `PolityRecord.ReserveQty/Grade`
  deleted; tests migrated. Gate: suite green except golden/carried.
- [x] **T2 — shipments + transit.** COMPLETE. Shipment record + SimState
  registry (in-flight only; NextShipmentId keeps identity; shipments layer
  v1); Dispatch/DispatchVia with route-priced leg years (FreightHexesPerYear-
  Base × TransitSpeed; off-lane crawl); sub-step transits deliver immediately
  (no record — coarse blur); Advance at Markets open (arrivals land BEFORE
  supply/draws), stalls at severed/quarantined/dead legs; piracy channel 75
  (loot to the hunting band's haven, band credited as supplier).
- [x] **T3 — MoveFreight transit conversion.** COMPLETE — Arbitrage routes
  through DispatchVia on its own lane; costs settle at departure, the sale
  lands with arrival.
- [x] **T4 — Pass-1 local draws + per-end gate draws.** COMPLETE (local draws
  landed in T1's Feed rewrite; owner-gated site-stock fallback). GatePair
  feeds per end, scarcer end paces; AddConstructionPull registers half at
  each end (skipping this killed every frontier pair — the full-run world
  went dead until the demand signal followed the draw).
- [x] **T5 — requisition channel.** COMPLETE for in-flight projects (plan-
  entry pre-positioning folded into T6's located-brief work). Allocation
  raises orders from own ports' stock toward under-covered sites (gate pairs
  both ends); quartermaster stores (provisions/fuel/parts/armaments) keep
  their target share — draining them wrecked every navy in the first
  attempt; construction stock moves freely. Orders capped at the route's
  weakest-lane capacity over the provisioning window; sources in port-id
  order (nearest-first = contract-economy refinement, flagged).
  **Found + fixed a pre-main dead knob**: `Budget.Reserves` (0.10) was never
  accrued or spent — the seed-42 golden holds ZERO RESERVE lines; polity
  procurement always lost to the drained credit balance. Stage 2 gives it
  its mechanism: `PolityRecord.ReservePoints` (POLITY tail, actors v7),
  accrued in the budget split, spent by Procure, split/merged/conserved
  everywhere the other treasuries are. `Shipment` record, `SimState.Shipments`,
  routing over `LaneNetwork` with transit years, arrival processing at Markets
  open, blockade/quarantine stall, serialization layer, fixed iteration by id.
  Tests: requisition transit (goods leave at departure, exist only in the
  shipment, land at arrival), routing determinism, conservation extended to
  in-transit goods, blockade stall.
- [ ] **T3 — MoveFreight transit conversion.** Arbitrage's routed goods become
  Shipment records arriving in a future year (costs settle at departure as
  today); ReleaseReserves/Procure stay local (same-port, no transit).
- [ ] **T4 — Pass-1 local-only draws + per-end gate draws.** Project Feed:
  site market + site port stockpile only (arrived shipments already landed);
  global-reserve fallback deleted; GatePair draws per end. Tests:
  starvation-by-lead-time (remote site starves at the pace of its last
  delivery), per-end gate draw.
- [ ] **T5 — requisition channel.** Allocation raises shipping orders from the
  polity's own port stockpiles toward project sites (in-flight shortfalls +
  pre-positioning for due-soon remote plan entries) — bypasses price, never
  time/route/capacity. Deterministic source ordering.
- [x] **T6 — located capability brief.** COMPLETE. `PortBrief.Stock`
  (Perception copies the larder — the view never aliases state); planner
  supply lean via `Controller.PlanSupplyWeight` (score × 1−w+w·coverage,
  coverage = worst-good share of the whole build basket in the site larder);
  `Planner.EntryBasketPerYear` shared with the quartermaster; requisitions
  pre-position due-soon plan entries (GroundBroken guard against
  double-cover). Suite 676/679 (golden window + 2 carried shape reds).
- [x] **T7 — fine-tick cadence normalizations.** COMPLETE. Founding cadence:
  `Expansion.FoundingCadenceYears` (25) enforced in `TryFound` from the
  owner's latest ColonyExpedition StartedYear — no new state, coarse behavior
  unchanged, fine founds at the same world pace (also throttles turn-back
  contention). Hull slot floor landed in T1 (stateless world-time clock);
  band tightening deferred to T12 with the tuning wave.
- [x] **T8 — residue quartet.** COMPLETE. White-peace revert verified ALREADY
  CLOSED by the stage-1 fix wave (F6 routes through TransferPort). Founding
  kit tier-scaled (`RequiredGateTier` at dispatch, mirrors BuildLanes; −1 →
  tier-3 kit) and recorded as CARGO on the expedition project (PerYearBasket
  doubles as the hold for travel kinds — excluded from construction pull and
  commitment briefs); turn-back banks the kit at the staging larder (neutral
  0.5 grade — the draw's blend isn't stored; reviewer note). Completion
  STATE stamps interpolated (CommissionedYear at windowStart +
  remaining/fedFraction; staged events still take Chronicle's step year —
  flagged, not built). Construction pull tapers to remaining years.
  **The taper + kit fixes recovered both carried shape reds** — suite
  683/684, golden window only.
- [x] **T9 — corp packing (scoped minimal).** COMPLETE. `InvestFacilities`
  packs the new build's rate (goods+wages/yr) beside committed rates under
  the trailing income via `BriefFor`, floored at one build (young corps
  bootstrap). Full corp StandingPlan deliberately NOT built — corps become
  fulfillment actors in the contract-economy pass; flag carried there.
- [x] **T10 — REPL surface.** COMPLETE. `efreight` (channel, route,
  cargo, sailed/total, live ETA, STALLED on a closed leg, owner); `emap
  works` (#=sites incl. both gate ends, >=shipments interpolated + convoys,
  *=ports); help text updated; smoke on seed 42 shows real off-lane
  requisitions crawling to remote sites.
- [ ] **T11 — fresh-eyes whole-branch review + one fix wave.**
- [ ] **T12 — tuning wave + goldens.** Dotted-domains eyeball lever
  (PortRaisePlanScore / planner weights — judged by emap, not a metric); war
  believability check on seed 42; golden re-frozen ONCE; determinism ×2;
  hex-tier untouched.
- [ ] **T13 — wrap-up docs.** Design-tree amendments (`economy/markets.md`
  freight transit, `economy/assets-and-investment.md` located draws,
  `frame/controller-contract.md` depot/stockpile mechanism,
  `substrate/infrastructure.md` Depot); HANDOFF rewrite; **contract-economy
  kickoff prompt**; user eyeball + merge decision.

## Carried / flagged (running)

- Golden red (sanctioned window) — re-freeze once at T12 after the fix wave.
- **Carried reds to T12 tuning** (root-caused as shape drift, not defects):
  `ExpansionTests.FullRun_EstablishesColonyPortsBeyondHomeworlds` (44 ports
  vs >45) and `GenesisShapeTests.FortyEpochHistory_StaysAlive` (57 ports vs
  67 polities) — colonization pace sits a hair under the acceptance bars
  after the located rewiring. Diag trend is HEALTHY (stock banks, lanes
  unstall, hull batches 49 vs 26 pre-channel); the throttle is colony-hull
  scarcity (D'Hondt slots go to freight/warships) plus high expedition
  turn-back contention. T5/T6 siting changes will move these again — tune
  once at T12 (colony shipbuilding weight / candidate contention).
- Expedition turn-back rate is high (~30 spawned → few foundings; targets
  collide at coarse-step lag). Review/tuning candidate, possibly candidate
  filtering against in-flight rival expeditions (P3 question — convoys are
  public residue).
- Requisition sourcing is port-id ordered, not nearest-first; per-order
  capacity cap approximates shared lane capacity — both flagged for the
  contract-economy pass.
- T7 scope now: founding-cadence world-time gate + FineTick hulls-band
  tightening (the slot floor itself landed in T1).
- Coarse-tick built-world output is thin galaxy-wide (1 facility groundbreak
  per 50y in the seed-42 continuation — PRE-EXISTING on main, verified by
  A/B). Watch it at the T12 eyeball; may fold into the dotted-domains tuning.
