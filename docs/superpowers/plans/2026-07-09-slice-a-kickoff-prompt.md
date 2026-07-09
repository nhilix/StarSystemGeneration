# Slice A Kickoff — Session Prompt

You are starting **Slice A (Foundations)** of the epoch-sim implementation
roadmap, under the **lighter protocol** defined in `/CLAUDE.md` (read it first —
it governs this session's workflow). You implement directly from the design
tree; there is no separate plan document for this slice.

## Read, in this order

1. `/CLAUDE.md` — the slice-session workflow and hard rules.
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — the governing
   meta-plan: slice table, greenfield transition rules, gates.
3. The frame docs Slice A implements: `docs/design/frame/principles.md`,
   `time.md`, `simulation-flow.md`, `controller-contract.md` — plus `actors.md`
   and `space-and-travel.md` for context (Slice B implements those; A's types
   must not contradict them).
4. `docs/design/narrative/chronicle-and-poi.md` §"The event grammar" — Slice A
   implements event grammar v2.
5. Reference code (informs, never constrains): `src/Core/Galaxy/GalaxyConfig.cs`,
   `GalaxyEvent.cs`, `EpochSim.cs`, `src/Core/Galaxy/Sim/*.cs`; shared substrate
   you WILL build on: `StableHash`/`RollChannel` machinery, `HexGrid`.

## Scope

- **World-year rates + GalaxyConfig reshape**: all rates in world-years
  (epoch ≈ 25y integration step, a knob); config gains the design's knob
  families (genesis, economy, sim) with seeded defaults, artifact-stamped.
- **Event grammar v2**: `(id, world-year, clock stratum, type, actors[],
  location, magnitude, valence, visibility, typed payload)`; eight type
  families; visibility public/regional/secret; per-place and per-actor indexes
  built as views.
- **Seven-phase EpochSim skeleton**: Perception → Markets → Allocation → Intent
  → Resolution → Interior → Chronicle, stepping a new sim-state container.
  Phases execute and log; most are empty — the frame later slices fill.
- **Controller interface types**: policies/acts records per
  `frame/controller-contract.md`; `Decide(perceivedState) → (policies, acts)`;
  a trivial default AI so the loop turns.

**Boundary**: the prototype sim stays in-tree untouched (Slice B deletes it).
The new frame is fresh code beside it — no adapters, no wiring into the old
skeleton build, no serializer changes (the new artifact format is Slice B).
Deterministic, unit-tested standalone. REPL surface for A: a command that steps
the new frame and prints the phase/event trace.

## Session shape (per /CLAUDE.md)

1. Read the above; give the user a one-message **scope confirmation** (what you
   will build, any boundary questions). Wait for the nod.
2. Branch `slice-a-foundations`. Create the task ledger
   (`docs/superpowers/plans/2026-07-09-slice-a-ledger.md`) — ordered checklist
   with gates — and keep it current as you work.
3. Implement directly: TDD, frequent commits. No per-task approval gates.
4. Before merge: dispatch one fresh-eyes whole-branch review subagent; apply one
   fix wave.
5. User gates: scope nod (step 1) · **REPL eyeball** (show the phase/event trace
   of a stepped galaxy; the user looks) · merge decision.
6. Wrap-up: merge to main · update `docs/HANDOFF.md` · **write the next kickoff
   prompt(s)** — Slice B (note: B is the one slice requiring a full written
   plan reviewed by the user before execution) and, if useful, Slice C (pure
   catalogs; parallelizable, may pair with a B session or run standalone) —
   grounded in the real interfaces and file paths Slice A just created · flip
   the checkbox below · push only when the user says.

- [ ] Slice A complete
