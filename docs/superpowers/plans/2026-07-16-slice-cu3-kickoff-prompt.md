# Slice CU-3 kickoff — federation-triggered currency consolidation

> ## ⚠ AMENDED 2026-07-17 — the sequencing decision is RESOLVED and the scope GREW
>
> This kickoff was written before Slice BF existed. **The big opening question
> below — "is the bank-reserve-flow redesign a prerequisite?" — has been answered:
> YES, and it is DONE.** That redesign became **Slice BF (merged, main `0bdb009`)**
> after the user chose to fix the prerequisite first. Do NOT re-litigate the
> sequencing; do NOT treat CU-2's banks as peripheral (BF fixed exactly that).
>
> **What BF changed, and why CU-3's scope is now bigger:** the per-currency `Bank`
> gained an **asset side** — `Bank.ClaimOnState` (the bank's outstanding claim on
> its own polity, from `LendToState`), plus `CumulativeLentToState`,
> `CumulativeRetired`, and `Currency.CumulativeFiatRetired` (the money sink). So a
> currency merger no longer consolidates two near-empty reserves — it consolidates
> two **balance sheets** (reserve AND claim book). The new open questions CU-3 must
> answer (BF design §8, `docs/superpowers/specs/2026-07-16-bf-bank-flow-design.md`):
> - When polity A absorbs polity B, what happens to **B's claim book** — the debt
>   B's now-retired bank held against B? Does it transfer to A's bank (A inherits
>   the claim on its new territory), extinguish, or fold into the merged reserve?
>   **An extinguished claim is conservation-neutral** (a claim isn't money — verify
>   against BF's §6 identity) **but monetarily consequential** (the sink that claim
>   represented disappears with it).
> - How do two **reserves** combine (the original CU-3 question — still open)?
> - Does the **FX-backing** relationship (BF §5: unbacked claim weighs on the rate)
>   survive the merge sensibly — the survivor's backing ratio must recompute over
>   the combined balance sheet.
>
> **Read BEFORE the reading list below:** BF's design spec (esp. §5, §6, §8) and
> the merged code — `src/Core/Epoch/Bank.cs` (the asset-side fields),
> `Phases.cs` `ServiceSovereignClaim`/`LendToState`, `FederationOps.MergeInto`
> (the seam — note it already force-converts treasury+pools; CU-3 adds the
> bank-balance-sheet consolidation beside that). The conservation instrument is
> the 32-run sweep; BF holds it at ~2e-15 relative — any consolidation moving
> reserves/claims across currencies must keep it there.
>
> The body below is the ORIGINAL framing — still useful for the currency/reserve/
> dangling-holdings questions, but read every "CU-2 banks are peripheral / weigh
> the sequencing" note as already-answered history.

---

You are opening the third slice of the CU chain. CU-1 gave every polity its own
`Currency`; CU-2 gave each currency a first-class `Bank` (reserve, spread,
reserve-funded issuance); **Slice BF made that bank a real monetary authority
(lending, a money sink, FX-backing) — see the amendment banner above.** CU-3's
job: replace CU-1's blunt forced-conversion-at-absorption **stub** with a real
mechanic — when polities federate or one absorbs another, their currencies **and
their banks' balance sheets (reserve + claim book)** consolidate, plausibly
gradually rather than instantly. Read first, then brainstorm; do NOT skip to
design.

## Read first, in order

1. `docs/HANDOFF.md` §"Slice CU-2 — the Bank actor (closed)" — the full mechanism
   as it shipped, and especially the **five filed follow-ups**. Follow-up #1
   (the bank-reserve-flow-gap) is load-bearing for this slice's sequencing — see
   below.
2. `docs/superpowers/specs/2026-07-15-cu2-bank-actor-design.md` §"Constraints
   carried to CU-3 / CU-4" and §"Forward roadmap" — the shape CU-3 was scoped at:
   two banks' reserves merge (likely gradual), the absorbed bank's regulatory
   role transferring to the survivor.
3. `docs/superpowers/specs/2026-07-14-cu-currency-fx-design.md` — CU-1's design;
   find where it names the **absorption stub** (the "colony-purse 1:1 nominal
   re-denomination at absorption" and the forced-conversion boundary CU-3 owns).
4. Code — the real surface:
   - `src/Core/Epoch/Bank.cs` (`CurrencyId`, `Reserve`, `CumulativeSpreadIntake`,
     `CumulativeReserveFunded`); `SimState.BankOf`, `SimState.Banks`,
     `SimState.FoundCurrency` (mints currency + bank 1:1 — is there a symmetric
     RETIRE/MERGE path? there is not yet; CU-3 builds it).
   - `src/Core/Epoch/Interpolity/FederationOps.cs` `MergeInto` — the absorption
     seam. Note what it does with the absorbed `Currency`/treasury TODAY: the
     treasury + faction wealth force-convert via `PolityRecord.DepositExempt`
     (no skim, Task 4f), pools via bare `ConvertCurrency`+`RecordConversion`; the
     absorbed polity's `CurrencyId` currency lingers `Retired` with any dangling
     holdings. CU-3 decides what happens to the absorbed CURRENCY and its BANK.
   - `src/Core/Epoch/Interior/GraduationOps.cs` — the split/schism seam (the
     inverse — a new currency+bank is founded; does CU-3 touch this or only
     fusion?).
   - `src/Core/Epoch/SimState.cs` `ConvertPortHoldings`, `Currency.Retired`,
     `SupplyOps.WalkNative` (how dangling holdings in a retired currency are
     walked), `MetricsOps` per-currency reserve-aware residual.

## The open design questions — weigh them in the brainstorm

- **Gradual vs instant consolidation.** CU-1's stub is instant 1:1. Real
  currency unions phase in. What does "gradual" mean in world-time (a peg that
  converges, a conversion window, a transition period)? What state carries it?
- **How do two banks' reserves combine?** Sum? Weighted by size/strength? Does
  the absorbed bank's reserve transfer to the survivor, or dissolve into the
  merged currency's supply? Conservation must hold (reserve is money).
- **The absorbed currency's fate.** Does it retire immediately, or persist during
  a transition while holders convert? Dangling holdings (corp wallets, segment
  wealth) in the absorbed currency — forced-converted when, at what rate?
- **Regulatory-role transfer.** The absorbed bank's authority (issuance backing
  for the absorbed polity) — transfers to the survivor's bank, or ends?

## THE sequencing decision this slice must make first (do not skip)

CU-2 follow-up #1 (`[[bank-reserve-flow-gap]]`): the bank is currently peripheral
— its reserve is fed ONLY by the FX spread, while a polity's dominant money flow
(receipts/taxes/wages) bypasses the bank entirely, so banks can never meaningfully
fund deficits at any spread. **Before designing CU-3, decide with the user
whether the bank-reserve-flow redesign (route the polity's money flow through the
bank) is a prerequisite that belongs BEFORE CU-3/CU-4, or whether CU-3 proceeds
on the banks as-is.** Merging two peripheral banks is far less interesting than
merging two banks that actually intermediate their economies — this may reorder
the roadmap. Surface it as the opening question of the brainstorm.

## Phase 1 — brainstorm (before any code)

Use `superpowers:brainstorming` directly. Open with the sequencing decision above.
Then the consolidation-mechanic questions, grounded in the real `MergeInto` code
and the CU-2 reserve/conservation machinery. Do not assume the roadmap's
"gradual, role transfers" sketch — weigh it.

## Boundary — NOT this session

- **CU-4** (bank/currency-union strength → federation generation) is the later
  slice; note where CU-3 constrains it, don't design it.
- The bank-reserve-flow redesign (follow-up #1) is a SEPARATE design pass — CU-3
  decides its *sequencing*, and only builds it if the brainstorm concludes it's a
  true prerequisite (in which case it likely becomes its own slice first).

## Traps carried from CU-2 (beyond the K4/K5/SH/L/CU-1 lists in HANDOFF)

- **The 32-run committed sweep is the conservation instrument, not seed-42 units.**
  Any consolidation that moves reserves/supplies across currencies is
  conservation-sensitive — run `dotnet run --project src/Inspector -c Release --
  sweep docs/superpowers/plans/2026-07-12-debt-diagnosis-experiment.json` and
  check the worst per-currency `Money.ConservationResidual` (should stay ~1e-9
  abs / ~1e-16 relative). Reserve is now part of the residual — a merge that
  drops or double-counts a reserve will show there.
- **Reserve is sequestered OUT of circulating `Supply`** — `SupplyOps` does not
  walk it, `MetricsOps` adds it back. Any CU-3 code that moves reserve must keep
  both sides consistent.
- **Classify every conversion site exchange-vs-exempt.** CU-2's audit
  (spec settlement §) is the model: absorption re-denomination is EXEMPT
  (`DepositExempt`), market exchange skims. A currency merge is re-denomination —
  almost certainly exempt (no spread), but decide explicitly.
- **The gross-up balance-trap**: any payment sized to an exact balance then
  clamped/reset mints the skim; and any capped `Withdraw`/`DebitLocal` whose
  return is discarded leaks (CU-2 finding A). If CU-3 moves money via those, honor
  the provided-amount return.
- **Register any new knob in `KnobRegistry.cs`** (CU-2 review finding 1 — an
  unregistered knob silently reverts on reload, breaking determinism AND blocking
  the tuning sweep).
- Slice-session workflow (scope nod · REPL eyeball · merge decision;
  subagent-driven-development, one whole-branch fable review + fix wave; task
  ledger; kickoff-prompt chaining). `git log main` before merge-out (L2 and
  others may move main).
