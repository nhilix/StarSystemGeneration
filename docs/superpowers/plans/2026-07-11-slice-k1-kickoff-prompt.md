# Slice K1 Kickoff — Session Prompt

You are starting **Slice K1 (Skeleton instrument)** — the first of five
sub-slices delivering the Unity atlas rebuild, under the lighter protocol
in `/CLAUDE.md` (read it first). K1 ships a thin end-to-end instrument:
a seed-42 artifact loads in-editor, an Eye-parameterized read model feeds
a rendered galaxy map, and the camera zooms galaxy→hex. The PoC atlas is
deleted outright at branch start. The eyeball gate is the P1 signature
image: wilds dark, domain glows with organic borders, lanes as literal
highways.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules (`unity/ProjectSettings` churn
   stays uncommitted, always).
2. `docs/superpowers/plans/2026-07-11-slice-k-roadmap.md` — the governing
   K plan: five sub-slices, transition rules (aggressive PoC deletion,
   Core behavior-frozen, eyes from day one), what is K1's vs later.
3. **The interface spec — the design of record**:
   `docs/superpowers/specs/2026-07-11-unity-atlas-design.md` + the living
   diagram `docs/diagrams/unity-atlas-design.html` (published artifact,
   URL inside; component inventory §7, lens catalog §8). Architecture:
   Eye-parameterized `AtlasReadModel` in `src/Core/Atlas`, thin Unity
   presentation, five-LOD zoom continuum. Deviations amend the spec
   in-branch, flagged.
4. `docs/superpowers/plans/2026-07-11-slice-k-kickoff-prompt.md` — the
   original whole-K kickoff, inherited context: **"What slice J left
   ready"** (ArtifactSerializer.Load, EpochMapView derivations to port,
   territory derives from the port registry — never stored), the design
   docs the atlas renders (its reading-list item 3), and the K boundary.
5. **How Unity sees Core**: `src/Core` IS the Unity package
   `com.stargen.core` (`unity/Packages/manifest.json`:
   `file:../../src/Core`; `StarGen.Core.asmdef`,
   `noEngineReferences: true`) — every new Core file carries a two-line
   `.meta` with a fresh guid. Core is netstandard2.1, no Unity deps.
6. **The PoC atlas — read before deleting** (~2,200 lines,
   `unity/Assets/Scripts/Atlas/*.cs`, `unity/Assets/Editor/
   AtlasSceneSetup.cs` + `AtlasAcceptance.cs`): salvage the RENDERING
   lessons by reading — HexMeshBuilder's mesh construction, LayerPalette's
   palette discipline, Navigator's camera handling, the UI Toolkit panel
   pattern, the EditMode-test pattern. Then delete it all at branch
   start; git history is the archive. Nothing is kept compiling.
7. `docs/HANDOFF.md` — current state.

## Scope (K1 only)

- **Delete the PoC atlas** — `unity/Assets/Scripts/Atlas/` (incl. Tests),
  `AtlasSceneSetup.cs`, `AtlasAcceptance.cs` — first commit on the branch.
- **`src/Core/Atlas` read model (plain C#, xUnit-covered)**:
  - `EyeContext` — `(scope: God | ActorId, worldYear)`; God implemented,
    ActorId a compiling seam (throws or returns God-equivalent, marked
    reserved). Views never know which eye looks.
  - `AtlasReadModel` — the single query surface over a loaded `SimState`.
  - LensQueries for K1's trio: **nature** (lean · gas · metal · age ·
    minerals · bio · emergence · features — the genesis rasters),
    **domains** (port-derived territory, organic borders, contested
    overlap visible — port EpochMapView's derivation, never store
    ownership), **lanes** (built highways; quarantine/sever state).
    Pure functions of (Eye, ReadModel) → primitives; no ASCII, no Unity
    types.
- **SimHost** (Unity side) — loads an artifact file via
  `ArtifactSerializer.Load`; the only component touching sim state. No
  stepping, no run-seed (K4).
- **MapSurface** — hex mesh + star rendering at epoch-sim scale, fed by
  lens primitives.
- **CameraRig/LODController** — galaxy → domains → region → hex bands
  (per the spec's zoom table; the system crossfade is K5). Per-lens fade
  curves owned by the LODController.
- **Provisional lens toggle** — any minimal UI to switch/stack the three
  lens groups; the real left rail is K2.

**Boundary:** no other lenses (K2) · no selection, tooltips, or panels
(K3) · no timeline or stepping (K4) · no system stage (K5) · no new sim
mechanics, no Core behavior changes — Core additions are read-only view
helpers only. Controller eye stays a seam.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-k1-skeleton` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-k1-ledger.md`. Never share a
   checkout with another live session — take a `git worktree`.
3. TDD the read model in `tests/Core.Tests`; Unity EditMode tests where
   they pay (mesh counts, LOD band selection — the PoC's pattern).
4. Gates: `dotnet test` green · golden untouched · determinism
   byte-identity untouched · the atlas opens and renders seed 42.
5. User gates: scope nod · **atlas eyeball** (load the seed-42 artifact:
   wilds visibly dark, domain glows with organic borders and contested
   overlap, lanes as literal highways; zoom galaxy→hex smoothly; nature
   lenses toggle under the political ones) · merge decision.
6. Wrap-up: merge · HANDOFF · tick K1 in the K roadmap · **write the K2
   kickoff prompt** (lens catalog — informed by the real read-model
   interfaces that just landed) · update/republish the living design
   diagram if the build deviated · push only on user say-so.

- [x] Slice K1 complete
