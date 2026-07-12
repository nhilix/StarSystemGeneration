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

- [ ] **T1 — Core lens queries, TDD** (each: port the `EpochMapView`/ops
      derivation with xUnit parity tests; every new file + two-line .meta):
  - [ ] TrafficLens (`FleetOps.TrafficPerYear` per lane → weight bands)
  - [ ] FleetLens (posture-differentiated marks at fleet hexes)
  - [ ] PriceLens (per-good ratio vs founding at nearest servicing port)
  - [ ] WarLens (belligerent accent + war stations: blockade/expedition)
  - [ ] TensionLens (owner's hottest live relation per domain)
  - [ ] TechLens (Astrogation tier per domain)
  - [ ] PlagueLens (infected burn / immune scarred / quarantined approaches)
  - [ ] NewsLens (god sees all pulses in transit)
  - [ ] PoiLens (`PoiCompiler.LiveAt`/`state.Pois` marks by type)
  - [ ] WorksLens (sites + shipments interpolated on route, STALLED flag,
        expedition convoys)
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

- (running)

## Carried notes

- Baseline test count pending (background run at branch start).
- Rendering conventions from K1 that bite: compose sRGB, linearize once on
  output; `_AtlasFocalY`/`_AtlasViewportPx` globals for billboard sizing;
  material floats in `CBUFFER_START(UnityPerMaterial)`, set from C#.
- Next free view-only RollChannel is 75 — but 75 was taken by T2 piracy
  (ShipmentLoss); VERIFY the actual next free channel before claiming one.
- Triple-overlap fill shades by top-two relation — revisit only if the war
  lens makes it read wrong.
