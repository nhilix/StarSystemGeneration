# Task AC3.2 report — PolityCard monetary block (REPL polity currency lines = parity)

## What landed

- `src/Core/Atlas/PolityPanel.cs`:
  - New `MonetaryLine` record — `CurrencyId, CurrencyName, NumeraireRate,
    Supply, Retired, BankReserve, CumulativeSpreadIntake,
    CumulativeReserveFunded, CumulativeFiatIssued, ClaimOnState,
    BackingRatio, CumulativeLentToState, CumulativeRetired`. This is
    `InteriorView.RenderPolity`'s currency/bank/claims block (lines 47-77)
    lifted field-for-field into the panel query.
  - `PolityCard` gained a trailing `MonetaryLine? Monetary` member.
  - New private `Monetary(SimState, int currencyId)` helper: returns `null`
    when `currencyId < 0` (pre-genesis sentinel — the brief's "no currency ⇒
    no block"); otherwise reads `state.CurrencyOf`/`state.BankOf` and
    computes `BackingRatio` with the **exact** InteriorView guard
    (`bank.ClaimOnState > 0 ? bank.Reserve / bank.ClaimOnState : -1`).
- `unity/Assets/Atlas/PanelViews.cs` — `Polity()` arm grows a conditional
  block (only when `card.Monetary != null`, right after the existing
  "treasury" section): a `Sect(body, "currency")` line with rate/supply plus
  a `RETIRED` tag when retired, then two `DockKit` tables (AC2.F1 table
  kit) — one for the bank (reserve, spread/res-fund/backstop cumulatives),
  one for claims (book, backing ratio, lent/retired cumulatives). The
  backing cell shows `-` instead of a number when `BackingRatio < 0` (the
  same "empty book, no ratio" read InteriorView gives via `"backing — · "`).
  A dim caption line under each table flags which columns are cumulative
  levels vs point-in-time, matching the density convention the other
  panel tables use.

## Drift derivability finding (per the brief's explicit ask)

Searched the whole `Currency`/`Bank`/`FxOps` surface for any stored prior
rate, a rates time series, or FX event/pulse residue: none exists.
`Currency.NumeraireRate` is a single mutable field recomputed **from
scratch** every epoch by `FxOps` (`NumeraireRate = 1.0 / (1.0 +
FxSensitivity * density)` — line 85), with no memory of its previous value
kept anywhere in `SimState`. There is no `WorldEventType` for an FX-rate
move either. Conclusion: recent drift is **not derivable read-only at a
keyframe** — per the brief, I did not add sim-side rate history (zero sim
behavior held). The block shows the rate alone; drift is a gap for a future
task that's willing to add a one-field `PriorNumeraireRate` (or a rates
series) to `Currency` — a sim-side change, out of this task's zero-sim-
behavior scope.

## Parity approach

**Did not re-point the REPL.** `InteriorView.RenderPolity` is left
byte-identical/untouched. Instead, parity is enforced by construction: the
Core query (`PolityPanel.Monetary`) reads the exact same source fields
(`state.CurrencyOf(id)`, `state.BankOf(id)`) the REPL derivation reads, and
`TheMonetaryBlockMirrorsCurrencyBankAndClaimFields` asserts every
`MonetaryLine` field equals the corresponding `Currency`/`Bank` field
directly (not a recomputed shadow value) — including asserting
`BackingRatio` against the literal guard expression copied from
`InteriorView.cs:70`. Two more tests cover the two edge cases the brief
calls out: `NoCurrencyMeansNoMonetaryBlock` (pre-genesis, `Monetary` is
null) and `BackingRatioGuardsAnEmptyClaimBook` (`ClaimOnState == 0` ⇒
`-1` sentinel, matching the REPL's `"backing — · "` fallback text). A
fourth test, `ARetiredCurrencyFlagsOnTheBlock`, checks the `Retired` flag
carries through — not explicitly required by the brief but cheap and
guards the `[retired]` REPL suffix's Core-side twin.

## Currency-zone color chip (AC3.1 carry)

Not added. The brief called it "optional nicety, not required," and no
existing panel arm in `PanelViews.cs`/`DockKit.cs` renders an Rgba-to-Unity-
Color swatch today (checked — no precedent). Adding one would mean
inventing a new DockKit primitive with no reuse elsewhere yet; skipped to
keep this task's diff to the measures the brief actually asks for.
`CurrencyLens.CurrencyColor` is still there, ready, if a later task (or
Eyeball 3 feedback) wants it.

## Gate evidence (editor closed throughout — confirmed via `tasklist`
returning no `Unity.exe` before every batch run)

1. `dotnet test StarSystemGeneration.sln` — `Passed! Failed: 0, Passed:
   1285, Skipped: 0, Total: 1285` (base 1281 + 4 new `PolityPanelTests`
   methods). Filtered `--filter FullyQualifiedName~Golden` separately:
   still 1/1 passed, byte-identical (this task touches only `Atlas` reads,
   never `Epoch`/sim code).
2. Unity batch compile — `Unity.exe -batchmode -quit -projectPath unity
   -logFile compile-ac3.2.log`. Log: 42,420 bytes (real run), `grep -c
   "error CS"` → 0, tail: `Exiting batchmode successfully now! ... return
   code 0`.
3. EditMode suite — `Unity.exe -batchmode -projectPath unity -runTests
   -testPlatform EditMode -testResults test-results-ac3.2.xml -logFile
   test-ac3.2.log`. Results (`unity/test-results-ac3.2.xml`):
   `total="16" passed="16" failed="0"` (unchanged count — this task didn't
   touch any Unity `[Test]` methods, only the plain `PanelViews.cs`
   renderer).
4. `unity/Assets/Scenes/Atlas.unity` — checked `git status --short` after
   both batch runs: clean, no dirtying this time (no `git checkout --`
   needed).

## Commit

`feat(ac): PolityCard monetary block — currency/bank/claims panel parity
(AC3.2)` — explicit paths: `src/Core/Atlas/PolityPanel.cs,
tests/Core.Tests/Atlas/PolityPanelTests.cs,
unity/Assets/Atlas/PanelViews.cs`, this report. `unity/ProjectSettings`
churn, the pre-existing stray `src/Core/Epoch/*.cs.meta` files, and other
untracked `.superpowers/sdd/*` files from earlier AC-slice tasks left
uncommitted, as instructed.

## Carries for AC3.3/AC3.4 and Eyeball 3

- `MonetaryLine`/`PolityPanel.Monetary` are ready for AC3.3 (MarketPanel
  currency label) to read the same `state.CurrencyOf`/`BankOf` primitives
  if it needs anything beyond a bare name/id.
- Drift-derivability gap noted above: if Eyeball 3 wants "recent drift" to
  actually show a number, that's a sim-side task (a stored prior rate),
  not a read-only panel task — flag it up front rather than have it
  surface as a surprise at the eyeball.
- The currency-zone color chip stays available but unused in the panel;
  worth a look only if the eyeball flags the currency block as hard to
  scan against the map's zone tints.

## Concerns

None. Zero sim behavior touched; golden byte-identical; REPL untouched
(byte-identical by construction, not by re-pointing); all four gates green.
