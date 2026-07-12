# Slice K3 Kickoff — Session Prompt

You are starting **Slice K3 (Selection & panels)** — the third of five
sub-slices delivering the Unity atlas, under the lighter protocol in
`/CLAUDE.md` (read it first). K1 shipped the skeleton instrument (2.5D
camera, domain field shader, starfield, lanes, ports, lattice); K2
widened it to **the full lens catalog** (traffic, fleets, price ▾ good,
war, tension, tech, plague, news, POIs, **works**) behind a UI Toolkit
left-rail lens stack. K3 makes the atlas *inspectable*: click a thing,
get its story — SelectionModel, hover tooltips, the pinnable
InspectorDock with typed panels at REPL parity, **Open Threads as the
opening screen**, the registry drawer, and the top bar.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules (`unity/ProjectSettings` churn
   stays uncommitted, always).
2. `docs/superpowers/plans/2026-07-11-slice-k-roadmap.md` — the governing
   K plan; row K3 and the gates.
3. **The interface spec**:
   `docs/superpowers/specs/2026-07-11-unity-atlas-design.md` — §panels;
   the living diagram `docs/diagrams/unity-atlas-design.html` §9 is the
   panel↔REPL parity table. **NOTE: §9 predates the T1/T2 logistics
   slices — the additions below are part of K3's spec.**
4. **The K2 ledger** — REQUIRED, the map of what exists:
   `docs/superpowers/plans/2026-07-12-slice-k2-ledger.md` (glyph atlas
   contract, render-queue layering, accent machinery, rail wiring, the
   camouflage/backing-chip lesson).
5. `docs/superpowers/plans/2026-07-11-slice-k-kickoff-prompt.md` — the
   whole-K inherited context and boundary.
6. `docs/HANDOFF.md` — current state.

## What K2 left ready (build on this, don't reinvent)

- **Core** (`src/Core/Atlas`, 76 atlas tests in the 725 suite): ten
  lens queries (Traffic/Fleet/Price/War/Tension/Tech/Plague/News/Poi/
  Works) + K1's Domain/Lane/Port/Nature/Starfield — every mark on the
  map already carries its registry id (PortMarker.PortId,
  FleetMarker.FleetId, PoiMark.PoiId, SiteMark.ProjectId,
  FreightMark.ShipmentId…), which is exactly what selection needs.
- **Presentation** (`unity/Assets/Atlas`): GlyphLayerBase (authored
  atlas `Resources/AtlasGlyphs.png`, 4×5 cells, cell 16 = backing chip;
  `AtlasGlyphs.UvRect` is the contract; GLYPH-CREDITS.md must be
  amended if icons are added) · DomainFieldLayer.SetAccent ·
  LaneLayer.SetMode · LensRail (code-built UI Toolkit on
  `Assets/Atlas/PanelSettings.asset`; AtlasPointerGuard is the
  chrome-owns-pointer hook CameraRig already consults — extend it for
  the dock, don't invent a second mechanism) · SimHost auto-loads the
  seed-42 golden in play mode.
- **Picking**: no colliders anywhere — the PoC lesson holds:
  `HexGrid.WorldToHex(plane intersection)` (CameraRig.PlanePoint shows
  the ray→plane math); hex → cell via `HexGrid.CellOf`; port/fleet/POI
  at hex via the read model.
- **AtlasSmoke** (StarGen > Atlas Smoke Shots / batch twin) renders 14
  shots; extend it with panel-open captures (UI Toolkit renders in
  play mode — smoke captures via cam.Render() do NOT include UI; K3
  should solve panel capture or gate panels on the in-editor eyeball).

## Scope (K3)

- **SelectionModel** + hover hex tooltip (what's here: system summary,
  owner, port tier, live POI).
- **InspectorDock** (right side, pinnable panels for comparison).
- **PanelQueries/ChronicleQueries/HandoffQueries in `src/Core/Atlas`**
  (plain C#, xUnit parity tests against the REPL renderers in
  `src/Inspector/Repl.cs`).
- **Typed panels** (diagram §9 + the T1/T2 additions):
  - Open Threads — **the opening screen** (`threads` /
    `HandoffView.OpenThreads`); each row jumps the camera to its subject.
  - Polity — `polity`/`tech`/`characters` **+ `ReservePoints` (the
    reserve treasury, actors v7) and the standing plan
    (`PolityPolicies.Plan`, `eplan` parity)**.
  - Market — `market <portId>` **+ the located larder: `Port.StockQty/
    StockGrade` per good, capacity (tier × StockCapPerPortTier + depot
    tiers × StockCapPerDepotTier), decay** (T2's located stockpiles).
  - **Project inspector (NEW, T1)** — click a works-lens site mark:
    kind, progress, per-year basket, `LastFedFraction` starvation
    readout, funder vs owner (`eprojects` parity).
  - **Shipment card (NEW, T2)** — click a works-lens freight mark:
    route, cargo+grades, sailed/total, live ETA, STALLED
    (`efreight` parity).
  - Fleet — `fleet`/designs; War — `wars`/`war <id>`; Relations;
    Character/Bio; Corporation — `corps` **(shows funded projects via
    Project.FunderActorId; corp standing plans do NOT exist yet —
    deferred to the contract-economy slice, don't invent them)**;
    POI — `poi`; Belief/News/Stances; Chronicle/Eras.
- **Registry drawer** — `find`/`stats`/`goods`/`knobs` inside the panel
  system (topbar search).
- **Top bar** — eye chip (god, controller reserved) · world-year +
  epoch + era name · config stamp (seed, radius, artifact id) · artifact
  load box (SimHost auto-load stays the default). The rail's minimal
  year readout retires into it.
- **Per-lens LEGEND** (user-requested at the K2 eyeball: "currently
  there is no information saying what icons/colors/regions represent"):
  when a lens is active, a compact legend surfaces its vocabulary —
  glyph shapes (the AtlasGlyph cells), color ramps (price bands, tension
  cold→ember, tech bronze→arc-light), lane stroke states. Keep the
  mapping data Core-side or in one authoritative table so the legend
  can never drift from the layers (the emap legend-line pattern, made
  visual). Fits the rail/dock chrome; scope it light.

**Boundary:** no timeline/stepping (K4) · no system stage (K5) · no new
sim mechanics, read-only queries only · controller eye stays a seam ·
lens catalog is done — panel work only (a lens bug found during K3 is a
fix, not a feature). **The K2 eyeball's other carry** — deep-dive
readability passes on each lens representation — is BACKLOG, not K3:
K3 adds the legend so lenses are decodable; redesigning how each lens
draws is its own later work (queue it behind K5 or fold into the
design-acceptance gap list).

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-k3-panels` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-k3-ledger.md`. Never share a
   checkout with another live session — take a `git worktree`.
3. TDD the panel queries (REPL parity); EditMode tests where they pay;
   panel eyeball needs play mode — plan the acceptance loop early.
4. Gates: `dotnet test` green · golden untouched · determinism
   untouched · EditMode green · smoke suite still renders every lens.
5. User gates: scope nod · **panels eyeball** (click a port → polity/
   market panels populate with REPL-parity numbers incl. the larder;
   click a site → project panel with starvation readout; threads rows
   jump the camera) · merge decision.
6. Wrap-up: merge · HANDOFF · tick K3 in the K roadmap · **write the K4
   kickoff prompt** (timeline) · republish the living diagram (§9 needs
   the Project/Shipment panel rows and the larder/plan additions) ·
   push only on say-so.

- [ ] Slice K3 complete
