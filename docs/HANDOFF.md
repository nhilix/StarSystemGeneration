# Session Handoff — 2026-07-08 (evening)

State: `main` at `87a171e` (orbit-diagram merged ff, branch deleted; push to origin is manual and pending — now 12 commits ahead).
Core tests 109/109 (`dotnet test StarSystemGeneration.sln`); Unity edit-mode 50/50 (batchmode, editor closed).
Durable task ledger: `.superpowers/sdd/progress.md` (git-ignored — read it before re-doing anything).

## Just shipped (context, don't redo)

- **Orbit-diagram system view** — roadmap phase 2 complete (DESIGN.md §4 updated). Spec
  `docs/superpowers/specs/2026-07-08-orbit-diagram-design.md` (incl. two acceptance errata),
  plan `docs/superpowers/plans/2026-07-08-orbit-diagram.md`. Fourth drill screen
  (`AtlasScreen.System`): nested-concentric diagram, one vertex-colored mesh
  (`OrbitLayout` pure geometry → `OrbitMeshBuilder.Compose` → `SystemView`), companions on
  widened gaps (≥2·DR) with sub-ring-scaled bodies, star halos / gas bands / ocean blobs
  (unkeyed overlays so selection recolor can't wipe them), hover tooltips, click →
  `SystemPanel.Highlight` row scroll. Entry: second click on selected hex or panel button;
  Cell breadcrumb now clears hex selection (old deferred ticket closed). Live acceptance:
  REPL dump exactly matched Unity for seed 42 hex (77,-34) — phase-2 done-when.
- **Setup knobs + live preview**, **circular footprint**, **Unity atlas** — see previous
  handoff sections in git history if needed.

## Next up (pick one)

1. **Sim economy slice** (biggest): regional spec stages 2–3 — polity budgets/military
   stockpiles, commodities/trade value/blockades; then stages 4–6. Spec:
   `docs/superpowers/specs/2026-07-07-regional-generation-design.md`. Needs brainstorm→plan
   cycle for the slice boundary.
2. **Atlas polish batch** (small): smooth pan/zoom (incl. system screen); cell-view hover
   highlight; Atlas.unity as build scene 0; orbit-diagram follow-ups below could ride along.
3. **Deferred ticket batch**: see below.

## Deferred ticket items (small — details in ledger)

Orbit-diagram (from final review + task reviews):
- `ClearHexSelection()` needs a screen guard — automation calling it on the System screen
  crashes `RenderSystemScreen` (`SelectedHex!.Value`). Only automation-reachable.
- Companion-moon pad scaling untested; `Stars.Count == 0` bounds fallback unreachable by tests.
- Doc note: multi-primary / zero-slot-primary inputs silently drop stars from the diagram.
- Spec note: adjacent companions can visually overlap sub-ring swaths at hash-adjacent angles.
- `SystemPanel.Highlight` silently no-ops on unknown BodyRef; `EnterSystem` exception message
  reads oddly from the System screen.
- Spec §8 follow-ups: in-diagram name labels (world→screen UI Toolkit), orbital-motion toggle,
  hover recolor on the diagram, pan/zoom.

Older (carried forward): golden v3 CONFIG-line literal test; skip preview rebuild on sim-only
knobs; `_setupErrorLabel` outside ScrollView; `IsShapeOnly` guard; dedupe show-preview block;
`_radiusField.isDelayed`; REPL `galaxy` knob parity; ValueNoise octaves≤0 guard; WarStarted
dedup; REPL goto wrap; StatsReport ignores satellites.

## Process conventions (established, keep following)

- Superpowers flow: brainstorm → spec (user reviews) → plan → subagent-driven execution →
  final whole-branch review (fable) + one fix wave → finishing-a-development-branch →
  merge to main locally, verify tests, delete branch. User pushes manually.
- Implementer reports MUST include verbatim test-summary lines + `test-results.xml`
  LastWriteTime. Two more gate-reporting incidents this feature (T3 skipped the gate with a
  fabricated excuse; T4 tried to pkill Unity when blocked) — keep the "gate mandatory, never
  kill processes" language in dispatch prompts.
- Cheap model for transcription tasks (plan contains full code), sonnet for integration,
  fable for final review.
- Mid-acceptance visual feedback is handled controller-inline (fix waves committed after
  gates), not via subagent — established this feature and prior.

## Unity gotchas (hard-won)

- Batchmode gates need the **editor closed**; live acceptance needs it **open** with the MCP
  bridge. Coordinate with the user.
- MCP RunCommand sandbox: `StarGen.Atlas` OK, `StarGen.Core` types NOT (even
  `Navigator.SelectedHex` trips CS0012 — log via menu items + a
  `Application.logMessageReceived` hook to capture their output).
- `Unity_GetConsoleLogs` can return empty even when logs exist — capture via the log hook.
- Unfocused editor doesn't tick the player loop: `EditorApplication.Step()` + manual
  `Camera.Render()`→RenderTexture→PNG (never `ScreenCapture`).
- UI Toolkit `Foldout` headers are `Toggle`s — always scope Toggle queries.
- PowerShell piping to the REPL mangles the FIRST stdin line (BOM) — use bash
  `printf 'cmd\n...' | dotnet run --project src/Inspector`.
- REPL syntax: `galaxy <seed> [radiusCells]`, `goto <q> <r>` (axial, e.g. SGC 2125-2014 →
  77 -34 via −2048 bias).
