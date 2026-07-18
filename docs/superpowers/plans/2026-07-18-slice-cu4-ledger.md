# Slice CU-4 task ledger — monetary strength → federation generation

Branch `slice-cu4-monetary-federation` (worktree `.worktrees/slice-cu4`), based on
`main` @ `41f1cb8`. Design:
`docs/superpowers/specs/2026-07-18-cu4-monetary-federation-design.md`. **Closes the
CU chain.** The resumability record if the session dies — updated as work lands.

Model dispatch: Sonnet default; **Opus escalation** for Task 7 (conservation +
tuning judgment + ensemble bar). All implementation subagents dispatched
**synchronously** (`run_in_background: false`) and verified via `git log`
(`[[subagent-dispatch-sync]]`). TDD; frequent commits; hex-tier suite never breaks;
determinism byte-identity always.

## Checklist

- [x] **T0 — design spec** committed `e35a742`.
- [x] **T1 — ledger** (this file).
- [x] **T2 — `Bank.BackedShare`** (Sonnet) — commit `dc0a00a`. Pure computed property
  on `Bank`. Unit tests: saver→1.0, debtor→0.0, balanced→0.5, fresh 0/0→0.0.
- [x] **T3 — knobs, default 0.0** (Sonnet) — commit `f2553e6`.
  `Relations.FederationCredibilityDiscount` + `Relations.VassalAbsorptionCredibilityDiscount`
  in `EpochSimConfig.cs`, both 0.0; both registered in `KnobRegistry.cs`
  (name-sorted table); `docs/TUNING.md` updated.
  **⚠ GOLDEN-WINDOW CORRECTION:** `ArtifactSerializer.Save` stamps *every*
  `KnobRegistry.All` entry into the config artifact, so **registering a knob busts
  the golden at T3** (not at T8), value-independent — exactly BF's `FxBackingSensitivity`
  precedent (`7dba6fb`). The golden is now RED and stays red until the single re-freeze
  at **T9**. The T7 inert proof is therefore *not* "golden byte-identical" but
  "**golden diff is exactly the two knob-stamp lines, zero behavioral/simulation
  diff**" — verify by regenerating and diffing at T7.
- [x] **T4 — fusion true gate** (Sonnet) — commit `e10dad2`. Added private helper
  `FederationOps.Credibility(state, pr)` (guards currency<0→0) — **reused by T6**.
  `FederationOps.FederationGateHolds`: subtract
  `FederationCredibilityDiscount × min(cred(a), cred(b))` from `gate`, where
  `cred(x) = x.CurrencyId < 0 ? 0 : state.BankOf(x.CurrencyId).BackedShare`. Tests:
  credible pair whose warmth sits just below the plain gate now passes; credible+debtor
  (min→0) does not; knob=0 ⇒ byte-identical gate. Golden still identical (knob 0).
- [x] **T5 — fusion perceived plumbing** (Sonnet) — commit `524c503`. Mirror `OverlapShare`:
  `RelationBrief` gains trailing `double OtherCredibility`; `PerceptionView` gains
  `OwnCredibility` (beside `OwnStrength`); populate **live** at the snapshot build
  (`Phases.cs` ~:219–233, beside the `RelationsOps.OverlapShare` call) from
  `state.BankOf(...).BackedShare`; thread `OwnCredibility` into `EffectiveGate` and
  apply `− FederationCredibilityDiscount × min(OwnCredibility, rel.OtherCredibility)`.
  Tests: `EffectiveGate` discounted for a credible pair (mirrors the true gate → an
  offer actually generates). **No serialization change** (transient snapshots). Golden
  identical (knob 0).
- [x] **T6 — absorption gate** (Sonnet) — commit `8fc2c11`. Reused the `Credibility`
  helper. `FederationOps.VassalExits`: replace the
  `rel.Warmth >= VassalAbsorptionWarmth` test with
  `rel.Warmth >= VassalAbsorptionWarmth − VassalAbsorptionCredibilityDiscount ×
  max(0, cred(overlord) − cred(vassal))`. Tests: long/warm-enough-only-with-discount
  vassalage under a credible overlord absorbs; vassal more credible than overlord gets
  no discount; knob=0 ⇒ identical. Golden identical (knob 0).
- [x] **T7-checkpoint — inert-at-0** (slice session) — **PASSED**. Fresh seed-42
  artifact diffs against the frozen golden by **exactly two lines** (the two new knobs
  in the config stamp at value 0); all 9924 simulation-history lines byte-identical.
  Full suite 1144 pass / 1 fail (only `GoldenTests`, the config-stamp red). Mechanism
  provably inert at default. (Verified with a throwaway dump harness, since removed.)
- [x] **T8 — activation + measurement** (**Opus** sub for measurement; slice session
  activated) — commit `4b49d25`. **Default = 0.20 for both knobs.** Measured on the
  committed instruments (variant-driven, no golden disturbance): 32-run conservation
  sweep worst **relative residual 1.223e-15** (= CU-3's 1.22e-15, ~6 orders below the
  1e-9 fail bar); clock instrument **telescopes** (`live_polities` byte-identical
  across 25/5/1y clocks, null-variant self-check clean); **no runaway** — mean
  `Polity.Live` 59.625 (baseline) → 59.375 (0.20) → 59.0 (0.30), monotone ~1%. Three
  CU-4 unit tests pinned their control knob explicitly (had asserted the now-live
  default 0). **Effect is deliberately targeted** (the runaway brakes): at seed-42/
  radius-12 it's a 0→0 no-op; it flips real absorptions at radius 21 (seeds 31337,
  9091). Report: `scratchpad/cu4-t8a-measure-report.md`.
- [x] **T9 — golden re-freeze** — commit pending. Regenerated seed-42 golden;
  diff is **exactly the two knob-stamp lines at 0.2** (no simulation-history change —
  seed-42/radius-12 is a 0→0 no-op), CRLF preserved. **Full suite 1145/1145 green.**
- [x] **T10 — EYEBALL (user checkpoint) — ACCEPTED at 0.20.** Driven histories
  (radius 21): seed 9091 y850 **Lusshaka (cred 1.00) absorbs Misha (cred 0.00)** and
  seed 31337 y950 **Nyduzen (1.00) absorbs Thano (0.00)** — absorption bar 0.600→0.400,
  warmth ~0.42 clears; without CU-4 both stay bound forever. Noted: at 0.20 the visible
  effect rides the ABSORPTION seam (peer fusion needs a rarer both-credible near-threshold
  pair); user accepted 0.20 over the 0.30/asymmetric headroom.
- [x] **T11 — whole-branch fresh-eyes review** (**fable**) — verdict **MERGE**, all
  global constraints verified in code. Findings for the one fix wave: (Important) the
  vacuous `Absorption_VassalMoreCredibleThanOverlord_GetsNoDiscount` test (warmth 0.55
  fails with or without the max(0) floor — give it teeth: warmth above the plain bar,
  assert absorption still fires); (Important) `docs/TUNING.md` still says both knobs
  default 0.0/inert (shipped 0.20); (Minor) dedup the credibility guard+lookup across
  `FederationOps.Credibility` and the `Phases.cs` snapshot. Review:
  `scratchpad/cu4-fable-review.md`.
- [ ] **T12 — MERGE (user checkpoint)**: merge `--no-ff` to `main`; update
  `docs/HANDOFF.md` (CU chain **CLOSED**); Trello sync if reachable; **no next
  kickoff** (chain closes — note follow-ups from spec §10). Push only on say-so.

## Gates (mechanical, mandatory)

`dotnet test` green throughout **except `GoldenTests`** (RED from T3 onward — the
config-artifact stamp lists the two new knobs; see the T3 correction). Golden frozen
**once** at T9. Determinism byte-identity of *simulation* holds throughout (the golden
diff is only the two config-stamp lines until T8 activation moves behavior). · 32-run
sweep ~1e-16 rel · clock instrument telescopes · both knobs registered.

## Notes / surprises

- (none yet)
