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
- [ ] **Task 4** — Patrol coverage falls off with orbital distance (`PatrolCoverage.At`,
  `War.PatrolCoverageFalloff`). Gate: coverage + knob green.
- [ ] **Task 5** — Off-lane alternative in routing (`OffLaneRoute` + `PlanBestRoute`).
  Gate: off-lane + existing routing suites green.
- [ ] **Task 6** — Detection roll on off-lane legs (conserved seizure), channel **78**.
  OPUS escalation. Gate: detection + ConservationTests + knob green.
- [ ] **Task 7** — Courier board routes off-lane when severed (`CourierOps.AcceptOpen`
  via `PlanBestRoute`). Re-verify against CU-1's CourierOps changes. Gate: courier +
  conservation green.

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
