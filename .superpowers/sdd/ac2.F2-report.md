# AC2.F2 report — recent flows for couriers + war convoys

**Status: DONE_WITH_CONCERNS** (Unity batch gates blocked: editor open) ·
**Commit: 6376180** on `claude/slice-ac-0e02d9`.

## Seam chosen + why

A `ShipmentObserver` **delegate held by `EpochEngine`** (`ShipmentObserver`
property, null default), threaded onto a **transient, never-serialized
`SimState.ShipmentObserver` field for exactly one step's duration** —
`EpochEngine.Step` assigns it before the phases and resets it in a
`finally`, so a tap can never leak past its step even on an exception.
This was picked over pure parameter-threading because launches happen five
call-layers below `Step` (phases → polity ops → `CourierOps.AcceptOpen` →
`Accept` → `ShipmentOps.Dispatch`) and every op is a static over
`SimState`; the state field is the one place all launch sites already
reach, while the engine property keeps ownership/reset semantics exactly
as the brief preferred. It is not a global static; each `SimState` is
test-isolated by construction.

Notification (`ShipmentOps.NotifyLaunch`) fires at **both** launch sites
(`Dispatch` internal overload, `DispatchVia`) **after** `Sail`, so every
launch is seen exactly once — including sub-step deliveries where
`Dispatch` returns null and the registry never hears of the shipment (the
Eyeball 2 invisibility, and the whole point). A null observer costs
nothing: the guard precedes even the cargo-snapshot allocation.

**Rider linkage is explicit, not looked up**: `CourierOps.Accept` passes
its contract into `Dispatch` (`rider: c`). At launch the contract is still
`Open`, and a sub-step transit resolves it immediately — so
`CourierOps.OfShipment` (the AC2.6 registry rule) can never answer at
capture time. The purpose rule itself stays single-sourced:
`FreightPurposeQuery.FromParts(channel, hasRider, riderPriority)` was
factored out and `Of` now calls it, so registry-derived and capture-derived
purposes can never drift.

## TimeMachine shape

`Keyframe` gained an init-only, in-memory `RecentFlows`
(`IReadOnlyList<RecentFlow>`, defaults `Array.Empty`) — **never
serialized** (keyframes only serialize their `Delta` text; flows live
beside it in the object). `TimeMachine.Step` wires one engine tap into a
reused list, clears it per epoch, and attaches a `ToArray()` snapshot to
each new keyframe. Consequences, all tested:

- base frame: empty (no step preceded it in-session) — correct per brief;
- scrubbing recalls each keyframe's own flows (`CurrentFlows` =
  `Active.Frames[Position].RecentFlows`);
- stepping over recorded frames replays via `ScrubTo` and therefore
  **recalls, never re-captures**;
- forks share `Keyframe` objects up to the anchor, so flows travel with
  the shared past for free.

`RecentFlowQuery` (Core/Atlas): `Capture` (launch → flow, purpose derived
now), `Renders` (courier/war-convoy only — capture records all four
purposes, the filter is the render boundary), `Trails` (corridor
aggregation per (origin, dest, purpose) in first-seen capture order;
alpha floor 70, +20 per extra flow, cap 130 — subordinate to live marks
at 220–250 and to lane strokes; overdraw reads as intensity).

## Rendering

`FlowTrailLayer` (new Unity layer, sibling of `WorksLayer` under the works
chip — **no new rail key, no legend change**): quad-strip strokes
origin→dest port hex at Z −0.03 (under the lanes' −0.05, far under the
glyphs' −0.22), 1.0 px width vs the lanes' 1.4, `LodBands.LaneFade`
altitude fade, vertex colors CPU-linearized (`.linear`, alpha untouched).
Wired into `AtlasRoot` (field/property/`Wire`/`ShowAll`/`OnZoomChanged`,
**null-guarded** so the committed scene keeps working until regenerated),
`LensRail` (`SetVisible(_works)`), `AtlasViewSceneSetup`, and `AtlasSmoke`
(EnsureMaterial/Show/style; visible in the works shot). The smoke's
loaded-artifact frame has no flows — an honest empty, same as the REPL.

## REPL surface chosen

**`eflows`** as its own command (not an `efreight` trailing section —
`efreight` is the in-transit registry and one estep on the golden shows
them telling opposite truths, which is exactly the story). The REPL holds
the last estep's captures (`_recentFlows`, cleared per step and at every
`_sim` load site: `epoch`, `watch`, `eload`, `edload`). Formatter filters
through `RecentFlowQuery.Renders`; derivation is Core's, REPL formats.

Live evidence (golden artifact, seed 42, epoch 40→41):
- `eflows` before any step: "no recent courier/war-convoy flows — estep
  to sail a step (a freshly loaded world starts with none)".
- after one `estep`: ~90 courier flows listed (routes, cargo, owners) while
  `efreight` prints "no shipments in transit".

## Gate evidence

- `dotnet test StarSystemGeneration.sln`: **1270/1270 green** (base 1256 +
  14 new). Includes `GoldenTests.ReferenceArtifact_MatchesTheFrozenGolden`
  — **golden byte-identity asserted, untouched**.
- Observer passivity **asserted, not assumed**:
  `TheObserver_IsPassive_TheSteppedWorldIsByteIdentical` steps two seeded
  worlds 12 epochs, one tapped one not — `ArtifactSerializer.ToText`
  equal. Reset-safety asserted (`EngineStep_Threads…AndResetsIt`).
- New tests: launch seen (in-transit + sub-step), rider carried from
  `Accept`, `FromParts` = the efreight rule (4 cases), render filter,
  corridor aggregation + alpha cap, base-frame-empty, scrub recall,
  replay-recalls-not-recaptures, and seeded-world capture (launches DO
  appear within 5 epochs of the test world — the finding, mechanized).
- **Unity gates: BLOCKED-on-editor** — `tasklist` showed two live
  `Unity.exe` (the user's eyeball session). Batch compile + EditMode
  (base 16) not run; commit `7aff333` (panel tables) ALSO still awaits its
  first batch compile, so the next batch run verifies both this commit and
  that one. `Atlas.unity` was already dirty from the open editor and was
  left uncommitted (as were ProjectSettings, per standing rule).

## Carries for the re-eyeball

1. **Run the Unity gates once the editor closes**: batch compile +
   EditMode; then `AtlasSmoke` (remember it regenerates
   `unity/Assets/Scenes/Atlas.unity` — `git checkout` it before staging).
   Verifies 7aff333 + 6376180 together.
2. **Regenerate the scene** (SceneSetup runs inside AtlasSmoke) so the
   serialized `AtlasRoot` gains the `FlowTrailLayer` reference — until
   then the live scene simply shows no trails (null-guarded, no errors).
3. Eyeball the trails in play mode: load/run a seed, **step at least
   once**, works chip on — courier violet / war-convoy red strokes at
   reduced alpha under the lanes. Check the subordination read (trails
   must feel like memory next to crates and lane strokes) and corridor
   intensity where flows stack.
4. AtlasSmoke's works shot will NOT show trails (loaded base frame has no
   flows by design). If a smoke-visible trail shot is ever wanted, the
   smoke would need to step the machine once first — deliberate non-goal
   this task.
5. Trail alpha constants (70/+20/cap 130) are a first guess at
   "subordinate"; tune at the eyeball if they read too loud/quiet.

---

# Fix appendix — per-leg trails (eyeball finding on 6376180)

**Status: DONE** · **Commit: 38de1e2** (on top of 6376180).

## The finding, fixed

Trails drew a straight origin→dest stroke for lane-routed shipments (the
freight RESULT, not the sailed route). Root cause exactly as diagnosed:
`ShipmentLaunch` captured only the endpoint port ids; the route computed
in `Dispatch` was dropped at the tap.

## What changed

- **`ShipmentLaunch.RouteHexes`** (`src/Core/Epoch/ShipmentObserver.cs`):
  the ordered chain of port hexes the route traverses (origin→…→dest,
  legs = Count−1). Built in `ShipmentOps.NotifyLaunch` by walking the
  route's lanes (far-port per lane) **at capture time** — leg endpoint
  HEXES, not lane ids, so a scrubbed keyframe's trails never depend on a
  lane registry that changed later in the run (the brief's preference,
  honored). Off-lane (`RouteLaneIds.Count == 0`) captures the endpoint
  pair — the honest special case; the crawl really sails that line.
- **`RecentFlow.RouteHexes`** rides through `Capture` unchanged.
- **`RecentFlowQuery.Trails` renders PER LEG**: one stroke per adjacent
  hex pair per flow; aggregation re-keyed per (leg, purpose) with the hex
  pair **normalized by (Q,R)** so opposed sailing directions stack one
  corridor's intensity ("every flow crossing it"); marks emit in
  first-seen order at first-seen orientation (capture order = dispatch
  order, deterministic). `Trails` no longer takes `SimState` at all — the
  flows are self-contained; `FlowTrailLayer.Show(flows)` dropped the
  model parameter (AtlasRoot + AtlasSmoke callers updated).
- **`eflows`**: extended minimally — route column now reads
  `#origin->#dest (N legs)` (seed-42 live: `#29->#4 (3 legs)` among the
  1-leg majority). Noted here per the brief.
- Design amendment §2 updated to the per-leg wording, same commit.

## New tests (6; total 1276)

`ALaneRoutedLaunch_CapturesTheLegEndpointHexes` (0—1—2 chain → 3 hexes) ·
`AnOffLaneLaunch_KeepsTheDirectEndpointPair` ·
`Trails_LaneRoutedFlow_RendersPerLeg_NeverTheStraightResult` ·
`Trails_OffLaneFlow_KeepsTheDirectLine` ·
`Trails_ASharedLeg_StacksIntensityAcrossFlows` (H0→H1→H2 + H3→H1→H2:
shared leg counts 2, alpha floor+1·step) ·
`Trails_OpposedDirections_ShareOneCorridorLeg`.

## Gate evidence (full set, editor closed)

- `dotnet test StarSystemGeneration.sln`: **1276/1276** (base 1270 + 6).
  Golden byte-identity (`GoldenTests`) and observer-passivity byte-assert
  both in the run, green.
- **Unity batch compile: clean** (exit 0, zero `error CS`; log noise is
  licensing-handshake only). This was also the FIRST batch compile for
  7aff333 (panel tables) — both commits verified.
- **EditMode: 16/16** (`unity/test-results.xml`).
- **AtlasSmoke: full suite rendered** (exit 0, all 18 shots incl.
  `atlas-smoke-works.png`); `unity/Assets/Scenes/Atlas.unity` checked out
  after the run per instruction (note: this also discarded the prior
  editor-session churn on that file — it now matches HEAD).
  ProjectSettings churn left dirty and unstaged, per standing rule.

## Carries

- The smoke works shot still shows no trails (loaded base frame has no
  captured flows by design) — a live eyeball needs one step, works chip
  on. Expect trails hugging the lane network now, with corridor
  brightening where couriers share legs; off-lane crawls remain the only
  straight lines.
- The regenerated-scene carry from the base report is now satisfied:
  AtlasSmoke's SceneSetup run rebuilt the scene with FlowTrailLayer wired
  — but the scene file was checked out afterward, so the COMMITTED scene
  still lacks it (slice-end `chore: atlas scene rebuilt` remains the
  plan, per HANDOFF deferred item 1).

---

## Fix wave — Eyeball 4 finding: trail/crawl double-draw

**Finding**: off-lane crawl dashed paths (AC4.1) overlapped the violet
recent-flow trails (AC2.F2) for the same shipment on the same launching
keyframe — a live courier still under way drew both its dashed crawl AND
its solid memory trail on the same origin→dest line. Redundant noise.

**Fix** (read-only query change, `RecentFlowQuery.Trails`): the trail
derivation now takes an optional `IReadOnlyCollection<Shipment>?
inFlightShipments` and skips any captured flow whose `ShipmentId` is still
present in it — a shipment still in flight at the queried moment is the
live crawl's job, not the trail's. Because each keyframe only holds its
OWN step's captured flows, this can only ever suppress the LAUNCHING
keyframe's trail; a flow that delivered within its step (the common
courier case, most lane-borne shipments) still trails as before, and later
keyframes never re-capture a flow they didn't launch, so their crawl-only
render was already correct — this fix only touches the one keyframe where
both used to draw.

**Callers updated** to pass the live registry: `FlowTrailLayer.Show` grew a
second optional param (`AtlasRoot.ShowAll` passes `simHost.State?.Shipments`;
`AtlasSmoke` passes `host.State.Shipments`). All six pre-existing `Trails`
tests still call the one-arg form and stay green (default `null` =
no filter, unchanged behavior) — no test churn beyond additions.

**`eflows` REPL semantics (per brief instruction)**: kept every row —
`eflows` lists what launched, unchanged in count. A still-in-flight
shipment now gets a `(in transit)` tag appended to its row so the two
surfaces (map trail vs REPL list) stay honest about the same fact rather
than one silently dropping rows.

### New tests (`tests/Core.Tests/Atlas/RecentFlowTests.cs`)

- `Trails_ShipmentStillInFlight_YieldsNoTrail` — a flow whose shipment is
  passed as still-in-flight yields no trail.
- `Trails_SameFlow_ShipmentDelivered_YieldsItsTrail` — the same flow with
  an empty in-flight set still trails (delivered-within-step case).
- `Trails_InFlightFilter_OnlySuppressesItsOwnShipment` — two flows, only
  one's shipment in flight: only that one is suppressed, the other still
  trails.

### Gate evidence

- `dotnet test StarSystemGeneration.sln`: **1301/1301** (base 1298 + 3),
  0 failed. Golden/determinism suites included and green; this is a
  read-only Atlas query change, zero sim code touched.
- Fixed a `CS8625`/`CS8600` nullable-reference warning on the new optional
  parameter (repo uses `Type?` for nullable refs elsewhere in
  `src/Core/Atlas`) — `IReadOnlyCollection<Shipment>?` / `HashSet<int>?`.
- **Unity batch compile: clean** (editor confirmed closed via `tasklist`
  before every run — no `Unity.exe`), exit 0, `error CS` count 0.
- **EditMode: 16/16** (`unity/test-results-ac2F2.xml`, not committed —
  build artifact, deleted after reading).
- **AtlasSmoke: 18/18 shots wrote**, exit 0, no non-licensing
  errors/exceptions in the log; `lens suite rendered` line confirms
  `host.State.Shipments` reads fine through the new `FlowTrailLayer.Show`
  overload at runtime.
- All batch-run scratch files (`compile-ac2F2.log`, `test-ac2F2.log`,
  `smoke-ac2F2.log`, `unity/test-results-ac2F2.xml`, `atlas-smoke*.png`)
  deleted after use — none committed.

### Concern carried forward (not mine to fix)

`unity/Assets/Scenes/Atlas.unity` was ALREADY modified in the working tree
before this task touched anything (897 insertions / 475 deletions vs HEAD
`a132853`) — present in `git status` before any Unity batch command ran
this session. This is much larger than the batch-run reserialization noise
AC4.4 discarded with a plain `git checkout --`; discarding it here risked
losing someone else's uncommitted scene work (the scene isn't this task's
surface — AC2.F2 owns `RecentFlowQuery`/the trail layer, not the scene
file), so it was left untouched and excluded from this commit by explicit
path. Flagging for whoever owns the scene file next: confirm whether that
diff is intentional (e.g. an in-progress `CrawlPathLayer` wiring) before
either committing or discarding it.

**Commit: (see top-level commit list on this branch for the exact SHA —
recorded by the caller after this report lands).**
