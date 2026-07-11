# Session Handoff — 2026-07-11 (Slice K1: Atlas skeleton instrument — merged)

State: `main`, merged locally at `27fefe7`, **not pushed** (push on user
say-so). Tests 602/602 green — hex-tier suite untouched, goldens and
determinism byte-identity untouched throughout (K adds no sim
behavior). Unity EditMode 3/3 headless. ProjectSettings churn remains
uncommitted as always.

## What this session did

**First: Slice K planning.** Row K (Unity atlas rebuild) was decomposed
into five sub-slices K1–K5 (walking skeleton, then widen), each a full
slice session — governing plan
`docs/superpowers/plans/2026-07-11-slice-k-roadmap.md`. The PoC atlas
died in K1's first commit (aggressive greenfield, user-confirmed).

**Then: Slice K1 (skeleton instrument), merged.** Ledger:
`docs/superpowers/plans/2026-07-11-slice-k1-ledger.md` — read it before
touching the atlas; it records four user eyeball passes and the
decisions they forced.

- **Read model** (`src/Core/Atlas`, plain C#, 42 xUnit tests, every
  file with `.meta`): EyeContext (God live; Controller a reserved
  seam), AtlasReadModel, NatureLens (9 rasters→colors),
  NatureFieldSampler (nebular fields), DomainLens (union territory,
  PolitySlots, OverlapShade: war>tension>warmth>neutral), LaneLens
  (open/quarantined/severed — distinct even though SeveredLaneIds folds
  both), PortLens (+service radius), StarfieldLens (two-population,
  filament-clumped), LensStack, AtlasPalette. Territory derives from
  the port registry at query time — a test pins query-time derivation.
- **Presentation** (`unity/Assets/Atlas`, namespace StarGen.AtlasView,
  draw+input only): **2.5D perspective camera** (focus+distance+pitch,
  damped, dolly-to-cursor, top-down = the 90° limit — spec amended
  in-branch, "The camera") over one continuous plane; per-pixel
  **domain field shader** (per-polity max = union regions, fwidth
  border outlines from EVERY polity's zero edge, Venn overlaps shaded
  by the relation matrix); two-population starfield billboards; nature
  rasters baked to bilinear data textures; lattice as faint GPU-line
  outlines fading in at Region; screen-constant lanes/ports; LodBands
  (bands gate what resolves; styling is continuous). AtlasHud is
  provisional IMGUI — K2 replaces it with the UI Toolkit rail.
- **The eyeball-rejection lesson** (memory + ledger): the first pass
  reproduced the PoC's filled-hex-board grammar and was rejected;
  the design artifact's draw code is the visual spec — dark space +
  starfield + computed fields + authored glyphs. Salvage technique,
  never aesthetics.
- **Rendering conventions that will bite** (also in the K2 kickoff):
  the project renders LINEAR — shaders compose in sRGB and
  SRGBToLinear once on output; billboard sizing uses explicit globals
  `_AtlasFocalY`/`_AtlasViewportPx` (built-in `_ScreenParams`/P are
  unreliable in batch RT renders — sizes silently ride pixel caps);
  material floats go in `CBUFFER_START(UnityPerMaterial)` and are set
  explicitly from C#.
- **Dark-wilds are value-poor, never blank** (user design
  clarification): IsVoid is only the traversability judgment;
  CosmicResidue writes real fields for every cell and the atlas
  renders them (dim, no base lift); the lattice draws every hex in the
  disc. Economic exclusion (PortDomains/expansion) untouched. **The
  atlas deliberately diverges from the REPL's blank-glyph voids going
  forward** (user-confirmed convention).
- **Registries**: NO new layers, NO sim changes. `RollChannel` gains
  **AtlasNebula = 74 (VIEW-ONLY — the sim never rolls here); next
  free: 75**.
- **Acceptance tooling**: AtlasSmoke (menu: StarGen > Atlas Smoke
  Shots; batch: `-executeMethod
  StarGen.AtlasView.EditorTools.AtlasSmoke.RunFromCli`, graphics ON)
  renders 4 PNGs at the repo root from the seed-42 golden — the
  pre-eyeball loop. EditMode headless: `-batchmode -nographics
  -runTests -testPlatform EditMode`. Unity 6000.5.2f1 at the standard
  Hub path. A batch run needs the editor CLOSED (project lock).
- Fresh-eyes review: 1 plausible + 6 notes, one fix wave, all
  addressed; the large 2.5D rework afterward was user-steered through
  four eyeball iterations. Eyeball accepted 2026-07-11.

## Deliberately deferred / flagged

- Per-hex domain sampling is O(hexes×ports) — build a spatial index
  before K4 animates epochs (K2 kickoff carries it).
- Triple-overlap fill shades by the top-two relation only (borders all
  draw); revisit if the K2 war lens makes it read wrong.
- Field shader folds polities past 32 slots into the last slot —
  seed-scale is ~13; raise MaxSlots or move to StructuredBuffers if
  galaxies grow.
- HUD input isn't gated (IMGUI is provisional); the K2 UI Toolkit rail
  must consume pointer events.
- Authored sprite vocabulary (fleet postures, POI types) sourced from
  game-icons.net (CC-BY) / Kenney (CC0), runtime-tinted — starts in K2.

## Next up

1. **Slice K2 (Lens catalog)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-11-slice-k2-kickoff-prompt.md`
   (complete: what K1 left ready, per-lens scope, conventions,
   boundary).
2. Then K3 (selection & panels), K4 (timeline — may parallel K3 in a
   worktree), K5 (system stage & roadmap close). Governing plan:
   `2026-07-11-slice-k-roadmap.md`.
3. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged unless noted)

Lighter protocol per /CLAUDE.md (scope nod · eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings
stays uncommitted; bash printf for REPL piping; parallel slices never
share a checkout — take a `git worktree` each; every new `src/Core`
file gets a two-line `.meta` with a fresh guid; every calibration
constant in a knob registry + TUNING.md (atlas visual constants are
code-level, not knobs — presentation, not sim). NEW this slice: the
atlas eyeball loop is smoke-shot-first (render, inspect, THEN hand the
gate); the design artifact's draw code is the visual spec; atlas
diverges from REPL rendering conventions by design. Golden regen
one-liner and older conventions: `git show 27fefe7~1:docs/HANDOFF.md`.
