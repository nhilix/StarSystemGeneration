# Slice K2 Kickoff — Session Prompt

You are starting **Slice K2 (Lens catalog)** — the second of five
sub-slices delivering the Unity atlas, under the lighter protocol in
`/CLAUDE.md` (read it first). K1 shipped the skeleton instrument: a 2.5D
perspective camera over one continuous map, a per-pixel domain field
shader, a two-population starfield, nebular nature fields, lanes, ports,
and the lattice — all fed by the Eye-parameterized read model in
`src/Core/Atlas`. K2 widens it: **every remaining lens** (traffic,
fleets, price-per-good, war, tension, tech, plague, news, POIs) plus the
real left-rail lens stack UI replacing the provisional IMGUI HUD.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules (`unity/ProjectSettings` churn
   stays uncommitted, always).
2. `docs/superpowers/plans/2026-07-11-slice-k-roadmap.md` — the governing
   K plan; row K2 and the gates.
3. **The interface spec**:
   `docs/superpowers/specs/2026-07-11-unity-atlas-design.md` — note the
   K1-amended **"The camera"** section (2.5D grammar: fields computed,
   glyphs authored, placement is data) and §lens catalog; the living
   diagram `docs/diagrams/unity-atlas-design.html` §8 specifies each
   lens's Core source and both eyes' behavior.
4. **The K1 ledger** — REQUIRED, it is the map of what exists and why:
   `docs/superpowers/plans/2026-07-11-slice-k1-ledger.md` (decisions,
   the eyeball-rejection lesson, rendering conventions, carried notes).
5. `docs/superpowers/plans/2026-07-11-slice-k-kickoff-prompt.md` — the
   whole-K inherited context ("what J left ready": the Core data sources
   each lens ports — `EpochMapView.Render` derivations, `FleetOps`
   traffic/postures, `PoiCompiler.LiveAt`, `NewsOps`, `Tech.Tier`,
   `PlagueOps`; boundary: no new sim mechanics, espionage reserved).
6. `docs/HANDOFF.md` — current state.

## What K1 left ready (build on this, don't reinvent)

- **Read model** (`src/Core/Atlas`, 42 xUnit tests): `EyeContext`
  (God live, Controller reserved), `AtlasReadModel` (cell index),
  `NatureLens` (9 rasters→colors), `NatureFieldSampler` (nebular
  fields; view-only `RollChannel.AtlasNebula = 74` — next free is 75),
  `DomainLens` (union territory + `PolitySlots` + `OverlapShade`
  war/tension/warm/neutral), `LaneLens` (open/quarantined/severed),
  `PortLens` (markers + service radius), `StarfieldLens`
  (two-population, filament-clumped), `LensStack.Composite`,
  `AtlasPalette` (golden-ratio owner colors, ramps).
- **Presentation** (`unity/Assets/Atlas`, namespace `StarGen.AtlasView`):
  layer stack = StarfieldLayer · DomainFieldLayer (uniform-array field
  shader, 32 polity slots, relation-matrix texture) · NatureFieldLayer
  (baked bilinear data textures) · LatticeLayer (GPU lines, zoom-curve
  alpha) · LaneLayer/PortLayer (screen-constant billboards) · CameraRig
  (focus+distance+pitch, damped, dolly-to-cursor, top-down = 90°) ·
  LodBands (bands gate what RESOLVES; styling is continuous curves) ·
  AtlasRoot (routing only) · AtlasHud (provisional IMGUI — **K2 deletes
  it** for the UI Toolkit rail).
- **Shaders** (`unity/Assets/Atlas/Shaders`): StarGen/DomainField,
  StarGen/AtlasBillboard. CONVENTIONS that will bite you if ignored:
  compose colors in sRGB and `SRGBToLinear` once on output (the project
  renders linear); billboard sizing uses explicit globals
  `_AtlasFocalY`/`_AtlasViewportPx` (built-in `_ScreenParams`/P proved
  unreliable in batch RT renders — sizes silently ride their pixel
  caps); material floats live in `CBUFFER_START(UnityPerMaterial)` and
  are set explicitly from C#.
- **Acceptance tooling**: `AtlasSmoke` (StarGen > Atlas Smoke Shots /
  batchmode twin) renders 4 PNGs at the repo root from the seed-42
  golden — extend it with each new lens; it is the pre-eyeball loop.
  Headless gates: `Unity -batchmode -runTests -testPlatform EditMode`.
- **Rendering grammar** (user-validated through four eyeball passes):
  dark space + starfield base, never a filled hex board; fields
  computed in shaders from the registries; glyphs authored sprites;
  screen-constant sizing; **dark-wilds are value-poor, never blank**
  (the atlas deliberately diverges from the REPL's blank-glyph voids).

## Scope (K2)

- **Lenses** (each: Core query in `src/Core/Atlas` porting the
  `EpochMapView`/ops derivation with xUnit parity tests + a thin
  presentation module; diagram artifact §8 is the per-lens spec):
  - **traffic** — lane trips/year (`FleetOps.TrafficPerYear`) weighting
    lane width/brightness (LaneLayer grows a traffic mode)
  - **fleets** — posture-differentiated glyphs at fleet hexes (the
    first AUTHORED sprite vocabulary: source tintable icons —
    game-icons.net (CC-BY) / Kenney (CC0) — one atlas, runtime-tinted)
  - **price ▾ good** — the parameterized lens: per-good price ratio vs
    founding price at the nearest servicing port, as a plane field or
    port-anchored shading; the chip carries the good
  - **war** — belligerent domain accenting + war fleets on station
    (blockade rings, expedition marks); reuse the domain field's
    machinery (slots already carry the relation matrix)
  - **tension** — the pressure gauge: owner's hottest live relation
    shading its domain
  - **tech** — Astrogation tier per domain
  - **plague** — infected ports burn, immune scarred, quarantined
    approaches marked (LaneLens already carries quarantine state)
  - **news** — god sees all pulses (controller inbox is the reserved
    eye seam)
  - **POIs** — `PoiCompiler.LiveAt`/`state.Pois` marks at their hexes
    (authored glyph set by POI type)
- **Lens rail** (UI Toolkit, code-built like the PoC's AtlasUI):
  left-rail groups POLITICAL / LOGISTICS / KNOWLEDGE / NARRATIVE /
  NATURE per the mockup; toggle chips with swatches; the price chip
  carries its good (`price ▾ provisions`); pointer events consumed so
  the map doesn't zoom under the rail (K1 carried note). Top-bar chrome
  (eye chip, year/era, config stamp) belongs to K3 — a minimal year
  readout may stay in the rail meanwhile. Delete AtlasHud.
- **Perf note carried from K1 review**: anything sampling per-hex over
  all ports needs a spatial index before K4 animates epochs; if a K2
  lens needs per-hex sampling, build the index then.

**Boundary:** no selection/tooltips/panels (K3) · no timeline/stepping
(K4) · no system stage (K5) · no new sim mechanics, no Core behavior
changes (read-only view queries only) · controller eye stays a seam ·
triple-overlap fill still shades by the top-two relation (flagged K1
note — revisit only if the war lens makes it read wrong).

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-k2-lenses` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-k2-ledger.md`. Never share a
   checkout with another live session — take a `git worktree`.
3. TDD the Core queries (parity with the REPL derivations); EditMode
   tests where they pay; extend AtlasSmoke per lens and eyeball the
   shots against the REPL `emap` before handing the user the gate.
4. Gates: `dotnet test` green · golden untouched · determinism
   untouched · EditMode green · smoke suite renders every lens.
5. User gates: scope nod · **atlas eyeball** (flip through every lens on
   seed 42 next to REPL `emap` — same story, rendered; the rail feels
   like the mockup) · merge decision.
6. Wrap-up: merge · HANDOFF · tick K2 in the K roadmap · **write the K3
   kickoff prompt** (selection & panels — informed by what landed) ·
   republish the living diagram if deviated · push only on say-so.

- [ ] Slice K2 complete
