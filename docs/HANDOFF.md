# Session Handoff — 2026-07-11 (Time & logistics Stage 1 on `slice-t1-project-ledger`)

State: branch `slice-t1-project-ledger` complete and green (**657/0**, twice
for determinism; hex-tier untouched; goldens re-frozen after the final-review
fix wave — the red window closed), **awaiting the user's REPL eyeball + merge
decision**.
`main` at `dd8457f` (the Stage-1 plan commit). ProjectSettings churn
uncommitted.

## Time & logistics Stage 1 — the project ledger (spec → plan → implementation)

Spec `docs/superpowers/specs/2026-07-11-time-and-logistics-design.md`
(§§1–6 = Stage 1; §4b located logistics = Stage 2), plan
`docs/superpowers/plans/2026-07-11-time-stage1-project-ledger.md`, ledger
`.superpowers/sdd/progress.md` (per-task, with the carried reds and flags).

The epoch→fine-tick move had broken every "completes within the generational
tick" hand-wave; the governing principle is **things take time, not ticks** —
durations are world-time state, never per-step or per-generation rate caps.
Stage 1 makes that real for every kind of in-flight work through one
mechanism, the **project**.

- **The record** (`src/Core/Epoch/Project.cs`): `ProjectKind`,
  `ProjectPriority`, `Project` (PerYearBasket / WagesPerYear / YearsRequired /
  YearsDelivered / LastFedFraction / StartedYear), held in `SimState.Projects`.
- **ProjectOps** (`src/Core/Epoch/ProjectOps.cs`): Spawn/SpawnAt,
  SpawnFacilityConstruction, SpawnGatePair (a founding link is goods-free per
  spec §4 — the expedition ships the kit, best-effort drawn at the staging
  market at departure), SpawnExpedition, SpawnHullBatch; **AdvanceAll**
  (priority-ordered starvation — fed fraction = min across goods AND the wage
  stream; progress += f × span); **Complete** (commissions facilities via
  `Facility.CommissionedYear`, raises port tiers, opens lanes, commissions
  hulls at accumulated component grade, founds colonies on arrival incl. the
  failed-founding ColonyCost refund); Cancel.
- **Capability brief** (`src/Core/Epoch/CapabilityOps.cs`):
  `ConstructionCandidatesFor` (top-3 per-port perceived candidates — the old
  `CanAfford` stock gate is deleted); `BriefFor` → `CapabilityBrief` (trailing
  IncomePerYear, GenerationPerYear estimate, CommitmentBriefs);
  `PortBrief(PortId, Tier, YardTiers)`.
- **Standing plan** (`Plan.cs` + `Planner.cs`): `StandingPlan` of
  `PlanEntry(Kind: Facility/PortRaise/HullBatch, Priority, StartYear absolute,
  …)`; fixed GenerationYears horizon; packs entry cost/yr against perceived
  IncomePerYear; D'Hondt hull batches; temperament-weighted scores;
  `PolityPolicies.Plan`; `GenesisController` emits it; serialized as PLANE
  lines (actors **v6**).
- **AllocationPhase**: SpawnMobilizations (war ramp,
  `PolityRecord.Mobilization`, peace decay) → Groundbreak (truth checks, spawns
  due entries) → BuildLanes (pass 1 founding links goods-free; pass 2
  densification streams honestly) → … → `ProjectOps.AdvanceAll`. **Deleted**:
  the greedy BuildFacilities / RaisePorts / BuildFleets loops,
  GatePairGoodsPresent, BuildGate.
- **Port raises cost real goods**: an Alloys/Machinery/Refined-Exotics basket
  × tier over `Expansion.PortUpgradeYears` (5) — knobs in TUNING.md
  (`Expansion.PortUpgrade*`).
- **MarketEngine**: `IsActive = CommissionedYear ≥ 0`; `AddConstructionPull`
  sums in-flight project baskets (the dead pull knobs are gone);
  `FleetOps.WarStrength` scales by `1 + (MobilizationFactor − 1) × Mobilization`.
- **Colony expeditions** travel at `Fleet.ExpeditionHexesPerYear` (6); the
  convoy hex interpolates en route; arrival founds (or fails gracefully,
  refunding ColonyCost).
- **Conquest**: `WarConduct.TransferPort` transfers in-flight projects with a
  captured port (ColonyExpedition excluded; Mobilization projects CANCEL on
  capture — a readiness ramp is polity state, not site-anchored work). White
  peace reverts ports through the same seam, so project ownership reverts too.
- **Serializer**: actors **v6** (POLITY + LastIncomePerYear + Mobilization,
  PLANE lines), facilities **v2** (CommissionedYear), corporations **v3**,
  trailing **projects layer v1**.
- **REPL**: `eprojects [actorId|all]`, `eplan <actorId>` (ETA under current
  starvation).
- **Tuning wave**: `WarTensionFloor` 0.55→0.35, `WarAppetiteThreshold`
  0.60→0.38 (the slowed economy lowered the tension ceiling). Wars on seed 42
  after the fix wave: **8 declared / 5 settled**; 202 lanes / 198 ports at
  y1000.
- **Final-review fix wave** (one whole-branch review, one wave, 8 new tests):
  captured mobilizations cancel instead of gifting the attacker the surge;
  corp dissolution / nationalization / federation absorption sweep in-flight
  projects (no zombie work holding gate slots and facility caps); Groundbreak
  honors `PlanEntry.StartYear` via `SpawnAt` (the staggered schedule is real
  at coarse tick); yard tiers cap concurrent hull batches; an in-flight
  founding link counts as connected (no duplicate goods-free pairs).
- **FineTick P7 honesty test** pins built-world completions
  (facilities/ports/hull-batches) coarse-vs-fine within an honest band;
  expansion/logistics foundings are deliberately excluded (decision-cadence
  divergence, per spec).
- **Design tree amended** to the project model (this session's docs commit):
  `frame/simulation-flow.md` (Perception capability brief + Allocation plan
  execution), `frame/controller-contract.md` (standing plan on polity/corp
  policies), `economy/assets-and-investment.md` (§Construction → project
  model), `substrate/infrastructure.md` (ConstructionYears load-bearing +
  port-upgrade basket), `frame/time.md` (durations-as-world-time-state),
  `economy/markets.md` (construction pull), `fleets/ships-and-fleets.md` (hull
  batches, expedition travel time), `interpolity/war.md` (mobilization ramp).

## Deliberately deferred / flagged

- **Stage 2 — located logistics** is the whole §4b of the spec, deliberately
  out of Stage 1: per-port stockpiles replacing the global
  `PolityRecord.ReserveQty/Grade` pool, `Shipment` records + transit years over
  `LaneNetwork`, `MoveFreight` transit conversion, the requisition channel,
  per-end gate draws, the located capability brief. Kickoff prompt:
  `docs/superpowers/plans/2026-07-11-time-stage2-kickoff-prompt.md` (complete
  scope + the hand-off interfaces + the flagged gaps below).
- **Two fine-tick invariance gaps** (flagged in the FineTick honesty test body,
  `tests/Core.Tests/Epoch/FineTickTests.cs`): the controller commits one
  founding per decision step (a finer clock founds more often over the same
  world-time), and the Planner's `Max(1, tier·rate·span)` hull-batch slot floor
  fires a unit batch every step at fine tick. Both want a world-time
  normalization in Stage 2; neither hides the failure the test guards.
- **Project cancellation stages no chronicle event** (no fitting existing
  `WorldEventType`; inventing one was out of slice scope) — abandoned works
  are P1 residue only via the uncommissioned facility row for now.
- **Future passes flagged in the spec** (not designed yet): the contract
  economy (buy/sell contracts fulfilled by freight-line actors — located stock
  + shipments + transit are its substrate), front supply lines (interdictable
  convoys to the front), and program-style plan entries (the entry schema
  already reserves the kind discriminator).

## Next up

0. **REPL eyeball + merge decision for THIS branch** (the taste gate): run the
   sim on seed 42, `eprojects all` / `eplan <polity>`; the throttle test —
   quarantine a lane feeding a construction site and watch the ETA slide.
   Merge `slice-t1-project-ledger` to main locally on the nod; push only when
   the user says so.
1. **Time & logistics Stage 2 (located logistics)** — fresh session, point it
   at `docs/superpowers/plans/2026-07-11-time-stage2-kickoff-prompt.md`.
2. **Slice K2 (Lens catalog)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-11-slice-k2-kickoff-prompt.md`.
3. Then K3 (selection & panels), K4 (timeline — may parallel K3 in a
   worktree), K5 (system stage & roadmap close). Governing plan:
   `docs/superpowers/plans/2026-07-11-slice-k-roadmap.md`.
4. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged unless noted)

Lighter protocol per /CLAUDE.md (scope nod · eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings stays
uncommitted; bash printf for REPL piping; parallel slices never share a
checkout — take a `git worktree` each; every new `src/Core` file gets a
two-line `.meta` with a fresh guid; every calibration constant in a knob
registry + TUNING.md. The design is the spec — a deviation amends the affected
`docs/design/` doc in the same branch (this slice amended eight). Golden regen
one-liner and older conventions: `git show 27fefe7~1:docs/HANDOFF.md`.
