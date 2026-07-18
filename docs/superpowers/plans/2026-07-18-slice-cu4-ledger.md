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
- [ ] **T4 — fusion true gate** (Sonnet; escalate if it fights the test kit).
  `FederationOps.FederationGateHolds`: subtract
  `FederationCredibilityDiscount × min(cred(a), cred(b))` from `gate`, where
  `cred(x) = x.CurrencyId < 0 ? 0 : state.BankOf(x.CurrencyId).BackedShare`. Tests:
  credible pair whose warmth sits just below the plain gate now passes; credible+debtor
  (min→0) does not; knob=0 ⇒ byte-identical gate. Golden still identical (knob 0).
- [ ] **T5 — fusion perceived plumbing** (Sonnet). Mirror `OverlapShare`:
  `RelationBrief` gains trailing `double OtherCredibility`; `PerceptionView` gains
  `OwnCredibility` (beside `OwnStrength`); populate **live** at the snapshot build
  (`Phases.cs` ~:219–233, beside the `RelationsOps.OverlapShare` call) from
  `state.BankOf(...).BackedShare`; thread `OwnCredibility` into `EffectiveGate` and
  apply `− FederationCredibilityDiscount × min(OwnCredibility, rel.OtherCredibility)`.
  Tests: `EffectiveGate` discounted for a credible pair (mirrors the true gate → an
  offer actually generates). **No serialization change** (transient snapshots). Golden
  identical (knob 0).
- [ ] **T6 — absorption gate** (Sonnet). `FederationOps.VassalExits`: replace the
  `rel.Warmth >= VassalAbsorptionWarmth` test with
  `rel.Warmth >= VassalAbsorptionWarmth − VassalAbsorptionCredibilityDiscount ×
  max(0, cred(overlord) − cred(vassal))`. Tests: long/warm-enough-only-with-discount
  vassalage under a credible overlord absorbs; vassal more credible than overlord gets
  no discount; knob=0 ⇒ identical. Golden identical (knob 0).
- [ ] **T7-checkpoint — inert-at-0** (slice session). With both knobs 0: full
  `dotnet test` green, **golden byte-identical to pre-CU-4 `main`**, determinism
  byte-identity. Proves the whole mechanism provably inert before activation. GATE.
- [ ] **T8 — activation + measurement** (**Opus**). Set conservative live defaults
  (start ~0.15–0.25, tune down if runaway). Run: (a) **32-run conservation sweep**
  (`2026-07-12-debt-diagnosis-experiment.json`) — worst per-currency residual holds
  **~1e-16 relative**; (b) **clock instrument** (`2026-07-17-clock-invariance-
  experiment.json`) — telescoping intact; (c) **runaway metrics** vs pre-CU-4 `main`
  baseline (same seeds): surviving-polity count in a sane band, federation/absorption
  counts rise but don't explode, fused polities skew high-`BackedShare` / absorbed
  vassals skew low. Iterate defaults against these instruments; add a countervailing
  brake **only** if over-federation actually shows. GATE.
- [ ] **T9 — golden re-freeze** (once, here — a merge in seed-42 history moves it) +
  REPL/eyeball surface check (existing `FederationFormed`/`VassalAbsorbed` events +
  BF `bank:`/`claims:` surface).
- [ ] **T10 — EYEBALL (user checkpoint)**: a driven history where a strong polity
  visibly federates while a weak vassal is absorbed — on committed instruments.
- [ ] **T11 — whole-branch fresh-eyes review** (**fable**) + one fix wave.
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
