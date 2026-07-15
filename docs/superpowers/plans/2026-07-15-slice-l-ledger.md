# Slice L ledger — locality (bodies become addressable)

Branch `slice-l-locality`, worktree `.worktrees/slice-l-locality/`.
Plan: `2026-07-14-locality-bodies-addressable-plan.md` (9 tasks).
Baseline: 900/900 tests green before any change.

## Tasks

- [x] Task 1: `BodyRef` — epoch-owned body address (Sonnet). Commits 97b0a2a..d8b1726
      (fix wave: Unity `SystemStage.cs` also consumed the deleted `OrbitRef` type via
      the `Atlas` namespace — added its own alias, mirroring `SystemQuery.cs`'s. Not
      Unity-compiler-verified in this environment; flag for a real Unity compile pass
      at slice-end eyeball.)
- [x] Task 2: Body-ref fields on Facility/Project/PopulationSegment/FleetRecord (Sonnet).
      Commit af69437. Clean, approved first review, no fix wave.
- [x] Task 3: `OrbitGeometry` — discrete OrbitDistance + local-hop knobs (Sonnet).
      Commit 5e6209f. Clean, approved. NOTE: golden test now red (KNOB dump gained 2
      entries) — expected red-window, re-freeze once at slice end.
- [x] Task 4: Serializer round-trips the four body-ref fields (Opus — serializer correctness hazard).
      Commit a8e82e1. All field indices independently re-verified by Opus reviewer, no
      off-by-ones. Minor (not blocking): FLEET/SEGMENT round-trip values and old-format
      truncation only directly tested for FACILITY/PROJECT — carry to final review.
- [x] Task 5: `SettledSystems` registry + idempotent commit + serialization (Opus —
      determinism invariant). Commit 63bcb80. Both invariants (memoize-once
      key-presence semantics; coordinate-only serialization, bodies re-derive)
      independently traced and confirmed by Opus reviewer.
- [x] Task 6: Body-assignment at groundbreaking, claim-aware (Sonnet). Commits
      1ab342e..6e9f71c. Fix wave: terminal `?? portBody` fallback wasn't claim-checked
      in any branch (mine/skimmer/agri/excavation/default) — two same-type facilities
      could still collide once preferred substrate was exhausted/absent. Restructured
      to a single claim-checked tail, falls to BodyRef.None if portBody itself is
      claimed. Re-reviewed, approved — fix structurally closes all branches.
      USER-RAISED FOLLOW-ON (deferred, not this slice): adjacent-hex spillover when
      a hex's bodies are all claimed/full — changes Facility.Hex semantics, touches
      Siting.cs, needs its own brainstorm/design pass. Flag prominently in the next
      kickoff prompt and HANDOFF.md.
- [x] Task 7: Atlas reads decided placement (Sonnet). Commit 90c3ca4. Clean,
      approved first review, no fix wave.
- [x] Task 8: Extraction reads claimed body's richness — throughline (Sonnet).
      Commits abfa98f..9124cc0. Fix wave: shared 6.0 divisor capped AgriComplex at
      neutral (Biosphere max 3 vs Size's 6-14) — split to divisor 3.0 for
      AgriComplex, 6.0 elsewhere, per user decision. Bounds re-verified both
      branches, re-reviewed, approved. ConservationTests green throughout.
      Minor (carry to final review, not blocking): non-extraction facilities also
      pick up a Size-driven richness multiplier via the port body (incidental,
      not a deliberate design signal for those types) — pre-existing since the
      original commit, not introduced by the fix.
- [x] Task 9: `Settlement.SettledHexes` sim-health metric (Sonnet). Commit cc680de.
      Brief's MetricRow snapshot was stale (predates the ME slice's two Cumulative*
      fields) — correctly appended after them, not at the brief's guessed position.
      Positional alignment independently field-by-field verified, single call site
      confirmed. Clean, approved first review.

## Gates

- [x] `dotnet test StarSystemGeneration.sln` fully green (926/926)
- [x] Determinism byte-identity (round-trip tests are the unit witnesses)
- [x] Goldens re-frozen (twice: once at Task-9 slice-end, once more after the final-review fix wave)
- [x] Fresh-eyes whole-branch review (model: fable) — found 1 Critical + 2 Important
      (RichnessModifier didn't deliver real variance for belts/wreckage/giants since
      the generator's actual Size ranges don't match the formula's assumption;
      genesis-path facilities render at deep-space instead of falling back to port
      body; richness leaked onto non-extraction facilities). User decided fixes for
      all three; fix wave landed (c107587) + re-frozen golden (e6e1610); re-review
      (fable) independently traced all three fixes against concrete values —
      Ready to merge: Yes. One minor non-blocking note carried forward: Skimmer/Mine
      landing on a fallback body of the wrong kind (via portBody) still gets a
      formula-shaped signal that isn't really there — same class as the belt fix,
      deferred.
- [~] REPL eyeball: two same-type extractors at one hex on different bodies —
      superseded by a deeper finding during the eyeball itself (see Phase 2 below).
      User found a hex with a port + facilities in a zero-body system during the
      Unity eyeball check. Root-caused: `Siting.Score` ranks hexes from regional
      raster fields entirely decoupled from `BodyGenerator`'s independent per-slot
      body-kind roll (can null out every slot) — pre-existing, not a Slice L
      regression, but Slice L's atlas work made it visible for the first time.
      Investigating this further exposed that `RichnessModifier` (the whole
      slice's stated "throughline") was never actually body-native — it's a
      bounded multiplier on unchanged hex-aggregate `CellFields` math, going
      inert (neutral 1.0) whenever there's no eligible body. User: this is not a
      minor miss, it's the slice failing its own fundamental goal. Design
      reopened — see Phase 2.

## Phase 2: body resource stock (reopens the design, corrects the throughline)

Design: `docs/superpowers/specs/2026-07-15-body-resource-stock-design.md`
(committed 645392a). Plan: `docs/superpowers/plans/2026-07-15-body-resource-stock-plan.md`
(7 tasks, committed e9514df — amended in review to also route the entry
starter-industry loop, `Phases.cs:1437-1439`, through the new `PlaceFacilityBody`
helper; the plan-authoring pass had only wired `CompleteExpedition`, missing that
every new polity's starting Mine+AgriComplex would otherwise permanently produce
zero under the new body-native model).

Model: Mine/ExcavationSite get a real finite depletable stock
(`SimState.BodyResources`, rolled once at groundbreaking from regional richness
+ per-body hash variance); Skimmer/AgriComplex get a renewable yield from the
claimed body's own real attributes (no depletion); groundbreaking now REJECTS
construction outright when an extraction type resolves no eligible body
(`BodySiting.Assign` no longer rides the port body for extraction types).
`RichnessModifier`/`ExtractionPotential` retired entirely.

### Tasks

- [x] BR-Task 1: Roll channel + `Economy` stock knobs (Sonnet). Commit 3d5847f.
      Clean, approved first review, no fix wave.
- [x] BR-Task 2: `SimState.BodyResources` + `BodyResourceOps` (Sonnet). Commit
      c9684ee. Determinism/idempotency independently traced (stateless roll,
      no double-commit path, zero-floor algebraic not clamped). Clean, approved.
- [x] BR-Task 3: `BodySiting` body-native extraction (Sonnet). Commit f0e9cc4.
      `IsExtraction` gate confirmed type-agnostic (all four types, not per-type
      patch) against the full InfraTypeId enum. RichnessModifier/BodyExtractionTests
      confirmed untouched. Clean, approved first review.
- [x] BR-Task 4: Groundbreak rolls stock + rejects bodiless extraction (Opus).
      Commit cbbf8e3. Surfaced 5 emergent-history test regressions (FineTick,
      Siege, WarDeclaration, KinClaim, Treaty ladder) — legitimate economy drift
      from facilities now getting real bodies instead of None (RichnessModifier
      no longer flat-neutral for them), NOT a per-tick depletion bug (confirmed:
      BodyResourceOps.Extract isn't wired into SupplyLands until Task 5, and
      Task 5's planned formula already scales by years-per-step correctly, no
      latent bug). All 5 re-tuned with diagnosed root causes, independently
      verified by Opus reviewer against real gating logic (WarOps.DeclareWar's
      vassal precedence, RelationsOps.BothLive, etc.) — none a goalpost move.
      Minor carried forward: FineTick's provisions tolerance now 0.85 (widened
      3x total: 0.6→0.7→0.75→0.85) — getting weak, worth splitting out if
      touched again.
- [x] BR-Task 5: `SupplyLands` depletes/yields from body, retires
      RichnessModifier/ExtractionPotential (Opus). Commits 9f4bef8..f9e3b99.
      Beyond-brief serialization fix (BodyResources was never wired into
      ArtifactSerializer — necessary, byte-identity suites can't be
      re-baselined), renamed to `bodyresources` per controller decision. 5
      emergent-history tests destabilized, all resolved with verified real
      mechanisms (not threshold-fudged) — notably a dedicated investigation
      caught and corrected the implementer's own wrong KinClaim diagnosis.
      Tick-invariance analytically + empirically confirmed. Opus review
      independently re-verified every high-stakes claim. Approved.
      NOTE: BR-Task 6 (below) is now mostly DONE as a side effect — becomes a
      light verification/extension pass, not a from-scratch build.
- [x] BR-Task 6: Serialize `BodyResources` (bodyresources v1) (Sonnet,
      re-scoped to verification). Commits cac30a1..9918b70. Verified layer
      name/version/lockstep/sort/strict-completeness all correct against real
      code — no implementation fix needed, just added the missing dedicated
      round-trip tests. Fix wave: sort-order test never exercised R as a
      discriminating key (dropped-R regression would've passed silently) —
      fixed with an R-differentiated pair, re-review traced the fix with a
      mutation-testing argument confirming it genuinely kills that regression
      class. Approved.
- [x] BR-Task 7: `Extraction.BodyStockRemaining` metric (Sonnet). Commit f1c9413.
      Field-position alignment (the exact hazard class this lineage escalates
      for) independently re-verified 16/16 fields, no drift. Clean, approved
      first review. All 7 Phase 2 tasks complete.

### Phase 2 gates

- [ ] `dotnet test StarSystemGeneration.sln` fully green
- [ ] Determinism byte-identity
- [ ] Golden re-frozen once at Phase 2 end
- [ ] Fresh-eyes whole-branch review (model: fable), one fix wave
- [ ] REPL + Unity eyeball: a Mine posts ore that draws its body's stock down
      over epochs (rich body outlasts poor); Skimmer/Agri yield steadily with no
      stock entry; a hex whose committed system holds no eligible body never
      grows that extraction facility

## Wrap-up

- [ ] Merge to main locally
- [ ] Update `docs/HANDOFF.md`
- [ ] Write population/off-lane slice's kickoff prompt (flag: adjacent-hex
      spillover when a body-poor hex's bodies are all claimed — still deferred,
      now doubly relevant given real depletable stocks)
- [ ] Sync Trello (In Progress → Eyeball/Merge Gate → Merged)
- [ ] Push only when user says to
