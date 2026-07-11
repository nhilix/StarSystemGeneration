# Slice K1 Ledger — Skeleton Instrument

Branch `slice-k1-skeleton` off main (`2d8f70d`). Governing plan:
`2026-07-11-slice-k-roadmap.md`; design of record:
`docs/superpowers/specs/2026-07-11-unity-atlas-design.md`. Baseline:
562/562 green.

Scope nod: user approved the K roadmap + K1 kickoff 2026-07-11 and said
to start K1 in the same session.

## Tasks

- [x] **T1 — PoC deletion** (`46e2ab3`): `unity/Assets/Scripts/`,
      `unity/Assets/Editor/Atlas*` + asmdef, `Scenes/Atlas.unity`.
      PanelSettings.asset kept (inert, reusable). Salvage lessons below.
- [x] **T2 — `src/Core/Atlas` read model, TDD** (6 red/green cycles,
      28 xUnit tests; each file with .meta):
  - [x] EyeContext (God | Controller reserved seam, worldYear)
  - [x] AtlasReadModel (single query surface; cell index by coord)
  - [x] NatureLens (9 raster layers → colors; same under every eye)
  - [x] DomainLens (OwnersAt wrapper, per-cell + per-hex shades,
        contested blend; never stored ownership)
  - [x] LaneLens (open/quarantined/severed; quarantine reads distinct
        from SeveredLaneIds' freight fold; clocks read the state year)
  - [x] LensStack.Composite (source-over) + PortLens markers
- [x] **T3 — Unity presentation** (`unity/Assets/Atlas`, `c7e676f`):
  - [x] asmdefs (StarGen.AtlasView + .Editor + .EditorTests)
  - [x] SimHost (artifact load; defaults to the seed-42 golden — which
        IS the eyeball scenario; run-seed/step deferred to K4)
  - [x] MapSurface (ONE hex-resolution mesh for the whole disc —
        cells are exact radius-5 superhexes, so per-cell spirals
        enumerate every hex once; recolor-in-place lens switching)
  - [x] LaneLayer (status-colored quads) + PortLayer (tier diamonds)
  - [x] CameraRig (zoom-to-cursor, right-drag/WASD pan) + LodBands
        (pure band table: galaxy/domains/region/hex)
  - [x] AtlasHud (provisional IMGUI — K2 replaces) +
        AtlasViewSceneSetup (StarGen > Setup Atlas Scene, batch twin)
- [x] **T4 — EditMode tests**: LodBandsTests, HexMeshBuilderTests —
      6/6 green headless (`Unity -batchmode -runTests`); plus
      **AtlasSmoke** (StarGen > Atlas Smoke Shot / batch twin): builds
      the scene, loads the golden, renders atlas-smoke.png +
      atlas-smoke-region.png at the repo root — the pre-eyeball
      verification loop. First shot verified: the P1 image reads
      (year 1000, 98 ports, 464 lanes).
- [x] **T5 — Fresh-eyes whole-branch review** + one fix wave. Verdict:
      "a clean K1" — hard rules verified holding (Core purity, meta
      completeness, no sim drift, deterministic iteration, query-time
      derivation). 1 plausible-bug + 6 notes; fixed: OnDestroy
      mesh/material release on all three layers, scene-save prompt
      before setup discards an open scene, smoke aspect set before
      FitTo (+ rig-matching extent), LaneLens doc-comment drift,
      remoteness test now uses a live far cell (not the void gate).
      Riding to K2 (flagged): per-hex OwnersAt is O(hexes×ports) —
      the K4 animation hot spot; HUD doesn't consume pointer events
      (provisional IMGUI).
- [x] **T6 — Gates** (post-fix-wave): `dotnet test` 590/590 (562
      baseline + 28 atlas) · golden untouched, determinism suites in
      that count (reviewer verified zero sim-behavior diff) · Unity
      EditMode 6/6 headless · AtlasSmoke renders seed 42 (galaxy +
      region shots).
- [x] **T7a — USER: atlas eyeball — REJECTED** (2026-07-11): "looks
      NOTHING like the interface design document. No smooth LOD zoom
      continuum, rendering choppy, massive port icons, lane spaghetti,
      harsh colored hexagons." Root cause: the presentation used a
      filled-hex-board grammar; the validated mockup
      (unity-atlas-design.html drawMap) uses dark space + starfield +
      per-port radial glows + thin screen-constant lanes + small
      screen-fixed port dots + lattice as faint outlines at region
      LOD only. The taste gate IS K1 scope ("a real taste gate, not a
      tech demo") — fix wave in-slice.
- [ ] **T7b — taste-gate fix wave** (user-steered, 2 course corrections:
      (1) field shader over gradient sprites for long-term control;
      (2) **2.5D perspective camera** — focus+distance+pitch on the
      plane, top-down = 90° limit, "localize every data point in space").
      Spec amended ("The camera"); asset boundary set: fields computed,
      glyphs authored (game-icons.net/Kenney, runtime-tinted), placement
      is data:
  - [x] Core: StarfieldLens (StableHash star points per cell by
        density, lean-tinted) · PortMarker.ServiceRadiusHexes (31 tests)
  - [x] Shaders: StarGen/DomainField (per-pixel port-registry field,
        512-port uniform array, contested-brightening, additive) ·
        StarGen/AtlasBillboard (camera-facing, px-clamped point sprites)
  - [x] Unity: StarfieldLayer (additive billboards) · DomainFieldLayer
        (plane quad + port upload) · NatureFieldLayer (rasters baked to
        bilinear data textures — nebular fields, off by default) ·
        LatticeLayer (GPU-line outlines, continuous fade near Region) ·
        LaneLayer/PortLayer screen-constant · **MapSurface +
        HexMeshBuilder deleted** (the hex-board grammar is gone)
  - [x] CameraRig: perspective, damped targets, dolly-to-cursor,
        middle-drag tilt 25–90°, ZoomChanged (continuous) + BandChanged
        (resolve gates only)
  - [x] Gates re-run + smoke shots (top-down / tilted domains / region)
        compared against the mockup; lanes recalibrated to the
        artifact's cyan restraint with altitude fade
- [x] **T7c — USER: atlas eyeball, second pass** — "a step in the right
      direction" + two notes driving T7d: ports should be solid circles
      (tier- AND zoom-relative); domains showed destructive interference
      banding, worst between SAME-polity glows (root cause: contested
      logic compared top-two PORT weights, not owners).
- [ ] **T7d — domain semantics wave** (user-specified):
  - [x] Core: DomainLens.PolitySlots (deterministic shader slot order)
        + DomainLens.OverlapShade (war > tension > warmth > neutral,
        symmetric; 33 atlas tests)
  - [x] Shader v2: per-POLITY max fields (union = uniform region, no
        self-interference) · flat fills · fwidth-crisp border outline
        per combined region · Venn overlap shaded by the relationship
        (RelationTex baked from OverlapShade), both outlines drawn
        through the overlap. Polities past 32 fold into the last slot
        (seed-scale is ~13).
  - [x] Ports: solid AA circles (SolidDot), dual sizing — tier-scaled
        pixel floor at altitude, tier-scaled world size resolving at
        region/hex
  - [x] Gates + smoke shots re-rendered (595/595 dotnet, 3/3 EditMode).
        Two rendering bugs found via a layer-isolation diagnostic
        (temporary AtlasDiag, deleted): (1) the project renders LINEAR —
        shaders now compose in sRGB (the artifact's space) and linearize
        once on output; billboard vertex colors CPU-linearized; the
        relation texture created linear so it stays in sRGB terms.
        (2) built-in _ScreenParams/P uniforms are unreliable in batch RT
        renders — billboard sizing rode its pixel caps (96px ports!);
        replaced with explicit _AtlasFocalY/_AtlasViewportPx globals set
        by CameraRig per frame and by capture tooling per shot.
- [x] **T7e — USER eyeball iterations** (much better; three refinement
      notes, all landed):
  - [x] 3+ overlap: borders now derive from EVERY polity's zero edge,
        not the locally strongest two (`e8d3842`)
  - [x] Nature reads nebular, not hex-pixelated: NatureFieldSampler —
        Gaussian cell blending, presence-scaled alpha, void feathering,
        ValueNoise cloud breakup on view-only RollChannel.AtlasNebula=74
        (`f269990`); disc reads as a blended whole (base presence lift,
        pinned by test) and quads stopped clipping (pad 48) (`22154d2`)
  - [x] **Dark-wilds are value-poor, never blank** (`c2de2ed`) — user
        design clarification: IsVoid is only the traversability
        judgment; CosmicResidue writes real fields everywhere, so the
        atlas renders them (wilds dim, no base lift), and the lattice
        draws every hex in the disc. Economic exclusion untouched.
        CONVENTION: the atlas diverges from the REPL's blank-glyph
        voids going forward (user-confirmed).
- [x] **T7f — USER: atlas eyeball — ACCEPTED** (2026-07-11, "good
      start, let's wrap up") after one last flavor wave: denser
      two-population starfield with filament clumping (`d7d4a15` — the
      distribution pin caught Frac(h>>55) starving the 21-bit mask).
      Final gates: 602/602 dotnet · EditMode 3/3 · goldens/determinism
      untouched throughout · smoke suite (4 shots incl. gas+starfield)
      renders the design language.
- [x] **T8 — Wrap-up**: merged to main · HANDOFF · K1 ticked in the K
      roadmap · K2 kickoff prompt written · design diagram amended +
      republished · push on say-so

## Salvaged rendering lessons (PoC read 2026-07-11, then deleted)

- **Hex mesh**: 7 verts + 6 tris per hex, vertex-colored Color32,
  `IndexFormat.UInt32`, inset 0.08, recolorable in place
  (SetColors) — one mesh per view; geometry from `HexGrid`
  (HexToWorld/CornerOffsets/WorldToHex) as single authority.
- **Palette discipline**: pure static layer→Color32 mapping; void
  #0A0A0E, unclaimed #282828; golden-ratio polity hues
  (`id * 0.6180339887 % 1`), brightness by tier; hover = +60 RGB
  highlight of the base color.
- **Picking**: `HexGrid.WorldToHex(cam.ScreenToWorldPoint(...))` — no
  colliders; chrome occlusion via `panel.Pick(...) != root` so UI owns
  the pointer (no click-through, tooltip pickingMode Ignore).
- **Camera fit**: ortho size = max(extents.y, extents.x/aspect) × 1.08.
- **UI Toolkit chrome built entirely in code** (no UXML/USS);
  PanelSettings needs a ThemeStyleSheet (empty fallback OK).
- **Scene setup**: idempotent `MenuItem` builder + batchmode twin
  (`-executeMethod ... RunFromCli`); wires SerializedObject fields;
  PanelSettings load-or-create.
- **asmdef shapes**: runtime refs StarGen.Core + Unity.InputSystem,
  autoReferenced false; EditMode tests ref TestRunner + nunit
  precompiled, defineConstraints UNITY_INCLUDE_TESTS,
  includePlatforms Editor.
- **Input**: `UnityEngine.InputSystem` (Keyboard.current/Mouse.current)
  — the legacy Input manager is not the project's input path.

## Decisions / deviations

- **One continuous hex-resolution mesh** instead of the PoC's per-band
  views: ~126k hexes at radius 21 (7 verts each) is one draw call and
  gives the spec's "one camera on one scene" literally; the mesh inset
  doubles as the lattice at Hex band. Flagged for K2 perf review if
  bigger radii choke (spatial index for per-hex OwnersAt is the known
  fix).
- **Clocks read the state's world-year, never the eye's** — the eye
  never time travels; scrubbing swaps keyframe states (K4). Pinned by
  LaneLensTests.
- **Quarantined ≠ Severed on the lens** even though Core's
  SeveredLaneIds folds both into freight closure (self-imposed closure
  vs interdiction are different stories on the map).
- **Unity namespace is StarGen.AtlasView** (asmdef ditto) so it never
  collides with Core's StarGen.Core.Atlas read model.
- **HUD is IMGUI on purpose** (provisional, zero assets); the UI
  Toolkit lens rail is K2's opening move.
