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
stored just to stay consistent. Generation input is a GalaxyConfig (master seed + galaxy size + tuning knobs), not a bare seed; the same seed at different sizes intentionally yields different galaxies.

**Game-layer readiness:** anything a future game needs to mutate (exploration state,
ownership, faction control, ship position) is stored as a **delta layer on top of the
procedural baseline**, never baked into generated data. Core stays a pure generator;
mutable state is a separate concern layered on top — and per the regional spec (§7.7), the future game layer inherits a continuing simulation seeded by the world-state handoff, with deltas recording player-visible divergence from it.

TODO: diagram of Core ↔ Presentation data flow once the interface shape is decided.

---

## 3. Data Model

Hierarchy (top to bottom): **Galaxy → Sector → Subsector → Hex (System) → Star →
Orbit → Body (World / Gas Giant / Planetoid Belt) → Satellite**.

The galaxy/sector/subsector/hex spatial layer mirrors Traveller's canonical sector
structure (a sector is 32×40 hexes, divided into sixteen 8×10 subsectors) as a spatial
convention only. The star/orbit/body/satellite layer's fields are defined in the
generation rules spec below, using original terminology (no Traveller UWP codes).

Field-level detail for System / Star / Orbit slot / Body / Society stats / Satellite:
see `docs/superpowers/specs/2026-07-07-generation-rules-design.md` §5.

**Hex layer (current scope):** a hex carries its coordinate, a presence flag (rolled
as pipeline stage 0 — not every hex has a system; see spec §4), and, when present,
the generated System. Because generation is deterministic, a Hex is a *view* of the
generator's output at a coordinate, not stored data — nothing is persisted per hex
until a future delta layer has something to record against it.

Deferred to the political/faction spec: hex-level allegiance, travel/hazard zone
ratings, trade-route data, and region-varying stellar density.

**Galaxy structure artifact:** above the hex layer sits the persisted galaxy structure artifact (regional spec §3.1): region-cell state, species/polity registries, and the event log — built once per GalaxyConfig, versioned, and loaded rather than regenerated so existing galaxies stay stable under newer generator code. Coordinates have a defined galaxy extent; hexes beyond the rim are empty space. Byte-identical regeneration is guaranteed per platform/runtime (floating-point library differences can vary across architectures); the persist-and-load design absorbs this — an artifact, once built, is authoritative.

---

## 4. Roadmap

1. **Build the Core generation engine** — implement the from-scratch system/body
   ruleset in `docs/superpowers/specs/2026-07-07-generation-rules-design.md`
   (presence roll, baseline + overlay pipeline, `WeightedTable<T>`, hash-based
   deterministic seeding, naming, the interactive inspector REPL).
   *Done when:* the full spec §9 test suite passes; the REPL can walk a sector's
   worth of hexes and every dump is coherent (no contradictory tag combinations on
   eyeball review); `stats` over a few thousand hexes shows the intended shape
   (presence rate near target, overlays rare, settlement rarer than biosphere).
2. **Single-system view** — Unity renders one generated system: stars, orbits,
   worlds, satellites, inspectable stats. Proves the Core↔Unity data contract.
   *Done when:* any `(seed, coordinate)` shown in the REPL renders identically-
   structured in Unity — same bodies, same stats in the inspect panel — with no
   generation logic in the Unity project.
3. **Sector/subsector map** — hex grid navigation, lazy per-hex generation, system
   summary icons, drill-down into system view. Proves on-demand generation + caching.
   *Done when:* panning a full sector (1,280 hexes) is smooth on first visit
   (generation is lazy and fast enough) and revisiting hexes is visibly identical.
4. **Galaxy scale** — multiple sectors, camera/LOD across galaxy → sector → system,
   persistence of GalaxyConfig + the galaxy structure artifact + deltas (regional spec §3.1).
   *Done when:* zooming galaxy → sector → system → body is seamless and a save file
   contains the GalaxyConfig, the galaxy structure artifact, and delta records.
5. **Game-layer hooks** — ship entity, travel between systems, discovery/ownership
   state, faction data. Scoped by its own future spec; not sized here.

**Regional / spatial generation** is specced in docs/superpowers/specs/2026-07-07-regional-generation-design.md (three-tier architecture: density fields, persisted galaxy skeleton with an epoch history simulation, per-hex integration) and implemented in slices — slice 1 (visible galaxy) covers Tier 1, seeding, sim stage 1, and the inspector atlas.

---

## 5. Tech Stack & Tooling

- **Engine:** Unity **6000.5.2f1** (Universal 2D / URP template), project at `unity/`.
- **Core:** C# class library targeting **.NET Standard 2.1** — the highest profile
  Unity consumes — so the identical assembly/source serves Unity, the inspector, and
  tests. No Unity references, no dependencies beyond the base class library if
  avoidable. This constraint is load-bearing: any NuGet package added to Core must be
  netstandard2.1-compatible or it silently breaks the Unity integration in Phase 2.
- **Inspector:** .NET 8 (or current LTS) console app referencing Core (see spec §10).
- **Tests:** xUnit project on .NET 8, referencing Core directly — plain `dotnet test`,
  no Unity Test Framework involvement for generation logic. Unity-side tests only for
  Unity-side behavior, later.
- **Repo layout** (single repo, single solution for the .NET side):

  ```
  src/Core/            # generation engine (netstandard2.1)
  src/Inspector/       # REPL console app (net8.0)
  tests/Core.Tests/    # xUnit (net8.0)
  unity/               # Unity project, added in Phase 2, references src/Core
  docs/
  ```

- **CI:** GitHub Actions running `dotnet build` + `dotnet test` on push once code
  exists. Unity build automation deferred until there's a Unity project worth building.
- **Inspiration reference (not a dependency):** `eSG.py` / `systemCreator.py` live in
  the separate `Traveller-SystemGenerator` repo — loose inspiration for tone/structure
  only, not ported or referenced by Core.

Unity references Core as a **local UPM package**: `src/Core` doubles as the package
(`package.json` + `StarGen.Core.asmdef` with `noEngineReferences: true` beside the
sources; `csc.rsp` supplies `-langversion:latest -nullable:enable` since Unity
ignores the csproj; dotnet build output lives in dot-prefixed folders Unity skips).
One source of truth — edit in the IDE, both `dotnet test` and Unity pick it up.
The editor is driven directly via the official Unity MCP bridge (AI Assistant
package) during development.

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
  reroll/find/stats) so seeds and coordinates can be explored quickly during tuning,
  independent of Unity. See spec §10.
- **Presence roll as pipeline stage 0** *(2026-07-07 review pass)* — not every hex
  has a system; empty hexes are what give a starmap shape and make travel meaningful.
  Density is a single tunable knob for now, region-varying later. See spec §4.
- **Biosphere and settlement are separate axes** *(2026-07-07 review pass)* — natural
  life and current habitation are different questions; conflating them makes both
  "colony on a dead rock" and "unclaimed garden world" impossible, and those are two
  of the most tone-defining system types. See spec §5.
- **Stateless hash-based RNG, not sequential streams** *(2026-07-07 review pass)* —
  each roll is a pure hash of (seed, coordinate, channel, index), which is what makes
  the "adding rolls later doesn't shift existing output" guarantee actually hold.
  See spec §8.
- **Two-layer naming** *(2026-07-07 review pass)* — every system has a deterministic
  catalog designation; only settled/notable systems get procedural proper names, so
  an unnamed code on the map itself signals "unexplored." See spec §7.
- **Tech stack** *(2026-07-07 review pass)* — Core targets .NET Standard 2.1 (the
  Unity-compatibility ceiling); inspector and xUnit tests on .NET 8; Unity 6 LTS;
  single repo (`src/` + `tests/` + `unity/` + `docs/`); GitHub Actions for
  build+test. See Section 5.

Open questions (unresolved):

- How Unity references Core in Phase 2 (local UPM source package vs. DLL drop) —
  deferred until the Unity project exists (see Section 5 TODO).
- Designation format for systems (sector prefix + hex number scheme) — decide during
  Phase 1 implementation; cosmetic, not structural.
- Field-level detail for hex-layer politics (allegiance, travel zones) and the
  faction layer — deferred to the political/faction spec, see Section 3.

---
