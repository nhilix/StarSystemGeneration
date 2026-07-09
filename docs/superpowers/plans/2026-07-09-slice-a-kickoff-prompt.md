# Slice A Kickoff — Session Prompt

You are starting **Slice A (Foundations)** of the epoch-sim implementation
roadmap. Your job this session: produce the full task-level implementation plan
for Slice A using the superpowers:writing-plans skill, get user review, then
execute it via superpowers:subagent-driven-development.

## Read first, in this order

1. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — the governing
   meta-plan: slice table, **transition rules** (greenfield, no adapters), gates.
2. `docs/design/README.md` — the design-tree map and conventions.
3. The frame docs (the constitution Slice A implements):
   `docs/design/frame/principles.md`, `time.md`, `simulation-flow.md`,
   `controller-contract.md` — plus `actors.md` and `space-and-travel.md` for
   context (implemented in Slice B, but A's types must not contradict them).
4. `docs/design/narrative/chronicle-and-poi.md` §"The event grammar" — Slice A
   implements event grammar v2.
5. Current code you will build alongside (reference, not preservation):
   `src/Core/Galaxy/GalaxyConfig.cs`, `GalaxyEvent.cs`, `EpochSim.cs`,
   `src/Core/Galaxy/Sim/*.cs`.

## Slice A scope (from the roadmap)

- **World-year rates + GalaxyConfig reshape**: all rates expressed in
  world-years (epoch = ~25y integration step, a knob); config gains the design's
  knob families (genesis, economy, sim) with seeded defaults, artifact-stamped.
- **Event grammar v2**: `(id, world-year, clock stratum, type, actors[],
  location, magnitude, valence, visibility, typed payload)`; eight type
  families; visibility public/regional/secret; index-building as views
  (per-place, per-actor).
- **Seven-phase EpochSim skeleton**: Perception → Markets → Allocation → Intent
  → Resolution → Interior → Chronicle, running over a new sim-state container.
  Phases execute and log; most are empty — the frame that later slices fill.
- **Controller interface types**: policies/acts records per
  `frame/controller-contract.md`; `Decide(perceivedState) → (policies, acts)`
  shape; a trivial default AI so the loop turns.

**Boundary:** the prototype sim stays in-tree untouched this slice (Slice B
deletes it). The new frame is fresh code beside it — no adapters, no wiring into
the old skeleton build, no serializer changes (new artifact format is Slice B).
Deterministic and unit-tested standalone.

## Rules that govern (do not re-derive)

- **Greenfield** (roadmap rule 1): no compatibility with prototype behavior or
  goldens. **Hex-tier suite stays green** (roadmap rule 2) — you're not touching
  it, verify anyway.
- Determinism discipline: stateless hash rolls keyed (step, actor id, channel),
  fixed iteration order.
- Gates per slice: `dotnet test StarSystemGeneration.sln` green; new goldens
  frozen at slice end (red-window inside the slice); REPL surface — for A this
  is minimal (a command that steps the new frame and prints the phase/event
  trace).
- Process: brainstorm is DONE (the design is the spec — do not re-open design
  questions; deviations require a design-doc amendment in the same branch).
  writing-plans → user reviews plan → subagent-driven execution → final
  whole-branch review (fable) + one fix wave → finishing-a-development-branch →
  merge to main locally. Push only when the user asks.
- Dispatch conventions (from `docs/HANDOFF.md`): implementer reports include
  verbatim test-summary lines; "gate mandatory, never kill processes" language
  in every dispatch prompt; every task names EVERY file it touches; plans
  pre-declare which tests are TDD-red vs coverage locks.
- Gotchas: bash (not PowerShell) for piping to the REPL; `docs/HANDOFF.md` is
  uppercase; ProjectSettings churn stays uncommitted; grep/diff can render `//`
  as `\ ` — Read before "fixing".

## Working branch

Create `slice-a-foundations` from main. Work there; merge per the process above.

## Definition of done for the session

A reviewed, committed plan; then all plan tasks executed, whole-branch review +
fix wave done, branch merged to main with tests green, HANDOFF.md updated for
the next session (Slice B+C kickoff), and this prompt file's checkbox flipped:

- [ ] Slice A complete
