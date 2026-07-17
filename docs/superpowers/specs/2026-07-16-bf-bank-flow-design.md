# Slice BF — the bank as monetary authority (bank-reserve-flow redesign)

Design date 2026-07-16. Closes CU-2 follow-up #1 (`[[bank-reserve-flow-gap]]`),
the architectural gap the user raised at the CU-2 eyeball. Sequenced **before
CU-3 and CU-4** by user decision (see §8) — CU-2's own kickoff-chain had CU-3
next; this slice preempts it.

## 1. The gap this closes

CU-2 shipped a `Bank` per `Currency` whose `Reserve` has exactly **one** inflow:
the FX conversion spread (`SimState.SettleConversion`, `EconomyKnobs.ConversionSpread`).
Meanwhile a polity's dominant money flows — receipts, taxes, wages, upkeep — write
straight to `PolityRecord.Credits` (`PolityRecord.cs:23`), never touching the bank.

Measured at the CU-2 eyeball: the bank funds **~0.1%** of deficit funding; even a
10× spread reaches only ~1%. **No spread value fixes this** — it is a scale
mismatch, not a tuning miss. The bank is a rounding error on its own economy.

The naive reading of "route the polity's money flow through the bank" is to levy
or intermediate every receipt site. This design rejects that (§7). The sim's
dominant monetary flow is already a single chokepoint: `IssueSovereignCredit`
(`Phases.cs:691`) backstops **every** deficit in the sim, and today it is a
free-floating fiat mint belonging to no actor. Making that one operation a bank
operation moves the bank from ~0.1% to ~100% of deficit funding **without
touching a single receipt site**.

## 2. What the Bank becomes

A **central bank**: its reserve backs the currency, and it lends to the state.
Not a stabilization fund (a formula with no agency), and not a deposit-taking
intermediary (which would re-base the conservation identity — §7).

`Bank` (`src/Core/Epoch/Bank.cs`) gains an asset side:

| Field | Meaning |
|---|---|
| `Reserve` (existing) | hard reserve — real money, sequestered out of circulating `Supply` |
| `ClaimOnState` (new) | the bank's claim against its polity — **not money** |
| `CumulativeLentToState` (new) | running total ever lent (level, never reset) |
| `CumulativeRetired` (new) | running total of principal ever destroyed on repayment |

**`ClaimOnState` is a claim, not a holder.** It never enters `Currency.Supply`,
is never walked by `SupplyOps`, and never appears in the residual's balance side.
This follows the precedent already documented at `MetricsOps.cs:24`: *"LoanPrincipal
rides beside the classes as a claim, not a holder: loans move credits between
ledgers, the principal is memory."* Same class of object, same treatment.

## 3. Lending replaces minting

`IssueSovereignCredit` becomes `Bank.LendToState`. The money creation is
**identical to today's**; what changes is that a creditor is booked:

```
Supply              += m
CumulativeFiatIssued += m      (unchanged — same counter, same magnitude)
ClaimOnState        += m       (new: the bank now holds a claim)
CumulativeLentToState += m
```

**ME's cap and floor are untouched.** The issuance cap stays
`SovereignIssuanceRate × max(0, Receipts)`; the backstop remains **absolute** and
a polity is **never** cut off for want of reserve. This is non-negotiable: ME's
spiral cure depends on the lender-of-last-resort floor, and any reserve *gate*
re-lights precisely the spiral ME was built to cure (§5, §7).

Stage 1 of `FundDeficit` (the existing ratio-capped `Reserve → Credits` draw,
`Phases.cs:652`) is **retained unchanged** and still runs first. It is a
*transfer* of hard reserve; `LendToState` is the *creation* backstop behind it.
The two-stage shape CU-2 shipped survives intact — only stage 2 gains a creditor.

## 4. Servicing: surplus-only, never capitalizing

A new `ServiceSovereignClaim` pass runs **after** `FundDeficit` in the Allocation
phase (so it sees the true end-of-epoch balance, the same discipline that put
`IssueSovereignCredit` after `Borrow`).

```
if (pr.Credits > 0 && bank.ClaimOnState > 0):
    years     = Config.Sim.YearsPerEpoch
    # BOTH rates are per-WORLD-YEAR and scaled to the epoch's length (§4a) —
    # a 25-year step must service exactly what twenty-five 1-year steps would
    share     = 1 − (1 − ClaimServicingSharePerYear) ^ years
    # the servicing budget is computed ONCE, before any mutation of Credits —
    # re-reading `Credits × share` after the interest debit would silently
    # shrink the principal budget (the compound-assignment trap already
    # documented at Phases.cs:444, where it was a real bug)
    budget    = Credits × share
    interest  = min(ClaimOnState × SovereignClaimInterestRatePerYear × years,
                    budget)
    Credits  -= interest;  Reserve += interest          # internal, neutral
    principal = min(ClaimOnState, budget − interest)
    Credits  -= principal;  ClaimOnState −= principal
    CumulativeFiatRetired += principal                  # money DESTROYED
    CumulativeRetired     += principal
```

`share ∈ [0,1)` so `budget ≤ Credits`, and `interest + principal ≤ budget`, so
`Credits` is provably still positive after servicing — rule 1 holds by
construction, not by a guard.

### §4a. Time, not ticks (amendment, 2026-07-16)

**This section amends the original §4, which was wrong.** It charged a fraction
of a treasury *stock* once *per epoch*, making servicing intensity scale as
1/`YearsPerEpoch` — a violation of this project's P7 / "time, not ticks"
principle (durations and rates are world-time state, never step counts).

Caught during Task 5 implementation by a clock-invariance probe at world-year
450: total treasury is clock-invariant on the pre-servicing branch (4514.8 at
25y/epoch vs 4515.6 at 1y/epoch) but diverged **4×** with the unscaled pass
(6879.8 vs 1704.4). Every other stock-fraction operation in the sim already
year-scales — `DecayIdlePools` (`Phases.cs:636`), `StockpileDecayPerYear`,
`ConditionDecayPerYear`. The original §4 simply did not consider epoch length.

The fix follows `DecayIdlePools`'s exact precedent, whose doc states the
invariant: *"Compounded per world-year like StockpileDecayPerYear (P7): a
25-year step recirculates exactly what twenty-five 1-year steps would."*

- **The servicing share compounds** per world-year:
  `share = 1 − (1 − ClaimServicingSharePerYear)^years`. Compounding is correct
  here because the share applies to a *stock that the same operation depletes* —
  identical in form to idle-pool decay.
- **Interest is LINEAR in years**, not compounded:
  `ClaimOnState × SovereignClaimInterestRatePerYear × years`. This is not an
  oversight — **rule 2 forbids compounding**. The claim does not grow between
  world-years within an epoch (unpaid interest is discarded, never accrued), so
  each world-year accrues interest on the same principal. Compounding the
  interest here would smuggle in exactly the growth term rule 2 exists to
  forbid, and would reintroduce the spiral risk the mechanism is designed
  around.

Both hard rules survive **structurally**: `share < 1` keeps `budget ≤ Credits`
for any epoch length, and interest remains bounded by `budget`.

**Knob renames** (repo convention is an explicit `PerYear` suffix —
`StockpileDecayPerYear`, `PoolIdleDecayPerYear`, `ConditionDecayPerYear`):

| Was (per-epoch) | Now (per-world-year) | Default | At 25y/epoch |
|---|---|---|---|
| `ClaimServicingShare` = 0.25 | `ClaimServicingSharePerYear` | **0.01** | share ≈ 0.222 |
| `SovereignClaimInterestRate` = 0.02 | `SovereignClaimInterestRatePerYear` | **0.001** | 0.025 × claim |

The per-year defaults are calibrated to reproduce the original §4 intent at the
default `YearsPerEpoch = 25` (0.222 ≈ the intended quarter-of-surplus; 0.025 ≈
the intended 2%), so this amendment is a clock-invariance fix rather than a
behavior change at default settings. Both remain placeholders for the
economic-balance tuning pass.

**This must land before the §6 acceptance sweep** — otherwise every swept result
is `YearsPerEpoch`-dependent and the tuning claims are meaningless.

Two hard rules, both load-bearing against SH's structural debt-spiral finding:

1. **Surplus-only, never forced.** A polity with `Credits <= 0` services nothing.
   It borrows more. Servicing never pushes a treasury negative — every term is
   bounded by a share of the *positive* balance.
2. **Interest never capitalizes.** Unpaid interest is **discarded, not accrued**.
   A permanently broke polity simply never pays, and its claim never grows.
   `ClaimOnState` grows **only** by new lending.

Rule 2 is what makes this structurally spiral-proof: **there is no compounding
term anywhere in the mechanism.** SH diagnosed the debt spiral as structural and
root-caused it to compounding interest (cf. `LoanCapitalizationCeiling`, the
ceiling loans needed precisely because they *do* capitalize). This claim needs no
such ceiling because it cannot compound by construction.

**Interest is not a spiral risk but it is real income**: it flows `Credits →
Reserve`, giving the reserve its first inflow proportional to the real economy —
the actual closure of §1's gap.

## 5. Backing feeds FX — no gate

Reserve must *mean* something, but it cannot gate lending (§3). CU-1/CU-2 already
built the coupling to reuse: `FxOps.RecomputeRates` (`FxOps.cs:59`) computes a
quantity-theory density. Today `Reserve` affects the rate only **indirectly**, by
being sequestered out of `Supply`; it appears in no term.

Unbacked sovereign debt weighs on a currency exactly like excess supply — same
units, same direction — so it enters the existing form as supply-equivalent money:

```
unbacked       = max(0, ClaimOnState − Reserve)
effectiveMoney = Supply + FxBackingSensitivity × unbacked
density        = effectiveMoney / max(Receipts, FxReceiptsFloor)
NumeraireRate  = 1 / (1 + FxSensitivity × density)
```

Properties preserved from CU-1: strictly positive, strictly decreasing in
density, never zero or negative (`ConvertCurrency` stays safe). `unbacked` is
clamped at 0, so a fully-backed bank is unaffected.

Reserve now enters the rate **directly** (it offsets the claim) *in addition* to
its existing sequestration effect. The discipline is endogenous and visible: a
bank whose claim book dwarfs its reserve watches its own currency slide, raising
real import costs and shrinking its people's wealth. No gate, no new failure mode,
no new machinery.

**`FxBackingSensitivity = 0` reproduces CU-2 byte-identically** — a safe landing
and a clean A/B at the eyeball.

## 6. Conservation & determinism

The per-currency identity gains exactly one term:

```
Supply + Reserve == endowment + CumulativeFiatIssued + CumulativeSteadyIssuance
                    + CumulativeConvertedIn − CumulativeConvertedOut
                    − CumulativeFiatRetired
```

Every flow checked against it:

- **Lending** (`Supply += m`, `CumulativeFiatIssued += m`): both sides move by
  `m`. `ClaimOnState` is not money and does not appear. ✓
- **Interest** (`Supply −= i` via Credits, `Reserve += i`): internal to one
  currency, `Supply + Reserve` unchanged, no counter moves. ✓
- **Principal repayment** (`Supply −= p`, `CumulativeFiatRetired += p`): both
  sides move by `p`. ✓ `ClaimOnState −= p` is not money. ✓
- **Reserve-funded deficit** (CU-2, unchanged): internal transfer. ✓

**The residual is a DELTA form, not an absolute** (`MetricsOps.cs:195`): it diffs
levels against the same currency's baseline row from last epoch. Therefore
`CumulativeFiatRetired` **must** be added to `CurrencyResidualRow` and carried by
the baseline — exactly as `Reserve` was in CU-2. Omitting it makes every epoch
with a repayment read as a false leak. This is the single easiest way to get this
slice wrong.

Determinism: all claim/reserve motion is by formula in fixed id order (polities
walk actor-id order, currencies/banks walk registry order, P6). No hash rolls, no
floating iteration order. The FX rate stays a pure formula reading the prior
epoch's ending state.

**Acceptance instrument (non-negotiable):** the 32-run committed sweep
(`dotnet run --project src/Inspector -c Release -- sweep
docs/superpowers/plans/2026-07-12-debt-diagnosis-experiment.json`), worst
per-currency `Money.ConservationResidual` at ~1e-9 abs / ~1e-16 relative. This
slice moves money across the identity in two new ways and adds a counter to the
delta form — CU-1's hard-won lesson (clean seed-42 units, sweep found leaks 5–9
orders over tolerance) applies with full force. Budget for running it more than
once.

## 7. Rejected alternatives

- **Receipt levy / sovereign wealth fund** — a fraction of every receipt
  sequestered `Credits → Reserve`. Conservation-trivial and it does close the
  scale gap, but the bank becomes a formula with no agency, and it touches every
  receipt site for a result the lending relationship delivers at one chokepoint.
- **True intermediary (deposits as claims)** — `pr.Credits` becomes a claim on the
  bank holding the asset. The most economically real option, and rejected on
  risk: it re-bases the per-currency conservation identity that CU-1 and CU-2
  spent entire slices earning, and touches every money site in the sim. Under it
  a coin is counted twice unless the identity is rewritten.
- **Reserve as a gate (`cap = multiplier × Reserve`)** — re-lights ME's spiral for
  any thin-reserve polity. Rejected outright.
- **Soft cap (reserve modulates issuance rate above a floor)** — safe, but a new
  knob and a floor needing ensemble validation, for discipline the FX coupling
  already delivers endogenously.
- **Two-tier / penalty interest beyond backing** — penalty interest on a stressed
  polity is exactly the spiral pressure §4 exists to avoid.
- **Perpetual claim (never repaid)** — zero spiral risk, but no money sink, and
  the claim is then just a renamed `CumulativeFiatIssued`.

## 8. Sequencing & forward constraints

Sequenced before CU-3/CU-4 by user decision. The orchestrator's recommendation was
CU-3-first (the currency-consolidation half is orthogonal to bank peripherality,
and the reserve-merge *rule* is identical either way); the user chose to land the
flow redesign first so CU-3 merges banks that genuinely intermediate.

- **CU-3** (federation-triggered currency consolidation) now merges **reserves and
  claim books** — strictly richer than merging two near-empty reserves. It must
  decide: do two claim books sum? Does an absorbed polity's claim to its own
  (now-retired) bank survive, transfer to the survivor's bank, or extinguish? An
  extinguished claim is money already in circulation with its creditor gone —
  conservation-neutral (the claim is not money) but monetarily consequential (the
  sink disappears). CU-3's existing kickoff prompt
  (`2026-07-16-slice-cu3-kickoff-prompt.md`) stays valid and is re-chained at this
  slice's wrap-up.
- **CU-4** (bank strength → federation generation) gets `Reserve ÷ ClaimOnState`
  as a genuine credibility measure, alongside CU-2's reserve depth / spread intake
  / FX track record. This is the signal follow-up #1 said CU-4 needed and lacked.
- **CU-2 follow-up #2** (spread/ratio tuning, deferred as "moot until #1 lands"):
  no longer moot after this slice. Still deferred to a dedicated economic-balance
  pass — this slice adds three more knobs to that pass's surface.
- **CU-2 follow-up #4** (observability): whole-sim `MoneyRow.Supply` excludes
  `Reserve`. Unchanged and still not a defect, but this slice makes a
  `MoneyRow.Reserves` + `MoneyRow.Claims` pair more clearly worth having.

## 9. Config, REPL, serialization, tests

**Knobs** (`EconomyKnobs`) — **every one registered in `KnobRegistry.cs`**; CU-2's
review finding 1 was an unregistered knob silently reverting on reload, breaking
determinism *and* blocking the tuning sweep:

| Knob | Default | Note |
|---|---|---|
| `SovereignClaimInterestRatePerYear` | 0.001 | interest on the claim book; **linear** in years, cannot compound (§4, §4a) |
| `ClaimServicingSharePerYear` | 0.01 | max share of a positive balance spent servicing; **compounds** per world-year (§4a) |
| `FxBackingSensitivity` | 0 → tuned | 0 = CU-2 byte-identical; raised once the sweep is green |

All three are placeholders flagged for the economic-balance pass, not swept
values. The first two are per-**world-year** rates scaled to epoch length per
§4a (P7 / "time, not ticks") — they are NOT per-epoch. An earlier draft of this
spec had them per-epoch; see §4a for why that was wrong and how it was caught.

**Serialization:** three new `Bank` fields + one `Currency` counter
(`CumulativeFiatRetired`). Extend the `BANK` and `CURRENCY` lines
(`ArtifactSerializer.cs:254`, `:1284`), bump the version tuple. Subject to the
byte-identity gate.

**REPL:** extend the currency line to show the claim book, the backing ratio
(`Reserve ÷ ClaimOnState`), and retired-to-date — so the lending relationship and
the money sink are both visible at the eyeball checkpoint.

**Tests:** TDD throughout. The hex-tier (Phase-1 generation) suite never breaks;
determinism byte-identity holds; new goldens frozen **once** at slice end.
Specific coverage: claim non-negativity across a full history; servicing never
drives `Credits` negative; interest never capitalizes on an insolvent polity; the
residual holds across a repayment epoch (the delta-form trap, §6);
`FxBackingSensitivity = 0` reproduces CU-2 exactly.
