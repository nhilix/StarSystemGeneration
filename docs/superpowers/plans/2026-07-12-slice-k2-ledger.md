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
- [ ] **T2 — Unity presentation modules** (thin; StarGen.AtlasView):
      LaneLayer traffic mode · fleet/POI/works glyphs (the first AUTHORED
      sprite atlas — game-icons.net CC-BY / Kenney CC0, runtime-tinted) ·
      price plane shading · war/tension/tech domain accents · plague marks ·
      news pulses
- [ ] **T3 — Lens rail** (UI Toolkit, code-built): POLITICAL / LOGISTICS /
      KNOWLEDGE / NARRATIVE / NATURE groups, toggle chips with swatches,
      `price ▾ good` chip, pointer events consumed; minimal year readout;
      **delete AtlasHud**
- [ ] **T4 — Spatial index if needed** (K1 carry: per-hex OwnersAt is
      O(hexes×ports); build the index the moment a K2 lens samples per-hex
      over all ports)
- [ ] **T5 — AtlasSmoke extended per lens** (the pre-eyeball loop; compare
      against REPL `emap` per layer)
- [ ] **T6 — Fresh-eyes whole-branch review** + one fix wave
- [ ] **T7 — Gates**: `dotnet test` green · golden untouched · determinism
      untouched · EditMode green · smoke suite renders every lens
- [ ] **T8 — USER: atlas eyeball** (every lens on seed 42 next to `emap`)
- [ ] **T9 — Wrap-up**: merge · HANDOFF · tick K2 in the K roadmap · K3
      kickoff prompt (with the panel notes above) · diagram §8 works row,
      republish · push on say-so

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

## Carried notes

- Baseline test count pending (background run at branch start).
- Rendering conventions from K1 that bite: compose sRGB, linearize once on
  output; `_AtlasFocalY`/`_AtlasViewportPx` globals for billboard sizing;
  material floats in `CBUFFER_START(UnityPerMaterial)`, set from C#.
- Next free view-only RollChannel is 75 — but 75 was taken by T2 piracy
  (ShipmentLoss); VERIFY the actual next free channel before claiming one.
- Triple-overlap fill shades by top-two relation — revisit only if the war
  lens makes it read wrong.
