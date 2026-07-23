# AC4.1 report — off-lane crawls render distinctly + ShipmentCard off-lane status

## What landed

### Core (TDD'd)

- `src/Core/Atlas/WorksLens.cs` — new `CrawlDashMark` record
  (ShipmentId, Origin, Dest, FromFraction, ToFraction, Color) and
  `WorksLens.CrawlPaths(model, eye)`: every LIVE off-lane shipment
  (`RouteLaneIds.Count == 0`) dashes its direct origin→dest line — dash
  count scales with hex span (`Math.Clamp(hexSpan, 6, 24)`), each dash
  covers 55% of its cell (`CrawlDashOnFraction`) leaving a visible gap,
  tinted by `FreightColorOf(purpose, stalled: false)` at a dimmer
  `CrawlPathAlpha = 110` (well under the freight mark's own 190–250) —
  brighter mark riding a dimmer path. Lane-routed shipments never dash
  (empty list) — the off-lane special case only, matching the RenderFreight
  idiom.
- `src/Core/Atlas/ShipmentPanel.cs` — `ShipmentCard` gains two fields:
  `OffLane` (the RouteLaneIds.Count == 0 idiom, spelled out so callers
  don't re-derive it from LaneCount) and `CrossesPatrolledSpace` (bool
  context only, never a probability). `CrossesHostileCoverage` samples
  `PatrolCoverage.At` at one hex per hop along the direct origin→dest line
  (lerp+round, the same technique `WorksLens.Freight` uses for the moving
  mark's position) — true if ANY hex on the path reads positive coverage.
  Always false for lane-routed shipments (the hazard doesn't apply to lane
  traffic). `PatrolCoverage.At` was already `public` — no visibility
  widening needed (unlike the AC2.7 `WarPresenceMap` precedent).
- Tests: `WorksLensTests.AnOffLaneCrawlDashesItsDirectPath`,
  `ALaneRoutedShipmentHasNoCrawlPath`;
  `ShipmentPanelTests.OffLaneFlagsByRouteLaneCount_CleanContextWithNoWar`,
  `CrossesPatrolledSpace_OnlyUnderActiveWarWithCoverageOnThePath` (peacetime
  patrol → clean; same patrol + declared war → flags; lane-routed sibling
  stays clean throughout).

### Unity

- `unity/Assets/Atlas/CrawlPathLayer.cs` (NEW) — mirrors
  `FlowTrailLayer`'s mesh/material pattern exactly (screen-constant width,
  CPU-linearized `.linear` vertex colors, altitude fade via
  `LodBands.LaneFade`) but draws one quad PER DASH (`WorksLens.CrawlPaths`
  already emits the discrete dash segments — no dashing logic needed on
  the Unity side). Z = -0.028 (just above `FlowTrailLayer`'s -0.03).
  Wired into `AtlasRoot` (SerializeField + `Wire(...)` param + ShowAll/
  OnZoomChanged, null-guarded like `flowTrailLayer`), `LensRail` (rides
  the `works` chip, same sibling-layer pattern as the trails),
  `AtlasViewSceneSetup` (GameObject + AddComponent + Wire call), and
  `AtlasSmoke` (EnsureMaterial/Show/SetVisible/OnZoom wiring — no new
  named shot; rides `atlas-smoke-works.png` alongside the trails, same as
  the AC2.F2 precedent, since a freshly loaded artifact has no off-lane
  shipments guaranteed at that exact year).
- `unity/Assets/Atlas/PanelViews.cs` (Shipment panel) — off-lane status
  row right after the route line: a `Tag` ("OFF-LANE" / "PATROLLED" with
  a `warn` modifier when crossing) plus a `Line` reading exactly the
  brief's example format: `"off-lane crawl · N years remaining"` with
  `" · crossing patrolled space"` appended when `CrossesPatrolledSpace`.

## Visual treatment chosen (taste call, for Eyeball 4)

Live crawl vs AC2.F2's memory trails distinguished on TWO axes at once:
1. **Dashed vs solid** — the crawl path is literally discontinuous
   (gaps between dashes); flow trails are solid quads.
2. **Full-purpose palette vs two-purpose palette** — crawl paths use all
   four `FreightColorOf` tints (state haul, spread run, courier, war
   convoy — since any purpose can go off-lane); trails only ever render
   courier-violet or war-convoy-red (`RecentFlowQuery.Renders`).
3. Alpha (110) sits below the trail's floor-to-cap band (70–130) but
   isn't the primary signal — dashing carries that job so the two never
   look like the same idiom at a glance.

The freight mark itself (`WorksLayer`, unchanged) rides on top of its own
crawl path at full purpose-tint alpha (190–250) — "brighter mark, dimmer
path" reads as the live position advancing along its own trail.

## PatrolCoverage access pattern

`PatrolCoverage.At` was already `public static` (no AC2.7-style
private→internal widening was needed). The AC4.1 read is deliberately
NOT the same sample ShipmentOps' actual seizure roll uses — `ShipmentOps.
Sail` (Epoch/ShipmentOps.cs:360-369) only checks coverage AT THE
DESTINATION (the drop point) when accumulating `coveredYears`/
`maxCoverage` for the roll. `CrossesHostileCoverage` instead samples every
hex along the FULL path, because it's answering a different question — a
player-facing "is this crawl passing through contested space right now"
context read, not a re-derivation of when/whether the seizure roll fires.
This is intentional and noted so it isn't mistaken for a duplicated rule:
the roll's own math (probability, prize routing) was never touched or
copied.

## REPL parity (requirement 3)

- `efreight` (`Repl.RenderFreight`) — untouched; not modified by this
  task. Still the idiom source (`RouteLaneIds.Count == 0 → "off-lane"`)
  and still independent of `ShipmentPanel` (it never called it).
- No `eship`/shipment-detail REPL command exists in this codebase — grep
  confirmed. Nothing to extend; noted here rather than invented.

## Where to find a live off-lane crawl for Eyeball 4

Per the brief's own note: off-lane transit years are long (25–60y
observed at epoch 80, seed 42) — scrub/step to late epochs and open the
works lens (`works` chip) to see a dashed crawl path with its freight
mark riding it. Click the mark to open the Shipment panel and see the
OFF-LANE/PATROLLED tag + status line. To see `CrossesPatrolledSpace`
flip true, look for/step to a period with an active war AND a Patrol
fleet postured near the crawl's origin→dest line — the coverage read is
hostile-only (peacetime and allied patrols project nothing, matching
`PatrolCoverage`'s own §5 gate, exercised directly in the new
`ShipmentPanelTests` case).

## Gate evidence (editor closed throughout — confirmed via `tasklist` for
`Unity.exe` returning nothing before AND after all three batch runs)

1. `dotnet test StarSystemGeneration.sln` — `Passed! Failed: 0, Passed:
   1293, Skipped: 0, Total: 1293` (base 1289 + 4 new: 2 `WorksLensTests`,
   2 `ShipmentPanelTests`). Includes the determinism/golden suite — no
   golden churn (`git status --short tests/Core.Tests/Goldens/` empty).
   Zero sim behavior touched — Core.Atlas-only additive fields/methods,
   `PatrolCoverage.At` called read-only, never modified.
2. Unity batch compile — `Unity.exe -batchmode -quit -projectPath unity
   -logFile compile-ac4.1.log`. Log 44,152 bytes (real run — genuine
   domain-reload/compile phases, not the ~2s editor-lock death).
   `grep -c "error CS"` → **0**. Tail: `Exiting batchmode successfully
   now! ... return code 0`.
3. EditMode suite — `Unity.exe -batchmode -projectPath unity -runTests
   -testPlatform EditMode -testResults test-results-ac4.1.xml -logFile
   test-ac4.1.log`. `test-ac4.1.log`: 336,050 bytes (genuine full-boot
   size class). `unity/test-results-ac4.1.xml`: `testcasecount="16"
   total="16" passed="16" failed="0"` — matches the standing base count
   exactly (no new EditMode tests added; the new layer is covered by the
   batch-compile + AtlasSmoke gates, matching the AC2.F2/AC2.7 precedent
   for pure-wiring additions).
4. AtlasSmoke (run — wiring changed) — `Unity.exe -batchmode -quit
   -projectPath unity -executeMethod
   StarGen.AtlasView.EditorTools.AtlasSmoke.RunFromCli -logFile
   smoke-ac4.1.log`. `grep -c "error CS"` → **0**. `grep -c "AtlasSmoke:
   wrote"` → **18**, matching the current (Phase-3-established) 18-shot
   baseline exactly — no new named shot (crawl paths ride
   `atlas-smoke-works.png` alongside the flow trails, same precedent).
   `atlas-smoke-works.png` renders (no off-lane shipments guaranteed at
   the loaded artifact's exact base year, same honest-empty caveat the
   AC2.F2 trails have at cold load). Scene reverted after:
   `git status --short unity/Assets/Scenes/Atlas.unity` showed ` M`
   immediately post-run; `git checkout --
   unity/Assets/Scenes/Atlas.unity` restored it clean.

Post-gate `git status --short` matches the standing churn exactly
(ProjectSettings asset churn + untracked earlier-task `.superpowers/sdd/*`
briefs/reports + the pre-existing stray `src/Core/Epoch/*.cs.meta` files)
plus this task's own new/modified paths — no unexpected residue.

## Commit

`feat(ac): off-lane crawl paths + ShipmentCard detection-risk context
(AC4.1)`, explicit paths: `src/Core/Atlas/{ShipmentPanel,WorksLens}.cs`,
`tests/Core.Tests/Atlas/{ShipmentPanelTests,WorksLensTests}.cs`,
`unity/Assets/Atlas/{AtlasRoot,CrawlPathLayer,CrawlPathLayer.cs.meta,
LensRail,PanelViews}.cs`, `unity/Assets/Editor/{AtlasSmoke,
AtlasViewSceneSetup}.cs`, this report. `unity/ProjectSettings` churn and
the pre-existing untracked `.superpowers/sdd/*`/`src/Core/Epoch/*.cs.meta`
files left uncommitted, per standing convention.

## Concerns

None blocking. One taste note already flagged above for Eyeball 4: the
dash-count/gap/alpha constants (`WorksLens.CrawlDashMin/Max/
OnFraction/CrawlPathAlpha`) are a first pass — easy single-file tuning
if the eyeball wants denser/sparser dashing or a different alpha floor.
