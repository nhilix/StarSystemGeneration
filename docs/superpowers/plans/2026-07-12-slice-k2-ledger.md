# Slice K2 Ledger — Lens Catalog

Branch `slice-k2-lenses` off main (`d3db905`, the T2 merge). Governing
plan: `2026-07-11-slice-k-roadmap.md` row K2; kickoff:
`2026-07-11-slice-k2-kickoff-prompt.md`; design of record:
`docs/superpowers/specs/2026-07-11-unity-atlas-design.md` + diagram §8.

Scope nod 2026-07-12, **amended after a T1/T2 impact pass** (user-requested
analysis, confirmed): the nine kickoff lenses **plus a tenth — `works`**
(construction sites from `state.Projects` + freight/convoys in transit from
`state.Shipments`/Expedition fleets, stalled shipments visually distinct),
porting `EpochMapView`'s T2-added works layer; LOGISTICS group. Analysis
also queued K3 kickoff notes: market panel gains the located larder
(StockQty/Grade, capacity, decay), polity panel gains ReservePoints + the
standing plan (`eprojects`/`eplan` parity), new Project inspector +
Shipment card panels (works-lens selection targets), corp panel shows
funded projects only (corp standing plans deferred to contract economy).
Diagram §8 gets the works row at wrap-up; §9 the panel notes.

## Tasks

- [x] **T1 — Core lens queries, TDD** (4 commits, 36 parity tests,
      725/725 full suite; every new file + two-line .meta):
  - [x] TrafficLens (TrafficPerYear; emap band parity None/Trickle/Light/
        Steady/Heavy; sqrt weight saturating at 5 trips/yr)
  - [x] FleetLens (posture + owner tint, hulkless skipped)
  - [x] PriceLens (nearest-servicing-port ratio, PriceGlyph band parity,
        NaN wilds; CellShades on the NatureFieldLayer bake pattern —
        cell-resolution, so NO spatial index needed, T4 stays dormant)
  - [x] WarLens (Stations = warring Blockade/Expedition fleets,
        WarStationCells parity; SlotBelligerence parallel to PolitySlots)
  - [x] TensionLens (hottest live BothLive relation; TensionGlyph ×9
        digit parity; cold→ember HeatColor)
  - [x] TechLens (Astrogation SlotTiers; bronze→arc-light TierColor)
  - [x] PlagueLens (Afflicted burn / ImmuneUntil scar on the state clock;
        quarantined approaches stay LaneLens's status, re-emphasized)
  - [x] NewsLens (DeliverPulses liveness parity 0≤age≤PulseMaxYears;
        age-faded parchment)
  - [x] PoiLens (live anchors only — !Depleted; typed colors; Dormant)
  - [x] WorksLens (Sites w/ Progress+LastFedFraction, gate pairs both
        ends; Freight lerped by sailed fraction w/ efreight STALLED
        derivation; Convoys = expedition fleets)
- [x] **T2 — Unity presentation modules** (thin; StarGen.AtlasView):
      LaneLayer modes (Status/Traffic/QuarantineOnly) · glyph layers on
      GlyphLayerBase (fleets/POIs/works/plague/war stations; authored
      game-icons atlas, runtime-tinted, backing chips) · PriceFieldLayer
      cell-shade bake · DomainFieldLayer accents (Owner/War/Tension/Tech
      slot retints) · NewsLayer additive ring-fronts · StarGen/AtlasGlyph
      shader (UV-rect billboards + LOD tint)
- [x] **T3 — Lens rail** (UI Toolkit, code-built): POLITICAL / LOGISTICS /
      KNOWLEDGE / NARRATIVE / NATURE groups, swatch chips (accents and
      lanes/traffic radio-like), `price ▾ good` chip + DropdownField,
      AtlasPointerGuard consumes pointer over chrome, year readout;
      **AtlasHud deleted**; SimHost auto-loads in play mode
- [x] **T4 — Spatial index NOT needed** (price bakes at cell resolution;
      no K2 lens samples per-hex over all ports — flag rides to K4)
- [x] **T5 — AtlasSmoke extended per lens** (14 shots incl. one per lens;
      pre-eyeballed against REPL `emap` — parity verified for price band
      dominance, tension heat, war belligerence/stations, works marks)
- [x] **T6 — Fresh-eyes whole-branch review** + one fix wave. Verdict:
      "MERGE-READY with one play-mode verification required" — hard
      rules verified holding (Core purity, meta completeness, zero sim
      mutation/RNG, id-order iteration, boundary clean), all ten parity
      claims checked line-by-line against EpochMapView/RenderFreight and
      confirmed exact. 1 plausible-bug + 9 notes; fixed: **rail root
      pickingMode=Ignore** (the full-screen document root would have made
      the pointer guard block ALL map input — verify zoom/pan at the
      eyeball), GlyphLayer.OnZoom null guard, PriceFieldLayer texture
      HideAndDontSave, WorksLens.RemainingYears clamped ≥0, price band
      edges pinned exactly (straddling asserts), war belligerence test
      de-tautologized + hulkless-blockade station case, atlas HEIGHT
      asserted (the 4×5 layout contract), .gitignore test-results glob.
      Declined-as-flagged: quarantine clock edge (`>=` on lanes vs `>`
      on freight stall) is upstream in main (FleetOps vs ShipmentOps) and
      faithfully ported — carried below for a Core cleanup; runtime
      texture/mesh HideFlags on K1 layers predate K2.
- [x] **T7 — Gates**: `dotnet test` 725/725 ×2 (determinism suites in
      the count) · golden untouched · EditMode 8/8 headless (LodBands +
      GlyphAtlas) · smoke suite renders every lens
- [x] **T8 — USER: atlas eyeball — ACCEPTED as foundational groundwork**
      (2026-07-12): "there is 'something' implemented for each lens, but
      they are all pretty unintuitive to read." Two user notes carried
      forward: (1) **deep-dive passes on each lens representation** are
      future work (per-lens readability/design polish — backlog, not K3
      scope by default); (2) **every lens needs a LEGEND** — nothing in
      the atlas says what icons/colors/regions mean (natural K3 chrome
      candidate alongside the panel system).
- [x] **T9 — Wrap-up**: merged · HANDOFF · K2 ticked in the K roadmap ·
      K3 kickoff finalized (panel notes + legend item) · diagram §8
      works row, republished · push on say-so

## Decisions / deviations

- **Glyph sourcing landed as specced**: 16 game-icons.net SVGs (authors
  lorc/delapouite/sbed, CC BY 3.0) rasterized white-on-transparent into
  `unity/Assets/Atlas/Resources/AtlasGlyphs.png` (4×4, 128px cells,
  Resources-loaded, runtime-tinted); attribution in
  `unity/Assets/Atlas/GLYPH-CREDITS.md`. Kenney's pack was UI iconography —
  wrong vocabulary — and was dropped. Build script kept in the session
  scratchpad only; the PNG is the artifact, the enum order is the contract.
- **StarGen/AtlasGlyph is a sibling shader**, not an AtlasBillboard edit:
  per-vertex UV rect (TEXCOORD1) + _Tint for the LOD fade; the
  K1-validated point-sprite path stays untouched.
- **Accent lenses are radio-like** (war/tension/tech), as are
  lanes/traffic: one fill, one stroke mode — they cannot stack by nature.
  Accents ride DomainFieldLayer.SetAccent (slot-color re-upload only; no
  shader change). War accent = peaceful polities fade to ash (emap ','
  parity).
- **LaneLayer grew modes** Status/Traffic/QuarantineOnly; the plague lens
  forces QuarantineOnly when the lanes/traffic chips are off (quarantined
  approaches marked without lighting the whole network).
- **Works starvation reads as color**: site glyph cools amber→ember by
  LastFedFraction; stalled freight is loud red and larger.
- **News rings**: sqrt-age growth (1 + 1.15·√age hexes, cap 14) — the
  diffusion-front metaphor; presentation-only curve.
- **SimHost auto-loads in play mode** (Start): the HUD's load box died
  with AtlasHud; K3's top bar takes over artifact chrome.
- **Pointer guard**: LensRail installs AtlasPointerGuard.Test
  (panel.Pick); CameraRig drops mouse input over chrome (K1 note closed).
- PriceFieldLayer bakes at cell resolution (256² texture) — no per-hex
  port scan, so the K1-flagged spatial index stays unneeded (T4 dormant).
- **First-smoke lessons (own fix wave, pre-review):**
  - *Camouflage*: fleet glyphs use the same quarter-white owner brighten
    as port dots — identical color, glyph invisible ON its own port. Fix:
    a **backing chip** cell (dark filled circle) added to the atlas (now
    4×5); GlyphLayerBase draws it as the first quad of each pair (in-mesh
    triangle order), so every identity mark reads against any background.
  - *Render order*: renderer-bounds transparent sorting drew port dots
    over same-hex glyphs — glyph materials take explicit renderQueue
    3100+ (war 3120, plague 3110), news rings 3040.
  - *News restraint*: 597 lifetime rings drowned the map; the layer now
    draws only word still spreading (display cap 40y — Core liveness
    stays PulseMaxYears), additive blend, peak alpha 0.35, ring radius
    cap 10 hexes (unreachable under the 40y cutoff; belt and braces).
  - Price glut-blue dominance and warm tension shading verified FAITHFUL
    against emap (67/125 price glyphs are '_'; tension digits mass 3–8).

## Carried notes

- Baseline test count pending (background run at branch start).
- Rendering conventions from K1 that bite: compose sRGB, linearize once on
  output; `_AtlasFocalY`/`_AtlasViewportPx` globals for billboard sizing;
  material floats in `CBUFFER_START(UnityPerMaterial)`, set from C#.
- Next free view-only RollChannel is 75 — but 75 was taken by T2 piracy
  (ShipmentLoss); VERIFY the actual next free channel before claiming one.
- Triple-overlap fill shades by top-two relation — revisit only if the war
  lens makes it read wrong (K2 war eyeball pending; smoke read fine).
- **Carried to a Core cleanup (upstream, pre-K2)**: the quarantine clock's
  edge is inconsistent in main — lanes/emap read `QuarantinedUntil >=
  WorldYear`, freight stall (efreight/ShipmentOps) reads `>` — a lane
  quarantined exactly to WorldYear is Quarantined on the lane lens but
  not stalled for its freight. K2 ported both faithfully.
- K1 runtime meshes/textures lack HideAndDontSave in edit mode (leak
  until scene reload) — cosmetic, sweep opportunistically.
