# Generation Rules Design — System/Body Layer

Status: **draft — expanded after review pass, awaiting re-approval**
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

This spec covers the **system/body generation layer** only: system presence
(stellar density), stars, orbits, bodies, satellites, per-world society stats, and
system/body naming, plus the overlay/archetype mechanism that injects exotic
phenomena on top of that layer. This matches Roadmap Phase 1 in `docs/DESIGN.md`.

**Explicitly out of scope for this spec** (deferred to a follow-up spec):

- The political/faction layer (allegiances, inter-system politics, conflict).
- Deeper exotic-phenomena content beyond the overlay mechanism's skeleton (this spec
  defines *how* overlays work and a few illustrative examples, not a full catalog).
- Sector/hex-map generation, Unity rendering, and persistence/delta-state — these
  remain covered at the architecture level in `docs/DESIGN.md`.

## 4. Generation Pipeline

A hex's contents are generated purely as a function of `(masterSeed, hexCoordinate)`,
in three stages:

0. **Presence roll** — before anything else, check whether this hex contains a system
   at all. Baseline stellar density is a single tunable probability (starting point:
   ~50%, tuned by eye in the inspector). An empty hex is a valid, stable result —
   regenerating it always yields empty. Empty hexes are what make a starmap readable
   and travel meaningful; a wall-to-wall starfield has no shape. Density is uniform
   for now; varying it by region (spiral arms, rifts, dense cluster cores) is a
   future layer that changes only this stage's probability input, not its contract.
1. **Baseline roll** — independent weighted draws build the system bottom-up:
   star arrangement → star(s) → orbit slots → body per slot → per-body descriptors
   (including the separate biosphere and settlement axes, Section 5) → society stats
   for settled bodies. Orbit band (inner/habitable/outer, relative to that star's own
   habitable band) skews atmosphere/biosphere/settlement odds, so habitable-zone
   worlds are more likely to be notable without it being forced.
2. **Overlay roll** — after the baseline is complete, a separate low-probability roll
   determines whether a curated overlay (Section 6) applies on top of it.

The baseline/overlay split is the core design decision: it keeps the common case
cheap, simple, and organically varied (stage 1), while concentrating all hand-authored
"interesting" content in a small, tunable second stage (stage 2) rather than trying to
engineer interest into every independent roll.

## 5. Entity Model

All terminology below is original (no Traveller UWP reuse):

| Entity | Key attributes |
|---|---|
| **System** | designation (deterministic, coordinate-derived — see Section 7), given name (procedural, only for settled or notable systems), star arrangement (single / binary / trinary), applied overlay (if any) |
| **Star** | type/size class, luminosity tag (defines habitable band), age tag (young / mature / old — also gates overlay eligibility, e.g. an "unstable star" overlay requires young/dying) |
| **Orbit slot** | index, band (inner / habitable / outer), occupant |
| **Body** | kind (rocky world, gas giant, ice world, planetoid belt, etc.), size, atmosphere, hydrographic coverage, **biosphere** axis, **settlement** axis (see below) |
| **Society stats** *(present whenever settlement > none, or biosphere is sapient)* | population tier, government archetype, order/law tier, infrastructure/starport tier, notable points of interest |
| **Satellite** | same schema as Body, attached to a parent Body's local orbit rather than the star's |

**Biosphere vs. settlement — two separate axes.** These are rolled independently
(with cross-influence), because they answer different questions:

- **Biosphere** — natural life: `barren → microbial → flourishing → sapient`.
- **Settlement** — who lives there now: `none → outpost → colony → major world`.

A colony on a dead rock (settlement without biosphere) is the most common inhabited
world in the target tone — Trek/Mass Effect/Traveller are full of mining outposts and
domed colonies on airless moons. A flourishing garden world nobody has settled
(biosphere without settlement) is a classic exploration hook. Conflating the two into
one ladder makes both impossible. Cross-influence: a flourishing biosphere in the
habitable band raises settlement odds (people settle where it's pleasant), and
sapient biosphere implies a native society regardless of the settlement roll.

**Star arrangement:** a weighted draw — single (common), binary (uncommon), trinary
(rare). The primary star owns the system's orbit-slot array; each companion star
occupies one of those slots and carries its own small set of close-in orbit slots
(same schema, one level of recursion — companions of companions don't occur).

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

## 7. Identity & Naming

Every non-empty hex gets two layers of identity:

- **Designation** — a deterministic catalog code derived from the coordinate (e.g.
  sector prefix + hex number, final format TBD during implementation). Every system
  has one; it's the fallback display name and the stable key for cross-referencing.
- **Given name** — a procedurally generated proper name (syllable-table driven, built
  on the same `WeightedTable<T>` machinery, deterministic like every other roll).
  Only systems that would plausibly *have* a name get one: settled systems, sapient
  homeworlds, and overlay-marked notable systems. Everything else shows only its
  designation — which itself reinforces the exploration tone: an unnamed catalog code
  on the map is implicitly "nobody has been here."

Bodies within a named system take derived names (name + orbit numeral, e.g.
"Veshara III") unless individually notable; per-culture naming flavor is a future
extension of the syllable tables, not in scope now.

## 8. Determinism & Seeding

Every roll is a **stateless hash-based draw**: the random value for a given roll is
computed by hashing `(masterSeed, hexCoordinate, rollChannel, index)` — for example
SplitMix64-style mixing over the packed inputs — rather than by advancing a shared
sequential RNG stream. `rollChannel` identifies *which* decision is being made
("star arrangement", "body kind", "settlement", ...); `index` disambiguates repeated
draws on the same channel (orbit slot 0, 1, 2, ...).

This is what makes the stability guarantee real rather than aspirational: because no
draw depends on how many draws happened before it, adding a new roll to the pipeline
later cannot shift the values of existing rolls. A sequential stream (even a seeded
one) breaks the moment a new roll is inserted mid-pipeline.

Channel discipline:

- Channel IDs are stable named constants in a single registry.
- A channel, once shipped, is never renumbered or reused. New rolls get new channels;
  a removed roll's channel is retired permanently.

## 9. Testing Strategy

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
- **Structural invariant tests** — generate many systems and assert schema-level
  rules always hold: presence roll respected (empty hexes stay empty), society stats
  present exactly when settlement > none or biosphere is sapient, companion stars
  never nest more than one level, every non-empty system has a designation.

## 10. Interactive Inspector REPL

A console REPL project, separate from Unity, referencing only Core — used for manual
exploration during tuning and as a base for snapshot-style checks alongside the
automated tests above. Spiritually similar to `systemCreator.py`'s old `raw_input`
loop and `eSG.py`'s ASCII output, redone properly for the new schema.

Commands:

- `seed <value>` — set the master seed.
- `goto <x> <y>` — generate and print the system at a coordinate.
- `next` / `prev` — step to an adjacent coordinate without retyping it.
- `reroll` — pick a new random master seed.
- `find <criterion>` — scan forward from the current coordinate and jump to the next
  system matching a criterion (`overlay`, a specific overlay id, `settled`,
  `sapient`, a body kind, ...) — for finding rare content without manual hunting.
- `stats <n>` — generate `n` hexes from the current position and print distribution
  summaries (presence rate, star arrangements, body-kind counts, settlement tiers,
  overlay frequency). This is the primary tuning instrument: after touching any
  weight, one command shows whether the universe still has the intended shape,
  without eyeballing systems one at a time.

Sketch of the per-system dump (format will evolve; the point is the shape —
scannable one-screen summary, indentation mirrors the orbit hierarchy):

```
[0412] KAV-0412 "Veshara"          binary · overlay: Precursor Ruins
  Star A — amber dwarf, mature
    1 [inner] scorched world · size 3 · no atmosphere
    2 [hab]   verdant world · size 7 · breathable · oceans 60% · flourishing
              colony · pop tier 5 · council rule · orderly · orbital-class port
              POI: precursor ruins (overlay)
    3 [outer] gas giant · 2 satellites
  Star B — red ember, old (slot 5)
    1 [inner] ice world · size 2 · barren
```

The REPL's job is fast iteration between seeds and coordinates, not anything beyond
that.

## 11. Follow-up Work (not in this spec)

- Political/faction layer design (allegiances, conflict, inter-system relationships).
- Expanded exotic-phenomena/overlay catalog beyond the illustrative examples above.
- Sector/hex map generation and Unity presentation (tracked in `docs/DESIGN.md`).
- Persistence of seed + deltas (tracked in `docs/DESIGN.md`).
