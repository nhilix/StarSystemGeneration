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

- [ ] **Task 1** — Segments gain a body at creation (`PopulationSiting.Assign`;
  4 segment-creation sites: Phases homeworld + `FindOrFoundSegment`, ProjectOps
  colony segment, NativeOps native segment). Gate: `PopulationSitingTests` green,
  interior/native suites green.
- [ ] **Task 2** — Colony-founding reject-on-bodiless (decision above) + verify the
  claim-aware body assignment Phase 2 already added. Gate: colony/project suites green.
- [ ] **Task 3** — Staffing weights labor by hex-hop + local-hop distance
  (`StaffingOps`, `Economy.StaffingDistanceFalloff`; `MarketEngine.SupplyLands`).
  Re-verify against CU-1's wage-conversion. Gate: staffing + conservation + knob green.
- [ ] **Task 4** — Patrol coverage falls off with orbital distance (`PatrolCoverage.At`,
  `War.PatrolCoverageFalloff`). Gate: coverage + knob green.
- [ ] **Task 5** — Off-lane alternative in routing (`OffLaneRoute` + `PlanBestRoute`).
  Gate: off-lane + existing routing suites green.
- [ ] **Task 6** — Detection roll on off-lane legs (conserved seizure), channel **78**.
  OPUS escalation. Gate: detection + ConservationTests + knob green.
- [ ] **Task 7** — Courier board routes off-lane when severed (`CourierOps.AcceptOpen`
  via `PlanBestRoute`). Re-verify against CU-1's CourierOps changes. Gate: courier +
  conservation green.

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
