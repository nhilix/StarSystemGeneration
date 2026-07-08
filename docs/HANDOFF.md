# Session Handoff — 2026-07-08

State: `main` at `57d0863` (all branches merged and deleted; push to origin is manual and pending).
Core tests 109/109 (`dotnet test StarSystemGeneration.sln`); Unity edit-mode 19/19 (batchmode, editor closed).
Durable task ledger: `.superpowers/sdd/progress.md` (git-ignored — read it before re-doing anything).

## Just shipped (context, don't redo)

- **Unity atlas** (galaxy → cell → hex drill-down, 5 layers, system data panel) — plan `docs/superpowers/plans/2026-07-08-unity-atlas.md`.
- **Circular galaxy footprint** — `InGalaxy` is Euclidean on cell centers; radius 21 = 1,615 cells; spiral arms no longer cropped.
- **Setup knobs + live preview** — spec `docs/superpowers/specs/2026-07-08-atlas-setup-knobs-design.md`, plan `...plans/2026-07-08-atlas-setup-knobs.md`. 12 sliders (Shape/Resources/History foldouts), density preview rebuilds per edit via `SkeletonBuilder.BuildShape` (no sim), artifact schema v3. User raised the radius clamp 45 → 100 (`57d0863`).

## Next up (pick one)

1. **Sim economy slice** (biggest): regional spec stages 2–3 — polity budgets/military stockpiles, commodities/trade value/blockades; then stages 4–6 (federations, reputation/news propagation, event→POI compiler). Spec: `docs/superpowers/specs/2026-07-07-regional-generation-design.md`. Needs brainstorm→plan cycle for the slice boundary.
2. **Orbit-diagram system view** — completes roadmap phase 2 (DESIGN.md §4): render one system's stars/orbits/bodies/satellites as a 2D diagram; the data panel half already exists.
3. **Atlas polish batch** (small): smooth pan/zoom; cell-view hover highlight; "Cell (q,r)" breadcrumb should clear hex selection (currently enabled but no-op, plan-mandated); Atlas.unity as build scene 0 (currently SampleScene).

## Deferred ticket items (small, from final reviews — details in ledger)

- Golden literal test pinning the exact v3 CONFIG line field order.
- Skip preview rebuild when only sim-only knobs change (epochs, years, homeworld rate, anchor mults).
- Move `_setupErrorLabel` out of the ScrollView so build-failed messages are always visible.
- `IsShapeOnly` guard: `Generate`/`StateOf` on a shape-only service should throw, not misbehave quietly.
- Dedupe show-preview block in `AtlasController` (Render Setup branch vs `RenderPreview`).
- Radius >45 preview cost: consider `_radiusField.isDelayed = true` now that the clamp is 100 (~12k cells per keystroke at max).
- REPL `galaxy` command doesn't take the new knobs (UI-only for now).
- Older Core items: ValueNoise octaves≤0 guard, WarStarted dedup, REPL goto wrap, StatsReport ignores satellites.

## Process conventions (established, keep following)

- Superpowers flow: brainstorm → spec (user reviews) → plan → **subagent-driven** execution → final whole-branch review (fable model) + one fix wave → finishing-a-development-branch → **merge to main locally**, verify tests, delete branch. User pushes manually.
- Implementer reports must include verbatim test-summary lines; Unity runs also need `stat` timestamp of `unity/test-results.xml` (fabrication incident happened once).
- Cheap model for transcription tasks (plan contains full code), sonnet for integration, fable for final review.

## Unity gotchas (hard-won)

- Batchmode gates need the **editor closed**; live acceptance needs it **open** with the MCP bridge. Coordinate with the user.
- MCP RunCommand sandbox: can reference `StarGen.Atlas` but NOT `StarGen.Core` types; use `AtlasAcceptance` menu items (`StarGen/Acceptance/...`) or UI-tree queries. `GetInstanceID` is obsolete → `GetEntityId`.
- Unfocused editor doesn't tick the player loop: use `EditorApplication.Step()` to advance frames and manual `Camera.Render()`→RenderTexture→PNG instead of `ScreenCapture` (never flushes unfocused).
- UI Toolkit `Foldout` headers are `Toggle`s — always scope Toggle queries (e.g. `Q("layer-toggle-row")`).
- Screenshots lag UI chrome by one frame vs mesh recolor; trust live UI-tree queries over captures.
