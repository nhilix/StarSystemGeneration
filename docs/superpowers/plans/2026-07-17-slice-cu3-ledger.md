# Slice CU-3 task ledger — federation-triggered currency consolidation

Branch `slice-cu3-currency-consolidation`, base current main (BF merged, `0bdb009`;
this branch off `fc402f4`-era main). Design:
`docs/superpowers/specs/2026-07-17-cu3-currency-consolidation-design.md`.

A tight, single-mechanism slice: one consolidation block in `FederationOps.MergeInto`
+ its tests. No new knobs, no serialization change. The whole risk is one
conservation subtlety (§3b/§3c: reserve records a conversion, claim does not).

## Tasks

- [x] **1. The consolidation block + TDD.** In `FederationOps.MergeInto`, after the
      treasury/pool transfer and before the loan reissue (design §3): guards (§3a),
      reserve transfer (§3b — `ConvertCurrency` + `RecordConversion`, exempt), claim
      transfer (§3c — `ConvertCurrency` reprice, **NO** `RecordConversion`), leave
      cumulative counters on the drained bank (§3d). *Opus* (conservation invariant;
      the central correctness point). **Verify the §3d flagged interaction** — does
      any BF test assert `ClaimOnState ≤ CumulativeLentToState`? Surface if so.
      Gate: TDD per design §7 — reserve transfers WITH a recorded conversion; claim
      transfers WITHOUT one (assert `CumulativeConvertedIn/Out` do not move for the
      claim); per-currency residual ~0 across a merge epoch; drained bank lingers;
      union-genesis pools both parents. `dotnet test` green except the standing
      golden (re-frozen at task 4). Hex-tier never breaks.

- [ ] **2. Conservation sweep + eyeball demonstration.** Run the 32-run committed
      sweep — worst per-currency residual must hold at **~1e-16 relative** (BF's
      2.15e-15 bar; judge relative, not absolute). Then drive a seed history with a
      real absorption and capture the survivor's BF `bank:`/`claims:` lines jumping
      at the merge (spec §7 — the eyeball artifact). *Opus* (conservation gate).
      Gate: sweep relative residual holds; a concrete merged-balance-sheet render.

- [ ] **3. USER: eyeball acceptance** — a survivor's reserve + claim book jump at an
      absorption; conservation still green.

- [ ] **4. Whole-branch fresh-eyes review** — *fable*, pinned. Then one fix wave.
      The review must check §3b/§3c hardest (reserve recorded, claim not).

- [ ] **5. Golden freeze** — once, at slice end (a merge in the seed-42 history
      moves it). Regenerate from the current serializer, verify determinism (two
      independent runs byte-identical), then re-freeze.

- [ ] **6. USER: merge decision** → merge to main locally (`git log main` first) ·
      update `docs/HANDOFF.md` · **write the CU-4 kickoff** (bank/currency-union
      strength → federation generation — the chain's next link) · sync Trello ·
      push only on say-so.

## Gates (all mandatory, all mechanical)

`dotnet test` green · hex-tier suite never breaks · determinism byte-identity ·
32-run sweep relative residual ~1e-16 · golden re-frozen once at slice end.

## Log

- 2026-07-17 — brainstorm complete (instant · reserve→reserve · claim
  transfer/inherit · lazy corp holdings · fusion-only); design approved and
  committed `0490ca1`; ledger opened.
- 2026-07-17 — Task 1 DONE. Consolidation block landed in `FederationOps.MergeInto`
  (after the character loop, before loan reissue): §3a guards, §3b reserve
  (`ConvertCurrency` + `RecordConversion`, exempt), §3c claim (`ConvertCurrency`
  reprice, NO `RecordConversion`), §3d counters left on the husk. 7 TDD tests in
  `tests/Core.Tests/Epoch/CurrencyConsolidationTests.cs` (reserve recorded · claim
  NOT recorded via reserve=0/claim≠0 · per-currency residual invariant · husk
  lingers · union-genesis pools both · pre-genesis & same-currency guards). Full
  suite green: 1137 passed, 0 failed — **including the golden** (seed-42 merges
  carry zero reserve/claim at the golden's epoch range, so no re-freeze needed for
  this task). §3d verification: **no** BF test or source guard asserts
  `ClaimOnState ≤ CumulativeLentToState` — the survivor's inherited claim exceeding
  its own lending counter is unconstrained; nothing to relax.
