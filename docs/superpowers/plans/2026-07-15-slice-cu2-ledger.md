# Slice CU-2 — Bank actor: task ledger

> **For agentic workers:** implement task-by-task via
> `superpowers:subagent-driven-development` (CLAUDE.md pins slice
> implementation to subagents — no inline edits by the slice session). Steps
> use checkbox (`- [ ]`) syntax; this ledger is the resumability record if the
> session dies. Design spec:
> `docs/superpowers/specs/2026-07-15-cu2-bank-actor-design.md` — the design is
> the spec; deviations amend it in-branch, flagged.

**Goal:** a first-class `Bank` per `Currency` that owns minting authority and
exchange management — capitalized by a conversion spread sequestered out of
circulating `Supply`, funding polity deficits from its reserve first, with ME's
bounded fiat mint as lender-of-last-resort backstop.

**Architecture:** `Bank` is a state record keyed by currency id (not an actor,
no controller), minted 1:1 in `SimState.FoundCurrency`. A `SettleConversion`
seam skims the spread on every real cross-currency money movement into a
currency, crediting that currency's Bank reserve and reducing the recipient.
`SupplyOps` keeps `Currency.Supply` circulating-only (so reserve accumulation
strengthens FX); `MetricsOps` adds the live reserve to the conservation
residual's balance side. Deficit issuance draws the reserve down (a transfer),
then falls back to the bounded fiat mint.

**Branch:** `slice-cu2-bank-actor` (worktree `.worktrees/slice-cu2-bank-actor`,
branched from main 81c03c6). L2 is parallel — never share a checkout.

## Global constraints (every task inherits these)

- Build/test: `dotnet test StarSystemGeneration.sln` green after each task.
- **Hex-tier (Phase-1 generation) suite never breaks.**
- Determinism byte-identity for same config; new goldens frozen once, at slice
  end (red window inside the slice is fine).
- Conservation is proven by the **full 32-run committed sweep**, not seed-42
  unit tests (CU-1's hard-won lesson) — run it after Task 5 and again after
  any later change touching money movement, more than once.
- Fixed id-order iteration everywhere; no hash rolls in any Bank/reserve/FX
  path (pure formulas only).
- Every new `src/Core` file gets a two-line `.meta` with a fresh guid.
- `src/Core` is netstandard2.1 (no Unity deps); Inspector is net8.0.

## Conversion-site inventory (authoritative — from the CU-1 audit)

**Exchange sites — get the spread (route through `SettleConversion`):**
`FleetOps.cs:452`, `ProjectOps.cs:463` (construction WAGES, reduce-recipient —
not a bid), `MarketEngine.cs:518`, `Phases.cs:1790`, `PolityRecord.cs:115` (Deposit),
<!-- RECONCILED (Task 8 review finding 6): MarketEngine.cs:1047 is the order-post
REFUND — moved to Exempt below (un-posting own escrow; FX charged once at the
gross-up post). Ordinary order *cancels* still skim their return leg. -->

`PolityRecord.cs:136` (Withdraw), `Corporation.cs:180`/`:189` (wallet
draw-down), `CorporationOps.cs:1058`.

**Exempt — sovereign re-denomination + own-escrow un-posting, no skim:**
`SimState.ConvertPortHoldings` (`:352`/`:359`/`:368`, port-ownership-change
re-denomination), `FederationOps.cs:389`/`441` + the absorbed-treasury/faction-
wealth transfers now via `PolityRecord.DepositExempt` (Task 4f), `GraduationOps`
seed-treasury + pools (`:220` and the `DepositExempt` legs), and
`MarketEngine.cs:1047` (order-post REFUND — un-posting a funder's own escrow;
Task 4b decision, reconciled here per Task 8 finding 6).

**The spread invariant (the whole slice turns on this):** skim exactly **once
per real cross-currency conversion**, charged by the **destination** currency's
Bank, deducted from the converted result so the recipient receives the net. A
missed exchange site starves that Bank; a double-charged conversion leaks. The
reserve-aware per-currency residual + the 32-run sweep is the completeness gate.

---

### Task 1: The `Bank` record, registry, and 1:1 founding

**Model:** Sonnet.

**Files:**
- Create: `src/Core/Epoch/Bank.cs` (+ `.meta`, fresh guid)
- Modify: `src/Core/Epoch/SimState.cs` (registry + `BankOf` + `FoundCurrency`
  `:216`)
- Test: `tests/Core.Tests/Epoch/` (new `BankFoundingTests.cs` or nearest
  currency test file)

**Produces (later tasks rely on these exact names):**
- `Bank` class: `int CurrencyId { get; }`, `double Reserve { get; set; }`
  (default 0), `double CumulativeSpreadIntake { get; set; }`,
  `double CumulativeReserveFunded { get; set; }`; ctor `Bank(int currencyId)`.
- `SimState.Banks` → `List<Bank>` (dense, id == CurrencyId, parallel to
  `Currencies`).
- `SimState.BankOf(int currencyId)` → `Bank` (dense-index + scan fallback,
  mirroring `CurrencyOf` at `SimState.cs:177`).

**Steps:**
- [ ] Write failing test: `FoundCurrency` creates a `Bank` keyed to the new
  currency id, `Reserve == 0`; `Banks` stays dense-parallel to `Currencies`;
  `BankOf(id)` resolves the same object.
- [ ] Run test — expect FAIL (no `Bank` type).
- [ ] Implement `Bank.cs`; add `Banks` registry + `BankOf` to `SimState`; in
  `FoundCurrency` add `Banks.Add(new Bank(id));` right after `Currencies.Add`.
  Add the `.meta`.
- [ ] Run test — expect PASS. Run full `dotnet test` — green.
- [ ] Commit.

---

### Task 2: Serialize the Bank registry (byte-identity honest early)

**Model:** Sonnet.

**Files:** Modify `src/Core/Epoch/ArtifactSerializer.cs` — Layers tuple `:26`
(add `("banks", 1)` after `("bodyresources", 1)` `:35`); a `BANK` write block
(mirror the `CURRENCY` write at `:254`); a `BANK` case in the load switch
(mirror `:1254`, with the same out-of-order id guard). Test:
`tests/Core.Tests/` serializer round-trip suite.

**Why now (not later):** HANDOFF records that CU-1/L's `BodyResources` broke
save→reload determinism because serialization lagged the state. Wire it the
moment the state exists, even though reserves are still 0 in every run until
Task 4 feeds them.

**Steps:**
- [ ] Write failing test: a `SimState` whose banks carry nonzero `Reserve` /
  cumulative counters round-trips byte-identically (`ToText` → parse → `ToText`
  equal), and a fresh-vs-reloaded determinism check matches.
- [ ] Run — expect FAIL (banks not serialized).
- [ ] Implement the `("banks", 1)` layer, `BANK` write (id, `R(Reserve)`,
  `R(CumulativeSpreadIntake)`, `R(CumulativeReserveFunded)`) after the
  bodyresources block, and the load case appending to `state.Banks`.
- [ ] Run — expect PASS. Full `dotnet test` green; hex-tier suite intact.
- [ ] Commit.

---

### Task 3: `SettleConversion` primitive, config knobs, reserve-aware residual

**Model:** Opus (conservation/determinism primitive + the residual instrument).

**Files:**
- Modify `src/Core/Epoch/EpochSimConfig.cs` — `EconomyKnobs` (near `:875`).
- Modify `src/Core/Epoch/SimState.cs` — add `SettleConversion`.
- Modify `src/Core/Epoch/Health/MetricsOps.cs` — residual at `:183`;
  `CurrencyResidualRow` (near `:47`) gains a `Reserve` field.
- Test: conservation + currency test suites.

**Produces:**
- `EconomyKnobs.ConversionSpread` (default `0.005`), `EconomyKnobs.
  IssuanceReserveRatio` (default `0.5` — per-epoch cap on how much of a Bank's
  reserve one deficit may draw). Doc comments; both flagged for the post-impl
  tuning pass.
- `SimState.SettleConversion(int fromCurrencyId, double outAmount,
  int toCurrencyId, double inAmount)` → `double net`. Contract: performs the
  same counter update as `RecordConversion` (records the **full** `inAmount` as
  `ConvertedIn`, `outAmount` as `ConvertedOut`); if both ids ≥ 0 and differ,
  `spread = inAmount * Config.Economy.ConversionSpread`,
  `BankOf(toCurrencyId).Reserve += spread`, `bank.CumulativeSpreadIntake +=
  spread`, returns `inAmount - spread`. Same-currency or pre-genesis (id < 0):
  no skim, returns `inAmount` unchanged (byte-identical to today).
  `RecordConversion` stays unchanged — the exempt sites keep using it.

**Reserve-aware residual (the conservation contract):** `SupplyOps` is
**unchanged** — `Currency.Supply` stays circulating-only (excludes reserve) so
`FxOps` (density = Supply/output) tightens as reserves accumulate. In
`MetricsOps`, the per-currency residual balance side becomes `cur.Supply +
BankOf(cur.Id).Reserve`; `CurrencyResidualRow` carries `Reserve` so the
baseline row captures it and the delta is correct:
`residual = (Supply+Reserve) − (baseSupply+baseReserve) − ΔFiat − ΔSteady −
ΔConvertedIn + ΔConvertedOut`.

**Steps:**
- [ ] Write failing tests: (a) `SettleConversion` across differing currencies
  skims `spread×inAmount` to the target Bank, returns the net, records full
  converted amounts; (b) same-currency / pre-genesis returns full, no skim, no
  reserve change; (c) a hand-built state with a nonzero reserve nets to
  residual ≈ 0 under the new `Supply+Reserve` formula (and would show a false
  leak under the old formula — the guard against regressing the residual).
- [ ] Run — expect FAIL.
- [ ] Implement knobs, `SettleConversion`, `CurrencyResidualRow.Reserve`, and
  the residual formula.
- [ ] Run — expect PASS. Full `dotnet test` green.
- [ ] Commit.

---

### Task 4: Wire the exchange sites through `SettleConversion` (the audit)

**Model:** Opus (conservation, spans multiple `src/Core` subsystems). This is
CU-1 Task 8 territory — split into three reviewable sub-waves, each ending
green; a full sweep follows in Task 7.

**The transform, per exchange site:** replace `in = ConvertCurrency(out,from,to)
; <credit recipient `in`>; RecordConversion(from,out,to,in)` with
`in = ConvertCurrency(out,from,to); net = SettleConversion(from,out,to,in);
<credit recipient `net`>`. The recipient/destination holder receives `net`;
the Bank keeps the skim. Never skim twice for one conversion (a payer-Withdraw
that converts A→B is one skim, charged by B; a paired payee-Deposit that
converts B→C is a *second, distinct* conversion, legitimately skimmed by C).

**Task 4a — ledger-mediated sites.** `PolityRecord.Deposit` (`:106-`, skim on
the converted `own`; credit `own−skim`; return net so the `Receipts` mirror is
honest), `PolityRecord.Withdraw` (`:127-`), `Corporation.Withdraw`
(`Corporation.cs:146-`, the two internal draw-down conversions `:180`/`:189`).
Trace each flow so a Withdraw+Deposit pair skims once per real conversion.
- [ ] Failing tests per ledger method (foreign-currency deposit/withdraw skims
  once, net credited, same-currency unchanged) → implement → green → commit.

**Task 4b — market/order/project sites.** `MarketEngine.cs:518` (buy-order
escrow post), `MarketEngine.cs:1047` (order refund/cancel), `ProjectOps.cs:463`
(project bid).
- [ ] Failing tests → implement → green → commit.

**Task 4c — remaining direct sites.** `FleetOps.cs:452` (foreign fleet upkeep),
`Phases.cs:1790` (migration: household wealth crossing to a different-currency
polity — recipient is the destination segment `home.Wealth`, a genuine
exchange), `CorporationOps.cs:1058`.
- [ ] Failing tests → implement → green → commit.
- [ ] After 4c: seed-42 per-currency residual still ≈ 0 (the local gate; the
  real gate is Task 7's sweep).

---

### Task 5: Reserve-funded issuance + bounded fiat backstop

**Model:** Opus (money movement, the regime-defining change).

**Files:** Modify `src/Core/Epoch/Phases.cs` — the issuance loop at `:506-508`
and `IssueSovereignCredit` at `:650`.

**Mechanism (two stages, replacing the bare `IssueSovereignCredit` call):** for
each entered polity with `pr.Credits < 0`:
1. **Reserve funding (transfer).** `bank = BankOf(pr.CurrencyId)`;
   `draw = min(-pr.Credits, IssuanceReserveRatio * bank.Reserve, bank.Reserve)`;
   if `draw > 0`: `bank.Reserve -= draw`, `pr.Credits += draw`,
   `bank.CumulativeReserveFunded += draw`. **No `CumulativeFiatIssued`
   change** — it is a transfer (`Reserve → Supply`), conservation-neutral under
   `Supply+Reserve`.
2. **Fiat backstop.** If still `pr.Credits < 0`, call the existing
   `IssueSovereignCredit(state, pr)` unchanged (bounded mint, grows Supply +
   `CumulativeFiatIssued`).

Steady issuance (`:418`) stays a plain fiat mint — untouched; it is money-base
growth, not deficit backstopping (design §5).

**Steps:**
- [ ] Failing tests: a polity with a well-capitalized Bank covers its deficit
  from reserve with **no** `CumulativeFiatIssued` growth (reserve drops,
  `Supply+Reserve` conserved); a polity with an empty Bank falls through to the
  bounded fiat mint (`NegativeTreasuries` still breathes); the per-epoch draw
  cap (`IssuanceReserveRatio`) bounds a single draw.
- [ ] Run — expect FAIL → implement the two-stage loop → PASS.
- [ ] Full `dotnet test` green.
- [ ] Commit.

---

### Task 6: REPL surface for banks/reserves

**Model:** Sonnet.

**Files:** Modify `src/Inspector/InteriorView.cs` (the currency inspection
surface). Extend it to show, per currency, its Bank: `Reserve`, cumulative
spread intake, cumulative reserve-funded, and the effective this-epoch issuance
split (reserve-funded vs backstop-minted) — so the reserve dynamics are visible
at the eyeball checkpoint.

**Steps:**
- [ ] Add the bank rows to the currency view; run the REPL manually
  (`printf 'cmd\n' | dotnet run --project src/Inspector` via bash) and confirm
  reserves render on a stepped history.
- [ ] Light assertion test if the view has a testable formatter; else eyeball.
- [ ] Full `dotnet test` green. Commit.

---

### Task 7: Sweep acceptance, golden freeze, emergent-history re-tune

**Model:** Opus (tuning judgment) — the slice session runs the sweep.

- [ ] Run the full **32-run committed sweep** from source. Confirm worst
  per-currency residual is in CU-1's tolerance class (~1e-7 relative), banks
  accumulate reserve, FX rates diverge, `NegativeTreasuries` breathes, loan
  principal stays bounded. Re-run at least once more after any fix.
- [ ] Re-tune the seed-42 emergent-history snapshots that legitimately shift
  (war/treaty/relations/fine-tick) — each re-tune rests on a verified real
  mechanism, disclosed in this ledger, never a threshold loosened to green.
- [ ] Freeze new goldens once (regenerate; determinism byte-identity holds).
- [ ] **REPL eyeball checkpoint** — user runs/views the bank surface and
  accepts (the taste gate).

---

### Task 8: Fresh-eyes whole-branch review + fix wave

**Model:** Fable (pinned, per CLAUDE.md).

- [ ] One whole-branch review subagent (fable) — conservation completeness
  (every exchange site skims once, no double-charge, no missed site), the
  residual/reserve accounting, determinism, the issuance two-stage, spec
  fidelity. Verify each finding against source before accepting (a
  "pre-existing/out-of-scope" claim needs commit-bisect proof — CU-1 Task 7
  lesson).
- [ ] One fix wave. Re-run the 32-run sweep against the fixed tip.
- [ ] `git log main` before merge-out; fold main in first if it moved (L2 is
  parallel). Then the merge decision (user checkpoint).

---

## Deferred / noted (do not build here)

CU-3 (bank mergers at federation consolidation), CU-4 (bank strength →
federation generation). CU-2 leaves `Retired`-currency banks intact and keyed
by currency id so CU-3 has a live object to merge; "strength" candidate
measures (reserve depth, spread-intake rate, FX track record) are made real
here for CU-4 to choose among.
