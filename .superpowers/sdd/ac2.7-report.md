# AC2.7 report — war-supply readout (forward depot + contested-lane shading)

## What shipped

**1. Forward depot (Fleet panel + War panel, Core + Unity + REPL).**
A DEPLOYED fleet (posture Blockade or Expedition — the exact criterion
`FleetOps.SupplyFleets` already uses to pick a forward depot over home
port) now names that depot everywhere the fleet surfaces:

- `src/Core/Atlas/FleetPanel.cs` — `FleetCard` gained
  `ForwardDepotPortId`/`ForwardDepotDistanceHexes` (-1/-1 for any other
  posture, or a deployed fleet whose owner holds no port). Computed with
  `FleetOps.NearestOwnedPortId` + `HexGrid.Distance` — no new derivation.
- `src/Core/Atlas/WarPanel.cs` — `WarFleetRow` (the `FleetsOnStation` list)
  gained the same two fields; every row there is already Blockade/
  Expedition by construction, so it's computed unconditionally.
- `src/Inspector/FleetView.cs` (`fleet <id>`) — one new `forward depot:
  port #N (Dh)` line, gated on the same posture check.
- `src/Inspector/InterpolityView.cs` (`war <id>`, the on-station fleet
  line) — ` · depot #N (Dh)` appended, same query.
- `unity/Assets/Atlas/PanelViews.cs` — `Fleet()` panel: a `forward depot`
  Kv row when `ForwardDepotPortId >= 0`. `War()` panel: ` · depot #N (Dh)`
  appended to each on-station fleet line.

REPL parity verified live (seed 42, `epoch 42 30`, war #7 "the Belal
Expulsion", fleet #340 Blockade at port #24): `war 7` prints
`fleet #340: 1 hulls at (-28,-32) under Drerarin (readiness 0.86) · depot
#26 (9h)`; `fleet 340` prints `forward depot: port #26 (9 hexes)` — same
port, same distance, both reading `FleetOps.NearestOwnedPortId`.

**2. Contested-lane shading — SHIPPED (the conditional resolved yes).**
Investigated `PatrolCoverage.At` (single-point coverage against one
named owner — wrong shape for a lane-level, owner-agnostic map read) and
`ShipmentOps`'s private `WarPresenceMap(state)` (warships bearing on each
lane: Blockade/Expedition within `InterdictionReachHexes` of either
endpoint, plus Escort riding the lane — exactly the brief's own example:
"an existing per-lane presence read used by ShipmentOps that can be
CALLED, not copied"). Chose `WarPresenceMap`: widened its accessibility
from `private` to `internal` (zero logic change — same assembly, same
method body) and added `WarLens.ContestedLanes` in
`src/Core/Atlas/WarLens.cs`, which calls it and classifies a lane
contested when any squadron on it is at active war with either endpoint
port's owner (`WarOps.ActiveWarBetween`, already public, already used by
`WarLens.SlotBelligerence`). No reach/roll/rule math is re-derived; the
new code is pure classification over an existing read's output.

- `src/Core/Atlas/WarLens.cs` — `ContestedLane` record,
  `ContestedLaneColor` constant, `ContestedLanes(model, eye)` query.
- `src/Core/Atlas/LegendQuery.cs` — one new entry under the existing
  `"war"` case (no rail-key change; `"war"` was already in
  `LegendDriftTests.RailKeys`).
- `unity/Assets/Atlas/LaneLayer.cs` — new `LaneMode.War`: draws only the
  contested lanes (sparse overlay, mirrors the war lens's own stations —
  not the full lane network).
- `unity/Assets/Atlas/LensRail.cs` — the `war` chip now also makes
  `LaneLayer` visible and selects `LaneMode.War`.

## Gate evidence (editor closed throughout — confirmed via `tasklist` for
`Unity.exe` returning nothing before each batch run)

1. `dotnet test StarSystemGeneration.sln` — `Passed! Failed: 0, Passed:
   1256, Skipped: 0, Total: 1256` (base 1251 + 5 new: 2 `FleetPanelTests`,
   1 `WarRelationsPanelTests`, 2 `WarLensTests`). Includes the
   determinism/golden suite — no golden churn, no failures. Zero sim
   behavior touched (only Atlas/Inspector read surfaces + one
   `private`→`internal` visibility change on `ShipmentOps.WarPresenceMap`).
2. Unity batch compile — `Unity.exe -batchmode -quit -projectPath unity
   -logFile compile-ac2.7.log`. Log: 328,827 bytes / 4,324 lines (real
   run). `grep -c "error CS"` → 0. Tail: `Exiting batchmode successfully
   now! ... return code 0`. `git status` on
   `unity/Assets/Scenes/Atlas.unity` clean before and after.
3. EditMode suite — `Unity.exe -batchmode -projectPath unity -runTests
   -testPlatform EditMode -testResults test-results-ac2.7.xml -logFile
   test-ac2.7.log`. `test-results-ac2.7.xml`: `total="16" passed="16"
   failed="0"` (base 16, unchanged — `"war"` was already a `RailKeys`
   entry, so the new legend row needed no test wiring; the lane/rail
   plumbing is covered by the batch-compile gate + REPL-parity, matching
   the AC2.4–AC2.6 precedent). Scene stayed clean.
4. AtlasSmoke — not run (not required for this task).

Commit: `feat(ac): war-supply readout — forward depot + contested-lane
shading (AC2.7)`, explicit paths (`src/Core/Atlas/{FleetPanel,
LegendQuery,WarLens,WarPanel}.cs`, `src/Core/Epoch/ShipmentOps.cs`,
`src/Inspector/{FleetView,InterpolityView}.cs`,
`tests/Core.Tests/Atlas/{FleetPanelTests,WarLensTests,
WarRelationsPanelTests}.cs`, `unity/Assets/Atlas/{LaneLayer,LensRail,
PanelViews}.cs`, this report). `unity/ProjectSettings` churn, the
pre-existing stray `src/Core/Epoch/*.cs.meta` files, and other untracked
`.superpowers/sdd/*` files from earlier AC2.x tasks left uncommitted as
instructed.

## Carries for Eyeball 2

- **Where to see contested-lane shading in seed 42**: `epoch 42`, `estep
  40` or later once a war is active with a Blockade/Expedition fleet
  within `InterdictionReachHexes` of a lane endpoint (war #7 "the Belal
  Expulsion" / fleet #340 at y700+ is one; its blockade sits AT port #24
  — any lane touching port #24 will show contested if it's also
  `severed` by the same blockade — worth checking in the Atlas whether
  the contested stroke and the severed-lane stroke ever coincide
  visually and whether that reads as redundant or as reinforcing).
- The war lens's lane overlay is intentionally sparse (contested lanes
  only, not the full network) to match the existing war-station grammar
  (glyphs only, no base domain fill of its own). If Eyeball 2 wants the
  full lane network visible too (dimmed, for context), that's a follow-up,
  not a defect — the brief asked for shading "on the war lens," not a
  lane-network toggle change.
- `ForwardDepotPortId`/`DepotPortId` are plain port ids (no port `Name`
  field exists anywhere in the model — every existing surface, REPL and
  Unity, already identifies ports by id only, so this task didn't invent
  a naming convention).
