# Atlas Setup Knobs — Design Spec

**Date:** 2026-07-08
**Status:** Approved design, pending implementation plan
**Depends on:** hex geometry spec (2026-07-07), unity atlas spec (2026-07-07), circular footprint (merged 2026-07-08)

## 1. Overview

The atlas setup screen exposes only seed and galaxy radius; every other input to
galaxy formation is either a `GalaxyConfig` default the UI never surfaces or a
literal constant inside `DensityField.ShapeAt` / `SkeletonBuilder.PassResourceAnchors`.
This feature promotes the remaining formation constants to `GalaxyConfig`,
stamps them in the persisted artifact, and rebuilds the setup pane as a grouped,
slider-driven form so the galaxy's shape, resource richness, and simulated
history are tweakable at generation time. The setup screen additionally shows a
**live shape preview**: every seed/knob edit rebuilds a shape-only skeleton
(density pass only, no simulation) and re-renders the galaxy mesh immediately;
the full seeding + epoch simulation still runs only when Generate is clicked.

**Invariant:** all new config fields default to the exact values of today's
constants — a default-config galaxy is bit-identical to one generated before
this change (same seed → same skeleton artifact text).

## 2. Goals / Non-Goals

**Goals**
- Every formation knob in the table below is settable from the atlas setup screen.
- New knobs are first-class `GalaxyConfig` fields, persisted in the skeleton artifact.
- Sliders make out-of-range values unreachable in the UI; Core itself does not clamp
  (config is trusted input, same as today).
- One-click reset to defaults.

**Goals (continued)**
- Live shape preview on the setup screen: edits to seed or any knob re-render the
  density-layer galaxy map without running the simulation; Generate remains the
  gate for seeding passes + epoch sim.

**Non-Goals**
- `TraversabilityThreshold` stays config-only (not surfaced; easy to break galaxies with).
- The inspector REPL's `galaxy <seed> [radius]` command is unchanged (follow-up noted in §8).
- No presets/randomize button — generate is still the gate for full history.
- The preview shows the density layer only; polity/zone/dev/lean layers require the
  sim and stay Generate-gated. No preview interaction (hover/click/drill) in setup.
- No backward compatibility for schema v2 artifacts (pre-release; loader already hard-rejects).

## 3. Core: `GalaxyConfig` fields

Existing fields gain no changes. New fields (defaults = current hard-coded values):

| Field | Type | Default | UI range | Consumed by |
|---|---|---|---|---|
| `ArmStrength` | double | 0.9 | 0.0 – 1.0 | `ShapeAt` arm contrast (was literal `0.9`) |
| `CoreRadius` | double | 0.18 | 0.08 – 0.4 | `ShapeAt` bulge sigma (was literal `0.18`) |
| `DiscFalloff` | double | 0.55 | 0.25 – 1.0 | `ShapeAt` disc sigma (was literal `0.55`) |
| `MineralAnchorMultiplier` | double | 1.0 | 0.0 – 3.0 | `PassResourceAnchors` mineral chance |
| `PrecursorAnchorMultiplier` | double | 1.0 | 0.0 – 3.0 | `PassResourceAnchors` precursor chance |

Existing fields exposed by the UI (ranges are UI slider bounds, not Core validation):

| Field | Default | UI range | Notes |
|---|---|---|---|
| `GalaxyRadiusCells` | 21 | 2 – 45 (IntegerField, warn > 40) | existing field + existing warn |
| `ArmCount` | 3 | 1 – 8 (SliderInt) | |
| `ArmTightness` | 0.35 | 0.15 – 0.8 | low = tightly wound |
| `ArmWidth` | 0.18 | 0.05 – 0.5 | ridge falloff width |
| `MeanDensityTarget` | 0.5 | 0.2 – 0.8 | overall abundance |
| `HomeworldRatePerCell` | 0.02 | 0.005 – 0.06 | polity count ≈ rate × cells, floor 2 |
| `EpochCount` | 12 | 0 – 24 (SliderInt) | 0 = seeded galaxy, no history |
| `YearsPerEpoch` | 50 | 10 – 200 (SliderInt) | event dating granularity |

## 4. Core: formula changes

`DensityField.ShapeAt` (src/Core/Galaxy/DensityField.cs) — replace the three
literals with config reads; formulas otherwise unchanged:

```csharp
double core = Math.Exp(-(r * r) / (2 * config.CoreRadius * config.CoreRadius));
double disc = Math.Exp(-(r * r) / (2 * config.DiscFalloff * config.DiscFalloff));
// arm gaussian unchanged; the trailing weight becomes:
double arms = Math.Exp(-(toRidge * toRidge) / (2 * config.ArmWidth * config.ArmWidth))
              * (1 - core) * config.ArmStrength;
```

`SkeletonBuilder.PassResourceAnchors` (src/Core/Galaxy/SkeletonBuilder.cs) —
multipliers scale the existing chances; couplings to metallicity and stellar
lean are preserved:

```csharp
double mineralChance = (0.10 + 0.25 * cell.Metallicity) * config.MineralAnchorMultiplier;
double precursorChance = (0.02 + (cell.Lean == StellarLean.RemnantGraveyard ? 0.02 : 0.0))
                         * config.PrecursorAnchorMultiplier;
```

Multiplier 0 ⇒ that anchor type never rolls. Chances above 1.0 are harmless
(`NextDouble < chance` is always true).

### 4.1 Shape-only build (preview path)

`SkeletonBuilder` gains a public entry point for the preview:

```csharp
/// <summary>Skeleton with cell densities/void/chokepoint marks only — no anchors,
/// homeworlds, or history. The cheap path behind the atlas setup live preview;
/// PassDensitySummary here is the same pass Build runs, so a preview's density
/// layer is pixel-identical to the same config's full build.</summary>
public static GalaxySkeleton BuildShape(GalaxyConfig config)
{
    var skeleton = new GalaxySkeleton(config);
    PassDensitySummary(skeleton);
    return skeleton;
}
```

`Build` is refactored to call `BuildShape` then the remaining passes, so the two
paths cannot drift. Measured cost at radius 21 is a few tens of ms (74k density
samples) — acceptable per-edit for a tool; the Atlas side coalesces bursts (§6).

## 5. Core: serializer schema v3

`GalaxySkeleton.SchemaVersion` bumps 2 → 3. The `CONFIG` record appends the five
new fields **after** the existing ten, in this exact order (all doubles as
`"R"`, invariant culture, matching the existing convention):

```
CONFIG|MasterSeed|GalaxyRadiusCells|MeanDensityTarget|ArmCount|ArmTightness|ArmWidth|EpochCount|YearsPerEpoch|HomeworldRatePerCell|TraversabilityThreshold|ArmStrength|CoreRadius|DiscFalloff|MineralAnchorMultiplier|PrecursorAnchorMultiplier
```

Loader parses `f[11]`..`f[15]` into the new fields. Version-mismatch behavior is
unchanged (hard `InvalidDataException`); v2 artifacts are rejected by the
existing check once the constant bumps.

## 6. Atlas: service and UI

**GalaxyService** (unity/Assets/Scripts/Atlas/GalaxyService.cs): constructor
becomes `GalaxyService(GalaxyConfig config)`; the old `(ulong seed, int radiusCells)`
overload is removed (compile errors locate every caller). `GalaxyConfig` is the
settings DTO — no generation logic moves into Unity. The service gains
`BuildShapeOnly()` (wraps `SkeletonBuilder.BuildShape`) alongside `Build()`;
a shape-only service supports `Skeleton`, `TryGetCell`, and the density layer,
and is never asked for `Generate`/`StateOf`/polity data (setup has no map
interaction). `IsShapeOnly` exposes which build produced the current skeleton.

**AtlasUI** (unity/Assets/Scripts/Atlas/AtlasUI.cs):
- `GenerateRequested` event becomes `Action<GalaxyConfig>`; `OnGenerateClicked`
  assembles a `GalaxyConfig` from the controls (seed parse failure keeps the
  existing inline error path).
- Setup pane layout, top to bottom:
  - Title, `Seed` TextField, `Galaxy radius (cells)` IntegerField + existing warn label.
  - Foldout **Shape** (expanded by default): ArmCount SliderInt, ArmTightness,
    ArmWidth, ArmStrength, CoreRadius, DiscFalloff, MeanDensityTarget.
  - Foldout **Resources** (collapsed): MineralAnchorMultiplier,
    PrecursorAnchorMultiplier, HomeworldRatePerCell.
  - Foldout **History** (collapsed): EpochCount SliderInt, YearsPerEpoch SliderInt.
  - `Reset defaults` Button, then the existing `Generate` Button.
- All sliders are UI Toolkit `Slider`/`SliderInt` with `showInputField = true`
  (label + draggable track + editable value). Labels use plain language:
  "Arm count", "Arm tightness", "Arm width", "Arm strength", "Core bulge size",
  "Disc falloff", "Mean density", "Mineral-rich anchors ×",
  "Precursor sites ×", "Homeworld rate", "Epochs", "Years per epoch".
- `Reset defaults` sets every knob control (radius included) from a fresh
  `new GalaxyConfig()` so UI defaults can never drift from Core defaults. The
  seed field is deliberately left untouched — resetting shape knobs should not
  discard the seed being explored.
- The setup pane gains a max height with vertical scroll
  (`style.maxHeight = Length.Percent(85)`, `ScrollView` wrapping the groups) so
  small game views still reach the Generate button.

- Every knob/seed control change additionally raises `ConfigEdited` (also
  `Action<GalaxyConfig>`), assembled from the same control-reading code as
  Generate. An unparsable seed suppresses the event (last valid preview stays).

**Live preview flow (AtlasController):**
- The controller owns a dirty-flag + config snapshot; `ConfigEdited` stores the
  config and marks dirty. `Update()` on the Setup screen consumes the flag at
  most once per frame (coalescing slider-drag bursts): build a
  `GalaxyService(config)`, call `BuildShapeOnly()`, and show it on `galaxyView`
  with the **Density** layer forced, then `FitCamera(galaxyView.MapBounds)`.
- On entering Setup (app start, Back, Escape) the controller renders an initial
  preview from the current control values, so the screen is never empty.
- The setup pane stays as the left overlay; the preview renders behind it in
  the same galaxy view the Galaxy screen uses. No hover, tooltip, or click
  handling on Setup (the existing `Update` switch already ignores Setup —
  unchanged).
- Preview services are throwaway: clicking Generate always constructs a fresh
  `GalaxyService(config)` and runs the full `Build()` — determinism is anchored
  to config alone, so the preview and the generated galaxy always agree on shape.

**AtlasController** (unity/Assets/Scripts/Atlas/AtlasController.cs):
`OnGenerate(GalaxyConfig config)` constructs `GalaxyService(config)` and runs
the full build; `_seed` for HUD labels reads `config.MasterSeed`. `Render()`'s
Setup branch keeps `galaxyView` active for the preview instead of deactivating
it (the Galaxy branch re-shows the full-build service as today).

**AtlasAcceptance** (unity/Assets/Editor/AtlasAcceptance.cs): unchanged in
spirit — it sets the seed/radius fields and submits the Generate button, which
now flows through the new assembly path. Slider defaults make the produced
config equal to defaults with seed 42.

## 7. Testing

**Core (xunit, tests/Core.Tests):**
1. *Defaults equivalence:* `ShapeAt(new GalaxyConfig(), nx, ny)` equals the old
   literal formula evaluated inline at a grid of sample points (tolerance 1e-12),
   pinning that defaults reproduce pre-change output.
2. *Shape responsiveness:* with `ArmStrength = 0`, `ShapeAt` at fixed radius is
   angle-invariant (max−min over sampled angles < 1e-12); with larger
   `DiscFalloff`, density at r = 0.8 strictly increases.
3. *Anchor multipliers:* radius-8 build with `MineralAnchorMultiplier = 0` has
   zero MineralRich anchors and `PrecursorAnchorMultiplier = 0` has zero
   PrecursorSite anchors; multiplier 3.0 yields strictly more of that type than 1.0.
4. *Serializer:* round-trip a skeleton built from a config with all five new
   fields non-default; assert loaded config field equality and byte-identical
   re-serialization. Assert v2 header is rejected.

5. *Shape-only build:* `BuildShape` cell `MeanDensity`/`IsVoid` values equal the
   full `Build`'s for the same config, and the shape-only skeleton has zero
   species, polities, anchors, and events.

**Unity edit-mode (Atlas tests):** update `GalaxyServiceTests` /
`LayerPaletteTests` fixtures to the new ctor; add one test that
`new GalaxyService(new GalaxyConfig { MasterSeed = 42, GalaxyRadiusCells = 3 })`
builds 37 cells (ctor-path regression), and one that `BuildShapeOnly()` sets
`IsShapeOnly` and produces the same cell count as `Build()`.

**Live MCP acceptance (controller-led):** open setup → grouped sliders render
and scroll, and the density preview is visible behind the pane immediately;
drag ArmCount to 6 → preview re-renders with six arms **without clicking
Generate**; change the seed → preview changes; generate with defaults (1,615
cells, sim runs); set MineralAnchorMultiplier 3 → regenerate → anchored (white)
hexes visibly denser; Escape back to setup → preview returns; Reset defaults →
controls return to stock and preview reverts; console clean.

## 8. Follow-ups (recorded, not in scope)

- REPL `galaxy` command flag parsing for the same knobs.
- Config presets ("dense core", "grand design", "anchor-rich") and a randomize button.
- Async/throttled preview builds if radius-45 previews ever feel sluggish
  (current coalescing is once per frame, synchronous).
