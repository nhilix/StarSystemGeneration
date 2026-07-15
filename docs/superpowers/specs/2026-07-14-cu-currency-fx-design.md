# Slice CU-1 — currency & FX foundation design

Date: 2026-07-14. Status: user-approved, ready for implementation planning.

Direct follow-on to Slice ME (monetary equilibrium, merged to main 2026-07-14).
Built from four Phase-1 research passes: `2026-07-14-cu-monetary-theory-research.md`,
`2026-07-14-cu-game-precedent-research.md`, `2026-07-14-cu-genre-precedent-research.md`,
`2026-07-14-cu-mechanism-options.md` — read those for the full evidence trail; this
doc states conclusions and the concrete design, not the research itself.

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
are a formula, not traded); multi-currency wallets on any single actor (every actor
holds exactly one currency's balance).

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
    double NumeraireRate             // this currency's value in the shared numeraire unit
    bool Retired                     // true once its issuing polity is absorbed/dies
}
```

`PolityRecord` gains `CurrencyId`. `Corporation.CurrencyId` mirrors whichever polity
currently hosts it (updated if hosting changes, not independently owned).
`Segment.Wealth`/`Faction.Wealth` do **not** get their own `CurrencyId` field — their
currency is resolved via their owning polity's `CurrencyId` at the point of use, so
there's no second field that can desync from `PolityRecord.CurrencyId` if ownership
changes. `ICreditLedger` is unchanged (`double Credits`); the new thing is which
currency that double is denominated in, tracked alongside it — not a
`Dictionary<CurrencyId,double>` wallet anywhere. No surveyed game or genre precedent
gives a single actor multi-currency holdings, and it would reopen the "shape 1"
option the mechanism-options research flagged as the more invasive of the two
representations.

## FX rate

Stored as a **numeraire rate per currency**, not an N×N pairwise table — `Currency.
NumeraireRate` is how much of a shared synthetic numeraire unit one unit of that
currency is worth. Converting `amount` from currency A to currency B is
`amount * A.NumeraireRate / B.NumeraireRate`. This is O(N) state instead of O(N²),
and scales cleanly as new polities (and therefore new currencies) are founded
mid-history.

Recomputed once per epoch, before the per-polity allocation loop, as a pure
deterministic formula over each currency's own `Supply` and its issuing polity's
`Receipts` — a money-per-output density (quantity-theory-style: a currency with more
supply relative to its own real output is weaker). No stochastic term, no new
`RollChannel` — this is a formula over aggregates `MetricsOps`-style code already
walks cheaply. One new knob, `Economy.FxSensitivity`, controls how sharply the rate
reacts to supply/output changes; tuned and validated against the committed sweep
like every other knob, not hand-picked.

A newly founded polity's `Currency` starts at `NumeraireRate = 1.0` at creation and
is recomputed normally from the next epoch onward.

## Conversion mechanics

One shared primitive:

```
ConvertCurrency(SimState state, double amount, int fromCurrencyId, int toCurrencyId) -> double
```

Called from exactly two kinds of sites — this is the key simplification over the
research's raw ~109-site estimate, because almost all of those sites are already
single-currency by the time money reaches them once conversion happens at the door:

1. **Order entry.** Before a trader's order enters a market denominated in a
   different currency, its amount converts at the current rate — automatically, as
   part of the same trade-submission code path (no new actor/AI decision to model in
   CU-1). The exact entry point (in `MarketEngine` or `OrderOps`, upstream of
   `OrderOps.Fill`) is an implementation-planning detail, not fixed here. Once an
   order is in the book, `OrderOps`, `MarketEngine`, and escrow (`OrderEscrow`,
   `CourierEscrow`, `ExpeditionPurses`) are **untouched** — single-currency
   internals, exactly as today. Every conversion at this site increments
   `Currency.CumulativeConverted` (new field) — a tally with no behavior in CU-1
   beyond bookkeeping, but the concrete hook CU-2's bank actor takes over to
   regulate/tax/monitor exchange.
2. **Direct bilateral transfers that don't go through a market**: `FederationOps.
   PayTribute`, `WarResolution` reparations, `CorporationOps` dividend/lobby/seizure,
   `GraduationOps` parent/child split, and cross-currency loans (`Phases.Borrow`/
   `ServiceLoans`) — each of these ~12 sites calls the same `ConvertCurrency` helper
   instead of growing bespoke FX logic per call site.

Tariffs and freight friction (`MarketEngine.cs` ~801–811) need **no** separate
conversion — by the time a foreign trader's order is in the book it has already
converted to the market's local currency, so fees collected from it are already
local-currency.

## Loans across currencies

A cross-currency loan is denominated in the **lender's** currency; the borrower
converts to service it each epoch, so a rate move changes how much of the
borrower's own currency a fixed foreign-currency payment costs. FX risk sits with
the borrower. This mirrors real cross-border sovereign debt (foreign-currency-
denominated debt is the textbook risk case) and, per the theory research, avoids
replicating the Latin Monetary Union's core failure mode — a borrower externalizing
their own currency's weakness onto whoever lent to them.

## Genesis & lifecycle

Every polity is founded with a brand-new `Currency` — a clean 1:1 invariant, matching
the "grow from one currency per polity" framing this whole chain is built on. A
`GraduationOps` parent/child split seeds the child's starting balance via one
`ConvertCurrency` call at the founding rate (parent currency → new child currency,
both concrete `Currency` records at that point).

When a polity is absorbed (`FederationOps.MergeInto`), CU-1 needs *some* defined
behavior even though the real mechanic doesn't ship until CU-3: the absorbed
polity's remaining balance force-converts into the absorber's currency at the
current `NumeraireRate`, and the absorbed `Currency` is marked `Retired`. This is a
deliberate stub — CU-3 replaces "forced instant conversion" with a real
currency-union-formation mechanic (gradual consolidation, bank involvement) — but
CU-1 cannot ship with orphaned currencies left dangling when polities die.

`FederationOps.MergeInto` is the confirmed absorption path, but other polity-death
paths exist in the code (`WarResolution`/`War` conquest, possible extinction —
`FactionOps.cs`, `EvolutionSim.cs` reference removal/dissolution and were not
individually audited for this doc). Implementation must find every path that
retires a `PolityRecord` and apply the same force-convert-and-retire stub to its
`Currency` — no polity-death path may leave a `Currency` live with no owner.

## Conservation & determinism

Each `Currency` gets its own conservation residual (mints tracked per-currency, same
tight tolerance ME validated — `≤1.3e-9`), which is strictly more precise than
today's single lump `Money.Supply` number, since summing unlike-denominated balances
into one figure doesn't dimensionally make sense once currencies diverge.
`ConservationTests` is rewritten to check N per-currency residuals instead of one.
The SIMHEALTH dashboard's galaxy-wide total becomes a **derived, numeraire-converted
display figure** (`Σ Currency.Supply × Currency.NumeraireRate`) — informative, but
not itself a correctness invariant.

No new `RollChannel` is needed anywhere in this design — the FX rate is a pure
formula, conversions are pure arithmetic, and the absorption stub is deterministic
given the current rate table. Existing per-polity mint entry points
(`IssueSovereignCredit`, the steady-issuance term in `AllocationPhase.Run`) are
untouched in shape — they still mint into their own polity's `Credits`, just now
scoped to that polity's own `Currency.Supply` instead of one galaxy-wide number.

## Design-doc amendment required

`docs/design/economy/markets.md` §Credit currently describes one universal currency;
this slice must rewrite it to describe per-polity currencies + FX, superseding that
framing (per the project's "design is the spec" rule — this is not a silent
deviation, it's the documented replacement).

## Acceptance criteria

Standard slice gates: `dotnet test` green (hex-tier suite never breaks); determinism
byte-identity for the same config; the committed sweep
(`2026-07-12-debt-diagnosis-experiment.json`) re-run and `NegativeTreasuries`
re-checked for breathing (this is the least disruptive of the three original CU
options to ME's spiral fix — no change to *why* a polity mints, only to what unit it
mints); conservation residuals hold per-currency at the same tolerance ME validated;
REPL surface exposes at least currency id/name and FX rate per polity; one
whole-branch fresh-eyes review (fable) before merge.

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
