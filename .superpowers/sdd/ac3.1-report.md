# Task AC3.1 report — currency-zone tint mode on the domain render

## What landed

- `src/Core/Atlas/CurrencyLens.cs` (new) — the Core-side derivation, parallel
  in shape to `WarLens`/`TensionLens`/`TechLens`:
  - `SlotCurrency(model, eye, slots)` — per-slot currency id, the same
    owner→currency hop `SimState.LocalCurrencyOf` makes via a port
    (`model.State.PolityOf(slots[i]).CurrencyId`).
  - `CurrencyColor(model, currencyId)` — the tint rule (see below). Returns
    `Rgba?`; `null` means "absent/untinted" for the two cases the brief
    calls out (id < 0, or `Currency.Retired`).
- **Color-assignment rule chosen**: reuse `AtlasPalette.OwnerColor`'s
  existing golden-ratio-hue idiom, keyed on the **currency id** instead of
  the actor id (`AtlasPalette.OwnerColor(currencyId)`). This was the
  simplest option that satisfies "union ports share a color" for free —
  since the key IS the currency id, any two polities that come to share a
  currency id (CU-3 consolidation, federation) automatically render the
  same hue with zero extra bookkeeping (no palette-assignment table to
  keep in sync as the id space grows/retires). No new palette slots, no
  drift risk between what CU-3 assigns and what the map shows.
- `unity/Assets/Atlas/DomainFieldLayer.cs` — `DomainAccent.Currency` member;
  `UploadSlotColors` grows a `Currency` arm that reads `CurrencyLens`,
  falling back to `AtlasPalette.Floor` (the existing "a lens has nothing to
  say here" base) when `CurrencyColor` returns null — this is the "zone
  visibly disappears" behavior for a retired or absent currency.
- `unity/Assets/Atlas/LensRail.cs` — fifth radio-exclusive chip alongside
  war/tension/tech (`_currency` field; all four accent setters now clear
  each other); `ActiveLegendKey`, the `Accent` property, and the
  `domainsVisible` visibility gate all extended in kind. Chip lives in the
  POLITICAL group (it's explicitly "a mode of the existing polity/domain
  rendering" per the brief, same family as war/tension/tech regardless of
  which UI group header they sit under).
- `src/Core/Atlas/LegendQuery.cs` — new `"currency"` case: one fill entry
  showing the golden-ratio-hue convention, one showing the Floor "retired/
  absent" fallback.
- `unity/Assets/Atlas/Tests/LegendDriftTests.cs` — **drift-test surface**:
  `RailKeys` gained `"currency"` (the array both existing drift tests
  iterate — `EveryLegendGlyphKeyNamesAnAtlasCell` and
  `EveryRailLensKeyYieldsEntries`). No new `[Test]` methods — the existing
  16 now additionally cover the currency key, so the EditMode total stays
  16.
- `unity/Assets/Editor/AtlasSmoke.cs` — the smoke driver already shoots one
  frame per accent (war/tension/tech), so I added a `currency` shot in the
  same spot (`domains.SetAccent(DomainAccent.Currency)` → capture →
  reset to `Owner`), before the existing pois/plague/news shots so wiring
  parity holds without disturbing camera framing for the later shots.

## REPL parity (skipped, as instructed)

Checked `EpochMapView`/`Repl.cs` (`emap` mode): `war`/`tension`/`tech` exist
as string-keyed layers there, but there is no `currency` layer today — the
brief says explicitly: if none exists, note it and skip; the REPL/panel
twin is AC3.2/AC3.3's territory. Skipped; not touched.

## Gate evidence (editor closed throughout — confirmed via `tasklist` for
`Unity.exe` returning nothing before every batch run)

1. `dotnet test StarSystemGeneration.sln` — `Passed! Failed: 0, Passed:
   1281, Skipped: 0, Total: 1281` (base 1276 + 5 new `CurrencyLensTests`).
   Ran the `GoldenTests` filter separately too — still green, byte-identical
   (this task never touches `Epoch`/sim code, only `Atlas` reads).
2. Unity batch compile — `Unity.exe -batchmode -quit -projectPath unity
   -logFile compile-ac3.1.log`. Log: 44,620 bytes (real run). `grep -c
   "error CS"` → 0. Tail: `Exiting batchmode successfully now! ... return
   code 0`.
3. EditMode suite — `Unity.exe -batchmode -projectPath unity -runTests
   -testPlatform EditMode -testResults test-results-ac3.1.xml -logFile
   test-ac3.1.log`. Results: `total="16" passed="16" failed="0"` (base 16,
   unchanged count — the drift-test change is a data addition inside an
   existing test, not a new test method).
4. AtlasSmoke — driver covers accents, so ran it:
   `-executeMethod StarGen.AtlasView.EditorTools.AtlasSmoke.RunFromCli
   -logFile smoke-ac3.1.log`. 0 errors/exceptions in the log; every shot
   including `atlas-smoke-currency.png` (338 KB, a real render) wrote
   successfully. Visually spot-checked: the seed-42 golden's year-1000
   state shows multiple distinct-hued zones (currencies had already been
   founded/consolidated by then), not a flat all-Floor fallback — the mode
   is doing something at this artifact's timestamp.
5. `unity/Assets/Scenes/Atlas.unity` — went dirty after the batch runs (as
   the brief warned it would), `git checkout`'d back to clean afterward,
   never staged.

## Commit

`feat(ac): currency-zone tint mode on the domain render (AC3.1)` — explicit
paths: `src/Core/Atlas/{CurrencyLens.cs,CurrencyLens.cs.meta,
LegendQuery.cs}`, `tests/Core.Tests/Atlas/CurrencyLensTests.cs`,
`unity/Assets/Atlas/{DomainFieldLayer.cs,LensRail.cs,
Tests/LegendDriftTests.cs}`, `unity/Assets/Editor/AtlasSmoke.cs`, this
report. `unity/ProjectSettings` churn, the pre-existing stray
`src/Core/Epoch/*.cs.meta` files, and other untracked `.superpowers/sdd/*`
files from earlier AC-slice tasks left uncommitted, as instructed.

## Carries for AC3.2–3.4 and Eyeball 3

- The `CurrencyColor`/`SlotCurrency` primitives in `CurrencyLens.cs` are
  ready for **AC3.2 (PolityPanel monetary block)** and **AC3.3
  (MarketPanel currency label)** to read directly, or for a REPL `emap
  currency` layer if that turns out to be wanted after all — nothing here
  is Unity-only.
- The chosen color rule means **AC3.4 (or wherever the currency legend
  gets a live swatch list)** could enumerate actual live currency ids from
  `state.Currencies` to build a real per-union legend, if a static
  two-entry legend (current state) turns out to be too thin at Eyeball 3.
- Eyeball 3's literal ask ("currency mode shows zones; a union shares one
  tint; a polity's own line reads its currency") is now checkable for the
  zone/union/retired half from the rail; the PolityPanel "own line" half is
  AC3.2's job.
- Chip color chosen for the rail swatch: `0xB08AE0` (soft violet) — distinct
  from every existing chip hue; no semantic tie needed since the actual
  fill tint is per-currency-id, not the chip swatch.

## Concerns

None. Zero sim behavior touched; golden byte-identical; all four gates
green.
