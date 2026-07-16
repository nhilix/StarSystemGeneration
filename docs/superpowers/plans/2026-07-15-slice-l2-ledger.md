# Slice L2 — Population & off-lane — task ledger

Branch `slice-l2-population-offlane`, worktree `.worktrees/slice-l2-population-offlane/`,
based off local `main` @ `81c03c6` (CU-1 + Slice L both merged in, unpushed).
Plan: `docs/superpowers/plans/2026-07-14-locality-population-offlane-plan.md`
(a strong draft — every file:line re-verified against current code per task).
Kickoff: `docs/superpowers/plans/2026-07-15-slice-l2-population-offlane-kickoff-prompt.md`.

## Scope-nod decisions (2026-07-15, user-confirmed)

- **Adjacent-hex spillover (Slice L follow-up #1): KEEP DEFERRING.** Own design
  question (changes `Facility.Hex` semantics, touches `Siting.cs`); not folded in.
- **Colony-founding bodiless dud (follow-up #2): CLOSE IT IN TASK 2.** Phase 2 already
  routes `CompleteExpedition` through `PlaceFacilityBody`; add reject-on-`None` there.

## Confirmed staleness (re-verified 2026-07-15, must honor in each task)

1. **RollChannel: detection roll is `ShipmentDetection = 78`, NOT 77.** Phase 2 took
   `77` for `BodyResourceStock` (`src/Core/Rng/RollChannel.cs:118`).
2. **CU-1 merged (81cea74) and touched Tasks 2/3/7 target files** —
   `MarketEngine.cs` (+105), `ProjectOps.cs` (+86), `CourierOps.cs` (+33),
   `ArtifactSerializer.cs` (at `src/Core/Epoch/`, not `src/Core/Serialization/`).
   Every line ref in those tasks is stale; Task 3 staffing now sits beside CU-1's
   wage-currency-conversion in `SupplyLands` — re-verify per task.
   **`ShipmentOps.cs` (Tasks 5/6) was NOT touched by CU-1 — clean.**
3. **Task 2 is largely done by Phase 2**: `CompleteExpedition` already assigns
   claim-aware bodies to both founding facilities via `PlaceFacilityBody`
   (`ProjectOps.cs:660,666`). Task 2 reduces to (a) the bodiless-dud reject
   (decision above) — the colony segment body is Task 1's job.

## Global constraints (from plan)

Determinism (only new nondeterminism = detection roll on ch. 78); conservation P4
(seizure is a conserved transfer, never mint/sink; staffing re-weights *who* earns,
never the total); C# 9; knob discipline (`KnobRegistry` name-sorted + `docs/TUNING.md`);
TDD + frequent commits (no Co-Authored-By trailer on in-slice commits).

## Task ledger

- [x] **Task 1** — Segments gain a body at creation (`PopulationSiting.Assign`;
  4 segment-creation sites: Phases homeworld + `FindOrFoundSegment`, ProjectOps
  colony segment, NativeOps native segment). DONE — commit `f3b2db7`, review clean
  (spec PASS + quality PASS, zero findings). Golden intentionally red mid-slice
  (only SEGMENT `Body` lines changed, reviewer-verified) — re-freeze at slice end.
- [x] **Task 2** — Colony-founding reject-on-bodiless. Impl commit `00352aa`
  (helper `FoundColonyFacilities` + groundbreaking-mirrored skip; 2 new
  `ColonyBodyTests` pass). Phase-2 claim-aware assignment confirmed already present.
  **BLOCKED on a design fork** surfaced by the impl: the skip exposed that
  `BodySiting.Assign` sites AgriComplex strictly on a biosphere body while
  `RenewableYield` floors AgriComplex at 0.3 on ANY body → a mineral colony whose
  Mine takes the only biosphere rock now loses its subsistence farm.
  `ColonyViabilityTests.MineralColony_FoundsWithAMine_AndASubsistenceFarm` and
  `FineTickTests` went green→red as a result (both trace to this change; FineTick
  is a tuned macro band, re-tune at slice end).
  **User decision (2026-07-15):** neither A nor B — deeper fix. (1) NO 0.3
  renewable floor: agri yield derives purely from biosphere+water (0 at fully
  barren) → skipping a barren/bodiless agri is now unambiguously correct. (2) NO
  cross-type body exclusivity: a rich rocky world hosts BOTH a Mine (depleting ore)
  and an AgriComplex (farming renewables). Body claims become per-resource-class:
  {Mine,ExcavationSite} share the single (hex,body) stock so they exclude each
  other; Skimmer/Agri exclude only their own type; cross-class coexists. This makes
  the mineral colony's Mine + subsistence farm co-locate on the one biosphere rock,
  so `MineralColony...` passes without weakening. Design refinement to Phase 2,
  user-approved — amend body-resource-stock spec. Extending Task 2 impl (same agent).
  **DONE** — chain `00352aa` (dud skip) → `4062d8b` (per-class claims + drop agri
  floor + doc amendments: body-resource-stock spec, infrastructure.md, BodySiting
  class-doc) → `ba00aab` (MineralColony split into two real invariant tests —
  barren→Mine-only, farmable→Mine+farm via a deterministic injected mine+garden
  system; seed-42 has 0 reachable mineral+biosphere hexes). Suite 1022 pass / 2 red
  (golden + FineTick, both deferred to slice-end re-freeze). Review clean (Opus):
  spec PASS + quality PASS, 0 Critical; Important = the FineTick watch item below.
  Minors (no fix; for final-review triage): redundant `IsExtraction` in farm guard
  (intentional symmetry w/ founding guard, ProjectOps.cs:720); MineralColony
  barren/farmable test paths asymmetric (barren=full ResolutionPhase path,
  farmable=direct helper call — acceptable given no reachable mineral+biosphere hex);
  `(int)year` narrowing cosmetic (completionYear already int upstream).
- [x] **Task 3** — Staffing weights labor by hex-hop + local-hop distance
  (`StaffingOps`, `Economy.StaffingDistanceFalloff`; `MarketEngine.SupplyLands`).
  DONE — commit `2a0f300`. Changes ONLY the production-labor magnitude (swaps the
  flat pool for `WeightedWorkforce`); `PayWages`/money flow untouched → conservation
  holds trivially. CU-1 wage-conversion sites confirmed unchanged. Suite 1024 pass /
  2 red (golden + FineTick). Review clean (Sonnet, independently re-ran tests + verified
  conservation boundary byte-for-byte): spec PASS + quality PASS, 0 Critical/Important.
  Minors (for final-review triage): `SegmentOnTheFacilitysBody_WeightsOne` passes via
  the uncommitted-system fallback (localHop=0) not via OrbitDistance-of-equal-bodies —
  asserts the right value, exercises a narrower path than its name (cheap to strengthen
  by committing the system first); `SupplyLands` now walks `state.Segments` twice per
  facility (flatLabor discarded + WeightedWorkforce) — inherited from the brief's shape.
- [x] **Task 4** — Patrol coverage falls off with orbital distance (`PatrolCoverage.At`,
  `War.PatrolCoverageFalloff`). DONE — commit `2de96a2`. New pure read fn, NO callers
  yet (Task 6 wires it in) → changed no existing behavior (suite 1026 pass / same 2 red;
  nothing else moved). `FleetRecord.Body` was pre-added in Plan 1 for exactly this.
  Review clean (Sonnet, independently re-ran + verified EpochGenesis seeds zero fleets):
  spec PASS + quality PASS, 0 Critical/Important; 2 trivial Minors (var-eco hoist nit;
  NoPatrol test's `Assert.Empty(Fleets)` coupling — both acceptable, no fix).
- [x] **Task 5** — Off-lane alternative in routing (`OffLaneRoute` + `PlanBestRoute`).
  DONE — commit `1f23962` (ShipmentOps, CU-1-clean). `PlanRoute` untouched; new methods
  no callers yet (Task 7 wires them). Suite 1029 pass / same 2 red; nothing else moved.
  Severed-check predicate matches Sail's in-flight check exactly (first-leg-only, by
  design). Review clean (Sonnet, independently re-ran + traced AddLane→live-lane): spec
  PASS + quality PASS, 0 Critical/Important. Minor (final-review triage): `OffLaneRoute`
  duplicates `PlanRoute`'s inline fallback — deliberate (brief forbade touching PlanRoute);
  natural dedupe = have PlanRoute's fallback call OffLaneRoute in a later touch.
- [x] **Task 6** — Detection roll on off-lane legs (conserved seizure), channel **78**.
  DONE — commit `c699d88` (Opus impl + Opus review). `ShipmentDetection = 78` (append-only;
  77 stays BodyResourceStock), keyed (MasterSeed, ch, EpochIndex, OwnerActorId, s.Id) like
  piracy/interdiction. Seizure block is a line-for-line mirror of interdiction's conserved
  transfer (PostSupply→prize port, portless captor takes nothing/cargo continues). Suite
  1033 pass / same 2 red. **ConservationTests GREEN, DeterminismTests GREEN.** Review clean:
  spec PASS + quality PASS, 0 Critical/Important. Reviewer verified seizure paths mutually
  exclusive (off-lane requires empty RouteLaneIds; piracy/interdiction require lane legs),
  exact-quantity conservation assertion, load-bearing tests. Minors (final-review triage):
  (1) `Math.Pow` negative-base NaN hazard at `p = 1-(1-rate*cover)^years` — UNREACHABLE at
  sane config (rate>1 or falloff<0 only), fails safe (no seizure), and PRE-EXISTING
  identically in the interdiction block (not a regression); a defensive clamp would harden
  both. (2) redundant `coveredYears>0 && maxCoverage>0` guard. (3) dual coverage compute
  (accumulator via PatrolCoverage.At, owner-attribution via inline rescan) — agree for
  BodyRef.None, unify if detection ever goes body-aware.
- [x] **Task 7** — Courier board routes off-lane when severed (`CourierOps.AcceptOpen`
  via `PlanBestRoute`). DONE — commit `4b702c2`. 2-line production change (compute
  `severed` once, swap PlanRoute→PlanBestRoute); `Accept`/escrow/CU-1 currency untouched.
  Suite 1035 pass / same 2 red; conservation green. Review clean (Sonnet, independently
  reproduced RED in an isolated pre-fix worktree): spec PASS + quality PASS, 0
  Critical/Important; 1 style-nit Minor (hardcoded ids matching fixture convention).

- [ ] **Task 8** (added mid-slice — user approved fixing the FineTick invariant break
  in-slice) — world-time cadence gate on facility groundbreaking. Per-port gate mirroring
  `Expansion.FoundingCadenceYears`; new knob `Infrastructure.FacilityGroundbreakCadenceYears`
  (default 25.0 = GenerationYears). Gate in `GroundbreakFacility` (Phases.cs:1169): hold if
  this polity has a FacilityConstruction at this port started within the cadence. Acceptance:
  FineTick telescopes (validated by a 6-14 coarse-step sweep, not band-widening);
  Conservation + Determinism green; minimal coarse-clock ripple (real sim barely changes).
  OPUS. Base a9f760b.

## Slice-end gates — progress

- All 7 tasks done + individually reviewed clean. HEAD `4b702c2`. Suite 1035 pass /
  2 red (golden + FineTick), both awaiting slice-end resolution below.
- [~] Resolve FineTick divergence — **INVESTIGATED (Opus, systematic-debugging):
  REAL invariant break, NOT benign tuning.** Full writeup:
  `scratchpad/finetick-investigation.md`. Mechanism: facility + port-raise
  groundbreaking (`Phases.cs:1169 GroundbreakFacility`, `:1205 GroundbreakPortRaise`)
  has NO world-time cadence gate — it commits one project per due plan entry PER
  EPOCH-STEP, and the plan reschedules the best candidate at placed=0 every step, so a
  finer clock breaks ground on more facilities → more shipyards → HullBatch-dominated
  divergence (coarse 2 hull-units vs fine 43). Facilities built in identical 200y
  windows: coarse 5 vs fine 15 (3×); hulls 2 vs 48 (24×). Bisection: merge-base PASSES
  (telescopes, ratio 1.4); failure appears exactly at Task 2 (`4062d8b`) — because
  per-resource-class claims removed the None-body rejection that had MASKED the extra
  fine-clock groundbreaks. So it's a PRE-EXISTING un-gated-cadence defect newly
  unmasked, not introduced by L2's logic. Fix pattern already exists elsewhere: colony
  foundings gate via `Expansion.FoundingCadenceYears` (`Phases.cs:1366-1375`), hull
  slots via a cumulative world-time clock (`Planner.cs:88-90`) — facility/port-raise
  groundbreak is the one hole. **PAUSED FOR USER DECISION: fix the cadence gate in this
  slice (scope expansion, brainstorm→impl) vs defer to its own slice + skip/split the
  guard to merge L2.** Violates [[time-not-ticks]] — durations must be world-time state,
  never step-count artifacts.
- [ ] Golden re-freeze (once).
- [ ] Determinism byte-identity check.
- [ ] Whole-branch fable review + fix wave.
- [ ] REPL eyeball (user gate) · merge decision (user gate).

## Watch items (must resolve before merge)

- **FineTick divergence (raised Task 2).** `FineTickTests.FineTick_ProjectCompletions_LandOnWorldYears_NotSteps`
  drifted hard: coarse-derived band ~[1,4] vs fine actual ~32 (an 8–32× coarse/fine
  divergence, up from ~16 earlier in Task 2). Slice L follow-up #4 already flagged this
  band as toothless (widened 4×, trips past ~6.7× divergence) and said "split into its
  own guard next time it's touched." We are touching it. **Do NOT just re-freeze it at
  slice end** — the whole-branch review MUST investigate whether this is a real
  coarse/fine consistency break (fine completing far more project-completions than the
  coarse pass predicts) or a benign downstream tuning artifact of colonies building more
  coexisting facilities. Fix if real; split the guard + re-tune if tuning.
  **Task-2 review (Opus) pinned the mechanism:** the per-class claim change is shared by
  `SpawnFacilityConstruction` (ProjectOps.cs:47) — groundbroken facilities that previously
  resolved a globally-claimed body → None → REJECTED (line 52) now get a shared body and
  proceed to BUILD. More facility completions is the intended consequence of the cross-type
  directive. Reviewer caveat: coarse denom is only 2 (band razor-thin, ratio unstable) and
  seed-42 sits in a near-floor expansion-over-construction regime. **Before slice-end
  re-freeze: instrument the per-KIND breakdown of fine's ~32 completions** — confirm they're
  not dominated by a per-step-cadenced groundbreak newly unmasked by body availability (a
  real time-not-ticks bug in shared groundbreaking, pre-existing but newly exposed) rather
  than world-time telescoping. Split into its own guard while here (Slice L follow-up #4).

## Slice-end gates

- `dotnet test StarSystemGeneration.sln` fully green (hex-tier suite never breaks;
  ConservationTests + DeterminismTests green).
- Determinism byte-identity (two runs same config; save→load→save).
- Golden re-freeze once at slice end (staffing/off-lane legitimately change output).
- REPL eyeball: blockaded lane elects an off-lane crawl; a segment's `Body` carries
  a real address.
- One fable whole-branch review + one fix wave.

## Progress log

- 2026-07-15: worktree created @ 81c03c6, baseline `dotnet test` 1014/1014 green.
  Scope nod done, two decisions locked (above). Ledger opened.
