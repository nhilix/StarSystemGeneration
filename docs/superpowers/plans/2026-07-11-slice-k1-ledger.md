# Slice K1 Ledger — Skeleton Instrument

Branch `slice-k1-skeleton` off main (`2d8f70d`). Governing plan:
`2026-07-11-slice-k-roadmap.md`; design of record:
`docs/superpowers/specs/2026-07-11-unity-atlas-design.md`. Baseline:
562/562 green.

Scope nod: user approved the K roadmap + K1 kickoff 2026-07-11 and said
to start K1 in the same session.

## Tasks

- [ ] **T1 — PoC deletion** (first commit): `unity/Assets/Scripts/` (the
      Atlas PoC is its only content), `unity/Assets/Editor/Atlas*` + its
      asmdef, `unity/Assets/Scenes/Atlas.unity` (refs die with the
      scripts). PanelSettings.asset kept (inert, reusable). Salvage
      lessons recorded below.
- [ ] **T2 — `src/Core/Atlas` read model, TDD** (each with .meta):
  - [ ] EyeContext (God | ActorId seam, worldYear)
  - [ ] AtlasReadModel (single query surface over SimState)
  - [ ] NatureLens queries (lean · gas · metal · age · minerals · bio ·
        emergence · features rasters)
  - [ ] DomainLens queries (port-derived glow/territory, organic
        borders, contested overlap — ported from EpochMapView, never
        stored ownership)
  - [ ] LaneLens queries (built highways; quarantine/sever state)
- [ ] **T3 — Unity presentation** (`unity/Assets/Atlas`):
  - [ ] asmdefs (runtime + editor + editmode tests)
  - [ ] SimHost (artifact load via ArtifactSerializer.Load; sole state
        owner)
  - [ ] MapSurface (hex mesh + star rendering from lens primitives)
  - [ ] CameraRig/LODController (galaxy→domains→region→hex bands,
        per-lens fade curves)
  - [ ] Provisional lens toggle UI + AtlasSceneSetup equivalent
- [ ] **T4 — EditMode tests** where they pay (mesh counts, LOD band
      selection)
- [ ] **T5 — Fresh-eyes whole-branch review** subagent + one fix wave
- [ ] **T6 — Gates**: `dotnet test` green · golden untouched ·
      determinism untouched · atlas renders seed 42 in-editor
- [ ] **T7 — USER: atlas eyeball** (P1 image: wilds dark, domain glows
      with organic borders + contested overlap, lanes as highways; zoom
      galaxy→hex; nature lenses toggle)
- [ ] **T8 — Wrap-up**: merge on user nod · HANDOFF · tick K1 in the K
      roadmap · write K2 kickoff prompt · republish design diagram if
      deviated · push only on say-so

## Salvaged rendering lessons (PoC read 2026-07-11, then deleted)

- **Hex mesh**: 7 verts + 6 tris per hex, vertex-colored Color32,
  `IndexFormat.UInt32`, inset 0.08, recolorable in place
  (SetColors) — one mesh per view; geometry from `HexGrid`
  (HexToWorld/CornerOffsets/WorldToHex) as single authority.
- **Palette discipline**: pure static layer→Color32 mapping; void
  #0A0A0E, unclaimed #282828; golden-ratio polity hues
  (`id * 0.6180339887 % 1`), brightness by tier; hover = +60 RGB
  highlight of the base color.
- **Picking**: `HexGrid.WorldToHex(cam.ScreenToWorldPoint(...))` — no
  colliders; chrome occlusion via `panel.Pick(...) != root` so UI owns
  the pointer (no click-through, tooltip pickingMode Ignore).
- **Camera fit**: ortho size = max(extents.y, extents.x/aspect) × 1.08.
- **UI Toolkit chrome built entirely in code** (no UXML/USS);
  PanelSettings needs a ThemeStyleSheet (empty fallback OK).
- **Scene setup**: idempotent `MenuItem` builder + batchmode twin
  (`-executeMethod ... RunFromCli`); wires SerializedObject fields;
  PanelSettings load-or-create.
- **asmdef shapes**: runtime refs StarGen.Core + Unity.InputSystem,
  autoReferenced false; EditMode tests ref TestRunner + nunit
  precompiled, defineConstraints UNITY_INCLUDE_TESTS,
  includePlatforms Editor.
- **Input**: `UnityEngine.InputSystem` (Keyboard.current/Mouse.current)
  — the legacy Input manager is not the project's input path.

## Decisions / deviations

(none yet)
