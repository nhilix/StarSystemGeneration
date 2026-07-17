# Slice BF task ledger — the bank as monetary authority

Branch `slice-bf-bank-flow` (worktree `.worktrees/slice-cu3` — directory name is a
stale artifact of the CU-3 pivot, `git worktree move` blocked by an open handle;
retry at wrap-up). Base: main `768a8e4`. Design:
`docs/superpowers/specs/2026-07-16-bf-bank-flow-design.md`.

Slice sequenced ahead of CU-3 by user decision (design §8). CU-3's kickoff prompt
(`2026-07-16-slice-cu3-kickoff-prompt.md`) stays on disk and is re-chained at
wrap-up.

## Model routing (CLAUDE.md "Model usage")

Escalate to Opus when a task touches conservation/determinism invariants, spans
subsystems, or is a design judgment call. Most of this slice is money movement, so
the escalation bar is met more often than usual — noted per task.

## Tasks

- [ ] **1. Data model** — `Bank.ClaimOnState`, `.CumulativeLentToState`,
      `.CumulativeRetired`; `Currency.CumulativeFiatRetired`. Fields + XML docs
      only, all default 0. No behavior. *Sonnet* (mechanical).
      Gate: `dotnet test` green, byte-identity unchanged (all fields 0).

- [ ] **2. Serialization** — extend `BANK` (`ArtifactSerializer.cs:254`/`:1284`)
      and `CURRENCY` (`:254`/`:1267`) lines; bump the version tuple; round-trip
      test. *Sonnet* (mechanical, but the dense id-order guards are load-bearing).
      Gate: round-trip + `LoadThenContinue` determinism tests green.

- [ ] **3. Knobs** — `SovereignClaimInterestRate` (0.02), `ClaimServicingShare`
      (0.25), `FxBackingSensitivity` (**0.0** — CU-2-identical landing).
      **All three registered in `KnobRegistry.cs`** (CU-2 review finding 1: an
      unregistered knob silently reverts on reload). Parallel with 1–2. *Sonnet*.
      Gate: knob-registration test; a reload preserves each.

- [ ] **4. LendToState** — `IssueSovereignCredit` → `Bank.LendToState`; books
      `ClaimOnState += m` alongside the **unchanged** `Supply`/`CumulativeFiatIssued`
      motion. ME's cap and floor untouched. `FundDeficit` stage 1 unchanged.
      *Opus* (conservation invariant + money creation).
      Gate: `dotnet test`; issuance magnitudes byte-identical to CU-2 (only the
      claim is new).

- [ ] **5. ServiceSovereignClaim** — the servicing pass, ordered **after**
      `FundDeficit` in Allocation. Budget computed ONCE before mutation (design
      §4 — the `Phases.cs:444` compound-assignment trap). Surplus-only; interest
      never capitalizes. *Opus* (the money sink + both hard rules).
      Gate: servicing never drives `Credits` negative; no capitalization on an
      insolvent polity; residual holds across a repayment epoch.

- [ ] **6. Residual** — add `CumulativeFiatRetired` to `CurrencyResidualRow` AND
      the baseline carry (`MetricsOps.cs:195` is a **delta** form). Design §6
      names this the single easiest way to get the slice wrong. *Opus*.
      Gate: residual ~0 across repayment epochs; **32-run sweep** (first run).

- [ ] **7. FX backing term** — `unbacked = max(0, ClaimOnState − Reserve)`;
      `effectiveMoney = Supply + FxBackingSensitivity × unbacked` into
      `FxOps.cs:59`. *Opus* (determinism formula + design judgment).
      Gate: `FxBackingSensitivity = 0` reproduces CU-2 **byte-identically**.

- [ ] **8. REPL surface** — claim book, backing ratio, retired-to-date on the
      currency line. *Sonnet*.
      Gate: the REPL surface works (piped via bash `printf`, not PowerShell).

- [ ] **9. Sweep + backing activation** — run the 32-run committed sweep; then
      raise `FxBackingSensitivity` off 0 to a defensible value and re-run.
      **The eyeball needs a non-zero run** — at 0 the FX coupling is inert.
      *Opus* (tuning claim ⇒ ensemble bar, SIMHEALTH.md).
      Gate: worst per-currency residual ~1e-9 abs / ~1e-16 relative.

- [ ] **10. USER: REPL eyeball acceptance** (the taste gate).

- [ ] **11. Whole-branch fresh-eyes review** — *fable*, pinned. Then one fix wave.

- [ ] **12. Golden freeze** — once, at slice end (red-window inside the slice).

- [ ] **13. USER: merge decision** → merge to main locally (`git log main` first —
      L2 is in flight and may have moved main) · update `docs/HANDOFF.md` ·
      **re-chain CU-3's kickoff** (amend it for the claim-book merge questions
      design §8 raises) · sync Trello · push only on say-so.

## Gates (all mandatory, all mechanical)

`dotnet test` green · hex-tier suite never breaks · determinism byte-identity ·
32-run sweep residual within tolerance · goldens frozen once at slice end · the
REPL surface works.

## Log

- 2026-07-16 — brainstorm complete; sequencing decision taken (BF before CU-3,
  user overrode the CU-3-first recommendation); design approved and committed
  `298f20f`; ledger opened.
