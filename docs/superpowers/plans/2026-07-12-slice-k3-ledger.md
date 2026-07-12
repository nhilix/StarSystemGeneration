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
- [x] **T1 — Core panel queries, TDD** (`src/Core/Atlas`, 13 new files w/
      two-line .meta; Eye-parameterized; id-order iteration; **786/786**
      full suite at completion, 59 new atlas panel tests):
  - [x] HandoffQueries (threads Kind/Text verbatim via HandoffView +
        jump hex; Core amendment: OpenThread carries SubjectId/SubjectId2)
  - [x] PolityPanel (+ ReservePoints, Credits, eplan plan w/ in-flight star)
  - [x] MarketPanel (+ larder; StockPerishFactor/ActiveDepotTiersAt
        EXTRACTED to MarketEngine so sim and panel share one derivation)
  - [x] ProjectPanel (honest starvation eta, funder vs owner, basket)
  - [x] ShipmentPanel (STALLED: the K2 clock-edge note RESOLVED — severed
        set folds quarantine in at >=, so efreight's effective edge is >=)
  - [x] FleetPanel (typed StationKind; vectors ARE FleetOps.Vectors)
  - [x] WarPanel (falls-at via WarConduct.SiegeThreshold; side strength
        fraction) + RelationsPanel (BothLive filter, 6+6 source terms)
  - [x] CharacterPanel + CorporationPanel (funded projects by FunderActorId)
  - [x] PoiPanel + BeliefPanel/NewsPanel/StancesPanel (verdicts ±0.3)
  - [x] ChronicleQueries (Annotated/ForActor/AtPlace/DeepTime) + EraQueries
  - [x] RegistryQueries (Find across registries w/ jump hexes · Stats =
        registry counts, an interpretation of the REPL's hex-tier `stats`
        for the drawer · GoodsCatalog · Knobs w/ live values)
  - [x] LegendQuery (rail-key → entries; lens color constants publicized
        as the single source; GlyphKey = AtlasGlyph enum member names —
        Unity side must Enum.TryParse-verify at T7)
- [x] **T2 — Unified UI chrome foundation** (USS skill read first):
      `Assets/Atlas/Resources/AtlasChrome.uss` — the whole K3 chrome
      vocabulary (topbar/rail/chips/dock panels/kv/meters/rows/tags/
      tooltip/legend), `ssg-` BEM classes, var() tokens only ·
      `AtlasChrome.cs` owns the UIDocument + hosts + THE pointer guard ·
      PanelSettings themeUss → SSG-Ice.tss (guid swap) · LensRail rebuilt
      onto classes (year readout retired, ActiveLegendKey/LensChanged
      exposed for T7) · scene setup builds one AtlasChrome GO ·
      EditMode 8/8, zero compile errors
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

- **Worktree trap (RESOLVED, keep for K4/K5)**: `unity/Packages/
  manifest.json`, `packages-lock.json`, and `src/Core/csc.rsp` are
  GITIGNORED — a fresh worktree lacks them and Unity regenerates a bare
  manifest (no com.stargen.core, no InputSystem, C#9 errors). Copy all
  three from the main checkout before any Unity batch run.
- Legend architecture: rail keys → LegendQuery entries; glyph rows carry
  AtlasGlyph member names as GlyphKey strings (Unity Enum.TryParse — an
  EditMode drift test rides T7).
- RegistryQueries.Stats = registry counts (the drawer's stats face), an
  atlas-appropriate reading of the REPL's hex-tier `stats`.

## Carried notes (from K2)

- Next free view-only RollChannel: VERIFY before claiming (75 was taken).
- K1 runtime meshes/textures lack HideAndDontSave in edit mode — sweep
  opportunistically.
- Quarantine clock edge (lanes `>=` vs freight stall `>`) is upstream — K3
  ports faithfully wherever it surfaces (ShipmentPanel STALLED uses `>`).
- Plague lens legitimately empty on seed-42 y1000 — mid-plague year demos it.
