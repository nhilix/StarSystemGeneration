# Slice CU-1 — task ledger / implementation plan

Design: `docs/superpowers/specs/2026-07-14-cu-currency-fx-design.md` (v2 — read
this in full before starting any task; it is the spec, this ledger is the task
breakdown). Branch: `slice-cu-currency`, worktree
`.worktrees/slice-cu-currency`. Baseline: 900/900 `dotnet test` green before
Task 1.

## Global constraints (apply to every task)

- **Determinism**: no new `RollChannel` anywhere in this slice — every new
  quantity (FX rates, conversions, the corp draw-down rule) is a pure
  deterministic formula or a fixed-order walk. Where iteration order matters,
  use ascending `CurrencyId` (new) or the existing ascending actor-id
  convention already used throughout `Phases.cs`/`FederationOps.cs`.
- **`ConvertCurrency(SimState state, double amount, int fromCurrencyId, int
  toCurrencyId) -> double`** is the one shared conversion primitive (design
  doc, "Conversion mechanics"). Every task that moves money across a currency
  boundary calls this — do not write bespoke per-site FX math.
- **`ICreditLedger.Deposit(SimState, double amount, int fromCurrencyId)`** and
  **`double Withdraw(SimState, double amount, int toCurrencyId)`** are the two
  new interface members (design doc, "Data model"). `PolityRecord` auto-
  converts to its own single currency on both; `Corporation` deposits into
  the matching `Holdings` bucket and withdraws via the draw-down rule
  (ascending-`CurrencyId` bucket walk, converting shortfall). Every task that
  credits/debits an actor's balance uses these, not raw `.Credits +=`/`-=`
  (raw field access on `PolityRecord.Credits` is still fine for same-currency
  internal bookkeeping; it is cross-currency movement that must go through
  `Deposit`/`Withdraw`).
- **`Corporation.Credits` becomes a computed read-only property** (numeraire-
  converted sum of `Holdings`). Do not add a setter. Every existing site that
  only *reads* `.Credits` for comparison/ranking must keep compiling
  unmodified against the new property.
- TDD: write the failing test first, then the implementation, per
  `superpowers:test-driven-development`. Run `dotnet test` before committing;
  the hex-tier (Phase-1 generation) suite must never regress.
- Commit after each task with a message describing what landed.
- If a task's scope turns out to require touching a file or mechanism not
  named in its brief, and doing so would silently change behavior the design
  doc didn't describe, stop and report `NEEDS_CONTEXT` rather than improvising.

## Task 1: Currency data model + ledger interface (Opus)

Add the `Currency` record exactly as specified in the design doc's Data model
section: `Id, Name, FoundingPolityId, Supply, CumulativeFiatIssued,
CumulativeSteadyIssuance, CumulativeConvertedIn, CumulativeConvertedOut,
NumeraireRate, Retired`. Add `PolityRecord.CurrencyId`. Add
`Corporation.Holdings: Dictionary<int,double>` and change `Corporation.Credits`
from a settable field to a computed property: `Holdings.Sum(kv => kv.Value *
state's Currency lookup for kv.Key .NumeraireRate)` — thread whatever access
to `SimState`/the currency table the existing `Corporation` class already has
a path to (check how other computed properties on this project's records
access `SimState`, e.g. via a method taking `SimState state` as a parameter if
`Corporation` doesn't hold a back-reference — do not add a `SimState` field to
`Corporation` if the codebase convention is to pass state explicitly).

Add `Deposit`/`Withdraw` to `ICreditLedger` (or introduce a new interface if
`ICreditLedger` cannot cleanly gain currency-aware members without breaking
existing implementers — check all current implementers of `ICreditLedger`
first) exactly as specified in the design doc, including the draw-down rule
for `Corporation.Withdraw` (ascending-`CurrencyId` bucket walk on shortfall,
draining fully-consumed buckets from the dictionary).

Add two new config knobs to `EpochSimConfig`/`KnobRegistry` (follow the exact
pattern of existing `Economy.*` knobs): `Economy.FxSensitivity` and
`Economy.FxReceiptsFloor`. No behavior wired to them yet — Task 2 uses them.

No `Currency` instances need to actually exist yet after this task (no
genesis wiring) — this task is purely the data model and the ledger
interface. Unit tests: `Corporation.Credits` computes correctly across a
`Holdings` dict with 2+ currencies at different `NumeraireRate`s;
`Corporation.Withdraw` drains the matching bucket first, then falls back to
others in ascending `CurrencyId` order, converting correctly; `Withdraw`
caps at total available (whatever behavior the codebase's existing
insufficient-funds handling does — inspect `Phases.Borrow`/`OrderOps` for the
existing convention before deciding; report `NEEDS_CONTEXT` if none of the
call sites this slice touches establish one).

## Task 2: FX rate computation (Opus)

A new deterministic pass, run once per epoch **before** the per-polity
allocation loop, computing every `Currency.NumeraireRate` from the **prior**
epoch's ending `Supply`/`Receipts` (design doc, "FX rate" — money-per-output
density, `Receipts` floored by `Economy.FxReceiptsFloor`, reactivity scaled by
`Economy.FxSensitivity`). A newly created `Currency` starts at
`NumeraireRate = 1.0` and is included in the very next epoch's recompute like
any other. Wire this pass into the epoch step sequence at the point the
design doc specifies (before `Borrow`/`ServiceLoans`/`PayTribute`/market
phases — find the exact phase-ordering location in `Phases.cs`'s epoch driver
and confirm this is before all of those). Unit/integration tests: two
currencies with different `Supply`/`Receipts` produce different rates; a
currency with `Receipts` at or near zero doesn't divide-by-zero or blow up
(exercises the floor); re-running the same epoch twice from the same state
produces byte-identical rates (determinism).

## Task 3: ConvertCurrency primitive + order-book integration (Opus)

Implement `ConvertCurrency(SimState state, double amount, int
fromCurrencyId, int toCurrencyId) -> double` using the `NumeraireRate` ratio
(design doc formula). Then integrate it at every order-book site the design
doc's "Conversion mechanics" §1 lists, with these exact behaviors:

- **Order entry** (`OrderOps.PostBuy`/`PostSell` or their call sites in
  `MarketEngine`): before an order from an actor whose currency differs from
  the market's local currency (the port-owning polity's `CurrencyId`) enters
  the book, convert the order's amount into local currency first.
- **`OrderOps.Fill`** (`OrderOps.cs:76-92`): restructure so tax
  (`SettleSale`) and the labor wage share deduct from the local-currency
  `paid` amount **before** conversion; only the seller's net remainder
  converts into the seller's own currency at the point of crediting via
  `Deposit`.
- **`OrderOps.CancelBuy`/`ExpireOrders`** (`OrderOps.cs:50-68, 181-188`):
  convert the refunded escrow back into the original buyer's currency before
  crediting.
- **`MarketEngine.MoveFreight`/`BookOps.LiftAsks`** (`MarketEngine.cs:
  705-820`): the trader converts its payment amount into the local market
  currency before paying cost/tariff/friction/fuel, mirroring the order-entry
  treatment.

Every conversion increments `Currency.CumulativeConvertedOut` on the source
and `CumulativeConvertedIn` on the destination (both fields added in Task 1).

Tests: a fill between two different-currency actors nets correctly on both
sides (seller receives net-of-tax-and-wage converted into their currency, tax
and wages stay in local currency at the sovereign/labor side); a cancelled
buy order's refund lands back in the original currency at the *current*
rate; conservation holds (total value in numeraire terms is unchanged by a
fill + its tax/wage split, modulo the tax/wage itself moving to the
sovereign/labor).

## Task 4: Corporation wallet integration at remaining trade sites (Opus)

Every other site where a corporation's balance changes moves to
`Deposit`/`Withdraw` instead of raw field writes (there should be none left
after Task 1 removed the setter, but this task is where each call site is
updated to call the right method): `CorporationOps` dividend/lobby/seizure,
`MarketEngine`'s facility input-cost and wage payments, and
`CorporationOps.cs:904`'s cartel skim. Each site deposits into whatever
currency it was paid in and withdraws via the draw-down rule when paying an
expense. Tests: a corporation paying a facility input cost in a currency it
doesn't directly hold enough of triggers the draw-down (converts from another
held currency); a dividend paid to a host polity in a different currency than
the corp's largest holding converts correctly.

## Task 5: Bilateral transfer sites (Sonnet)

**Amended after Task 4** (not in the original design doc's site inventory —
a real gap surfaced during implementation, not a plan error to silently
patch around): `CourierOps.cs:38,137,143,173` — a courier's fee is escrowed
from the poster's ledger at post time (`Withdraw`), then either credited to
the fulfiller on success or refunded to the poster on failure/expiry
(`Deposit`). The poster and fulfiller may be different actors with
different currencies. Use `Withdraw` at post time (poster's currency),
`Deposit` at resolution (fulfiller's currency, converting from the escrowed
currency; or the poster's own currency for a refund — no conversion needed
since it returns to the same actor/currency it left).

`FederationOps.PayTribute`, `WarResolution` reparations, and
`GraduationOps` parent/child split all convert via `ConvertCurrency` +
`Deposit`/`Withdraw` at the point money crosses currencies. For
`GraduationOps`, the child polity's starting balance is seeded via one
`ConvertCurrency` call from the parent's currency into the child's
brand-new currency (see Task 6 for where the child's `Currency` itself gets
created — this task assumes it already exists by the time the seed-transfer
runs, per the ordering Task 6 establishes). Tests: tribute paid in the
overlord's currency lands correctly on a same- or different-currency vassal;
reparations likewise; a graduation seed transfer converts at the founding
rate; a courier fee posted and fulfilled across two different currencies
settles correctly, and a refund (failure/expiry) returns to the poster
without spurious conversion.

## Task 6: Genesis & lifecycle (Opus)

Every new polity gets a brand-new `Currency` at creation (`NumeraireRate =
1.0`), covering both `GraduationOps` splits and federation formation's
`young` polity (`FederationOps.cs:113-136`). Confirmed exhaustive list of
polity-death paths (design doc, "Genesis & lifecycle" — do not search for
others, this list is verified complete): vassal absorption
(`FederationOps.cs:310-311`), war submission/annexation
(`WarResolution.cs:261-263`), and federation formation's double merge
(`FederationOps.cs:135-136`, two parents merging into the new `young`
polity). For all three, fix `FederationOps.MergeInto` (`FederationOps.cs:
334-415`): the `Credits` transfer (`375-376`) force-converts via
`ConvertCurrency`/`Deposit` into the surviving polity's currency, and the
loan-reissue (`399-414`) converts `loan.Principal` into whichever side's
currency changed (lender's if the lender changed, borrower's context
unaffected since the loan stays lender-denominated) before reissuing. The
absorbed `Currency` is marked `Retired = true` in every case, including both
parents in the federation-formation case (two retirements into one brand-new
currency, not one absorption into a pre-existing one — see design doc for why
this composes from the same two rules). Tests: absorption converts the
absorbed polity's balance and retires its currency; a federation formation
retires both parents' currencies into a brand-new one; a reissued loan's
principal is correctly converted, not carried over raw.

## Task 7: Borrow/ServiceLoans cross-currency fixes (Opus)

`Phases.Borrow` (`Phases.cs:730-779`): `existingPrincipal`
(`744-750`) numeraire-converts each open loan's principal before summing
against the borrower's own-currency debt ceiling. Lender-candidate ranking
(`754-769`) numeraire-converts both `candidate.Credits` and `principal`
before comparing. The loan itself, once a lender is chosen, still issues
denominated in the lender's currency (for a corporation lender, drawn from a
specific bucket via `Withdraw`) — this part of ME's mechanism is unchanged in
shape, only the comparison math is fixed. `ServiceLoans` — confirm (and fix
if needed) that epoch-to-epoch amortization/interest, already denominated in
the lender's currency, doesn't re-introduce a cross-currency comparison bug
of its own; migrate its remaining raw `corp.Credits +=/-=` interest-crediting
write (Task 4's implementer found this is one of the two remaining callers
keeping `Corporation.Credits`'s transitional `_legacyCredits` bridge alive —
the other was `CourierOps`, migrated in Task 5). After this migration, check
whether the bridge now has zero remaining write-callers on `Corporation`; if
so, remove it and make `Credits` the purely computed read-only property the
design doc specifies (same as Task 4's brief asked, deferred to here since
this is genuinely the last caller). Tests: a borrower with loans from two
different-currency lenders is correctly gated by the debt ceiling; lender
selection picks correctly
across currencies; existing ME loan-mechanism tests (capitalization ceiling,
debt-to-income gate) still pass unchanged in behavior for the single-currency
case.

## Task 8: Segment/Faction wealth port-transfer conversion (Sonnet)

Every port-ownership-change site — `FederationOps.MergeInto`'s port loop,
`WarConduct.TransferPort` (locate the exact method; the design doc names it
generically), and `GraduationOps` secession — force-converts every
`Segment`/`Faction` resident at that port from the old owner's currency into
the new owner's currency via `ConvertCurrency` at the moment of transfer.
Tests: a conquered port's resident segments' `Wealth` converts correctly at
transfer, not silently re-denominated 1:1.

## Task 9: MetricsOps / conservation rework (Opus)

`Currency.Supply` for currency X = Σ(`PolityRecord.Credits` where
`CurrencyId == X`) + Σ(every corporation's `Holdings[X]`) +
Σ(`Segment.Wealth`/`Faction.Wealth` resolved to X via owning polity) +
escrow currently denominated in X. Rewrite `MetricsOps.Money`/`Snapshot` to
produce a per-currency residual (mints tracked per-currency at the same
`≤1.3e-9` tolerance ME validated) instead of one lump number, netting
conversions via the `CumulativeConvertedIn`/`CumulativeConvertedOut` pair
(design doc, "Conservation & determinism" — a conversion is a transfer
between two currencies' supplies, not a mint, and must net to zero across
the pair). The SIMHEALTH dashboard's galaxy-wide `Money.Supply` becomes a
derived, numeraire-converted display figure
(`Σ Currency.Supply × Currency.NumeraireRate`), not itself checked by
`ConservationTests`. Rewrite `ConservationTests` to check N per-currency
residuals. Tests: conservation holds per-currency across a multi-epoch run
with mixed-currency trades, loans, tribute, and at least one polity
absorption; the aggregate display figure is informative but not asserted as
an invariant.

## Task 10: Serializer (Sonnet)

Add `Currency` records, `PolityRecord.CurrencyId`, `Corporation.Holdings`
(variable-length — follow the existing pattern this serializer already uses
for any other variable-length per-actor collection), and the per-currency
cumulative mint/conversion counters to `ArtifactSerializer`, with a markets-
format version bump. Follow the exact existing read/write pattern already in
this file (do not invent a new serialization style). Tests: round-trip
save/load preserves `Currency` state, `CurrencyId`s, and `Holdings` exactly;
loading an old-format save (pre-version-bump) either upgrades cleanly or
fails with a clear version-mismatch error (check how this project's
serializer already handles version bumps — follow that convention exactly,
per this project's greenfield/no-compatibility-shims rule: do not build a
migration path unless the existing pattern already has one).

## Task 11: REPL surface (Sonnet)

Expose currency id/name and current `NumeraireRate` per polity, and a
corporation's per-currency `Holdings`, in the REPL (find the existing
polity/corp inspection commands and extend them following their existing
output format/conventions — do not invent a new command style). Test via the
REPL directly (`printf 'cmd\n' | dotnet run --project src/Inspector`, per
project convention — PowerShell mangles piped stdin).

## Task 12: Design-doc amendment (Sonnet)

Rewrite `docs/design/economy/markets.md` §Credit to describe per-polity
currencies + FX + corporation multi-currency wallets, superseding the
one-universal-currency framing, once Tasks 1-11 are complete and the actual
shipped mechanism is stable enough to document accurately. Cross-reference
`docs/superpowers/specs/2026-07-14-cu-currency-fx-design.md` for the full
rationale rather than repeating it.

## Acceptance (after Task 12, before merge)

`dotnet test` green (hex-tier suite intact); determinism byte-identity for
the same config; the committed sweep
(`2026-07-12-debt-diagnosis-experiment.json`) re-run,
`NegativeTreasuries` re-checked for breathing; per-currency conservation
residuals hold at ME's tolerance; REPL surface eyeballed by the user; one
whole-branch fresh-eyes review (fable) before merge, specifically re-checking
the order-book conversion sites and the corporation draw-down rule against
the real implementation.
