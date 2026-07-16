# Slice CU-2 — the Bank actor (design)

Decision record for the CU-2 brainstorm (2026-07-15), the direct follow-on to
CU-1 (currency & FX foundation, merged to main 2026-07-15). CU-1 gave every
living polity its own `Currency` with a live FX rate and a working conversion
primitive, but left two roles unowned: **minting authority** (issuance mints
straight into a polity's own currency from inside `AllocationPhase.Run`, with no
institution between fiscal need and the supply the polity controls) and
**exchange management** (`ConvertCurrency`/`RecordConversion` are bare `SimState`
functions with a bookkeeping tally, no owning actor). CU-2 introduces a
first-class **Bank**, one per `Currency`, that takes over both.

This is the same critique that opened Slice CU (the Eurozone/EVE-ISK precedent:
minting authority belongs to one institution, decoupled from any individual
actor's own need for money). CU-1 answered it at the *currency* level; CU-2
answers it at the *minting-authority* level within a currency.

## Scope

In: a `Bank` record attached to each `Currency`; a reserve capitalized by a
conversion spread; reserve-funded deficit issuance with a bounded fiat backstop;
a Bank-aware exchange-settlement seam; the FX coupling that lets reserve
accumulation strengthen a currency; conservation/determinism/serialization/REPL
for all of it.

Out (later slices in this chain — CU-2 only notes where its decisions constrain
them, does not design them): **CU-3** (federation-triggered currency
consolidation — banks merging when currencies merge), **CU-4** (bank/currency
strength as a feedback input to federation generation). CU-1's filed follow-ups
(corp bankruptcy reachability, sub-1e-12 dust sinks, the relative conservation
tolerance, the three scope-boundary gaps, `Segment.MeanSoL`) are separate and
untouched unless CU-2's work lands in the same code.

## Design decisions (the brainstorm's forks, as resolved)

1. **Agency — stateful, formula-reactive, no AI.** The Bank has its own balance
   sheet (a reserve) that evolves by deterministic per-epoch formula, not a
   controller/`Decide`. It is *not* an `ICreditLedger`, *not* an actor with a
   controller slot — it is closer to the `Currency` record than to
   `Corporation`. This delivers institutional independence (a Bank that can push
   back on issuance and be thinly capitalized) without a perception surface or
   the determinism/ordering care a controller adds. A Bank AI is a candidate for
   a much later slice, explicitly out here.

2. **Founding — 1:1 with each `Currency` at founding.** A Bank is minted inside
   `SimState.FoundCurrency` (`SimState.cs:216`), the single chokepoint every
   polity-creation path already routes through (entry, graduation splits,
   federation fusion). Keyed by **currency id**, so the Bank travels with the
   currency and outlives its founding polity: when the polity dies the currency
   goes `Retired`, and the Bank persists alongside it (stops earning spread),
   left intact as the party to a future CU-3 merger. No separate maturity
   trigger, no dual banked/unbanked code path — the "emergence" lives in the
   reserve dynamics, not the birth condition.

3. **Reserve source — a conversion spread.** The Bank of a *target* currency
   skims a small spread on every real cross-currency money movement *into* that
   currency; the **target** currency's Bank charges (the one being acquired),
   and it earns from demand for it. **Incidence is direction-specific**
   (refined during implementation, Task 4a — the original uniform
   "recipient always receives net" could not hold conservation cleanly because
   the market settles by paying sale recipients gross *before* the buyer is
   debited; both realizations below are conservation-identical, the target
   Bank's reserve always gets `spread × inB`, and neither requires reordering
   market settlement):
   - **Repatriation (Deposit — earning foreign money and converting it home):**
     the earner is the terminal recipient, so it banks the **net**
     `inB × (1 − spread)` and its own Bank keeps `inB × spread`.
   - **Payment (Withdraw — converting own money to pay in a foreign currency):**
     the **payer is grossed up** and the payee stays whole — the payer sources
     `inB` (full) for the payee *plus* `spread × inB` for the destination Bank's
     reserve, paying the FX spread as a premium on top. (This is why sale
     recipients paid gross before the buyer debit stay correct.)

4. **Reserve is sequestered OUT of circulating `Supply`.** The spread
   **decrements `Currency.Supply`** and **increments `Bank.Reserve`**. Because
   `FxOps.RecomputeRates` reads `density = Supply / output` (`FxOps.cs:59`),
   reserve accumulation shrinks circulating Supply and **strengthens the
   currency's rate** — a real monetary-tightening lever the Bank "controls" by
   holding reserves.

5. **Issuance — reserve-funded first, bounded fiat mint as backstop.** This is
   the crux, and resolves a tension the brainstorm surfaced (see below). A
   polity's end-of-epoch deficit is covered in two stages:
   - **Primary — reserve funding (a transfer, not a mint).** The Bank funds the
     shortfall by drawing its own reserve down into the polity's treasury
     (`Reserve → Supply`). This is the reserve's genuine economic **sink**: a
     polity that spends less than its Bank earns in spread accumulates reserve;
     one that spends more draws it down. No new money is created.
   - **Backstop — bounded fiat mint (lender of last resort).** Only if the
     reserve is exhausted *and* the polity is still short does the fiat mint fire
     — ME's existing bounded issuance (`SovereignIssuanceRate × max(0,
     Receipts)`), the only flow that grows total money and touches
     `CumulativeFiatIssued`. This preserves ME's treasury-spiral cure as a floor
     for chronically trade-poor polities whose Banks never accumulate reserve.

   Trade-rich, fiscally-disciplined polities rarely reach the mint (strong
   currencies, funded from earned reserves); chronic-deficit trade-poor polities
   still get the ME spiral floor. **No artificial reserve decay** — the sink is
   real spending, so the reserve does not need to be eroded to keep the
   constraint live.

   The always-on **steady-issuance** term (`Phases.cs:418`, ME's baseline
   money-growth channel) stays a small fiat mint: it is not deficit-triggered, so
   it does not route through the reserve-funding stage — it is the term that
   grows the money base with the economy, distinct from deficit backstopping.
   (Implementation note: keep it a mint; do not reserve-fund it.)

### Why issuance became a two-stage channel (the tension the brainstorm exposed)

The user's question — "isn't the sink on the reserve the polity's financial
spending?" — caught a real weakness in the first-draft design, which kept
issuance a pure fiat mint and let the reserve only *cap* the mint. In that draft
the reserve is never actually spent, so it only grows, which forced an inelegant
artificial decay to keep the cap biting. The hinge is whether funding a polity
from reserve is a **transfer** (existing money moves `Reserve → Supply`; the
reserve is a true sink; total galaxy money is fixed) or issuance stays a **mint**
(new money; reserve untouched; needs a separate sink). A pure-transfer
(full-reserve) regime is elegant but re-opens the debt-spiral risk ME cured for
trade-poor polities. The two-stage channel (reserve-funded primary + bounded
fiat backstop) keeps the real sink *and* the spiral cure, maps cleanly to real
central banking, and — verified below — is conservation-clean.

## The exchange-settlement seam

The spread lives on **money movement**, never on **valuation**. `ConvertCurrency`
(`SimState.cs:234`) stays a pure, spread-free arithmetic primitive — it is used
for "what is this worth" math (corporation wallet draw-down in
`Corporation.Withdraw`, cross-actor comparisons via numeraire) as well as for
sizing real transfers, and clipping a spread inside it would corrupt every
valuation.

CU-2 introduces **`SimState.SettleConversion(...)`** (the Bank-aware settlement
path): convert → apply the target Bank's spread → credit that Bank's reserve →
`RecordConversion`. The real money-movement sites route through the spread —
as implemented (Task 4 audit): order-book fills and cross-currency order
*cancels* (the return leg skims as a repatriation Deposit), freight/tariffs,
migration (destination segment banks net), construction wages, loan servicing,
fleet upkeep, and corporation wallet draw-down. Incidence is **direction-specific**
(§ Design decision 3): money arriving into a holder's own currency NETS the
recipient (`SettleConversion`); a payer converting its own money to pay a foreign
currency is GROSSED UP (`SkimToReserve` + full `RecordConversion`, payee whole).

**Exempted from the spread — as implemented and verified:**
- **Sovereign re-denomination at federation/war absorption and graduation
  splits** (`FederationOps`, `GraduationOps`) — a polity's own money merging or
  splitting is not a market exchange, and clipping it is wrong. The absorbed
  *treasury* and faction wealth transfers route through a dedicated
  `PolityRecord.DepositExempt` (convert + record, no skim), matching the always-
  exempt pool transfers.
- **Port-ownership-change re-denomination** (`SimState.ConvertPortHoldings`) —
  the forced re-denomination of resident wealth/escrow at a port whose owner
  changes is the same sovereign class, exempt.
- **Project-bid escrow refunds** (`MarketEngine` order-post refund) — un-posting
  a funder's own unfilled escrow; the real FX (and its single spread) was already
  charged at the gross-up post, so the refund is exempt. **Note (flagged for the
  tuning pass):** ordinary order *cancels* skim their return leg while project-bid
  *refunds* do not — a deliberate asymmetry (a post+cancel round trip is taxed
  twice on the order book, once on project bids); revisit if it feels wrong.

This is an explicit, documented boundary, not an omission — CU-1's Task 8 "audit
every site" discipline applies: a missed site is a silent conservation or
behavior bug.

## Conservation & determinism

The per-currency residual identity gains the reserve on the balance side:

```
Supply + Reserve == endowment + CumulativeFiatIssued + CumulativeSteadyIssuance
                    + CumulativeConvertedIn − CumulativeConvertedOut   (± tol)
```

`Reserve` is a **live balance** (like `Supply`), not a new cumulative flow
counter — `MetricsOps`/`SupplyOps` add each currency's Bank reserve to its Supply
side when computing the residual. Every CU-2 flow is conservation-clean against
this identity:

- **Spread** (`Supply −= s`, `Reserve += s`): internal to one currency, `Supply +
  Reserve` unchanged; `ConvertedIn/Out` unchanged (they still count the full
  `inB` that entered the currency). ✓
- **Reserve-funded deficit** (`Reserve −= f`, `Supply += f`): internal transfer,
  `Supply + Reserve` unchanged, no counter moves. ✓
- **Backstop fiat mint** (`Supply += m`, `CumulativeFiatIssued += m`): the one
  flow that grows total money; both sides of the identity move by `m`. ✓

Determinism: all reserve motion is by formula in fixed id order (currencies and
banks walk registry/id order, P6) — no hash rolls, no floating iteration order.
The FX rate stays a pure formula reading the prior epoch's ending state.

**Acceptance instrument (non-negotiable, CU-1's hard-won lesson):** the full
32-run committed multi-seed sweep is the conservation gate, not a single seed's
unit tests. Run it after the settlement-seam task and again after issuance
gating, and after anything that touches `SettleConversion` / the issuance
stages. CU-1 shipped apparently-clean seed-42 tests and the real sweep
immediately found a leak 5–9 orders of magnitude over tolerance. Budget for
running it more than once.

## Config, REPL, serialization, tests

- **Knobs** (`EconomyKnobs`): `ConversionSpread` (proposed default 0.005 = 50 bps
  — small enough not to swamp trade, real enough to capitalize Banks over a full
  history), `IssuanceReserveRatio` (how much of the reserve a single epoch's
  deficit funding may draw), and any parameter the backstop needs beyond ME's
  existing `SovereignIssuanceRate`. Defaults are chosen so a fresh galaxy behaves
  recognizably like CU-1 at first and diverges as Banks capitalize; a **tuning
  pass after full CU-2 implementation** narrows the final values, which must
  clear the ensemble bar (SIMHEALTH.md) before landing in TUNING.md.
- **Serialization:** `Bank` is real serialized state (`ArtifactSerializer`) —
  reserve balances persist and are subject to the byte-identity gate. Bump the
  serializer version tuple for the new layer.
- **REPL:** extend the currency inspection surface to show each currency's Bank —
  reserve balance, this-epoch spread intake, reserve-funded vs backstop-minted
  split — so the reserve dynamics are visible at the eyeball checkpoint.
- **Tests:** TDD throughout; the hex-tier (Phase-1 generation) suite never
  breaks; determinism byte-identity holds; new goldens frozen once at slice end.

## Constraints carried to CU-3 / CU-4 (noted, not designed)

- The Bank is keyed by **currency id** and persists after its founding polity
  dies (`Retired` currency), so CU-3 has a live object to merge. CU-3 decides how
  two Banks' reserves combine when currencies consolidate (likely gradual), and
  whether the absorbed Bank's role transfers to the survivor.
- "Bank strength" for CU-4 now has concrete candidate measures this slice makes
  real: **reserve depth**, **spread intake rate**, and the **FX-rate track
  record** the reserve coupling produces. CU-4 defines which of these feeds
  federation generation.
