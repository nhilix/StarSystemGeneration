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
- [ ] Task 7: Atlas reads decided placement (Sonnet)
- [ ] Task 8: Extraction reads claimed body's richness — throughline (Sonnet, verify ConservationTests stays green)
- [ ] Task 9: `Settlement.SettledHexes` sim-health metric (Sonnet)

## Gates

- [ ] `dotnet test StarSystemGeneration.sln` fully green
- [ ] Determinism byte-identity (round-trip tests are the unit witnesses)
- [ ] Goldens re-frozen once at slice end
- [ ] Fresh-eyes whole-branch review (model: fable), one fix wave
- [ ] REPL eyeball: two same-type extractors at one hex on different bodies

## Wrap-up

- [ ] Merge to main locally
- [ ] Update `docs/HANDOFF.md`
- [ ] Write population/off-lane slice's kickoff prompt
- [ ] Sync Trello (In Progress → Eyeball/Merge Gate → Merged)
- [ ] Push only when user says to
