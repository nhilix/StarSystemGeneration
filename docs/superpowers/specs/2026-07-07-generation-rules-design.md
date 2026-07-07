# Generation Rules Design — System/Body Layer

Status: **approved, ready for planning**
Date: 2026-07-07

## 1. Overview

This spec defines a from-scratch procedural generation ruleset for star systems and
their bodies. It supersedes the earlier "port `eSG.py`" framing in `docs/DESIGN.md` —
`eSG.py` (Mongoose Traveller's 2D6-based rules) was only a loose jumping-off point for
the project overall, not something to carry forward. Nothing in this spec reuses
Traveller's specific tables, dice mechanics, or UWP-style codes.

## 2. Goals

- Produce an **interesting, somewhat fantastical sci-fi universe** rather than a
  physically accurate one. Tone leans space-opera-with-exploration — closer to Star
  Trek / Mass Effect / Traveller than Star Wars — with pockets of exotic phenomena
  scattered through an otherwise-grounded-feeling universe.
- All category/terminology choices (star types, body kinds, government archetypes,
  etc.) are **original**, only inspired by Traveller's conventions, not reused from
  them.
- Generation is **deterministic**: the same `(masterSeed, hexCoordinate)` always
  produces the same system, and stays stable as the schema grows over time (adding a
  new roll later must not shift the output of existing rolls).
- Generation is **software-native**: probabilities are expressed as weighted
  distributions defined in code/data, not simulated dice (no 2D6/3D6 metaphor).

## 3. Scope

This spec covers the **system/body generation layer** only: stars, orbits, bodies,
satellites, and per-world society stats, plus the overlay/archetype mechanism that
injects exotic phenomena on top of that layer. This matches Roadmap Phase 1
(single-system view) in `docs/DESIGN.md`.

**Explicitly out of scope for this spec** (deferred to a follow-up spec):

- The political/faction layer (allegiances, inter-system politics, conflict).
- Deeper exotic-phenomena content beyond the overlay mechanism's skeleton (this spec
  defines *how* overlays work and a few illustrative examples, not a full catalog).
- Sector/hex-map generation, Unity rendering, and persistence/delta-state — these
  remain covered at the architecture level in `docs/DESIGN.md`.

## 4. Generation Pipeline

A system is generated purely as a function of `(masterSeed, hexCoordinate)`, in two
stages:

1. **Baseline roll** — independent weighted draws build the system bottom-up:
   star(s) → orbit slots → body per slot → per-body descriptors → per-world society
   stats (only rolled for worlds whose biosphere is sapient/colonized). Orbit band
   (inner/habitable/outer, relative to that star's own habitable zone) skews
   atmosphere/biosphere/population odds, so habitable-zone worlds are more likely to
   be notable without it being forced.
2. **Overlay roll** — after the baseline is complete, a separate low-probability roll
   determines whether a curated overlay (Section 6) applies on top of it.

This two-stage split is the core design decision: it keeps the common case cheap,
simple, and organically varied (stage 1), while concentrating all hand-authored
"interesting" content in a small, tunable second stage (stage 2) rather than trying to
engineer interest into every independent roll.

## 5. Entity Model

All terminology below is original (no Traveller UWP reuse):

| Entity | Key attributes |
|---|---|
| **Star** | type/size class, luminosity tag (defines habitable band), age tag (young / mature / old — also gates overlay eligibility, e.g. an "unstable star" overlay requires young/dying) |
| **Orbit slot** | index, band (inner / habitable / outer), occupant |
| **Body** | kind (rocky world, gas giant, ice world, planetoid belt, etc.), size, atmosphere, hydrographic coverage, biosphere tag (barren → microbial → flourishing → sapient) |
| **Society stats** *(only present when biosphere is sapient/colonized)* | population tier, government archetype, order/law tier, infrastructure/starport tier, notable points of interest |
| **Satellite** | same schema as Body, attached to a parent Body's local orbit rather than the star's |

Every table driving these rolls (star types, body kinds, government archetypes, etc.)
is an instance of a generic **`WeightedTable<T>`** — data, not hardcoded branching
logic — so tuning weights or adding entries later doesn't touch generation code.

## 6. Overlay / Archetype System

This is what produces exotic phenomena without every system needing hand-authored
content:

- **`OverlayDefinition`** — id/name, rarity weight (likelihood it's even considered),
  an **eligibility predicate** (e.g. "requires young or dying star," "requires no
  existing sapient world," "requires at least one empty orbit slot"), and a
  **mutation function** that transforms the already-generated baseline system (inject
  a body/feature, override a tag, attach a point-of-interest, mark the system as
  narratively notable).
- Illustrative examples (not an exhaustive catalog — that's follow-up work):
  - *Precursor Ruins* — ancient structure on an otherwise unremarkable world.
  - *Unstable Star* — star nearing collapse; hazard tag propagates to all bodies.
  - *Derelict Fleet* — wreckage occupying an empty orbit slot.
  - *Anomalous Signal* — no physical change, just a hook/flag for a future game layer
    to attach content to.
- The overlay roll fires only after the baseline is fully generated, and only
  considers overlays whose eligibility predicate passes against that specific
  baseline — overlays can never contradict the system they land on (no "unstable
  star" tag on a calm, mature star). Resolution is two steps: (1) a single "does any
  overlay apply to this system" check against a global rarity probability; if it
  triggers, (2) a weighted pick among only the overlays whose eligibility predicate
  passes for this baseline, using each overlay's rarity weight. If no overlay is
  eligible when step 1 triggers, no overlay is applied — it is not retried or forced.
- The overlay catalog is pure data (a list of `OverlayDefinition`s) — authoring a new
  one is additive, no core roll logic changes. Same philosophy as `WeightedTable<T>`.
- Rarity is a single tunable weight per overlay. Varying rarity by location (e.g.
  rarer near sector capitals) is a possible future extension, not needed now.

## 7. Determinism & Seeding

Every roll draws from an RNG sub-stream keyed by
`(masterSeed, hexCoordinate, rollChannel)` rather than one shared stream. This means
adding a new roll to the pipeline later doesn't shift the sequence of *existing*
rolls — regenerating an already-visited system stays stable across schema growth, not
just across repeated calls against the current schema.

## 8. Testing Strategy

Core is a plain C# library with no Unity dependency, so all of this is testable
headless:

- **Determinism tests** — same `(seed, coordinate)` produces identical output across
  repeated calls and across schema additions (guards against accidental
  stream-shifting bugs).
- **Distribution sanity tests** — sampling a `WeightedTable<T>` over N draws roughly
  matches its configured weights (catches data/weight typos, not a statistical proof).
- **Eligibility invariant tests** — for each `OverlayDefinition`, generate many
  baselines and assert the overlay never applies where its predicate should exclude
  it (e.g., never "unstable star" on a mature star).

## 9. Interactive Inspector REPL

A console REPL project, separate from Unity, referencing only Core — used for manual
exploration during tuning and as a base for snapshot-style checks alongside the
automated tests above. Spiritually similar to `systemCreator.py`'s old `raw_input`
loop and `eSG.py`'s ASCII output, redone properly for the new schema.

Commands:

- `seed <value>` — set the master seed.
- `goto <x> <y>` — generate and print the system at a coordinate.
- `next` / `prev` — step to an adjacent coordinate without retyping it.
- `reroll` — pick a new random master seed.
- `find overlay` — scan a coordinate range and jump to the next system where an
  overlay applied, for quickly finding exotic phenomena without manual hunting.

Each command prints a human-readable dump of the generated system (stars, orbits,
bodies, tags, overlay if any). The REPL's job is fast iteration between seeds and
coordinates, not anything beyond that.

## 10. Follow-up Work (not in this spec)

- Political/faction layer design (allegiances, conflict, inter-system relationships).
- Expanded exotic-phenomena/overlay catalog beyond the illustrative examples above.
- Sector/hex map generation and Unity presentation (tracked in `docs/DESIGN.md`).
- Persistence of seed + deltas (tracked in `docs/DESIGN.md`).
