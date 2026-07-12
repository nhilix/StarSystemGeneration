# Session Handoff — 2026-07-12 (Slice K3, Selection & panels — MERGED)

State: `slice-k3-panels` merged to `main` locally (not pushed — push on
say-so). Gates at merge: **790/790 dotnet** (determinism suites in the
count, golden untouched — K3 adds zero sim behavior) · **10/10 EditMode**
(incl. new LegendDriftTests) · AtlasSmoke renders every lens ·
fresh-eyes whole-branch review ("one confirmed bug") + fix wave landed ·
**user eyeball ACCEPTED 2026-07-12** after four fix waves (below).
ProjectSettings churn stays uncommitted.

## Slice K3 — Selection & panels + the unified UI layer (closed)

Ledger `docs/superpowers/plans/2026-07-12-slice-k3-ledger.md` (per-task,
decisions, review findings, eyeball waves, carried flags).

- **Core** (`src/Core/Atlas`, 16 new files, 96 atlas tests in the 790
  suite): PanelQueries for every diagram-§9 panel at REPL parity —
  PolityPanel (+ ReservePoints, standing plan w/ eplan's in-flight ★),
  MarketPanel (+ the located larder; capacity/decay ride the SAME
  MarketEngine derivations the sim uses — StockPerishFactor/
  ActiveDepotTiersAt extracted, behavior-identical), ProjectPanel
  (honest starvation eta), ShipmentPanel (STALLED; the K2 clock-edge
  note RESOLVED: SeveredLaneIds folds quarantine at >=, so efreight's
  effective edge matches the lane lens), FleetPanel (+Designs), WarPanel
  (falls-at via SiegeThreshold), RelationsPanel (6+6 source terms),
  CharacterPanel, CorporationPanel (funded projects via FunderActorId),
  PoiPanel, Belief/News/Stances, ChronicleQueries (Annotated/ForActor/
  AtPlace/DeepTime) + EraQueries, RegistryQueries (Find w/ jump hexes,
  Stats, Goods, Knobs), LegendQuery (rail key → entries from the lens
  constants themselves — drift-proof; GlyphKey = AtlasGlyph member
  names), HexQuery (tooltip content). Core amendment: OpenThread carries
  SubjectId/SubjectId2 (camera jumps).
- **Chrome** (`unity/Assets/Atlas`, cassette × ice everywhere):
  AtlasChrome owns the one UIDocument + `Resources/AtlasChrome.uss`
  (`ssg-` BEM, var() tokens only) + THE pointer guard + named hosts ·
  PanelSettings → SSG-Ice.tss, **ScaleWithScreenSize @1920×1080** ·
  LensRail re-skinned (year readout retired) · SelectionModel (plane
  picking, no colliders; click-vs-drag; right-click clears; hex-RING
  mesh highlight in the lattice's grammar, never LOD-fades) · HexTooltip
  (0.45s hover-rest) · InspectorDock (pinnable; port click opens Market
  + owner Polity; every §9 panel via PanelViews/DockKit) · **Open
  Threads opens on load, rows jump the camera** · TopBar (GOD chip ·
  year/epoch/era · config stamp · find search · THREADS/STATS/GOODS/
  KNOBS drawer · artifact load box) · LegendPanel (sprite-crops the
  authored glyph atlas by enum cell) · scrollers hidden everywhere.
- **Acceptance**: panel capture stays on the in-editor eyeball (UI
  Toolkit never renders in batch cam.Render()); EditMode LegendDriftTests
  pin the legend↔glyph contract.

## Eyeball waves (all landed in-branch)

1. Atlas PanelSettings was ConstantPhysicalSize → tiny on big displays;
   now ScaleWithScreenSize; dock 470px/60%, type +2px across chrome;
   scroll bars hidden (wheel scroll stays).
2. Menu scanline rendered as a yellow screen-door: on a fresh checkout
   MainMenu.uss imports BEFORE the builder generates scanline.png →
   broken url() → UITK missing-image placeholder. Builder now rebinds
   the USS and imports the tile as Default/no-dilation. Generated menu
   assets un-tracked + gitignored (spec: no binaries in git).
3. Tooltip hover-rest 0.45s; right-click clears selection.
4. Selection highlight = hexagonal ring MESH on HexGrid.CornerOffsets
   (the lattice's grammar, bolder, no LOD fade) — LineRenderer rejected.

## Carried / flagged

1. **Credit-loop equilibrium (user-flagged, → contract-economy slice)**:
   every entered polity holds deeply negative Credits (seed-42: −6k to
   −278k). Deficit financing is intentional (AllocationPhase budgets
   max(Credits, Receipts)) but Phases.Borrow needs a lender at 2.4× the
   hole — once ALL polities are negative no lender exists, insolvency
   never clears, and host.Credits ≤ 0 silently disables the
   corp-leverage/nationalization thread. Panel labels it "(in deficit —
   credit-financed)".
2. **Worktree traps (K4/K5, real cost)**: `unity/Packages/manifest.json`,
   `packages-lock.json`, `src/Core/csc.rsp` are GITIGNORED — copy from
   the main checkout before any Unity batch run. Batchmode cannot run
   while an editor holds the project (and stale test-results XML lies —
   delete before re-runs). The editor MCP bridge verifies compiles live.
3. Per-lens readability deep-dives — backlog (behind K5 / gap list).
4. K1 runtime meshes/textures lack HideAndDontSave in edit mode — sweep
   opportunistically (K3's new resources all set it).
5. Menu F1–F4 remain stubs; NEW GALAXY hands its seed to the atlas flow
   in a later slice.

## Next up

1. **Slice K4 (Timeline)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-12-slice-k4-kickoff-prompt.md`
   (TimeMachine keyframes as delta saves · TimelineStrip w/ era bands +
   event sparkline + scrubber · play/step coarse+fine · resolution fork ·
   SimHost run-seed; includes K3's chrome integration notes and traps).
2. **Contract economy** — still queued (parallel session may be live):
   `docs/superpowers/plans/2026-07-11-contract-economy-kickoff-prompt.md`
   — now carries the credit-loop equilibrium flag above.
3. Then K5 (system stage & closeout) per the K roadmap.
4. User read-through of the design specs — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings
stays uncommitted; bash printf for REPL piping; parallel slices take
worktrees (never a shared checkout); every new `src/Core` file gets a
two-line `.meta` with a fresh guid; the design is the spec — deviations
amend `docs/design/` in-branch. The living atlas diagram
(`docs/diagrams/unity-atlas-design.html`) is republished to its stable
URL on change (§9 gained the Project/Shipment rows + larder/plan notes
this slice). Unity gates: `Unity -batchmode -runTests -testPlatform
EditMode` + AtlasSmoke batch twin (editor 6000.5.2f1).
