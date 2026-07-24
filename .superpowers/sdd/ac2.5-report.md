# AC2.5 report — contracts panel (`econtracts` parity)

## Shape chosen, and why

**New Core query, new `PanelType`, dock panel (TopBar drawer button) —
follows the K3 THREADS/STATS precedent exactly, as scoped.**

- New file `src/Core/Atlas/ContractsPanel.cs`: `ContractRow` record +
  `ContractsPanel.Rows(model, eye, posterFilter = -1)` static class.
  Registry order (P6, `state.Couriers` creation order — the same order
  `econtracts` always walked). Reuses the existing `CargoLine` record
  from `ShipmentPanel.cs` rather than declaring a parallel cargo-line
  shape (a courier's escrowed basket is the identical Qty/Grade-per-good
  pair a shipment's cargo already is).
- `ContractRow` fields: `Id, Priority, Status, OriginPortId,
  OriginPortOwnerName, DestPortId, DestPortOwnerName, Cargo, FeeEscrow,
  PosterActorId, PosterName, FulfillerActorId, FulfillerName`. Ports
  have no `Name` field of their own, so "route by port names" follows
  the established Core.Atlas idiom (`HexQuery`/`SystemQuery`'s
  `PortOwnerName`): route reads by port id *and* the port owner's name.
  `FulfillerActorId`/`FulfillerName` are `-1`/`null` while `Open`
  (`Accept` always sets a real fulfiller before flipping to `InTransit`,
  so the two states partition cleanly — same split the REPL's own
  ternary already made).
- `SimState.Couriers` holds ONLY `Open`/`InTransit` contracts — `Resolve`
  and `ExpireOpen` both `Remove` immediately on `Delivered`/`Lost`/
  `Expired`. No status filter was needed in the query; documented inline
  so a future reader doesn't add a defensive filter that changes nothing.
- `Repl.cs RenderContracts` is now a pure formatter over
  `ContractsPanel.Rows` (the `DomainView`/`ebook` precedent): it walks
  the returned rows, takes the first 3 cargo lines (`Cargo` is already
  full, ascending-`GoodId`, non-zero-only — the same subset the REPL's
  own `g < Length && count < 3` loop always produced), and prints the
  identical text. The REPL's local `OwnerName` helper stays (still used
  by `RenderBook`/`ebook`).
- Unity: new `PanelType.Contracts`; `InspectorDock`'s enum gains one
  entry, `TopBar` gets a `CONTRACTS` drawer button next to `THREADS`
  (same `DrawerButton(bar, label, type)` call, same `dock.Show(...)`
  wiring — no new routing mechanism). `PanelViews.Contracts(ctx, body)`
  lists every row: a clickable `Row` (opens the **destination** port's
  Market panel — the `ShipmentPanel` row-click idiom, since that's where
  the delivery lands), a `WAR` tag in the `ssg-tag--bad` accent (the
  same red STALLED already wears) when `Priority == War`, then one line
  with id, route (owner names), cargo, fee, status, and poster/fulfiller.

### Drawer-vs-dock seam (for Eyeball 2)

Built as a **dock panel reached via a TopBar drawer button** (like
THREADS/STATS/GOODS/KNOBS): always-available, not tied to a map
selection. This is the cheapest shape to re-house — if Eyeball 2 wants
it as a slide-out drawer distinct from the pinnable inspector-dock
panels (e.g. a persistent side rail, auto-refreshing without taking a
dock slot), the query and row-building code don't move; only
`TopBar.DrawerButton` + a differently-chromed container would change.
No `PanelRequest.SubId`/selection wiring was added, so there's nothing
selection-specific to unwind either way.

## Parity evidence

Captured `epoch 42 / estep 40 / econtracts` before and after the
`Repl.cs` re-point via `printf '...' | dotnet run --project
src/Inspector` (bash, per CLAUDE.md) — a 130-row board (mix of OPEN,
in-transit, Normal and one War-priority contract, id `#6313`). `diff`
on the full transcripts: only difference is an unrelated `stepped in
NNNNN ms` timing line; the isolated `econtracts` block (header + all
130 rows) is **byte-identical**. Saved at
`econtracts_before.txt`/`econtracts_after.txt` (full transcripts) and
`econtracts_before_block.txt`/`econtracts_after_block.txt` (isolated
block) in the session scratchpad.

## Gate evidence (editor closed throughout — confirmed via
`Get-Process -Name Unity` returning nothing before each batch run)

1. **`dotnet test StarSystemGeneration.sln`** — `Passed! Failed: 0,
   Passed: 1243, Skipped: 0, Total: 1243` (base 1238 + 5 new
   `ContractsPanelTests` cases). Includes the determinism/golden suite —
   no golden churn (no failures at all).
2. **Unity batch compile** — `Unity.exe -batchmode -quit -projectPath
   unity -logFile compile-ac2.5.log`. Log: 173,739 bytes / 2,224 lines
   (real run, not a stale cache hit). `grep "error CS"` → 0 matches.
   Tail: `Exiting batchmode successfully now! ... return code 0`.
3. **EditMode suite** — `Unity.exe -batchmode -projectPath unity
   -runTests -testPlatform EditMode -testResults
   test-results-ac2.5.xml -logFile test-ac2.5.log`.
   `unity/test-results-ac2.5.xml`: `total="16" passed="16" failed="0"`
   (base 16, unchanged — no new EditMode test added; the Core TDD tests
   cover the new derivation and REPL parity was verified by transcript
   diff, matching the AC2.4 precedent).
4. AtlasSmoke — **not run** (not required for this task); `git status`
   confirms no `unity/Assets/Scenes/Atlas.unity` churn either way.

Commit: `feat(ac): contracts panel — econtracts parity (AC2.5)`,
explicit paths (`src/Core/Atlas/ContractsPanel.cs` +
`ContractsPanel.cs.meta`, `src/Inspector/Repl.cs`,
`tests/Core.Tests/Atlas/ContractsPanelTests.cs`,
`unity/Assets/Atlas/InspectorDock.cs`, `unity/Assets/Atlas/PanelViews.cs`,
`unity/Assets/Atlas/TopBar.cs`). `unity/ProjectSettings` churn and the
pre-existing stray `src/Core/Epoch/*.cs.meta` files, plus other
untracked `.superpowers/sdd/*` files from earlier AC2.x tasks not in
this task's scope, were left uncommitted as instructed.

## Carries for the Eyeball

- The board is unconditionally visible, no pagination/foldout — at seed
  42/y~1750-ish (40 steps) it ran ~130 rows in the REPL capture; worth a
  look in the atlas scroll view at that density (same open question
  AC2.4 flagged for the order-book section — `DockKit` still has no
  `Foldout` helper). Not a correctness issue, a legibility one.
  I did not have an interactive Unity session in this sandbox to
  eyeball the rendered panel directly (batchmode compile + EditMode
  tests only) — a visual pass at Eyeball 2 is the first live look.
  Confirms only that a) the code path an editor drawer button reaches
  compiles clean, and b) the underlying data is right (TDD'd in Core,
  REPL-diff-verified).
- WAR call-out reuses the `ssg-tag--bad` accent (same as STALLED
  shipments) rather than a distinct color — consistent with the
  existing accent vocabulary, but worth confirming at Eyeball 2 that
  reusing "bad" red for a WAR *priority* (not a failure state) reads
  right next to a STALLED shipment tag that means something different
  ("broken" vs "urgent").
- A contract row opens the **destination** port's Market panel on
  click; I didn't wire the origin port or poster/fulfiller actor as
  additional link targets (no `Polity`-panel jump the way a port
  *selection* triggers one) — kept to one unambiguous link target per
  row, matching `ShipmentPanel`'s single `DestPortId` link. Flag if
  Eyeball 2 wants richer link-through.
