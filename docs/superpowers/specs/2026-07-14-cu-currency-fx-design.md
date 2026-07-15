# Slice CU-1 — currency & FX foundation design

Date: 2026-07-14. Status: v2 — revised after a fable fresh-eyes review of v1
surfaced real gaps, independently verified against the code before this rewrite.
User-approved corrections folded in below; ready for implementation planning.

Direct follow-on to Slice ME (monetary equilibrium, merged to main 2026-07-14).
Built from four Phase-1 research passes: `2026-07-14-cu-monetary-theory-research.md`,
`2026-07-14-cu-game-precedent-research.md`, `2026-07-14-cu-genre-precedent-research.md`,
`2026-07-14-cu-mechanism-options.md` — read those for the full evidence trail; this
doc states conclusions and the concrete design, not the research itself.

## What changed from v1

A fable review read v1 cold and flagged that its central simplification — "convert
only at order entry, everything downstream of that is already single-currency" —
was false: `OrderOps.Fill`/`SettleSale`/`CancelBuy`/`ExpireOrders` all move money
into or out of a foreign ledger with zero conversion in the code as it exists today,
and `MarketEngine.MoveFreight`/`BookOps.LiftAsks` bypass the order book entirely.
Verified directly against the source (line numbers below), not taken on faith. Also
surfaced: `FederationOps.MergeInto`'s `Credits` addition and loan-reissue principal
are unconverted; `Phases.Borrow` compares balances/principals across currencies as
if they were commensurable; hostless corporations (`HostPolityId == -1`, cartels and
pirate bands) have no host currency to mirror; the serializer and the conservation
residual's handling of conversions were underspecified. Separately, discussing the
hostless-corporation gap with the user surfaced that corporations in general — not
just the outlaw ones — should hold multi-currency wallets, since they routinely
trade across polity borders. That is a real scope increase over v1's "every actor
holds exactly one currency" rule, deliberately accepted into CU-1 rather than
deferred (see Data model below).

## Problem recap

ME gave every polity independent, unilateral minting authority over `Credits`, a
single universal currency shared by every polity, corporation, and market — no
exchange rates, no per-polity separation. On the committed acceptance sweep this
produced 83–97% of the final money supply as fiat-issued, each polity's minting
diluting every other polity's holdings with no coordinating mechanism. Research
found: no game precedent for this combination (every comparable game either
centralizes minting entirely or gives each faction its own currency); real-world
currency unions with distributed, uncoordinated minting have a concrete historical
death (the Latin Monetary Union, 1865–1927); and every genre precedent this project
draws on (Victoria 3, EU4, Stellaris, CK3) gives each nation its own currency rather
than sharing one fungible pool.

## Goal and scope

Split `Credits` into one `Currency` per living polity, with a deterministic FX rate
between any two currencies and an explicit conversion step wherever money crosses a
currency boundary. This is the foundation slice (CU-1) in a four-part chain the user
has laid out: CU-1 (this doc) → CU-2 (bank actor) → CU-3 (federation-triggered
currency consolidation) → CU-4 (bank/currency strength feeding federation
generation). Only CU-1 is designed in detail here; CU-2–CU-4 are captured as a
forward roadmap at the end so nothing from the brainstorm is lost, but their design
work happens in their own future sessions once CU-1's actual shape exists to build
on.

**Explicitly not built in CU-1**: any Bank actor or entity; any real currency-union
formation mechanic beyond a minimal forced-conversion stub at polity absorption; any
feedback from monetary state into federation generation; a live FX order book (rates
are a formula, not traded).

## Data model

A new record, `Currency`:

```
Currency {
    int Id
    string Name
    int FoundingPolityId
    double Supply                    // this currency's own money supply
    double CumulativeFiatIssued      // per-currency, mirrors ME's existing fields
    double CumulativeSteadyIssuance
    double CumulativeConvertedIn     // signed pair: value converted INTO this currency
    double CumulativeConvertedOut    // value converted OUT of this currency
    double NumeraireRate             // this currency's value in the shared numeraire unit
    bool Retired                     // true once its issuing polity is absorbed/dies
}
```

`PolityRecord` gains `CurrencyId` and keeps a single `Credits` field — a polity is
always single-currency (it's the thing minting that currency). `Segment.Wealth`/
`Faction.Wealth` do **not** get their own `CurrencyId` — resolved via their owning
polity's `CurrencyId` at point of use, with one exception: when a port changes
owner (`FederationOps.MergeInto`'s port loop, `WarConduct.TransferPort`,
`GraduationOps` secession), every `Segment`/`Faction` resident at that port force-
converts its `Wealth` from the old owner's currency into the new owner's currency at
that moment, via the same `ConvertCurrency` primitive below — otherwise a conquered
population's wealth would silently re-denominate at 1:1 the instant the port's owner
changes, breaking per-currency conservation.

**Corporations hold multi-currency wallets.** This is the one deliberate deviation
from "every actor holds exactly one currency," decided with the user after the
review surfaced that hostless corporations (cartels/pirate bands, `HostPolityId ==
-1` per `Corporation.cs:40-41`, which DO hold `Credits` — see `CorporationOps.cs:
904`) have no host currency to default to. Rather than special-case just those,
**all** corporations get a real per-currency holdings structure, since any
corporation trading across polity borders can legitimately accumulate a foreign
market's currency rather than force-converting on every transaction:

```
Corporation.Holdings: Dictionary<int CurrencyId, double Amount>
```

`Corporation.Credits` stops being a settable field and becomes a **computed,
read-only property**: `Holdings.Sum(kv => kv.Value * CurrencyOf(kv.Key).
NumeraireRate)` — a numeraire-converted total. This is the key move that bounds the
blast radius: every existing site that only *reads* `corp.Credits` for a comparison
or ranking (e.g. `Phases.Borrow`'s lender-candidate scan) keeps compiling and keeps
being meaningful, completely unmodified. Only sites that *write* to a corporation's
balance change — a bounded, identifiable list (dividend/lobby/seizure, market
fills, wage/input-cost payments, cartel skim, loan issuance/service) — and each of
those moves to the explicit currency-aware API below instead of `corp.Credits +=`.

**`ICreditLedger` gains two members**, implemented differently by the two ledger
types:

```
Deposit(SimState state, double amount, int fromCurrencyId)
double Withdraw(SimState state, double amount, int toCurrencyId)   // returns amount actually debited, in the ledger's own terms
```

- `PolityRecord.Deposit`: if `fromCurrencyId != CurrencyId`, convert `amount` via
  `ConvertCurrency` first, then add to `Credits`. A polity is single-currency and
  always auto-converts on receipt (this is how e.g. `PayTribute` paid in a foreign
  currency lands on the receiving polity).
- `PolityRecord.Withdraw`: if `toCurrencyId != CurrencyId`, convert the needed
  amount of the polity's own currency into `toCurrencyId`, deduct the source amount
  from `Credits`, return the converted `toCurrencyId` amount to hand to the
  recipient's `Deposit`.
- `Corporation.Deposit`: adds directly into `Holdings[currencyId]` (creating the
  entry if new) — no conversion, money simply accumulates in whatever currency it
  was earned.
- `Corporation.Withdraw`: **draw-down rule**, deterministic —
  1. If `Holdings[toCurrencyId] >= amount`, debit directly from that bucket.
  2. Else, walk the corporation's *other* currency buckets in ascending
     `CurrencyId` order, converting each bucket's held amount into `toCurrencyId`
     (via `ConvertCurrency`) and applying it to the shortfall until covered or
     buckets are exhausted. Buckets that get fully drained are removed from
     `Holdings`; a partially-drained bucket keeps its remainder.
  3. If still short after exhausting all buckets, the withdrawal is capped at what's
     available — implementation must confirm this matches how each existing call
     site already handles an insufficient-funds case (none of the sites audited for
     this doc assume withdrawal can silently invent money; this needs a check
     during implementation planning, not an assumption baked in here).

## FX rate

Stored as a **numeraire rate per currency**, not an N×N pairwise table — `Currency.
NumeraireRate` is how much of a shared synthetic numeraire unit one unit of that
currency is worth. Converting `amount` from currency A to currency B is
`amount * A.NumeraireRate / B.NumeraireRate`. This is O(N) state instead of O(N²),
and scales cleanly as new polities (and therefore new currencies) are founded
mid-history.

Recomputed **once per epoch, at the very start of the epoch, from the prior
epoch's ending `Supply`/`Receipts`** — pinned explicitly so every phase that
converts money during the epoch (`Borrow`, `ServiceLoans`, `PayTribute`, market
fills, freight lifting, `MergeInto`) uses the exact same frozen rate table, with no
ordering ambiguity between phases. The formula is a money-per-output density
(quantity-theory-style: a currency with more supply relative to its own real output
is weaker), with `Receipts` floored to a small positive constant
(`Economy.FxReceiptsFloor`) so a freshly split polity with near-zero receipts
doesn't blow the rate up or divide by zero. One new knob, `Economy.FxSensitivity`,
controls how sharply the rate reacts to supply/output changes; tuned and validated
against the committed sweep like every other knob, not hand-picked.

A newly founded polity's `Currency` starts at `NumeraireRate = 1.0` at creation and
is recomputed normally from the next epoch onward.

## Conversion mechanics

One shared primitive:

```
ConvertCurrency(SimState state, double amount, int fromCurrencyId, int toCurrencyId) -> double
```

It is called from more places than v1 claimed. Two categories:

**1. Inside the order book/market path — money crossing a currency boundary on
entry, exit, or bypass:**

- **Order entry** (buy or sell): before an actor's order enters a market
  denominated in a different currency, its amount converts at the current rate,
  automatically, as part of the same trade-submission path (no new actor/AI
  decision). The exact entry point (`MarketEngine`/`OrderOps`, upstream of
  `PostBuy`/`PostSell`) is an implementation-planning detail.
- **`OrderOps.Fill`** (`OrderOps.cs:76-92`): `paid` is computed in the market's
  local currency. Tax (`SettleSale`) and the labor wage share are deducted **in
  local currency first** — the sovereign and local segments are local by
  construction, no conversion needed for their share. Only the **seller's net
  remainder** converts into the seller's own currency at the point of crediting.
  This reorders the Fill→SettleSale relationship slightly from today's code (which
  credits the seller the full gross `paid`, then deducts tax/wages from that same
  ledger): local deductions must happen before the currency conversion, not after.
- **`OrderOps.CancelBuy`/`ExpireOrders`** (`OrderOps.cs:50-68, 181-188`): a buy
  order's remaining escrow is in the market's local currency (per the entry-point
  conversion above); on cancellation/expiry it converts **back** into the original
  buyer's own currency before crediting their ledger.
- **`MarketEngine.MoveFreight`/`BookOps.LiftAsks`** (`MarketEngine.cs:705-820`):
  these lift asks and pay cost/tariff/friction/fuel directly from a trader's own
  ledger without ever posting an order. They get the same "convert before paying"
  treatment a buy order gets at entry — a courier/trader paying in a foreign
  market converts the payment amount before it leaves their ledger.
- Tariffs/friction fees themselves need no separate conversion once the payer has
  already converted at the point above — the fee is collected in local currency
  from an already-local-currency payment.

**2. Direct bilateral transfers that don't go through a market**: `FederationOps.
PayTribute`, `WarResolution` reparations, `CorporationOps` dividend/lobby/seizure,
`GraduationOps` parent/child split, cross-currency loans (`Phases.Borrow`/
`ServiceLoans`), and `FederationOps.MergeInto`'s `Credits` transfer and loan-reissue
principal (see Genesis & lifecycle) — each calls the same `ConvertCurrency` helper.

This is a materially larger site list than v1's "~12 bilateral sites" claimed, but
still bounded and enumerable — not the full ~109-site sprawl the raw grep count
suggested, because most of those sites remain single-currency reads/writes once the
sites above handle the actual boundary crossings.

## Loans across currencies

A cross-currency loan is denominated in the **lender's** currency; the borrower
converts to service it each epoch, so a rate move changes how much of the
borrower's own currency a fixed foreign-currency payment costs. FX risk sits with
the borrower. This mirrors real cross-border sovereign debt and, per the theory
research, avoids replicating the Latin Monetary Union's core failure mode — a
borrower externalizing their own currency's weakness onto whoever lent to them.

`Phases.Borrow` (`Phases.cs:730-779`) needs two fixes, both **comparison-only** —
the actual transferred principal still denominates in the lender's currency as
above:
- `existingPrincipal` (`744-750`) sums `open.Principal` across a borrower's open
  loans, which may be denominated in different lenders' currencies, then compares
  the sum against a debt ceiling computed from the borrower's own-currency income.
  Each loan's principal must numeraire-convert before summing.
- Lender-candidate ranking (`754-769`) compares `candidate.Credits` (numeraire
  total, per the Corporation model above, or the polity's own-currency `Credits`)
  against `principal` (in the borrower's currency). Both sides must be
  numeraire-converted for the comparison; the loan itself, once a lender is chosen,
  still issues in that lender's own currency (or, for a corporation lender, the
  specific bucket it draws from via `Withdraw`).

## Genesis & lifecycle

Every polity is founded with a brand-new `Currency` — a clean 1:1 invariant, matching
the "grow from one currency per polity" framing this whole chain is built on. A
`GraduationOps` parent/child split seeds the child's starting balance via one
`ConvertCurrency` call at the founding rate (parent currency → new child currency,
both concrete `Currency` records at that point).

**Confirmed exhaustive list of polity-death paths** (verified in code, not assumed):
`Retired` is set in exactly one place, `FederationOps.Retire` (`FederationOps.cs:
420-424`), reached from three call sites — federation formation (`113-155`),
vassal absorption (`310-311`), and war submission/annexation
(`WarResolution.cs:261-263`). No extinction path exists elsewhere in the codebase.
Every one of these three needs the same currency handling:

- **Vassal absorption / war submission** — a straightforward absorption:
  `MergeInto(state, fromId, intoId)` where `intoId` already exists. The absorbed
  polity's remaining `Credits` (`FederationOps.cs:375-376`, currently a raw
  unconverted addition) force-converts into the absorber's currency via
  `ConvertCurrency` before adding, and the absorbed loan reissue
  (`FederationOps.cs:399-414`, currently reissues `loan.Principal` unconverted)
  converts the principal into the surviving lender's or borrower's currency —
  whichever side changed — at reissue. The absorbed `Currency` is marked `Retired`.
- **Federation formation** — the review's one genuine wording gap in v1:
  `FederationOps.cs:113-136` doesn't have one polity absorb another; it creates a
  **brand-new** polity (`young`, `newId`) and calls `MergeInto` **twice**
  (`rel.PolityAId → newId`, then `rel.PolityBId → newId`). Under the rules above
  this composes cleanly without new mechanism: `young` gets a brand-new `Currency`
  (the standard genesis rule), and each `MergeInto` call force-converts one parent's
  remaining balance into that brand-new currency — it is two forced conversions
  into a fresh currency, not one absorption into a pre-existing one. Both parent
  `Currency` records retire.

This is a deliberate stub in all three cases — CU-3 replaces "forced instant
conversion" with a real currency-union-formation mechanic (gradual consolidation,
bank involvement) — but CU-1 cannot ship with orphaned currencies left dangling
when polities die.

## Conservation & determinism

Each `Currency` gets its own conservation residual (mints tracked per-currency, same
tight tolerance ME validated — `≤1.3e-9`), which is strictly more precise than
today's single lump `Money.Supply` number. Every conversion is a **transfer between
two currencies' supplies**, not a mint: it decreases the source currency's
effective circulating total and increases the destination's by the converted
amount, tracked via the paired `CumulativeConvertedIn`/`CumulativeConvertedOut`
fields so the per-currency residual formula nets transfers out cleanly, the same
way the existing wealth levy already nets a same-currency transfer without a
residual term. `ConservationTests` is rewritten to check N per-currency residuals
instead of one.

`Currency.Supply` itself is a bigger walk than a single-actor field sum: for
currency X it is Σ(`PolityRecord.Credits` where `CurrencyId == X`) +
Σ(**every corporation's** `Holdings[X]`, since any corporation anywhere may hold
some of currency X) + Σ(`Segment.Wealth`/`Faction.Wealth` resolved to X via owning
polity) + escrow currently denominated in X. `MetricsOps.Money`'s galaxy-wide
`Supply` total becomes a **derived, numeraire-converted display figure**
(`Σ Currency.Supply × Currency.NumeraireRate`) for the SIMHEALTH dashboard —
informative, but not itself a correctness invariant.

No new `RollChannel` is needed anywhere in this design — the FX rate is a pure
formula, conversions are pure arithmetic, and the draw-down rule and absorption
stub are deterministic given the current rate table and a fixed (ascending
`CurrencyId`) iteration order. Existing per-polity mint entry points
(`IssueSovereignCredit`, the steady-issuance term in `AllocationPhase.Run`) are
untouched in shape — they still mint into their own polity's `Credits`, just now
scoped to that polity's own `Currency.Supply` instead of one galaxy-wide number.

## Serializer

`Currency` records, `PolityRecord.CurrencyId`, `Corporation.Holdings` (a variable-
length collection, not a fixed column), and the per-currency cumulative
mint/conversion counters all need new serializer entries and a markets-format
version bump — dropped from v1, restored here per the mechanism-options research's
original flag.

## Design-doc amendment required

`docs/design/economy/markets.md` §Credit currently describes one universal currency;
this slice must rewrite it to describe per-polity currencies + FX, superseding that
framing (per the project's "design is the spec" rule — this is not a silent
deviation, it's the documented replacement).

## Acceptance criteria

Standard slice gates: `dotnet test` green (hex-tier suite never breaks); determinism
byte-identity for the same config; the committed sweep
(`2026-07-12-debt-diagnosis-experiment.json`) re-run and `NegativeTreasuries`
re-checked for breathing; conservation residuals hold per-currency at the same
tolerance ME validated; REPL surface exposes at least currency id/name and FX rate
per polity, and a corporation's per-currency holdings; one whole-branch fresh-eyes
review (fable) before merge — informed by this session's experience, that review
should specifically re-check the expanded order-book conversion list and the
corporation draw-down rule against the actual implementation, since those are where
v1 broke down.

## Forward roadmap: CU-2 through CU-4

Captured at the level of detail from this session's brainstorm — not designed in
depth; each gets its own future design session once the prior slice's real shape
exists to build on.

- **CU-2 — Bank actor.** A new first-class actor type attached to a `Currency` (not
  a polity), holding minting/regulation authority and formally taking over the
  `ConvertCurrency` primitive's exchange-management role that CU-1 leaves as a bare
  function call with a bookkeeping tally. Open questions for that session: is a Bank
  created 1:1 with each `Currency` at founding, or does it need its own separate
  founding condition; what does "regulation" concretely gate (issuance caps? conversion
  fees/spreads?); how banks interact with existing polity AI/behavior.
- **CU-3 — Federation-triggered currency consolidation.** Replaces CU-1's blunt
  forced-conversion-at-absorption stub with a real mechanic: federating/absorbing
  polities' currencies (and their CU-2 banks) merge, likely gradually rather than
  instantly, plausibly with the absorbed bank's regulatory role transferring to the
  surviving one.
- **CU-4 — Bank/currency-union strength as a feedback input to federation
  generation.** The most novel piece, deliberately left open until CU-2/3 exist to
  define what "strength" even measures (reserve depth? rate stability track record?
  size?) and how it would plug into whatever currently drives federation formation
  (`GraduationOps`/`FederationOps` — not yet audited for this purpose).
