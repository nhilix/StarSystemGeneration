# Unity Interactive Atlas — Drill-Down Galaxy Browser

Status: **draft — awaiting user review**
Date: 2026-07-07
Depends on: `2026-07-07-hex-geometry-design.md` (axial coordinates, `HexGrid`,
91-hex cells, origin-centered galaxy) — implemented first.

## 1. Overview

The inspector REPL's capabilities as an interactive Unity UI: a runtime (play-mode)
application that generates a galaxy from a seed, renders it as a clickable hexagonal
map, and drills down — galaxy → cell → individual hex → structured star-system data
panel — with the REPL's five map layers as toggles. Purpose: fully exercise the
Core↔Unity interface and establish the zoom-layer rendering architecture that later
phases (continuous zoom, game views) build on.

This begins Roadmap Phases 2–3 in reshaped form: the drill-down atlas delivers the
sector-map phase's navigation and the system view's data panel together; the visual
orbit diagram remains a later step.

## 2. Decisions (from brainstorm)

- **Navigation:** discrete drill-down views with breadcrumb — no continuous zoom in
  v1 (that is Phase 4; pan/zoom-within-view is a possible v1.5).
- **Rendering:** one **procedural hex mesh per view** (triangulated hexes, vertex
  colors), rebuilt on view entry, recolored in place on layer switches. No
  per-hex GameObjects, no texture painting.
- **Picking:** pure math — screen point → world → `HexGrid.WorldToHex` → cell/hex.
  No colliders. Core's `HexGrid` is the single geometry authority for both
  rendering and picking.
- **UI chrome:** **UI Toolkit** (runtime) for setup screen, breadcrumb, layer
  toggles, tooltips, and the system data panel.
- **System view depth:** structured data panel (no orbit diagram in this spec).
- **App type:** runtime play-mode app — the cartography tool is the product.

## 3. App Structure

One scene; an `AtlasController` state machine over three states (views
activate/deactivate; no scene loads):

1. **Setup** — seed field, galaxy radius field (default 21), Generate button,
   progress hint. Validates radius (≥ 2; warn above ~40). Build failures surface
   here as messages, never a silent hang.
2. **Galaxy** — the cell-lattice map (1,387 hexes at default radius): one mesh,
   vertex-colored by active layer; **layer toggles**: density / polity / zone /
   dev / lean (the REPL's five); hover tooltip (cell coord, owner, development);
   click a cell → Cell state.
3. **Cell** — that cell's 91 hexes, colored by the sector-glyph scheme as fills:
   empty / system / settled / anchored (+ capital ★ marker); side panel shows the
   cell's data (owner polity, lean, metallicity, anchors, its event-log lines).
   Click a hex → **System panel** slides in beside the map.

**System panel** (within Cell state): designation + given name header, star list
(type, age), orbit-by-orbit body entries (kind, size, atmosphere, hydrographics,
biosphere, settlement), society lines (population tier, government, order, port),
system tags/POIs — `SystemFormatter`'s content as structured UI Toolkit elements
bound to Core model objects. Clicking an **empty** hex opens the same panel with
designation, "no system," and the hex's density — empty is data too, and it
exercises the same path.

**Breadcrumb** (persistent): `Galaxy 42 › Cell (3,-5) › SGC 2051-2043`. Clicking an
ancestor pops to it; Escape steps up one level.

## 4. Core↔Unity Seam

- **`GalaxyService`** — a plain C# class (no MonoBehaviour): owns the
  `GalaxyContext`, builds/holds the skeleton, exposes `CellAt(cellCoord)`,
  `Generate(hex)`, cell/hex enumeration for the views, and per-layer color queries.
  Views touch Core types only through it (auditable "no generation logic in Unity");
  Phase 4 caching/streaming gets a single home later.
- **Layer coloring is pure functions** (`LayerPalette`: cell × layer → Color; hex
  state → Color), unit-testable without rendering; carries the spike's conventions
  forward (golden-ratio polity hues, brightness by development, grayscale density,
  white capitals).
- **Assembly:** `StarGen.Atlas.asmdef` under `unity/Assets/Scripts/Atlas/`,
  referencing `StarGen.Core` — atlas code out of Assembly-CSharp, dependency
  direction explicit. The spike (`GalaxyMapSpike`, menu item) is retired when the
  atlas lands.

## 5. Rendering & Interaction Details

- **Mesh building:** flat-top hexes from `HexGrid.HexToWorld` positions with a small
  inset gap (lattice reads without outline shaders); one mesh per view; galaxy mesh
  ≈ 1,387 hexes × 4 tris — trivial. Vertex-color arrays swapped in place for layer
  toggles and hover highlights (no rebuild).
- **Hover:** brightened tint + tooltip. **Selection:** thin outline overlay on the
  picked hex.
- **Camera:** orthographic, auto-fit to each view's bounds with padding; no free
  pan/zoom in v1.
- **Visual baseline** (deliberately modest, Dwarf-Fortress spirit): near-black
  background, vertex colors and text only — no art assets, no custom shaders; voids
  as barely-visible dark hexes; UI Toolkit dark theme, default fonts.
- **Performance budget:** galaxy mesh build < 100 ms; cell-view transitions feel
  instant; layer/hover recolors are array writes.

## 6. Verification

1. **Core:** existing suite + `HexGrid` suite stay green (owned by the hex-geometry
   spec).
2. **Unity edit-mode tests** (first in the repo, pure logic only): `LayerPalette`
   mappings, `GalaxyService` behavior (build, lookups, generate-on-demand),
   breadcrumb/state transitions of `AtlasController`. No rendering tests.
3. **Live MCP verification:** drive the editor into play mode via the bridge, click
   through galaxy → cell → system programmatically, screenshot each state. The
   **acceptance moment**: a captured drill-down from the full galaxy view to a
   homeworld's populated data panel, plus a screenshot set of all five layer
   toggles.

## 7. Out of Scope (deliberate)

- Continuous zoom / LOD (Phase 4), pan/zoom within views (v1.5 candidate).
- Orbit diagram / visual system view (dedicated later phase), sprites/art/shaders.
- Search ("find overlay" as UI), stats dashboards — REPL retains these; atlas may
  grow them later.
- Saving/loading galaxies from the UI (REPL `gsave`/`gload` remain the tools).
- Game-layer anything.
