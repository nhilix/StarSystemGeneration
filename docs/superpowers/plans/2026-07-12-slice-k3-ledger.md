# Slice K3 Ledger — Selection & Panels + the Unified UI Layer

Branch `slice-k3-panels` off main (`ae19064`, the UI-language merge), in
worktree `.claude/worktrees/slice-k3-panels` (contract-economy session runs
parallel — never a shared checkout). Governing plan:
`2026-07-11-slice-k-roadmap.md` row K3; kickoff:
`2026-07-12-slice-k3-kickoff-prompt.md`; design of record:
`docs/superpowers/specs/2026-07-11-unity-atlas-design.md` §panels + diagram §9
(amended by the T1/T2 additions named in the kickoff) +
`docs/superpowers/specs/2026-07-12-ui-language-design.md` (the visual language).

Scope nod 2026-07-12: kickoff scope confirmed unamended — unified UI layer
(cassette × ice; K2 rail re-skinned, PanelSettings → SSG-Ice.tss),
SelectionModel + hover tooltip, InspectorDock (pinnable), PanelQueries/
ChronicleQueries/HandoffQueries in `src/Core/Atlas` with REPL-parity xUnit
tests, typed panels (Open Threads as opening screen w/ camera jumps; Polity
+ ReservePoints + standing plan; Market + located larder; NEW Project
inspector w/ LastFedFraction; NEW Shipment card w/ live ETA/STALLED; Fleet;
War; Relations; Character/Bio; Corporation = funded projects only; POI;
Belief/News/Stances; Chronicle/Eras), registry drawer (find/stats/goods/
knobs), top bar (eye chip · year/epoch/era · config stamp · artifact load
box; rail year readout retires), per-lens LEGEND (one authoritative mapping,
no drift). Menu-scene in-editor eyeball folds into the first play-mode
eyeball. Boundary: no timeline (K4), no system stage (K5), read-only queries
only, controller eye stays a seam, no corp standing plans, lens readability
deep-dives stay backlog.

Baseline at branch: **727/727** `dotnet test` in the fresh worktree.

## Parity target map (renderer → K3 query)

REPL renderers live in `src/Inspector` (InteriorView, InterpolityView,
MarketView, FleetView, NarrativeView, EpochMapView) + three Repl-local
renderers (RenderProjects/RenderPlan/RenderFreight) + Core-side
`HandoffView.OpenThreads`/`EraDetector`. Parity = the K2 pattern
(TrafficLensTests): queries consume the same Core ops; tests assert the
derivations (bands, ETAs, filters, orderings) against Core directly —
Core.Tests never references Inspector.

## Tasks

- [x] **T0 — Worktree + baseline**: `slice-k3-panels` @ ae19064, 727/727.
- [ ] **T1 — Core panel queries, TDD** (`src/Core/Atlas`, two-line .meta
      per file; Eye-parameterized; id-order iteration):
  - [ ] HandoffQueries (threads parity + jump-target hex per row)
  - [ ] PolityPanel (polity/tech/characters + ReservePoints + Plan w/
        in-flight star — eplan parity)
  - [ ] MarketPanel (market parity + larder: StockQty/StockGrade per good,
        capacity from tier × StockCapPerPortTier + depot tiers ×
        StockCapPerDepotTier, decay)
  - [ ] ProjectPanel (eprojects parity: honest ETA under LastFedFraction,
        funder vs owner)
  - [ ] ShipmentPanel (efreight parity: route, cargo+grades, sailed/total,
        live ETA, STALLED derivation)
  - [ ] FleetPanel (fleet/designs parity)
  - [ ] WarPanel (wars/war <id> parity) + RelationsPanel
  - [ ] CharacterPanel (characters/bio) + CorporationPanel (funded projects
        via Project.FunderActorId)
  - [ ] PoiPanel + BeliefPanel/NewsPanel/StancesPanel
  - [ ] ChronicleQueries (log + place/actor filters, era-annotated) +
        EraQueries
  - [ ] RegistryQueries (find/stats/goods/knobs)
  - [ ] LegendQuery — the authoritative per-lens legend table (glyph cells,
        color ramps, lane stroke states) sourced from the lens constants so
        the legend can never drift
- [ ] **T2 — Unified UI chrome foundation**: read
      `.claude/skills/translating-css-to-uss/SKILL.md` FIRST; atlas chrome
      USS (structure, `ssg-` classes, var() tokens only);
      `Assets/Atlas/PanelSettings.asset` theme → `SSG-Ice.tss`; LensRail
      re-skinned (inline style constants → USS where practical)
- [ ] **T3 — SelectionModel + picking + hover tooltip** (plane
      intersection → `HexGrid.WorldToHex`/`CellOf`, no colliders; tooltip:
      system summary, owner, port tier, live POI)
- [ ] **T4 — InspectorDock + typed panels** (right side, pinnable;
      AtlasPointerGuard extended, not duplicated)
- [ ] **T5 — Open Threads opening screen** (opens on load; rows jump the
      camera to their subjects)
- [ ] **T6 — Top bar + registry drawer** (eye chip god/controller-reserved ·
      world-year + epoch + era · config stamp · artifact load box; rail year
      readout retires; drawer behind top-bar search)
- [ ] **T7 — Per-lens LEGEND UI** (rail/dock chrome, LegendQuery-driven)
- [ ] **T8 — Acceptance tooling**: panel capture in AtlasSmoke or gate on
      in-editor eyeball (UI Toolkit doesn't render via cam.Render());
      EditMode tests where they pay
- [ ] **T9 — Fresh-eyes whole-branch review** + one fix wave
- [ ] **T10 — Gates**: `dotnet test` green ×2 · golden untouched ·
      determinism untouched · EditMode green · smoke renders every lens
- [ ] **T11 — USER: panels eyeball** (click port → polity/market w/ REPL
      numbers incl. larder; click site → starvation readout; threads rows
      jump camera; menu scene eyeball folded in)
- [ ] **T12 — Wrap-up**: merge · HANDOFF · tick K3 in roadmap · write K4
      kickoff (timeline) · republish diagram (§9 Project/Shipment rows +
      larder/plan additions) · push on say-so

## Decisions / deviations

(recorded as they happen)

## Carried notes (from K2)

- Next free view-only RollChannel: VERIFY before claiming (75 was taken).
- K1 runtime meshes/textures lack HideAndDontSave in edit mode — sweep
  opportunistically.
- Quarantine clock edge (lanes `>=` vs freight stall `>`) is upstream — K3
  ports faithfully wherever it surfaces (ShipmentPanel STALLED uses `>`).
- Plague lens legitimately empty on seed-42 y1000 — mid-plague year demos it.
