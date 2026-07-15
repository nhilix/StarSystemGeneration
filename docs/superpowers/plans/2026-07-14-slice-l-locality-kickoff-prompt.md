# Slice L kickoff — locality (bodies become addressable)

You are opening the locality slice: the foundation half of the
2026-07-14 locality mega-slice (population/fleet sub-domain locality,
the genesis-vs-simulation body disconnect, off-lane movement as a real
alternative — see the design spec for the full three-thread context).
Design and planning are DONE; this is a straight implementation
session. One session, lighter protocol per `/CLAUDE.md`: scope nod →
branch `slice-l-locality` (worktree, see below) → implement task-by-task
via subagent-driven-development → gates → eyeball → merge decision.

## Reading list (in order)

1. `docs/superpowers/specs/2026-07-14-locality-mega-slice-design.md` —
   the approved design: why a hex needs to become an addressable place,
   not just what changes. Read this before the plan, not instead of it.
2. `docs/superpowers/plans/2026-07-14-locality-bodies-addressable-plan.md`
   — **THE plan for this slice.** 9 tasks, TDD-structured, real file
   paths and line numbers already verified against the current
   codebase. Follow it task-by-task; do not re-derive the design.
3. `CLAUDE.md` — slice-session workflow, hard rules (determinism, P4
   conservation, `hex tier never persisted`, greenfield discipline, the
   subagent-driven-development requirement for implementation tasks).
4. `docs/superpowers/plans/2026-07-14-locality-population-offlane-plan.md`
   — skim only, for awareness of what this slice's interfaces need to
   support later. **Not this slice's scope** (see Boundary).

## What this slice actually builds (don't re-derive it)

- A new epoch-tier `SettledSystems` registry: memoizes the pure
  hex-tier generator's output the first time anything touches a hex.
  Idempotent, deterministic, re-derivable — only the *set* of settled
  hexes is ever serialized, never the bodies themselves (the hex tier
  stays never-persisted, per the hard rule).
- `BodyRef (StarIndex, SlotIndex)` — the epoch-owned body address, added
  to `Facility` and `Project` (this slice); `PopulationSegment` and
  `Fleet` get theirs in the follow-on slice.
- Facility body-assignment moves from `SystemQuery.FacilityOrbit`'s
  per-render type-affinity guess to a one-time, claim-aware decision at
  groundbreaking (`ProjectOps.SpawnFacilityConstruction`) — this is what
  fixes the two-mines-one-belt bug for free.
- A discrete `OrbitDistance` primitive (same-star slot difference,
  fixed cross-star hop cost) — the local-hop cost basis the follow-on
  slice's staffing/patrol/off-lane work depends on.
- Extraction reads the specific claimed body's richness instead of a
  hex-aggregate — the actual payoff: real body-level variance finally
  reaches the price signal.
- The atlas reads decided placement from state; it no longer guesses.
- A new `SIMHEALTH.md` settled-hex-count metric.

## Scope

Implement Plan 1 exactly as written (9 tasks). The plan's own **Global
Constraints** section is binding: determinism (fixed iteration order,
sorted-dictionary serialization, idempotent commit), P4 conservation
(this slice mints/sinks nothing), C# 9 language level (netstandard2.1 —
no C# 10+ features), serializer discipline (append-only fields, version
bump, length-guarded reader), knob/metric registry discipline. Don't
deviate from the plan's task order — later tasks depend on exact
signatures earlier tasks produce (`BodyRef`, `OrbitGeometry`,
`SettledSystems`, `BodySiting`).

**Mechanical acceptance:** `dotnet test` green (hex-tier suite never
breaks), determinism byte-identity for the same config, new goldens
frozen once at slice end (extraction output legitimately changes once
grade varies per body — expected, not a regression). New coverage per
the plan: `OrbitDistance`, hex-commit idempotency, the
two-mines-different-bodies case.

**Eyeball gate:** REPL — two same-type extraction facilities sited at
one hex should show up on *different* bodies (where the system has more
than one eligible body); `emap`/system-stage query for a settled hex
should read from the frozen registry, not regenerate a fresh guess each
call.

## Boundary (NOT this slice)

- Everything in `2026-07-14-locality-population-offlane-plan.md` —
  population segment body-refs, distance-weighted staffing, Patrol
  orbital coverage falloff, off-lane routing with the detection roll,
  courier off-lane routing. **Explicitly gated on this slice merging
  first** — its plan consumes `BodyRef`/`OrbitGeometry`/`SettledSystems`/
  `BodySiting` by name. Write that slice's kickoff prompt at this
  session's wrap-up, using the real interfaces as they actually landed
  (not the plan's pre-implementation guesses) — standard kickoff-chaining
  discipline.
- Everything in the design spec's own "Boundary (deferred, not decided
  here)" section: intra-domain population migration/passenger ships,
  local-hop atlas travel visualization, the off-lane election formula,
  local-hop cost scaling with port tier/tech.
- Multi-hop actor runs / retiring relay bids — unrelated economy slice.
- ME (monetary equilibrium) — parallel track if still in flight; take a
  worktree, never a shared checkout, and re-check `git log main` before
  any merge-out (parallel sessions can move main mid-slice).

## Worktree setup

Use the `using-git-worktrees` skill. As of 2026-07-13, isolated
slice-session workspaces have a standard home: `.worktrees/` is
gitignored specifically for this (`.gitignore`: "slice-session isolated
workspaces (using-git-worktrees skill)"). Expect the worktree to land at
`.worktrees/slice-l-locality/` — verify with the skill rather than
assuming the exact mechanics, this convention is brand new as of this
kickoff being written.

Once the worktree exists, copy gitignored files a fresh worktree needs
before any build/batch run (carried forward from prior slices' traps):
`src/Core/csc.rsp`, `unity/Packages/manifest.json`,
`unity/Packages/packages-lock.json`. Confirm this list is still current
— check the most recent slice ledger for anything added since.

## Model usage (per CLAUDE.md)

Route every task through subagent-driven-development — Sonnet default,
escalate to Opus per-task when it touches conservation/determinism
invariants directly or spans multiple `src/Core` subsystems. Concretely
for this plan: the `SettledSystems` commit/idempotency task and the
serializer changes (Task 4's field-index-counting safeguard is a real
correctness hazard, not busywork) are strong Opus-escalation candidates;
`BodyRef` itself and the atlas read-path update are more mechanical,
Sonnet-fine. Decide per task at dispatch time, not up front for the
whole slice. One fresh-eyes whole-branch review (pinned to `model:
fable`) before merge, one fix wave, per standard protocol.

## Wrap-up, in order (per CLAUDE.md)

Merge to main locally → update `docs/HANDOFF.md` (it is currently stale
— still shows the 2026-07-12 K5 state; this is a good session to bring
it current) → write the population/off-lane slice's kickoff prompt →
sync the Trello board (`StarSystemGeneration`, move the Locality
mega-slice card through Kickoff Ready → In Progress → Eyeball/Merge Gate
→ Merged as you go; the card currently sits in Backlog since it predates
this design/plan pass — move it to Kickoff Ready before branching) →
push only when the user says to.
