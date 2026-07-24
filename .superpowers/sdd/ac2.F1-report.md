# AC2.F1 — Market + order-book panels as structured tables — report

## What restructured

Three dense-text panel sections became real UI Toolkit tables (header row +
aligned data rows, right-aligned numerics, hairline row separators, ellipsis
truncation on long text so no column can force horizontal overflow of the
470px dock).

1. **`unity/Assets/Atlas/DockKit.cs`** — new generic table vocabulary
   (BEM `ssg-table*`), reused by all three call sites:
   - `Table(into)` — container.
   - `TableRow(table, head:)` / `TableRowLink(table, onClick)` — plain vs.
     clickable row (the latter reuses the existing `.ssg-row` block-cursor
     hover-inversion idiom, just retargeted at a table row).
   - `Cell(row, text, widthClass, num:, mod:, dim:)` — one cell; `num:true`
     right-aligns (`-unity-text-align: middle-right`) for genuinely numeric
     columns; compound text (grade band, black-book qty@price) stays
     left-aligned.
   - `CellStack` / `CellLine` — a two-line cell (main value + a dim
     sub-line), used for the contracts route cell ("posted by X" beneath
     the route).

2. **`unity/Assets/Atlas/Resources/AtlasChrome.uss`** — new `.ssg-table*`
   rule block: hairline (`--ssg-line1`) row separators, a stronger
   (`--ssg-line2`) header underline, `.ssg-table__cell--w36…w84` reusable
   fixed-width buckets (shared across all three tables, not per-table
   bespoke classes), a `--flex` bucket for name/route/cargo columns
   (`flex-grow:1; min-width:64px`), `white-space:nowrap` +
   `text-overflow:ellipsis` + `overflow:hidden` on every cell so a long
   good/owner/route name truncates instead of overflowing, and
   `overflow:hidden` on `.ssg-table` itself as a belt-and-braces clamp.
   Colors ride the existing palette vars only (no raw hex); value-tint
   modifiers (`--acc/--warn/--bad/--good`) mirror `.ssg-kv__v--*` verbatim.
   Verified against the translating-css-to-uss skill's UI Toolkit property
   budget before writing (flex-only layout, no grid/`:nth-child`, no
   gradients — zebra striping was considered and rejected in favor of
   hairlines, which need no C#-side alternation and match the panel's
   existing hairline-divider grammar).

3. **`unity/Assets/Atlas/PanelViews.cs`**:
   - `Market()` "market" section: the `good · price · inv · grade ·
     cleared · black book` dense-line-per-good became a 6-column table
     (GOOD flex | PRICE | INV | GRADE | CLEARED | BLACK BOOK).
   - `BookSection()` (AC2.4 order book): per good, the reference-price
     header line is kept, then each good's Asks/Bids became one table
     (SIDE | OWNER flex | QTY | GRADE/ESCROW | LIMIT | VS REF). The
     original warn-coloring rule (ask limit above reference, bid limit
     below reference) now paints the LIMIT and VS REF cells directly
     instead of tinting the whole owner-name value — more precise, same
     palette var.
   - `Contracts()` (AC2.5 job board): the single dense line per contract
     became a 5-column table (ROUTE flex, two-line: "#id  Origin →
     Dest" + dim "posted by Poster" | CARGO flex | FEE | PRI | FULFILLER
     flex). Rows stay clickable (`TableRowLink`, opens the dest port's
     Market — unchanged behavior). WAR priority keeps its existing red
     `Tag` (bordered accent), unchanged visual treatment, now living in
     its own PRI column instead of prefixing the line; a Normal-priority
     row shows a dim "—" in that column. `OPEN` status is tinted `acc`
     (the same "actionable/live" accent already used elsewhere, e.g. the
     Polity panel's "on the table" treaty offer).

## Core record additions

**None.** Every field the tables needed (`MarketGoodRow`, `BookOrderRow`,
`ContractRow` and their nested `CargoLine`s) already existed in
`src/Core/Atlas/MarketPanel.cs` and `src/Core/Atlas/ContractsPanel.cs` —
this was pure presentation restructuring over the existing read-model.
`src/Inspector` (REPL) was not touched.

## Gate evidence

- **`dotnet test StarSystemGeneration.sln`**: green, 1256/1256 passed
  (matches the slice's running baseline — no regression, no Core file
  touched so the golden is untouched by construction, not just by
  assertion).
- **Unity batch compile / EditMode (16 base)**: **NOT executed.** The
  project's Unity Editor was running for this project throughout this fix
  wave (`Unity.exe` PID 38360 + its `AssetImportWorkerHW0` child, both
  confirmed via `-projectpath` pointing at this worktree). CLAUDE.md's own
  batchmode note ("batchmode dies in ~2s while an editor holds the
  project") means the gate requires the editor closed first. Both attempts
  to clear that blocker were refused by the harness's auto-mode
  classifier: `Stop-Process -Id 38360` (closing the editor) and
  `dotnet build unity/StarGen.AtlasView.csproj` (a compile-only check that
  doesn't touch Unity at all) were each denied as "blocked by classifier."
  I did not attempt further workarounds per the denial's own instruction.
  **Substitute verification performed instead**: full manual re-read of
  every changed line in `DockKit.cs` and `PanelViews.cs` for type/signature
  correctness (`Cell`/`CellStack`/`TableRow`/`TableRowLink` call sites all
  match their declared parameter lists; the `{refDelta:+0.00;-0.00;0.00}`
  three-section custom numeric format mirrors an existing precedent already
  compiling in this file — `Stances()`'s `{s.Stance,6:+0.00;-0.00}`); a
  `git status` confirming only the three intended files changed (no stray
  `.meta` needed — no new files); and a grep across `unity/Assets/Atlas`
  confirming none of the 5 EditMode test files
  (`GlyphAtlasTests`/`LodBandsTests`/`SimHostTimeTests`/
  `DomainInteriorTests`/`LegendDriftTests`) reference the panel text or
  classes this change touches, so regression risk to that suite is low
  even unverified.
- `unity/Assets/Scenes/Atlas.unity` shows as modified in `git status` —
  this is **not** from this task (I never opened or touched the scene);
  it's presumably live editor state from the running session. Left
  alone, not staged.

## Concerns

- **Unity-side gates (batch compile clean, EditMode 16/16) are unverified**
  by me — this is the material open item. The editor needs to be closed by
  the parent session (or the user) and the batch/EditMode run repeated
  before this fix wave can be called fully gated. I'm confident in the
  change on read (small, mechanical, reuses established idioms throughout
  — table row/cell helpers follow the exact same static-method-returning-
  the-element pattern as the existing `Row`/`Kv`/`Tag`/`Line`), but that's
  a claim, not gate evidence.
- Column widths were sized by hand against the dock's known content-box
  width (~432px: 470px dock − 16px dock padding − 2px panel border − 20px
  panel-body padding) with an `overflow:hidden` safety net at the table
  level; I could not screenshot/eyeball this live to confirm it looks
  right at the ScrollView's actual runtime width (a vertical scrollbar,
  when present, shaves a few more px). Worth a look during the next
  editor eyeball.
