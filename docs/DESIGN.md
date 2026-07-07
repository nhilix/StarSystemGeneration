# Design Document — Procedural Star System / Galaxy Generator

Status: **draft, in progress**. This document is built up section by section as we
work through decisions. Sections marked `TODO` are placeholders for future passes.

This is a standalone project in its own repo. It was originally sketched inside the
separate `Traveller-SystemGenerator` repo (a Python 2 prototype implementing Mongoose
Traveller's 2D6 rules, `eSG.py`/`systemCreator.py`); that repo remains untouched as a
loose inspiration reference only — nothing here is ported from it. See
`docs/superpowers/specs/2026-07-07-generation-rules-design.md` for the from-scratch
generation ruleset.

---

## 1. Concept & Vision

A deterministic, seed-based procedural universe engine with original generation rules
(inspired by, but not reusing, Mongoose Traveller's conventions), paired with a Unity
front end for browsing and exploring the generated universe.

**Product framing:** tool first, game-ready later. The near-term deliverable is a
cartography/inspection tool — generate a galaxy, browse sectors and systems, inspect
bodies in detail — with no gameplay loop. It is architected so that a game layer
(ships, travel, factions, encounters) can be built on top later without reworking the
generation core.

**Visual style:** abstracted and readable, not photorealistic. Think Dwarf Fortress —
sprite/icon-based representations, procedural shader accents (e.g. star glow,
atmosphere tint) rather than a simulated space-sim renderer like Elite Dangerous.
Priority is legibility of generated data over visual fidelity.

**Scale ambition:** full galactic scale from the start, made tractable by leaning on
Traveller's native spatial hierarchy (galaxy → sector → subsector → hex) and
generate-on-demand determinism — a system only exists once something looks at its
hex, and looking again always reproduces the same result from the same seed. Feature
depth (rendering, UI, game hooks) still rolls out in phases; the scale of the data
model does not need to be deferred.

---

## 2. Architecture

**Two-project split:**

- **Core** — plain C# class library, no Unity dependencies. Owns the data model and
  all generation logic (from-scratch ruleset, see the generation rules spec).
  Generation is a pure function of `(coordinates, seed) → data`. Testable headless;
  reusable from a CLI, tests, or any future non-Unity consumer.
- **Presentation (Unity)** — consumes Core, owns rendering and interaction only:
  - **Sector/galaxy map view** — hex grid, systems shown as icons colored/shaped by
    starport class, population, danger, etc.
  - **System view** — 2D top-down orbits, sprite bodies, inspect panel for stats.

No generation logic lives in Unity. This keeps the simulation testable independent of
the engine and leaves the door open to a different front end later if needed.

**Determinism & seeding:** every generated entity's randomness is derived from a
master seed plus its coordinates/identity (e.g. hex coordinate, orbit index), not from
an unseeded global RNG. This is what makes "generate on demand" safe — the same
coordinates always produce the same system, so nothing needs to be precomputed or
stored just to stay consistent.

**Game-layer readiness:** anything a future game needs to mutate (exploration state,
ownership, faction control, ship position) is stored as a **delta layer on top of the
procedural baseline**, never baked into generated data. Core stays a pure generator;
mutable state is a separate concern layered on top.

TODO: diagram of Core ↔ Presentation data flow once the interface shape is decided.

---

## 3. Data Model

Hierarchy (top to bottom): **Galaxy → Sector → Subsector → Hex (System) → Star →
Orbit → Body (World / Gas Giant / Planetoid Belt) → Satellite**.

The galaxy/sector/subsector/hex spatial layer mirrors Traveller's canonical sector
structure (a sector is 32×40 hexes, divided into sixteen 8×10 subsectors) as a spatial
convention only. The star/orbit/body/satellite layer's fields are defined in the
generation rules spec below, using original terminology (no Traveller UWP codes).

Field-level detail for Star / Orbit slot / Body / Society stats / Satellite: see
`docs/superpowers/specs/2026-07-07-generation-rules-design.md` §5.

TODO: field-level detail for the galaxy/sector/subsector/hex layer itself (allegiance,
travel zone, etc.) — not yet specced; deferred alongside the political/faction layer.

---

## 4. Roadmap

1. **Build the Core generation engine** — implement the from-scratch system/body
   ruleset in `docs/superpowers/specs/2026-07-07-generation-rules-design.md`
   (baseline + overlay pipeline, `WeightedTable<T>`, deterministic seeding, the
   interactive inspector REPL).
2. **Single-system view** — Unity renders one generated system: stars, orbits,
   worlds, satellites, inspectable stats. Proves the Core↔Unity data contract.
3. **Sector/subsector map** — hex grid navigation, lazy per-hex generation, system
   summary icons, drill-down into system view. Proves on-demand generation + caching.
4. **Galaxy scale** — multiple sectors, camera/LOD across galaxy → sector → system,
   persistence of seed + deltas only (not full generated data).
5. **Game-layer hooks** — ship entity, travel between systems, discovery/ownership
   state, faction data.

TODO: acceptance criteria per phase; rough sizing/estimate once Phase 1 scope is
locked down.

---

## 5. Tech Stack & Tooling

- **Engine:** Unity (C#).
- **Core:** standalone C# class library, no Unity dependency, unit-testable.
- **Inspector:** console REPL project, Core-only, no Unity dependency (see spec §9).
- **Inspiration reference (not a dependency):** `eSG.py` / `systemCreator.py` live in
  the separate `Traveller-SystemGenerator` repo — loose inspiration for tone/structure
  only, not ported or referenced by Core.

TODO: Unity version/LTS choice, repo/solution layout (single repo with `Core/` +
`Unity/` folders vs. separate repos), test framework for Core (NUnit via Unity Test
Framework vs. plain xUnit/NUnit project), CI.

---

## 6. Open Questions / Decisions Log

Decisions made so far, with rationale:

- **Tool first, game-ready later** — avoids committing to full game scope
  (UI/UX, save systems, balancing) before the generation core is proven.
- **Desktop via Unity, not web** — chosen so this can serve as a foundation for a
  future game layer; slight leaning to Unity over Unreal given prior experience and
  because the target visual style (sprite-based, Dwarf Fortress-like) doesn't need
  Unreal's large-world/fidelity strengths.
- **Full galactic scale from day one (architecturally)** — made tractable via
  Traveller's native hex hierarchy + deterministic generate-on-demand, so scale is a
  data-model decision now rather than a feature deferred to later.
- **Generation rules built from scratch, not ported from `eSG.py`** — `eSG.py` was
  only ever a loose jumping-off point. New rules use original terminology (no UWP
  codes), software-native weighted distributions (no dice metaphor), and a
  procedural-baseline-plus-curated-overlay pipeline aimed at a space-opera/exploration
  tone (Star Trek/Mass Effect/Traveller-ish) with pockets of exotic phenomena rather
  than a physically accurate simulation. Full detail in
  `docs/superpowers/specs/2026-07-07-generation-rules-design.md`.
- **Own repo, separate from `Traveller-SystemGenerator`** — keeps this project's
  history and scope self-contained rather than mixed into the old prototype's repo.
- **Interactive text inspector, not a one-shot dump** — a REPL (seed/goto/next/prev/
  reroll/find-overlay) so seeds and coordinates can be explored quickly during tuning,
  independent of Unity. See spec §9.

Open questions (unresolved):

- Unity version, repo/solution layout for `Core` vs. Unity project vs. inspector REPL,
  and Core test framework (see Section 5 TODOs).
- Field-level detail for the galaxy/sector/subsector/hex layer (allegiance, travel
  zone, etc.) and the political/faction layer — both deferred, see Section 3.

---
