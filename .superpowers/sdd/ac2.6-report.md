# AC2.6 report — freight purposes on the map + ShipmentPanel purpose

## Helper shape chosen, and why

**One new Core.Atlas query, `FreightPurposeQuery.Of(state, shipment)`, ported
byte-for-byte from `Repl.RenderFreight`'s inline derivation.** All three
consumers (REPL, ShipmentPanel, WorksLens) now call it — nobody re-derives
the rule.

- New file `src/Core/Atlas/FreightPurposeQuery.cs`:
  - `enum FreightPurpose { WarConvoy, Courier, SpreadRun, StateHaul }`
  - `readonly record struct FreightPurposeInfo(FreightPurpose Purpose, int?
    RiderContractId)` — nullable id, per the brief.
  - `FreightPurposeQuery.Of(SimState state, Shipment shipment)`: calls the
    existing `CourierOps.OfShipment(state, shipment.Id)` (already `internal`,
    already exactly the rider lookup `RenderFreight` used inline — same
    assembly, no visibility change needed). Rider found → its `Priority ==
    War` picks WarConvoy else Courier, `RiderContractId = rider.Id`. No
    rider → `shipment.Channel == Freight` picks SpreadRun else StateHaul,
    `RiderContractId = null`.
- `ContractsPanel.cs` gained `ContractsPanel.Row(model, eye, contractId)` —
  a single-row lookup mirroring `ShipmentPanel.Card`'s pattern, so the
  ShipmentPanel's rider link reads the SAME `ContractRow` (route by owner
  name, fee, poster) `econtracts`/the job board already formats — no
  duplicated contract text anywhere.
- `ShipmentPanel.CardOf` now takes `(model, eye, shipment, severed)`
  instead of `(state, shipment, severed)` (plumbed through from `Cards`/
  `Card`) so it can call `ContractsPanel.Row` with the same eye/model the
  caller already has. `ShipmentCard` gained two fields: `Purpose` and
  `Rider` (`ContractRow?`, null unless Courier/WarConvoy).
- `Repl.cs RenderFreight` re-points its purpose derivation to
  `Core.Atlas.FreightPurposeQuery.Of(sim, s)` — a 4-way switch on the
  returned enum picks the same four label strings ("war convoy", "courier",
  "spread run", "state haul") it printed before.

## Map treatment chosen

**Tint only — no new glyph shape.** `WorkFreight` is a single authored icon
(cardboard-box, `AtlasGlyphs.cs` slot 12); the codebase's own convention
(Sites' starvation-cascade tint, Fleets' owner-tint) is color-carries-state,
so purpose follows that grammar rather than inventing per-purpose shapes.

- `WorksLens.FreightMark` gained a `Purpose` field (from
  `FreightPurposeQuery.Of`, computed once per shipment in `Freight()`).
- Four named `Rgba` constants replace the old `FreightMoving`/
  `FreightStalled` pair (`FreightStalled` kept, unchanged value):
  `FreightStateHaul` (== the old `FreightMoving` — no visual change for the
  most common case), `FreightSpreadRun` (= `TradeLens.MarginGold` — a
  trader's own margin, literally the same gold the trade-margin lens
  already uses), `FreightCourier` (a violet the rest of the vocabulary
  doesn't claim), `FreightWarConvoy` (= `WarLens.StationBurn` red at max
  alpha — red already means "war" throughout the atlas: `DomainLens.
  WarShade`, `WarLens.StationBurn`; reusing it for war convoys is the
  grammar-consistent choice, not a new color invented for this task).
- **STALLED still overrides to the one `FreightStalled` red for ANY
  purpose** — a closed leg is the same alarm regardless of whose cargo it
  is (kept the existing `WorksLensTests.AClosedLegReadsStalled` invariant
  unmodified — it still passes). `WorksLens.FreightColorOf(purpose,
  stalled)` centralizes the two-step pick.
- Unity `WorksLayer.cs`: size now carries war-convoy identity on TOP of
  color, independent of stalled state — moving non-war 11f (unchanged),
  moving war-convoy 13f (matches the expedition-convoy glyph's existing
  13f — "war-scale things read at 13+"), stalled non-war 14f (unchanged),
  stalled war-convoy 15f (biggest of all, matching the construction-site
  glyph's 15f). So a war convoy is the most distinguishable of the four
  whether it's under way or stalled — color while moving, size always.
- `LegendQuery.cs`'s `"works"` case: the old 2-entry freight pair (moving/
  stalled) became a 6-entry freight set — 4 purposes (moving colors) + 1
  STALLED (any purpose) — plus the unchanged site/convoy entries. **No rail
  key changed** (still `"domains","war",...,"works",...,"nature"` — I only
  added entries inside the existing `"works"` case), so the three-place
  contract (`LegendQuery` / Unity `LensRail.cs` / Unity
  `LegendDriftTests.RailKeys`) needed no edits — verified `LegendDriftTests.
  RailKeys` still lists `"works"` unchanged and every new entry's
  `GlyphKey` is still `"WorkFreight"` (a real `AtlasGlyph` member), so
  `EveryLegendGlyphKeyNamesAnAtlasCell` still holds by construction.

## ShipmentPanel (Unity) treatment

`PanelViews.Shipment(...)`: a purpose tag row up top (`WAR CONVOY` in the
`ssg-tag--bad` red accent — the same STALLED/WAR-contract accent — for
WarConvoy, plain text for the other three). After the cargo section, when
`card.Rider != null` (Courier/WarConvoy only): a "rider contract" section
with route (by owner name), fee, poster, and a `Link` button ("OPEN
CONTRACTS BOARD (#id)") that opens `PanelType.Contracts`. This opens the
whole board, not a single scrolled-to row — `ContractsPanel`/`PanelType.
Contracts` has no per-row `SubId` routing (AC2.5's report flagged this same
gap for its own row-click target), so a single-row deep link isn't
available yet; flagged below for Eyeball 2.

## Parity evidence (`efreight`, bash `printf | dotnet run --project
src/Inspector`, per CLAUDE.md)

Captured `epoch 42 / estep 40 / efreight` before and after the `Repl.cs`
re-point (`git stash push -- src/Inspector/Repl.cs` isolates just that
file's change so every other AC2.6 Core addition is present in BOTH
captures — a true before/after of only the re-point).

- Full transcripts (`efreight_before.txt`/`efreight_after.txt`, session
  scratchpad) diffed with the one known-variable line (`stepped in NNNNN
  ms`) stripped from both sides: **identical**.
- The isolated `efreight` table block (`efreight_before_block.txt`/
  `efreight_after_block.txt`, both lines 9714-9730 of their transcripts):
  **byte-identical**, `diff` reports no differences. 16 shipments in
  transit at that seed/step; purposes present: `courier` (most rows) and
  **one live `war convoy`** — shipment `#10984`, route `#9->#296 off-lane`,
  cargo `0.4 Fuel, 0.5 Ship Components`, owner `Kaqua`, eta `y2002` — the
  Eyeball 2 pointer below.

## Gate evidence (editor closed throughout — confirmed via `Get-Process
-Name Unity` returning nothing before each batch run)

1. **`dotnet test StarSystemGeneration.sln`** — `Passed! Failed: 0, Passed:
   1251, Skipped: 0, Total: 1251` (base 1243 + 8 new: 5
   `FreightPurposeQueryTests`, 2 `ShipmentPanelTests`, 1 `WorksLensTests`).
   Includes the determinism/golden suite — no golden churn, no failures.
2. **Unity batch compile** — `Unity.exe -batchmode -quit -projectPath
   unity -logFile compile-ac2.6.log`. Log: 331,483 bytes / 4,348 lines
   (real run). `grep -c "error CS"` → 0. Tail: `Exiting batchmode
   successfully now! ... return code 0`. `git status` on
   `unity/Assets/Scenes/Atlas.unity` clean before and after (no
   regeneration to discard).
3. **EditMode suite** — `Unity.exe -batchmode -projectPath unity
   -runTests -testPlatform EditMode -testResults test-results-ac2.6.xml
   -logFile test-ac2.6.log`. `test-results-ac2.6.xml`: `total="16"
   passed="16" failed="0"` (base 16, unchanged — no new EditMode test;
   the purpose derivation is Core-TDD'd and the map/panel wiring is
   covered by the batch-compile gate + the byte-identical REPL-parity
   diff, matching the AC2.4/AC2.5 precedent).
4. AtlasSmoke — not run (not required for this task); scene stayed clean
   per point 2.

Commit: `feat(ac): freight purposes on the map + ShipmentPanel (AC2.6)`,
explicit paths (`src/Core/Atlas/FreightPurposeQuery.cs` +
`.cs.meta`, `src/Core/Atlas/{ContractsPanel,LegendQuery,ShipmentPanel,
WorksLens}.cs`, `src/Inspector/Repl.cs`,
`tests/Core.Tests/Atlas/{FreightPurposeQueryTests(new),ShipmentPanelTests,
WorksLensTests}.cs`, `unity/Assets/Atlas/{PanelViews,WorksLayer}.cs`,
this report). `unity/ProjectSettings` churn, the pre-existing stray
`src/Core/Epoch/*.cs.meta` files, and other untracked `.superpowers/sdd/*`
files from earlier AC2.x tasks (out of this task's scope) left uncommitted
as instructed.

## Carries for Eyeball 2

- **Where to find a live war convoy in seed 42**: `epoch 42`, `estep 40`,
  `efreight` (REPL) — shipment `#10984`, `#9->#296 off-lane`, owner
  `Kaqua`, eta `y2002`. On the atlas map this renders as the biggest
  (13f), reddest (`WarLens.StationBurn`) freight crate — click it to open
  the ShipmentPanel and confirm the `WAR CONVOY` red tag + rider contract
  section (route/fee/poster) + "OPEN CONTRACTS BOARD" link. I did not have
  an interactive Unity session in this sandbox to eyeball the rendered
  panel/map directly (batchmode compile + EditMode tests only, same
  constraint AC2.5 flagged) — this is the first live look.
- **Rider link opens the whole board, not the one row**: `PanelType.
  Contracts` has no `SubId`/selection routing (same gap AC2.5's report
  flagged for its own destination-port link). The clicked shipment's rider
  contract id is IN the button label ("OPEN CONTRACTS BOARD (#id)") so the
  user can visually confirm the right row once the board opens, but there's
  no auto-scroll/highlight. Worth deciding at Eyeball 2 whether that's
  worth a `SubId` wiring pass (would touch `ContractsPanel`, `PanelViews.
  Contracts`, and `PanelRequest` together — bigger than this task's scope).
- **War-convoy red vs. STALLED red**: both are variants of the same
  "war/bad" red family already in the palette (`WarLens.StationBurn` ≈
  `WorksLens.FreightStalled`, ~15 RGB units apart). A STALLED war convoy
  is distinguishable only by size (15f, the largest) since its color
  collapses to the shared STALLED red like every other purpose. This was
  a deliberate choice (STALLED reads as one universal alarm, independent
  of whose cargo it is) but it does mean "is this red thing STALLED or is
  it a moving war convoy" needs the size cue, not just color, at a glance.
  Flag if Eyeball 2 wants a war convoy to keep its own hue even while
  stalled (e.g. a red/orange split) instead.
- **Purpose tag styling**: only WarConvoy gets the `ssg-tag--bad` accent;
  Courier/SpreadRun/StateHaul render as plain lowercase text (no tag chip)
  in the ShipmentPanel — consistent with "war convoy should be the most
  distinguishable" but means the other three purposes are text-only, easy
  to skim past. Worth a look alongside the WAR contract-board tag (AC2.5)
  for a consistent "how loud should priority read" answer across both
  panels.
