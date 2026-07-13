# Slice K Kickoff — Session Prompt

> **SUPERSEDED as a session prompt (2026-07-11):** Slice K is delivered as
> five sub-slices — see `2026-07-11-slice-k-roadmap.md`; start at
> `2026-07-11-slice-k1-kickoff-prompt.md`. This document remains the
> inherited context for every K sub-slice: the reading list, "what slice J
> left ready," the scope, and the boundary below are still authoritative.

You are starting **Slice K (Unity atlas rebuild)** — the LAST slice of
the epoch-sim implementation roadmap, under the lighter protocol in
`/CLAUDE.md` (read it first). K rebuilds the Unity atlas once, against
the settled data model: the PoC atlas renders only the galaxy skeleton;
K makes the atlas render **the simulated history** — domain/lane/price/
war/faction layers, panels, drill-down — the P1 legibility surface at
its final fidelity. After K the roadmap is complete.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules (note: `unity/ProjectSettings`
   churn stays uncommitted, always).
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — row K
   ("new atlas against the settled model: domain/lane/price/war/faction
   layers, panels, drill-down") and transition rule 1 (the PoC atlas is
   reference-only, replaced outright, deleted as superseded).
   **THE INTERFACE DESIGN EXISTS** (design session of 2026-07-11):
   `docs/superpowers/specs/2026-07-11-unity-atlas-design.md` + the living
   diagram `docs/diagrams/unity-atlas-design.html` (interactive mockup,
   published artifact — URL inside). It fixes the architecture (Eye-
   parameterized read model in `src/Core/Atlas`, lens stack, typed
   panels, five-LOD zoom continuum, delta-keyframe timeline) and answers
   this kickoff's two scope questions (data source: both, artifact-load
   first; skeleton layers survive as the nature lens group). Implement
   from it; deviations amend the spec in-branch.
3. **The design docs K renders** (the atlas is the P1 evidence of every
   subsystem — each doc's "Legible residue" bullet is a layer/panel
   requirement):
   - `docs/design/frame/space-and-travel.md` §P1 — "empires as
     port-domain glows with organic borders; lanes as literal highways;
     blockades as fleets at addresses; the wilds visibly dark."
   - `docs/design/substrate/market-geography.md` — the price map is
     "the most legible economic layer the atlas renders."
   - `docs/design/interpolity/relations.md` + `war.md` §P1 — warmth/
     tension shading; objectives, blockades, supply lines.
   - `docs/design/narrative/chronicle-and-poi.md` + `handoff.md` — POIs
     render at their hexes; the handoff view is the atlas's opening
     screen material (the world in motion).
   - `docs/design/frame/system-map.md`, `principles.md` — P1's
     two-customer test is THE gate for every layer.
4. **The PoC atlas** (reference-only, ~2,200 lines):
   `unity/Assets/Scripts/Atlas/*.cs` — AtlasController/Navigator/UI,
   GalaxyService (builds a GalaxySkeleton in-editor), GalaxyView +
   HexMeshBuilder + CellView + LayerPalette (hex mesh + per-cell layer
   shading), SystemView/OrbitLayout/SystemPanel (per-hex drill-down),
   `unity/Assets/Editor/AtlasSceneSetup.cs` + `AtlasAcceptance.cs`,
   tests under `Scripts/Atlas/Tests`. Salvage the RENDERING lessons
   (hex mesh, palette discipline, navigator, UI Toolkit panels); the
   data spine is superseded.
5. **How Unity sees Core**: `src/Core` IS the Unity package
   `com.stargen.core` (`unity/Packages/manifest.json`:
   `file:../../src/Core`; `src/Core/StarGen.Core.asmdef`,
   `noEngineReferences: true`) — that is why every new Core file
   carries a two-line `.meta`. The atlas asmdef references
   `StarGen.Core`. Core is netstandard2.1, no Unity deps — keep it so.
6. `docs/HANDOFF.md` — what J left ready.

## What slice J left ready (the settled model K renders)

- **The full state is loadable anywhere Core runs**:
  `ArtifactSerializer.Load(TextReader)` → a complete `SimState` (22
  layers, byte-stable) — the atlas can `eload` an artifact file OR run
  `EpochGenesis.Seed` + `EpochEngine` in-editor (the PoC's
  GalaxyService pattern, pointed at the epoch sim).
- **Every REPL surface has a Core-side data source** the atlas should
  consume instead of reinventing: `EpochMapView.Render` layer logic
  (domains/lanes/traffic/price/war/tension/tech/plague — port the
  COLOR/VALUE derivations, not the ASCII), `HandoffView.OpenThreads`
  (the world-in-motion panel), `EraDetector.Detect`, `EventLog`
  indexes (AtPlace/ForActor/ForWar/ForCharacter),
  `PoiCompiler.LiveAt`/`state.Pois`, `NewsOps`/`BeliefOps` (belief vs
  truth), `WarConduct.SiegeThreshold`, `FleetOps` postures/traffic.
  Territory derives from the port registry — never store ownership.
- **Fine tick**: the same engine steps a loaded artifact at any
  `YearsPerEpoch` (set it before stepping; `GenerationYears` stays 25).
  An animated atlas timeline = the `watch`/`ewatch` pattern with real
  rendering — byte-identity with unwatched runs is certified.
- **Delta saves**: `DeltaSerializer.Diff/Apply` — if the atlas mutates
  anything (it should not; it is a viewer), it saves deltas, never new
  bases.
- Determinism discipline holds in-editor: same config → same history;
  the hex tier stays a pure function (`Generator.Generate`), never
  persisted.

## Scope (roadmap row K)

- **Galaxy view, epoch-sim layers**: domains (port-derived territory
  glows with organic borders), lanes (built highways, quarantine/sever
  state), traffic, per-good price shading, war (objectives, blockades,
  fronts), warmth/tension, tech tiers, plague, POIs at their hexes,
  wilds visibly dark. Layer switching like the PoC's LayerPalette.
- **Timeline**: run/step the sim in-editor (coarse and fine) with the
  domains layer animating — the `watch` experience, rendered.
- **Panels**: polity (interior/factions/reign), market (prices/black
  book), war (fronts/exhaustion), relations, chronicle (era-annotated),
  and the **open-threads panel** as the "world in motion" summary.
- **Drill-down**: cell → hex tier (the PoC's SystemView survives in
  spirit) — the battlefield POI you click is the hex the war ground
  down on (P1 pre-commitments).
- **Delete the superseded PoC data spine** as its replacement lands
  (greenfield rule; git history is the archive). Keep what genuinely
  survives (mesh building, navigation, UI Toolkit patterns) by choice,
  not inertia.
- **Wrap-up**: this is the last roadmap slice — HANDOFF should close
  the roadmap and point at whatever the user wants next (the gap-list
  backlog in `docs/superpowers/specs/2026-07-11-design-acceptance.md`
  is the natural successor queue).

**Boundary**: no new simulation mechanics — K renders what exists. The
gap-list items (player verbs, perceived-price trading, sanctions…) are
NOT K. Espionage stays reserved. Core stays Unity-free.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod. (The two scope questions
   this step used to ask — data source and nature layers — are answered
   in the interface spec above; confirm the spec is still the design of
   record instead.)
2. Branch `slice-k-atlas` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-k-ledger.md`. Don't share a
   checkout with another live session — take a `git worktree`.
3. Unity testing is thinner than Core's: keep Core-side view-model
   logic (layer value/color derivations) in plain C# classes the xUnit
   suite can cover; Unity EditMode tests for the rest (the PoC's
   pattern). `dotnet test` stays green throughout — the atlas must not
   touch Core behavior (any Core additions are pure read-only view
   helpers).
4. Gates: `dotnet test` green · determinism byte-identity untouched ·
   golden untouched (K adds NO sim behavior) · the atlas opens, layers
   switch, timeline steps, panels populate, drill-down works.
5. User gates: scope nod · **atlas eyeball** (the taste gate: load seed
   42, watch 40 epochs animate, click the Alloys War's siege hex, open
   the threads panel) · merge decision.
6. Wrap-up: merge · HANDOFF (close the roadmap) · flip the box below ·
   push only on user say-so.

- [x] Slice K complete (K5 merged 2026-07-12 — the taste gate passed:
      seed 42, 40 epochs watched, the siege hex drilled to its orbit
      view, threads panel open over it)
