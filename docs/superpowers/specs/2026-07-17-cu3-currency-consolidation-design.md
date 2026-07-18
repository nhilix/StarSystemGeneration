# Slice CU-3 — federation-triggered currency consolidation

Design date 2026-07-17. Third of the CU chain: CU-1 gave every polity its own
`Currency`; CU-2 gave each currency a `Bank`; **Slice BF** (merged, main `0bdb009`)
made that bank a monetary authority with an **asset side** (`ClaimOnState`,
lending, a money sink, FX-backing). CU-3 replaces CU-1's instant-1:1
forced-conversion **stub** at the absorption seam with a real consolidation of two
banks' **balance sheets**.

## 1. What CU-3 owns

When one polity absorbs another (federation fusion, vassal absorption, war
submission/annexation — all reach `FederationOps.MergeInto` + the `Retire`
chokepoint), the absorbed polity's currency retires. Today `MergeInto` moves the
treasury, investment pools, loans, ports, and port-resolved holdings — but touches
**neither the absorbed `Bank` nor its balance sheet**. So today:

- the absorbed bank's **reserve** is *stranded* — real money (sequestered out of
  `Supply`, counted in the per-currency residual) locked forever in a dead
  currency's bank;
- after BF, the absorbed bank's **claim book** (`ClaimOnState`) is orphaned debt.

CU-3 consolidates that balance sheet into the survivor's bank, conservingly.

## 2. Decisions (all taken in the brainstorm)

- **Instant, not gradual** — a one-shot merge at the seam, symmetric with how
  treasury/pools/loans already consolidate. No transition state, no converging
  peg. The absorbed currency retires immediately (as today). Rationale: the coarse
  clock (25y/epoch default) would collapse any realistic transition window to 1–2
  steps, and a stateful transition multiplies conservation surface for texture the
  granularity can't show.
- **Reserve → survivor's RESERVE** (stays sequestered) — the union pools its
  members' monetary backing; reserve depth accumulates through federation,
  deepening the survivor's FX strength (BF's backing coupling). Parallels the
  treasury transferring whole to the successor.
- **Claim book → survivor's CLAIM book** (transfer/inherit) — the survivor's bank
  takes the claim on its enlarged self and services it going forward, so **BF's
  money sink is preserved**. NOT the loan "internal debt cancels" case: a
  bank-on-its-own-state claim does not net to zero when both sides become the
  survivor — the union genuinely created that money and has not retired it.
- **Corp dangling holdings resolve lazily** — CU-3 does not touch corp wallets
  (faithful to CU-1's corp-wallet model; conservation-clean; the only non-port
  holder of the absorbed currency).
- **Fusion-only** — `GraduationOps`/schism untouched; a schism child keeps its
  fresh empty bank (BF's "earn your backstop" emergence). The federation
  union-genesis path is handled for free (see §5).

## 3. The mechanism (inside `FederationOps.MergeInto`)

A new block, placed **after** the treasury + investment-pool transfer
(`FederationOps.cs` ~:419–445, the `DepositExempt`/pool-convert region) and
**before** the loan reissue, operating on the two banks
`state.BankOf(from.CurrencyId)` and `state.BankOf(into.CurrencyId)`.

### 3a. Guards (skip the CONSOLIDATION BLOCK if any holds)

```
skip the block when
    from.CurrencyId < 0 || into.CurrencyId < 0   // pre-genesis: no banks
 || from.CurrencyId == into.CurrencyId            // defensive: never for distinct
                                                  // living polities; a self-transfer
                                                  // would double the reserve
```

**Skip only the consolidation block, not the rest of `MergeInto`.** A bare `return`
would also skip the loan reissue that follows — wrap the block in
`if (!skip) { ... }` (or extract a helper and early-`return` inside it). The
implementation uses the guarded-block form.

### 3b. Reserve — money, so convert AND record

```
var fromBank = state.BankOf(from.CurrencyId);
var intoBank = state.BankOf(into.CurrencyId);

double reserveIn = state.ConvertCurrency(fromBank.Reserve,
                                         from.CurrencyId, into.CurrencyId);
state.RecordConversion(from.CurrencyId, fromBank.Reserve,
                       into.CurrencyId, reserveIn);
intoBank.Reserve += reserveIn;
fromBank.Reserve = 0;
```

**Exempt** (no spread skim) — this is re-denomination of the polity's own monetary
backing at the merge, not a market FX trade, exactly like the treasury
(`DepositExempt`) and the pools (`ConvertCurrency` + `RecordConversion`). Use the
plain `ConvertCurrency`, never the skimming `SettleConversion`.

### 3c. Claim — NOT money, so reprice WITHOUT recording

```
double claimIn = state.ConvertCurrency(fromBank.ClaimOnState,
                                       from.CurrencyId, into.CurrencyId);
intoBank.ClaimOnState += claimIn;
fromBank.ClaimOnState = 0;
```

**No `RecordConversion`.** `ClaimOnState` is a claim, not a holder — it never
enters `Supply` and never appears on the conservation residual's balance side
(BF `Bank.cs` doc; the `MetricsOps.cs:24` LoanPrincipal precedent). Recording a
conversion here would falsely tell the per-currency residual that money moved out
of `from` and into `into`, injecting a phantom leak on both sides. This mirrors
**loan-principal reissue** (`FederationOps.cs` ~:483), which likewise `Convert`s
the principal to reprice it but does **not** `RecordConversion`.

> **THIS IS THE SLICE'S CENTRAL CORRECTNESS POINT.** Reserve is money → convert +
> record. Claim is memory → convert (reprice) only. Getting either wrong is a
> conservation bug the sweep will catch — a recorded claim shows as a phantom
> residual on the repayment path; an unrecorded reserve shows as a real leak.

### 3d. Cumulative counters — stay on the drained bank

Only the **live balances** (`Reserve`, `ClaimOnState`) transfer. The historical
cumulative counters (`CumulativeSpreadIntake`, `CumulativeReserveFunded`,
`CumulativeLentToState`, `CumulativeRetired`) **stay on the absorbed bank**:

- `CumulativeRetired` is the bank-side mirror of `Currency.CumulativeFiatRetired`,
  which stays on the retired currency (the residual references it as a historical
  level) — so its bank mirror must stay too, or the mirror desyncs.
- The others are pure observability of the *absorbed* polity's own activity;
  attributing them to the survivor would be a false readout.

**Flagged interaction (verify in implementation):** transferring `ClaimOnState`
without `CumulativeLentToState` means the survivor's outstanding claim can exceed
what it *itself* ever lent (`ClaimOnState > CumulativeLentToState`). This is
truthful for a union (it inherited claims), and both are observability-only (not in
the residual). **Verify no BF test or guard asserts `ClaimOnState ≤
CumulativeLentToState`.** If one does, the correct resolution is to relax it with
this rationale (not to transfer the counter, which would falsely attribute the
absorbed polity's lending to the survivor) — surface it to the user.

### 3e. The drained bank lingers

After the transfer the absorbed bank is an empty husk (`Reserve` 0, `ClaimOnState`
0) still in `state.Banks`, keyed to its currency id — exactly parallel to the
retired `Currency` record lingering in `state.Currencies`. The dense-parallel
registry (banks keyed by currency id) is never compacted; nothing is removed. No
change to `Retire` is needed — it already flips `Currency.Retired`.

## 4. Conservation & determinism

The per-currency residual identity (BF §6) is unchanged; this slice moves a
reserve (money) and a claim (not money) across the seam:

```
Supply + Reserve == endowment + CumulativeFiatIssued + CumulativeSteadyIssuance
                    + CumulativeConvertedIn − CumulativeConvertedOut
                    − CumulativeFiatRetired
```

- **Reserve transfer** — source: `Reserve −= R` (the `Supply + Reserve` side drops
  R) and `CumulativeConvertedOut += R` (the residual's `+ConvertedOut` term rises
  R) → nets 0. Survivor: `Reserve += conv(R)` and `CumulativeConvertedIn +=
  conv(R)` → nets 0. ✓ `ConvertCurrency` is linear, so `conv(R)` is exact.
- **Claim transfer** — `ClaimOnState` is not in the identity on either side;
  moving it perturbs nothing. ✓ (Precisely why it must NOT be recorded.)
- The survivor later retiring the inherited principal is BF's normal sink
  (`Supply −= p`, `CumulativeFiatRetired += p`) in the survivor's currency —
  conserves in the survivor, and it does not matter that the money was originally
  issued in the absorbed currency (the `ConvertedIn` already accounted for the
  money arriving).

**Determinism:** the block runs inside `MergeInto`'s existing deterministic
sequence; banks resolve by currency id, no new iteration order, no hash rolls. The
conversion reads the epoch's frozen `NumeraireRate` table (FxOps runs at epoch
start), exactly like the treasury/pool converts in the same method.

**Acceptance gate (non-negotiable):** the 32-run committed conservation sweep
(`docs/superpowers/plans/2026-07-12-debt-diagnosis-experiment.json`), worst
per-currency `Money.ConservationResidual` at ~1e-9 abs / **~1e-16 relative**
(judge on relative — post-MC nominal supply inflates the absolute; BF holds it at
2.15e-15). This slice moves reserves and claims across the merge seam on every
absorption — a conservation-sensitive change; run the sweep and check the worst
relative residual holds. Federation/absorption fire across the multi-seed history,
so the sweep genuinely exercises this path (unlike a single seed-42 unit test).

## 5. The union-genesis path (handled for free)

Federation of two peers mints a fresh union currency then merges both parents in
(`FederationOps.cs` ~:135): `FoundCurrency(newId)` (fresh currency + empty bank),
then `MergeInto(A, newId)` and `MergeInto(B, newId)`. Each `MergeInto` runs §3, so
the union bank accumulates **both** parents' reserves (converted into the union
currency) and **both** claims. The union owes its own bank the sum of both
founders' sovereign debts, backed by the pool of both reserves — coherent, no
special-casing.

## 6. Constraints carried to CU-4 (noted, not designed)

CU-4 keys federation *generation* off bank/currency-union strength. CU-3 makes the
survivor's post-merge balance sheet real: a union's reserve is the pooled backing
of its members, and its claim book the pooled sovereign debt — so `Reserve ÷
ClaimOnState` (BF's credibility measure) is now meaningful *across a union's whole
history of absorptions*. CU-4 defines which of reserve depth / backing ratio / FX
track record feeds generation; CU-3 only guarantees they consolidate correctly.

## 7. Config, tests, serialization

- **No new knobs.** Consolidation is mechanism, not a tunable — it reuses the
  existing `ConvertCurrency`/`RecordConversion` primitives and the frozen rate
  table. (If the brainstorm's rejected "gradual" path were ever revived it would
  need transition knobs; instant needs none.)
- **No serialization change.** `Bank.Reserve`/`ClaimOnState` and the cumulative
  counters are already serialized (BF); CU-3 only moves values between existing
  live banks. No new fields, no layer-version bump.
- **Tests (TDD):** a federation/absorption merge transfers the reserve (survivor
  `Reserve` rises by the converted amount, source → 0) with the conversion
  recorded; transfers the claim (survivor `ClaimOnState` rises, source → 0)
  **without** a recorded conversion (assert `CumulativeConvertedIn/Out` do NOT move
  for the claim amount); the per-currency residual stays ~0 across a merge epoch;
  the drained bank lingers with its cumulative counters intact; the union-genesis
  path pools both parents. The hex-tier suite never breaks; determinism
  byte-identity holds; the golden re-freezes once at slice end (a merge in the
  seed-42 history will move it). The 32-run sweep is the real gate.
- **REPL:** the existing BF `bank:`/`claims:` surface already renders a survivor's
  post-merge reserve and claim book — no new surface needed, but a merge in a
  driven history is the eyeball (a survivor whose `claims: book` and `reserve`
  jump at an absorption).
