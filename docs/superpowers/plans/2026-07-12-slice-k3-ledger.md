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
- [x] **T3 — SelectionModel + picking + hover tooltip**: plane
      intersection → WorldToHex, no colliders; click-vs-drag by pointer
      wander; priority port → site → freight → fleet → live POI → hex;
      selection ring quad; HexTooltip rides AtlasChrome's picking-ignored
      layer (HexQuery: system summary · owners · port tier · live POI)
- [x] **T4 — InspectorDock + typed panels**: pinnable (PIN keeps for
      comparison, unpinned replaced), X closes; PanelViews builds every
      §9 panel over the T1 queries (Polity w/ plan+reserves, Market w/
      larder, Project, Shipment, Fleet+Designs, Wars/War, Relations,
      Character, Corporations w/ funded links, POI, Beliefs, News+journey,
      Stances, Chronicle/Place/Eras, Find, Goods, Knobs, Stats); port
      click opens Market AND owner Polity (the eyeball line); guard owned
      by AtlasChrome alone; dock ScrollView chain picking-ignored
- [x] **T5 — Open Threads opening screen**: opens on SimHost.Loaded;
      each row jumps the camera to its subject hex (HandoffQueries)
- [x] **T6 — Top bar + registry drawer**: GOD ▮ eye chip (controller
      reserved) · y/epoch + era name (EraQueries) · config stamp (seed,
      radius, artifact) · THREADS/STATS/GOODS/KNOBS drawer buttons ·
      find search (Enter → Find panel w/ jump+open) · artifact load box
- [x] **T7 — Per-lens LEGEND UI**: LegendPanel over LegendQuery keyed by
      LensRail.ActiveLegendKey; glyph swatches sprite-crop the authored
      atlas by enum cell; EditMode LegendDriftTests pin GlyphKey↔AtlasGlyph
      (10/10 EditMode)
- [x] **T8 — Acceptance tooling**: DECISION — panel capture stays on the
      in-editor eyeball (the kickoff's sanctioned alternative: UI Toolkit
      renders in play mode only; batch cam.Render() can never include it;
      PlayMode-test screenshotting is K4+ if ever wanted). EditMode grew
      LegendDriftTests (GlyphKey↔AtlasGlyph + non-empty legends per rail
      key). AtlasSmoke unchanged and still green (renders every lens:
      191 fleets, 106 POIs, 297 projects, 16 shipments, 5 plagues).
- [x] **T9 — Fresh-eyes whole-branch review** + one fix wave. Verdict:
      "NOT READY — one confirmed bug", hard rules verified holding (Core
      purity, meta guids unique, Phases refactor provably identical,
      id-order iteration, all six parity spot-checks exact, one pointer
      guard, USS var()-only + theme-swap-clean, boundary clean). Fixed
      all four findings: **port click destroyed its own Polity panel**
      (Show's clear-unpinned ran twice — clearUnpinned:false for the
      stacked Market panel), SelectionModel marker material leak
      (OnDestroy), war panel siege text now gated on SiegeYears > 0
      (REPL parity), dead TopBar line. Fix wave itself caught one more:
      optional param broke the `Open = Show` delegate (lambda).
      Declined-as-noted: synthetic jump-hex tests for tension/succession/
      corporation threads (verified by inspection + seed-42 sweep);
      inactive-depot larder case (impossible to drift — shared derivation).
- [x] **T10 — Gates**: `dotnet test` 790/790 ×3 (determinism suites in
      the count) · golden untouched · EditMode 10/10 post-fix · smoke
      renders every lens post-fix
- [ ] **T11 — USER: panels eyeball** (click port → polity/market w/ REPL
      numbers incl. larder; click site → starvation readout; threads rows
      jump camera; menu scene eyeball folded in)
  - Eyeball wave 3 (2026-07-12, selection feel): hover tooltip now
    waits for the cursor to REST 0.45s before showing (was instant —
    spammed every hex crossed) · right-CLICK (no wander; right-drag
    stays the pan) clears the selection highlight · the highlight is now
    the actual HEX BORDER (LineRenderer on HexGrid.CornerOffsets, ice
    accent, screen-constant-ish stroke) instead of the white ring quad.
  - Eyeball wave 2 (2026-07-12, menu scene): the scanline overlay
    rendered as a yellow screen-door — on a fresh checkout MainMenu.uss
    imports BEFORE the builder generates scanline.png, so the compiled
    stylesheet holds a broken url() and UITK tiles its missing-image
    placeholder (plus the texture imported as Sprite w/ alpha dilation).
    Fixed live via the editor bridge (reimport + Default texture type);
    builder hardened: sets textureType=Default, alphaIsTransparency=false,
    force-reimports MainMenu.uss after first generating the texture.
  - Eyeball wave 1 (2026-07-12): (1) atlas PanelSettings was
    ConstantPhysicalSize — tiny on big displays; now ScaleWithScreenSize
    @1920x1080, match height (the menu builder already did this) ·
    (2) panels earn more space: dock 340->470px, panel max-height
    46%->60%, body type 11->13px, chips 22->27px, rail 200->236px, all
    chrome type up 2px · (3) scroll bars HIDDEN everywhere (wheel
    scrolling stays — ScrollerVisibility.Hidden on rail, dock, panel
    bodies; AtlasChrome.HideScrollers is the one place)
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
- **FLAGGED UPSTREAM (user, K3 eyeball; for the contract-economy slice)**:
  every entered polity in the seed-42 golden holds deeply negative Credits
  (-6k..-278k). Not a K3 display bug (panel shows registry truth; the REPL
  never surfaced this number). Deficit financing is intentional
  (AllocationPhase budgets max(Credits, Receipts) — "credit picks up the
  slack") BUT the credit loop cannot equilibrate: Phases.Borrow needs a
  lender with Credits >= 2.4x the hole, and once ALL polities are negative
  no lender exists — insolvency never clears, and host.Credits <= 0 also
  silently disables the corp-leverage/nationalization thread. The panel
  now labels negative treasuries "(in deficit — credit-financed)" in warn
  color.
