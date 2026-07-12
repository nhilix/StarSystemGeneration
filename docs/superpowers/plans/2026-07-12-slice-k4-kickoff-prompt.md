# Slice K4 Kickoff — Session Prompt

You are starting **Slice K4 (Timeline)** — the fourth of five sub-slices
delivering the Unity atlas, under the lighter protocol in `/CLAUDE.md`
(read it first). K1 shipped the skeleton instrument, K2 the full lens
catalog, K3 made the atlas inspectable (SelectionModel, hover tooltip,
pinnable InspectorDock with every §9 panel at REPL parity, Open Threads
as the opening screen, top bar + registry drawer, per-lens legend — all
in the cassette × ice UI language). K4 makes the atlas **move**: the
`watch` experience, rendered — epoch keyframes, a timeline strip, scrub,
and play/step at coarse or fine tick.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules (`unity/ProjectSettings` churn
   stays uncommitted, always).
2. `docs/superpowers/plans/2026-07-11-slice-k-roadmap.md` — row K4 and
   the gates.
3. **The interface spec**:
   `docs/superpowers/specs/2026-07-11-unity-atlas-design.md` — §Time
   (grounded by Slice J's P7 certification) is K4's contract; the living
   diagram `docs/diagrams/unity-atlas-design.html` §5 (time model) + §7
   (TimeMachine / TimelineStrip components).
4. **The K3 ledger** — REQUIRED, the map of what exists:
   `docs/superpowers/plans/2026-07-12-slice-k3-ledger.md` (chrome
   architecture, panel/query surface, the worktree traps, the four
   eyeball waves).
5. `docs/superpowers/specs/2026-07-12-ui-language-design.md` — the
   visual language the strip renders in.
6. `docs/superpowers/plans/2026-07-11-slice-k-kickoff-prompt.md` — the
   whole-K inherited context and boundary.
7. `docs/HANDOFF.md` — current state.

## What K3 left ready (build on this, don't reinvent)

- **Core** (`src/Core/Atlas`, 96 atlas tests in the 790 suite): the full
  query surface — 15 lens queries + PanelQueries for every §9 panel
  (PolityPanel, MarketPanel w/ larder, ProjectPanel, ShipmentPanel,
  FleetPanel, WarPanel, RelationsPanel, CharacterPanel, CorporationPanel,
  PoiPanel, Belief/News/Stances, ChronicleQueries + **EraQueries** (era
  bands for the strip!), HandoffQueries w/ jump hexes, RegistryQueries,
  LegendQuery, HexQuery). Everything Eye-parameterized, read-only,
  id-order. **K4 adds TimeMachine beside them** (spec architecture layer
  2): epoch keyframes as delta saves (`DeltaSerializer.Diff/Apply` landed
  in J — base + changed layers only; genesis strata never re-record).
- **Chrome** (`unity/Assets/Atlas`): `AtlasChrome` owns the one
  UIDocument, the stylesheet (`Resources/AtlasChrome.uss`, `ssg-` BEM
  classes, var() tokens only, SSG-Ice theme on PanelSettings —
  **ScaleWithScreenSize @1920×1080**), the pointer guard, and named
  hosts (top bar / rail / dock / tooltip layer / legend). **The
  TimelineStrip gets a new bottom host here** — follow the host pattern,
  never a second UIDocument or guard. `DockKit` has the content
  primitives; scrollers are hidden everywhere by convention
  (`AtlasChrome.HideScrollers`).
- **SimHost** (`unity/Assets/Atlas/SimHost.cs`): loads artifacts, raises
  `Loaded`; every layer rebuilds by re-`Prepare`-ing on it. **K4 grows
  run-seed/step here** (the spec's writer role) — stepping likely wants a
  `Stepped`/re-`Loaded` event so all K1–K3 layers and the TopBar clock
  refresh; they already rebuild fully on `Loaded`, so re-raising it after
  a step or scrub is the cheap integration (measure before inventing
  incremental refresh).
- **TopBar** shows year · epoch · era (via EraQueries) and refreshes on
  `Loaded` — the strip's readouts should share, not duplicate, this.
- **Selection** (`SelectionModel`): plane picking, hex-ring highlight,
  right-click clear; hover tooltip (0.45s rest). After a scrub, open
  panels show the loaded keyframe's state when re-requested — decide and
  document whether panels auto-refresh on step (they are built once per
  Show; the dock could re-Show unpinned panels on `Loaded`).
- **AtlasSmoke** (StarGen > Atlas Smoke Shots / CLI twin) renders the
  lens suite; UI Toolkit still does NOT render in batch captures — K3
  gated panels on the in-editor eyeball; K4's animation gate is
  in-editor by nature.

## Scope (K4, roadmap row)

- **TimeMachine** (`src/Core/Atlas`, TDD): capture epoch keyframes while
  stepping, stored as delta saves against the loaded base; snap-to-
  keyframe scrub (re-query everything); chronicle/era queries stay
  log-backed (no keyframe needed).
- **TimelineStrip** (bottom chrome, cassette × ice): era bands
  (EraQueries), event-density sparkline (EventLog), world-year scrubber,
  active-tick marker.
- **Play / step, coarse and fine**: the same engine steps the loaded
  artifact at any `YearsPerEpoch` (`estep n years` pattern —
  `GenerationYears` stays the calendar); play = step-per-interval with
  the domains lens animating (the `ewatch` experience).
- **Resolution change forks a branch** from the current keyframe (one
  timeline belongs to one (config, tick-path) run — spec §Time).
- **SimHost run-seed in-editor** (the PoC GalaxyService pattern pointed
  at the epoch sim) — the artifact-load default stays.

**Boundary:** no system stage (K5) · panels/lenses are done — K4 only
makes them refresh over time (a panel bug found is a fix, not a feature)
· no new sim mechanics; stepping uses `EpochEngine` as-is (watching is
certified byte-invisible) · controller eye stays a seam · per-lens
readability deep-dives stay backlog.

## Traps K3 hit (save yourself the hour)

- **Worktree setup**: `unity/Packages/manifest.json`,
  `packages-lock.json`, and `src/Core/csc.rsp` are GITIGNORED — copy all
  three from the main checkout into a fresh worktree BEFORE any Unity
  batch run, or you get a bare manifest and C#9 errors.
- **Batch vs editor**: Unity batchmode cannot run while the editor holds
  the project (stale test-results XML lies — delete it before re-runs
  and check the log tail). The editor-side MCP bridge can verify
  compiles/console live when the editor is open.
- **Generated menu assets** (scanline.png, MainMenuPanelSettings, menu
  scene) are gitignored by design; the menu builder self-heals the
  USS→texture import-order trap now.
- Unity-side smoke can't capture UI Toolkit; plan the acceptance loop
  around the in-editor eyeball early.

## Carried flags (not K4 scope, know they exist)

- **Credit-loop equilibrium** (user-flagged at the K3 eyeball): every
  polity ends deeply negative; Borrow needs a 2.4× lender so galaxy-wide
  insolvency never clears — flagged to the contract-economy slice.
- Per-lens readability deep-dives — backlog behind K5.
- K1 runtime meshes/textures lack HideAndDontSave in edit mode — sweep
  opportunistically (K3's new resources all use it).

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-k4-timeline` from main **in a fresh worktree** (the
   contract-economy session may still be live; never share a checkout);
   ledger `docs/superpowers/plans/YYYY-MM-DD-slice-k4-ledger.md`.
3. TDD the TimeMachine (keyframe capture/restore byte-identity against
   DeltaSerializer; scrub determinism); EditMode where it pays; the
   animation eyeball is in-editor.
4. Gates: `dotnet test` green · golden untouched · determinism untouched
   (stepping a COPY never mutates goldens) · EditMode green · smoke
   still renders every lens.
5. User gates: scope nod · **timeline eyeball** (roadmap: watch 40
   epochs animate on the domains lens; scrub back to a mid-war year;
   step fine) · merge decision.
6. Wrap-up: merge · HANDOFF · tick K4 in the K roadmap · **write the K5
   kickoff prompt** (system stage & closeout) · republish the living
   diagram if the time model taught us anything · push only on say-so.

- [ ] Slice K4 complete
