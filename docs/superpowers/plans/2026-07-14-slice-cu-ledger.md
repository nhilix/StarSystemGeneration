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

## Task 6b: Corporation debit-cap conservation fix (Opus)

**Inserted after Task 6's review** (not in the original design — Task 6
turning on real currencies exposed a genuine, pre-existing conservation
leak in Task 4's corp-wallet migration; the leak is a real bug, not a
residual-formula artifact, and Task 9's per-currency measurement rewrite
cannot fix it on its own).

`Corporation.Withdraw` caps at wallet holdings (no overdraft — a deliberate
Task 1 design choice, unlike a polity, which may go negative). Several corp
debit sites charge the corp for goods/fees settled to a counterparty at
**full requested value**, discarding `DebitLocal`/`Withdraw`'s actual
(possibly-capped) return — so the counterparty is credited more than the
corp actually paid, a real leak. Confirmed buggy sites: `MarketEngine.cs`
~811-834 (the freight-spread run — tariff `fee`, friction `burn`, and
`fuelCost` all lifted/charged at `budget: double.MaxValue` and credited in
full downstream, uncapped by the corp's actual wallet); `CorporationOps.cs`
~583-587 (facility upkeep, same pattern — goods lifted at
`budget: double.MaxValue`, sellers paid full, corp debit capped and
discarded).

Contrast the CORRECT existing sites, which establish the fix pattern to
apply everywhere: dividends/lobby (`CorporationOps.cs` ~496-505) capture
`paid` and credit only that; hull-build and `BuyDraw` (`CorporationOps.cs`
~874, ~974) pass `budget: corp.Credits` into `LiftAsks` up front, so
`cost ≤ wallet` by construction and the cap never bites.

Fix every buggy site with the SAME discipline, chosen per site to match
whichever of the two correct patterns fits more naturally: (a) bound the
lift/charge to the corp's actual available funds via the existing `budget`
parameter (matching hull-build/`BuyDraw`) wherever a single upfront amount
can be computed; (b) where charges are necessarily sequential (goods, then
tariff, then friction, then fuel, each potentially draining the wallet
further), cap each subsequent charge to what `Withdraw`/`DebitLocal` actually
returns and credit the counterparty only that capped amount (matching
dividends/lobby). Do NOT remove the corp overdraft cap itself (option "let
corps overdraft like polities") — that reopens Task 1's already-reviewed
design choice without new justification.

Tests: a corp with insufficient wallet funds attempting a freight run pays
exactly what it has (capped), and every counterparty (goods seller, tariff
collector, friction recipient, fuel seller) is credited exactly that capped
amount, never more — conservation holds end-to-end across the whole
freight-spread sequence, not just the first debit. Same for facility
upkeep. Re-run `ConservationTests`/`ShapeAcceptanceTests`/the Graduation
conserve test from Task 6's red window and confirm they now pass (modulo
the per-currency residual rewrite still pending in Task 9 — if a residual
mismatch remains after this fix, it should now be a measurement-shape
question for Task 9, not a real leak).

## Task 7: Borrow/ServiceLoans cross-currency fixes (Opus)

`Phases.Borrow` (`Phases.cs:730-779`): `existingPrincipal`
(`744-750`) numeraire-converts each open loan's principal before summing
against the borrower's own-currency debt ceiling. Lender-candidate ranking
(`754-769`) numeraire-converts both `candidate.Credits` and `principal`
before comparing. The loan itself, once a lender is chosen, still issues
denominated in the lender's currency (for a corporation lender, drawn from a
specific bucket via `Withdraw`) — this part of ME's mechanism is unchanged in
shape, only the comparison math is fixed.

**Amended after Task 6b's review** — the bridge-removal scope named below was
originally "just migrate `ServiceLoans`," confirmed insufficient: `Phases.
Borrow` (`Phases.cs:771`) itself does a raw `lender.Credits -= principal` when
the chosen lender is a corporation (the LEND side); `ServiceLoans`
(`Phases.cs:~678-685`) does the matching raw `lender.Credits += payment` (the
REPAY side). Task 6b's reviewer traced a real, confirmed conservation gap to
exactly this pair: `Phases.Borrow` lending from a corp and `ServiceLoans`
repaying it both bypass `Withdraw`/`Deposit`, so `Corporation.Credits`'s
transitional `_legacyCredits` bridge (Task 1) accumulates a phantom balance
that diverges from the corp's real withdrawable `Holdings` — confirmed to
produce the exact residual (+58.4702 on the committed history) behind three
still-red tests from Task 6/6b (`ConservationTests`, `ShapeAcceptanceTests`,
the Graduation conserve test). Migrate **both** sides — `Borrow`'s lend-side
debit via `Withdraw`, `ServiceLoans`'s repay-side credit via `Deposit` — not
just the repay side. Also confirm `SimState.CreditLocal`/`DebitLocal`'s
`localCurrencyId < 0` raw-`.Credits` fallback branches (`SimState.cs:~253,
267`) are genuinely dead for corporations by the time this task ends (no
remaining caller can reach them with a corp ledger) before concluding the
bridge is safe to remove. Only once BOTH raw writes are migrated and the
fallback branches are confirmed dead should the transitional `_legacyCredits`
bridge/setter be removed, making `Credits` the purely computed read-only
property the design doc specifies. Re-run `ConservationTests`/
`ShapeAcceptanceTests`/the Graduation conserve test and confirm all three are
now genuinely green (not just closer) — if a residual still remains, treat it
as a new, distinctly-diagnosed finding, not evidence this fix was incomplete
in a hand-wavy way.

Tests: a borrower with loans from two
different-currency lenders is correctly gated by the debt ceiling; lender
selection picks correctly
across currencies; existing ME loan-mechanism tests (capitalization ceiling,
debt-to-income gate) still pass unchanged in behavior for the single-currency
case.

## Task 7b: Currency-activation Markets settlement leak (Opus)

**Inserted after Task 7's review** — a real conservation leak (−196.967
residual at epoch 37 on the committed seed-42 history, behind
`ConservationTests`/`ShapeAcceptanceTests`/the Graduation conserve test
staying red). Task 7's implementer misdiagnosed this as a pre-existing,
out-of-slice bug ("Slice-D/CE territory"); the reviewer directly bisected it
across commits and confirmed it does **not** exist pre-slice or through
Task 3 — it first appears exactly at Task 6 (currency activation) and is
unchanged through Tasks 6b/7. This is a genuine CU-1 regression, not
inherited work, and must be fixed inside this slice before merge.

Rate-independence (it reproduces identically at `NumeraireRate = 1.0`
everywhere) does not mean it's currency-unrelated — it means a
currency-integration settlement step fails to conserve even at par: a
capped `Withdraw`/`DebitLocal` whose shortfall isn't propagated, a
convert-then-credit pair that double-counts or drops a remainder, or a
mixed raw/currency-aware debit-credit pair somewhere in the Markets phase
that Tasks 3/4/6/6b didn't fully migrate. Bisect within Task 6's diff
specifically (that's where the leak first appears) to find the exact
mechanism — do not assume it's the same class of bug Task 6b already fixed
(corp debit-cap) without checking, since Task 6b's own fix didn't close it.

Tests: reproduce the epoch-37 seed-42 residual first (RED), then fix and
confirm `ConservationTests`/`ShapeAcceptanceTests`/the Graduation conserve
test are all genuinely green — these three are the acceptance bar, not a
smaller unit test in isolation. Full `dotnet test` suite must stay green
otherwise (hex-tier suite never regresses).

## Task 7c: True lender-currency loan denomination (Opus)

**Inserted after Task 7's review, per explicit user decision** ("fix it now,
inside CU-1" — not amend the design to match the code, not defer to a
follow-up slice). The design doc's "Loans across currencies" section is the
spec here: a cross-currency loan denominates in the LENDER's currency, FX
risk sits with the borrower. Task 7 only fixed the cross-currency
*comparison* math (debt ceiling, lender ranking); the loan's actual
`Principal`/service payments are still computed and paid in the BORROWER's
currency (`principal = -pr.Credits * 1.2`, a pre-slice ME formula,
unchanged) — the opposite of the approved design. This task builds the real
mechanism, not just fixes a formula:

- **At issuance** (`Phases.Borrow`): the borrower's deficit-derived amount
  (still computed from the borrower's own `Credits`/currency — that part of
  ME's mechanism is correct and unchanged) converts via `ConvertCurrency`
  into the LENDER's currency; THAT converted amount becomes `Loan.Principal`
  going forward. The lender fronts `Principal` in their own currency
  (`Withdraw` — already correct for a corp lender per Task 7; the polity
  lender path is currently raw currency-blind arithmetic, per Task 7's
  reviewer note, `Phases.cs:~824,743` — fix this here too, `Withdraw`/
  `Deposit` like the corp path). The borrower receives the proceeds
  converted back into their OWN currency at the issuance rate (a `Deposit`
  in the borrower's currency) — this is the amount they actually get to
  spend; only the DEBT is lender-currency-denominated, not the cash they
  hold.
- **At servicing** (`Phases.ServiceLoans`): amortization/interest is computed
  in the lender's currency (fixed schedule against `Loan.Principal`, which
  never changes shape). Each epoch, convert that epoch's lender-currency
  payment into the BORROWER's currency **at that epoch's current rate** (not
  the issuance rate — this is the FX-risk mechanism: the same lender-currency
  payment can cost the borrower more or less of their own currency as rates
  drift) to determine how much to `Withdraw` from the borrower; `Deposit` the
  lender-currency amount to the lender. If the borrower can't cover the
  converted cost, this should interact with the existing default/
  capitalization-ceiling mechanics (`Economy.LoanCapitalizationCeiling`) the
  same way an ordinary missed payment does today — don't build new default
  semantics, reuse what ME already established.
- Fix the polity↔polity raw-arithmetic path (`Phases.cs:~824,743`) to route
  through `Withdraw`/`Deposit` like the corp path already does, so it
  doesn't silently break conservation once FX rates genuinely diverge.

Tests: a loan issued when rates are at parity, then serviced after rates
drift, costs the borrower MORE (or less) of their own currency for the same
lender-currency payment — this is the core FX-risk behavior the design
doc's rationale depends on, and must be directly demonstrated, not just
inferred from unit-level correctness of the conversion calls. Existing ME
loan-mechanism tests (capitalization ceiling, debt-to-income gate) still
pass unchanged in behavior when both currencies are the same (parity case).
Conservation holds across issuance and every servicing epoch — no value
created or destroyed by the conversions themselves, only transferred.

## Task 8: Full cross-currency movement audit (Opus)

**Widened after Task 9's review** — this was originally scoped narrowly as
"Segment/Faction wealth at port-ownership-change sites." Task 9's precise
per-currency conservation measurement (unavailable until Task 9 landed —
the old lump measure was mathematically blind to this class of leak)
confirmed a real, rate-independent leak: raw 1:1 cross-currency transfers at
**migration** (`Phases.cs:~1756-1761` — the gradient migration path has no
same-owner guard on its destination-port pick, unlike the refugee off-lane
branch) and **construction wages** (`ProjectOps.cs:~318-331` →
`MarketEngine.PayWages` — a polity-funded project at a foreign-owned port
credits household wages in the building port's currency while debiting the
funder's own pool in its own currency). Both confirmed genuinely new gaps
in the original design's site inventory — not something a prior task
skipped. The report explicitly warns there are **likely more** omitted
sites of the same shape.

**Do not scope this as "fix these two sites."** Audit every raw
cross-currency `Wealth`/`Credits` move in the codebase — start from the two
confirmed sites, but systematically check every place `Segment.Wealth`,
`Faction.Wealth`, `PolityRecord.Credits`, or `Corporation.Holdings` moves
between two actors whose `CurrencyId` can differ, and confirm each one
routes through `ConvertCurrency`/`Deposit`/`Withdraw`/`CreditLocal`/
`DebitLocal` rather than a raw `+=`/`-=`. This includes but is not limited
to: the original port-ownership-change sites (`FederationOps.MergeInto`'s
port loop, `WarConduct.TransferPort`, `GraduationOps` secession — force-
convert every resident `Segment`/`Faction` at the moment of transfer),
migration, and construction wages.

**The real acceptance bar is the per-currency `ConservationTests`** Task 9
wrote and marked `[Skip]` (pending this task) — un-skip them and make them
pass at ME's validated `≤1.3e-9` tolerance, across the full committed
acceptance sweep, not just the two named sites in isolation. If they still
don't pass after fixing the two known sites, keep auditing — that's the
signal there are more, per the report's own warning. Also re-check the
`ShapeAcceptanceTests`/`FederationTests`/`WarResolutionTests`/
`GraduationTests`/`CorporationTests` failures Task 9's reviewer flagged as
"obsolete native-sum measure, but could also hint at another leak site" —
confirm each is genuinely explained by the old-measure-vs-new-measure gap
and not evidence of yet another omitted conversion site.

Tests: the per-currency `ConservationTests` pass, un-skipped, at ME's
tolerance; a conquered port's resident segments' `Wealth` converts
correctly at transfer, not silently re-denominated 1:1; a migration between
different-currency ports converts correctly; a foreign-port construction
wage payment converts correctly.

## Task 9: MetricsOps / conservation rework (Opus)

**Amended after Task 7c's review** — a confirmed, significant finding:
`Currency.Supply` has **zero write sites anywhere in `src`** through Task
7c. It is permanently 0, which pins every `Currency.NumeraireRate` at
exactly 1.0 (`FxOps.RecomputeRates`'s formula gives `1/(1+k·0) = 1`) —
every conversion in the entire slice so far has been bit-exact identity,
and the FX-risk behavior Task 7c built has never fired through any real
gameplay path (only through tests that set `NumeraireRate` directly on
manually-constructed `Currency` objects). **This task must fix that, not
just measure around it** — the two deliverables below are DISTINCT and
both required:

1. **A deterministic end-of-epoch pass that WRITES
   `Currency.Supply = <the walked aggregate>` back onto the live `Currency`
   record**, ordered so `FxOps.RecomputeRates` (which runs at the START of
   the NEXT epoch, reading "the prior epoch's ending `Supply`" per the
   design doc) reads a genuinely fresh, diverging value — not just a local
   computed inside a snapshot/residual check that never reaches the field
   `FxOps` actually reads. Without this, rates will keep reading 0 forever
   and the entire FX-rate mechanism (Tasks 2, 3, 4, 5, 6, 7c) stays
   permanently dormant regardless of how correct its conversion math is.
   `Currency.Supply` for currency X = Σ(`PolityRecord.Credits` where
   `CurrencyId == X`) + Σ(every corporation's `Holdings[X]`) +
   Σ(`Segment.Wealth`/`Faction.Wealth` resolved to X via owning polity) +
   escrow currently denominated in X.
2. **The conservation-residual rework** (the originally-scoped part): rewrite
   `MetricsOps.Money`/`Snapshot` to produce a per-currency residual (mints
   tracked per-currency at the same `≤1.3e-9` tolerance ME validated)
   instead of one lump number, netting conversions via the
   `CumulativeConvertedIn`/`CumulativeConvertedOut` pair (design doc,
   "Conservation & determinism" — a conversion is a transfer between two
   currencies' supplies, not a mint, and must net to zero across the pair).
   The SIMHEALTH dashboard's galaxy-wide `Money.Supply` becomes a derived,
   numeraire-converted display figure (`Σ Currency.Supply ×
   Currency.NumeraireRate`), not itself checked by `ConservationTests`.
   Rewrite `ConservationTests` to check N per-currency residuals.

Once (1) lands, rates will start actually diverging for the first time in
this slice — re-run the full suite and the committed acceptance sweep
expecting to SEE real behavior change (not just a passing test), and watch
specifically for the kind of red window Tasks 6/6b/7b hit, since this is the
first time any of that machinery runs against non-identity rates. Diagnose
any new failure at its real mechanism — do not widen a tolerance to make a
newly-live FX effect disappear.

Tests: `Currency.Supply` visibly changes epoch-to-epoch as money moves and
mints happen, and `NumeraireRate` visibly diverges from 1.0 between two
currencies with different mint/trade behavior, in an ordinary full-history
run (not a hand-constructed unit test) — this is the acceptance bar for
deliverable (1). Conservation holds per-currency across a multi-epoch run
with mixed-currency trades, loans, tribute, and at least one polity
absorption; the aggregate display figure is informative but not asserted as
an invariant — this is the acceptance bar for deliverable (2).

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

## Task 13: LaneBuilderTests threshold recalibration (Sonnet)

**Inserted at acceptance time, per explicit user decision.** The FX system
going genuinely live (Task 9) produced a denser, more interconnected lane
network as a real, investigated, and user-confirmed healthy consequence —
not a bug. Investigated concretely: polity 48's 4 ports form a legitimate
internal hub (port 2 → ports 7/8/11) PLUS multiple ports independently build
direct lanes to busy foreign trade partners (all four ports reach port 0;
two ports also reach two more foreign polities each) rather than routing
everything through the hub — plausible as real arbitrage-driven trade
activity now that currencies genuinely diverge. `LaneBuilderTests.
DefaultHistory_BuildsTreesAndHubs_NotAllPairsWebs`'s heuristic threshold
(`meanDegree <= 0.6*(ports-1) || meanDegree <= 3.0`) was calibrated for the
pre-FX (single-currency, less trade-active) equilibrium; polity 48 now sits
at `meanDegree = 3.50`.

Recalibrate the threshold to accommodate the new, user-confirmed-healthy
equilibrium — this is a deliberate recalibration with a documented reason,
not a blind widening to make a red test go away. Re-run the full committed
acceptance sweep/history to confirm no OTHER polity's topology looks
pathological (a duplicate-lane loop, an unbounded degree growth) under the
new threshold — the recalibration should still catch a genuine web, just
not this specific denser-but-healthy pattern.

## Task 14: Sweep-scale conservation leak (Opus)

**Inserted after running the real committed acceptance sweep** (8 seeds × 4
variants, `docs/superpowers/plans/2026-07-12-debt-diagnosis-experiment.json`)
for the first time this slice. Task 8/9's per-currency `ConservationTests`
only exercise seed 42 in isolation and passed cleanly — but at real sweep
scale, roughly **15 of 32 runs** show `Money.ConservationResidual` (the
worst per-currency residual) spike far above ME's `≤1.3e-9` bar, by 5-9
orders of magnitude, at specific epochs. Directly confirmed in
`runs/sweeps/debt-diagnosis/cheap-credit/2718.csv`: residual sits at
`~1e-9`–`~1e-8` for most epochs, then spikes to `0.116` at epoch 36 and
`1.11` at epoch 39 — coinciding with `Money.SegmentWealth` swinging wildly
(1.9M → 15.1M → 9.25M across epochs 37-39), a clear event-triggered leak,
not steady drift or floating-point noise. This is NOT the corp debit-cap
leak (Task 6b) or the migration/wages/escrow leaks (Task 8) — those are
confirmed fixed and this is a different, larger-scale trigger.

**A second, likely-related anomaly**: aggregate `Money.LoanPrincipal` peaks
wildly beyond ME's validated ~37k bound in the SAME runs that show residual
spikes — `baseline`/seed 42 peaks at **1.92M**, `baseline`/seed 2718 at
**971k**, `cheap-credit`/seed 1001 at **400k**. The loan capitalization
ceiling (`Economy.LoanCapitalizationCeiling`) that ME validated to bound
loan principal is apparently not holding under the cross-currency loan
mechanism (Task 7c) at real scale, even though the smaller Task 7c unit
tests passed. The overlap between residual-spike runs and loan-blowout runs
strongly implicates cross-currency loan/absorption mechanics
(`Phases.Borrow`/`ServiceLoans`, `FederationOps.MergeInto`'s loan reissue,
or the interaction between the two under many concurrent loans/currencies
over 40 epochs) as the shared root cause — but this is a hypothesis to
verify, not a conclusion to assume.

**Your job**: bisect the actual mechanism at REAL sweep scale, not just
against the seed-42 unit-test scale that already passed. Re-run the flagged
seeds specifically (`cheap-credit`/2718, `baseline`/42, `baseline`/2718,
`lean-labor`/42, `cheap-credit`/1001 at minimum) with finer-grained
per-epoch/per-phase instrumentation around the spike epochs to isolate
which specific event (federation formation, war absorption, a loan
reissue, a servicing cycle under a large/complex loan book) triggers it.
Check whether the loan-principal blowout and the conservation-residual
spike share one root cause or are two separate bugs. Fix the actual
mechanism — this project's hard rule is diagnose first, never widen a
tolerance to make a real leak disappear.

**Do not declare this slice's conservation story settled until the FULL
committed acceptance sweep re-runs clean** — every one of the 32 runs'
final (and ideally every-epoch) `Money.ConservationResidual` at or near
ME's tolerance, and `Money.LoanPrincipal` back within a sane bound
consistent with ME's validated behavior. Re-run the full sweep after the
fix and report the actual numbers, not just "the specific flagged epoch no
longer spikes."

**RESOLVED.** The residual spike and the loan-principal "blowout" were two
independent phenomena. (1) The residual leak's root cause was `FleetOps.
DrawUpkeep` charging a foreign market's LOCAL-currency `cost` raw 1:1 against the
polity's OWN-currency `MilitaryPoints` pool — fired when a fleet victuals at a
`HomePortId` captured by another polity (the absorption correlation). Fixed with
the standard convert-and-`RecordConversion` discipline. Full 32-run sweep worst
per-epoch residual now **1.123e-07** (relative ~1e-13 on multi-million supply,
far inside `1.3e-9 × supply`), down from 1.11. (2) The loan-principal blowout is
NOT a leak or ceiling failure — it is nominal weak-currency denomination in the
raw mixed-currency `Money.LoanPrincipal`; numeraire loan book is ~4.6k and the 2×
cap ceiling holds (max ratio 1.83). Also fixed three whole-branch-review loan
bugs as correctness hardening (Borrow debt-ceiling per-loan-currency rate;
MergeInto corp-lent borrower-change conversion; MergeInto OriginalPrincipal
preservation). Regression test `FleetSupplyCurrencyTests`. 972/972 green; seed-42
golden regenerated (deliberate history change). See `.superpowers/sdd/task-14-report.md`.

## Acceptance (after Task 12, before merge)

`dotnet test` green (hex-tier suite intact); determinism byte-identity for
the same config; the committed sweep
(`2026-07-12-debt-diagnosis-experiment.json`) re-run,
`NegativeTreasuries` re-checked for breathing; per-currency conservation
residuals hold at ME's tolerance; REPL surface eyeballed by the user; one
whole-branch fresh-eyes review (fable) before merge, specifically re-checking
the order-book conversion sites and the corporation draw-down rule against
the real implementation.
