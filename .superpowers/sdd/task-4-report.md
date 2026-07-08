# Task 4 Report: GalaxyConfig + origin-centered DensityField

(Note: this file previously held a report for a differently-numbered task on an
unrelated branch, `regional-slice1` — that content has been replaced below with
this task's report, per the hex-geometry plan on branch `hex-geometry`.)

## Summary

Replaced the rectangular `GalaxyConfig`/`DensityField` pair with the origin-centered,
hex-radius model per the brief. `ShapeAt` is byte-identical to the pre-task version
(verified by diff — only its callers changed). Applied the Step 4 "mechanical
unblocking" pass across every downstream file that referenced the deleted
`SizeSectors`/`CellsX`/`CellsY`/`WidthHexes`/`HeightHexes`/`HomeworldRatePerSector`
members, using `#warning HEXMIGRATION` markers wherever the fix is a compiling
placeholder rather than a real fix. No tuning of noise frequency or `ShapeAt`'s 0.45
disc weight was needed — the density bands passed with the brief's literal values on
the first run.

## TDD evidence

**Step 1 (RED — write failing tests).** Replaced
`tests/Core.Tests/Galaxy/DensityFieldTests.cs` with the brief's exact test file
(`At_IsDeterministic_AndBounded`, `OutsideGalaxy_IsZero_AndNotInGalaxy`,
`Core_IsDenserThanMidDisc`, `MeanInsideDisc_NearTarget`).

**Step 2 (verify RED).** `dotnet test --filter DensityFieldTests` failed to compile
with `CS0117: 'DensityField' does not contain a definition for 'InGalaxy'` (and
`'WorldRimRadius'`) — the expected "API missing" failure, not a typo or pre-existing
pass.

**Step 3 (GREEN — implement).** Wrote `GalaxyConfig.cs` and `DensityField.cs` exactly
as specified in the brief (`GalaxyRadiusCells = 21`, `HomeworldRatePerCell = 0.02`,
`InGalaxy`/`WorldRimRadius`/`At` as given; `ShapeAt` body untouched). Re-ran:

```
dotnet test --filter DensityFieldTests
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4
```

**Step 4 (mechanical unblocking).** Fixed every downstream compile error from the
deleted `GalaxyConfig` members (production + test code) — details below.

**Step 5 (verify the required green filter).**

```
dotnet test --filter "DensityFieldTests|GalaxyPresenceTests|HexGridTests"
Passed! - Failed: 0, Passed: 19, Skipped: 0, Total: 19
```

(12 HexGridTests + 4 DensityFieldTests + 3 GalaxyPresenceTests.)

## Tuning performed

None. `MeanInsideDisc_NearTarget` and `Core_IsDenserThanMidDisc` passed with the
brief's literal noise `frequency: 0.02`, `warpStrength: 30.0`, and the existing
`ShapeAt` `0.45` disc weight — no adjustment needed, so none was made (the tuning
clause is a contingency, not a mandate).

## Mechanical unblocking — what changed and why

### Trivial / correct renames (no HEXMIGRATION marker — these are exact, not placeholders)
- `src/Core/Galaxy/SkeletonSerializer.cs`: `SizeSectors` → `GalaxyRadiusCells`,
  `HomeworldRatePerSector` → `HomeworldRatePerCell` in both `Save` and `Load`. This is
  a 1:1 field rename of the persisted config record, not a logic change.
- `src/Core/Galaxy/RegionContext.cs` (`RegionContext.For`): the old
  `hex.Q >= WidthHexes || hex.R >= HeightHexes` rectangular bounds check is now
  genuinely correctly expressed as `!DensityField.InGalaxy(galaxy.Config, hex)` — the
  new membership test *is* the bounds check, so this is a real fix, not a stopgap.
- `src/Inspector/Repl.cs` `galaxy <seed> [size]`: `SizeSectors = size` →
  `GalaxyRadiusCells = size` (default bumped 10 → 21 to match the new default).

### Placeholder square-grid shim (introduces `GalaxySkeleton.GridSize`)
The rectangular `RegionCell` lattice (`Cx`/`Cy`, 8×10-hex subsectors) still underlies
`GalaxySkeleton`, `SkeletonBuilder`, `EpochSim`, `RegionContext`'s settlement-scale
interpolation, and both inspector map/REPL views. None of these are rewritten onto
the real hex lattice until their own tasks (5–10). To keep them compiling without
silently pretending the model is hex-native, `GalaxySkeleton` gained a temporary
`GridSize` derived as `GalaxyRadiusCells * 2 + 1` (a square standing in for the old
`SizeSectors`-derived `CellsX`/`CellsY`), plus a static
`GalaxySkeleton.GridSizeFor(GalaxyConfig)` for the few call sites that only hold a
config. Every call site that consumes this placeholder is marked. All 12 markers:

| # | File | Line | What's placeholder |
|---|------|------|---------------------|
| 1 | `src/Core/Galaxy/GalaxySkeleton.cs` | 17 | `GridSize` itself |
| 2 | `src/Core/Galaxy/RegionCell.cs` | 22 | `LinearIndex` width |
| 3 | `src/Core/Galaxy/EpochSim.cs` | 31 | `Adjacent()` 4-neighbor rectangular walk |
| 4 | `src/Core/Galaxy/SkeletonBuilder.cs` | 46 | `MarkChokepoints` grid width/height |
| 5 | `src/Core/Galaxy/SkeletonBuilder.cs` | 174 | `PassHomeworlds` target/spacing sizing |
| 6 | `src/Core/Galaxy/SkeletonBuilder.cs` | 251 | `NeighborhoodHasPrecursor` bounds |
| 7 | `src/Core/Galaxy/RegionContext.cs` | 63 | bilinear settlement-scale neighbor clamp |
| 8 | `src/Inspector/GalaxyMapView.cs` | 17 | `CellMap` render loop bounds |
| 9 | `src/Inspector/GalaxyMapView.cs` | 65 | `SectorMap` range check |
| 10 | `src/Inspector/GalaxyMapView.cs` | 74 | `CellZoom` range check |
| 11 | `src/Inspector/Repl.cs` | 69 | `cell` command bounds check |
| 12 | `src/Inspector/Repl.cs` | 138 | `WalkWidth` (goto/next/prev/find/stats linear walk) |

`dotnet build --no-incremental` on the full solution confirms all 12 emit
`warning CS1030: #warning: 'HEXMIGRATION: ...'` and nothing else — 0 errors. Per the
brief, Task 10 Step 6 greps for `HEXMIGRATION` and must find zero; these 12 are the
full current inventory.

### Test-file mechanical fixes (compile-only, not required to pass)
`SerializerTests.cs`, `SkeletonModelTests.cs`, `EpochSimTests.cs`,
`RegionIntegrationTests.cs`, `SeedingPassTests.cs`, `ValueNoiseTests.cs` all
constructed `GalaxyConfig` with `SizeSectors`/read old fields; renamed to
`GalaxyRadiusCells`/`HomeworldRatePerCell`/`GridSize` so the test assembly compiles.
`ValueNoiseTests.GalaxyConfig_Defaults` was updated to assert the new default fields
directly (`GalaxyRadiusCells == 21`, `HomeworldRatePerCell == 0.02`) instead of the
deleted ones.
`GalaxyPresenceTests.ShapedGalaxy_CornersEmpty_CoreDense` was rewritten per Step 4's
explicit instruction: corner assertion is now
`Assert.True(Generator.Generate(galaxy, new HexCoordinate(400, 0)).IsEmpty)`, and the
core-density sample centers on `(0,0)`.

## Red window: full-suite results

Required filter is green (19/19). Full suite: **90 passed, 9 failed, 99 total**.
Failing suites (all pre-existing rectangular-model logic, not touched by this task's
own scope — they burn down in Tasks 5–9 per the brief):

- `SkeletonModelTests` — 1 failure (`Skeleton_CellLookups_Work`: hardcoded cell
  indices from the old 8×8 assumption now exceed the smaller placeholder `GridSize`)
- `EpochSimTests` — 2 failures (`Polities_Expand_ButWildsRemain`,
  `ClaimedFraction_AtReferenceConfig_IsWithinAcceptanceBand`: claimed-fraction bands
  tuned to the old grid scale)
- `SeedingPassTests` — 3 failures (`Homeworlds_CountAndSpacing`,
  `Anchors_ArePlaced_OnePerHex_InsideTheirCell`, `MineralAnchors_FollowMetallicity`:
  homeworld/anchor counts tuned to the old cell count)
- `SerializerTests` — 1 failure (`GoldenSnapshot_SmallGalaxyHeader`: golden polity
  count frozen against the old grid scale)
- `RegionIntegrationTests` — 2 failures (`AnchoredHexes_AlwaysHaveSystems_WithAnchorTags`,
  `SettlementScale_RaisesSettlementInsidePolities`: anchor/settlement sampling tuned
  to the old grid scale)

All failures trace to the same root cause: the placeholder `GridSize` (a small square
sized off `GalaxyRadiusCells`) produces a different cell count/shape than the old
`SizeSectors`-derived `CellsX × CellsY`, so counts and bands calibrated to the old
scale no longer land. This is exactly the "skeleton/sim still rectangular-wrong" red
window the brief anticipates.

## Self-review

- `ShapeAt` byte-identical: confirmed via `git diff` — the method body has zero
  changes; only `At`'s call site (world coords, new noise frequency/warp per the
  brief) changed.
- `GalaxyConfig` matches the brief's interface exactly: `MasterSeed`,
  `GalaxyRadiusCells = 21`, `MeanDensityTarget`, `ArmCount`, `ArmTightness`,
  `ArmWidth`, `EpochCount`, `YearsPerEpoch`, `HomeworldRatePerCell = 0.02`,
  `TraversabilityThreshold`. No `WidthHexes`/`HeightHexes`/`CellsX`/`CellsY` remain
  on it (verified: only `GalaxySkeleton.GridSize`, a different type, carries the
  placeholder now).
- No `System.Random`/`DateTime` introduced; no new package references; Core project
  still targets `netstandard2.1` (untouched).
- Verified full solution builds (`dotnet build StarSystemGeneration.sln`) — 0 errors.
- Verified the required filter is green and captured the full-suite failure list
  above rather than only asserting the filter passed.
- Considered whether `RegionContext.For`'s bounds-check replacement
  (`DensityField.InGalaxy`) needed a HEXMIGRATION marker: it doesn't — the semantic
  meaning ("is this hex a galaxy member") is now expressed more correctly than
  before, not deferred.
- Considered whether the `SkeletonSerializer` field renames needed a
  `HomeworldRatePerCell`/`GalaxyRadiusCells` value-scale HEXMIGRATION note: no —
  they're a straight field carry-through with no interpretation attached at this
  layer; the golden-snapshot test failure downstream captures the scale change
  honestly.
- Did not touch `unity/Assets/Scripts/GalaxyMapSpike.cs` or
  `unity/Assets/Editor/StarGenSpikeMenu.cs` — out of scope per task instructions
  (Unity-only, deleted in Task 10, cannot affect `dotnet build`).

## Commit

`feat: origin-centered hexagonal galaxy config and density field` (see repo log for
final hash; commit body enumerates the failing suites above).

## Post-review fix

Added defensive guard in `RegionContext.For` after the `InGalaxy` check to reject negative-coordinate hexes, which the placeholder rectangular cell store (`GalaxySkeleton`, replaced in Task 5) cannot index. Guard is temporary; Task 8 rewrites the method onto the hex-lattice store. Also updated stale comments in test files mentioning `SizeSectors` to reference `GalaxyRadiusCells` and config sizes instead.
