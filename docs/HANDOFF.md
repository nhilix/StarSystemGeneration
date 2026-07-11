# Session Handoff — 2026-07-11 (Slice J: Handoff & certification — merged)

State: `main`, merged locally at `0e01bd0`, **not pushed** (push on user
say-so). Tests 562/562 green — hex-tier suite untouched at 100%.
ProjectSettings churn remains uncommitted as always.

## What this session did: Slice J of the epoch-sim rebuild, merged

The generated galaxy stopped being a simulation output and became **a
game's initial conditions, delivered whole and in motion**. Both slice-I
deferrals adopted at the scope nod. Ledger (tasks, decisions, the review
wave, eyeball exhibits):
`docs/superpowers/plans/2026-07-11-slice-j-ledger.md`. Acceptance record:
`docs/superpowers/specs/2026-07-11-design-acceptance.md`.

- **The two wires** (J1/J2): a standing Ruins/RuinedCapital POI within
  `Poi.LawlessnessReachHexes` of a lane mouth makes it lawless — raid
  floor × `Poi.LawlessRaidFactor`, navyless requirement waived (no navy
  roots a band out of the walls). A standing Memorial holds any
  audience's stance about its perpetrator at −`Poi.MemorialStanceAnchor`
  (suppression memorials carry `SubjectId` = the suppressor; famines
  keep −1). Seed 42's memorials name Marno et al. and grief persists.
- **P7 certification** (J3, the slice's core): **`Sim.GenerationYears`**
  (ESIM, config v6) split the calendar unit from the integration step —
  `YearsPerEpoch` is ONLY the step now. Every persisted clock became a
  world-year: relations Met/Rung/Offer/LastIncident/VassalSince (v5),
  `WarObjective.SiegeYears` (wars v2), `Corporation.LeanYears` (v2),
  `Faction.NichePersistenceYears` (interior v6); entry + native
  emergence fire on the calendar (generation-quantized); era buckets
  are generations. Per-generation intensities scale by
  **`Sim.StepFraction`**: incident sparks, battle loss shares
  (hash-rounded hulls, RollChannel 72), facility damage, commander
  rout-death, corporate receipts floors. Regional news delivers by
  age-crossing (once per observer when age crosses its delay; horizon
  one generation + straddle window). Coarse tick stayed byte-identical
  throughout (reviewer byte-audited the golden).
- **The certification nets caught three real P7 bugs** — the kickoff's
  prediction ("integration-rate effects will surface") was right:
  (1) `AdjustPrices` compared per-step demand (flow) to inventory
  (stock) — fine ticks read universal glut and crashed every price to
  the floor; demand now normalizes by StepFraction. (2) Yard slots
  truncated fractional throughput — a tier-1 yard built 1.25 hulls per
  generation coarse and 0 forever fine; fine steps hash-round
  (RollChannel 73), coarse keeps truncation. (3, fresh-eyes review)
  corporate `Receipts` (per-step flow) vs per-generation floors — corps
  mass-died lean and magnates stopped minting at fine tick.
- **FineTickTests**: fine determinism byte-identity, LoadThenContinue at
  play resolution, seven-phase/clock honesty, hull-ledger conservation
  through 100 fine steps, no-genesis-only liveness + macro bands
  (coarse vs fine over the same 100 world-years, 2 seeds). Residual
  divergence is keyed-roll path divergence — different histories, not
  bias — bounded by the bands.
- **Controller handover certified** (J4, HandoverTests): wrapping every
  controller mid-run is byte-invisible; a scripted player takes a
  polity throne (pinned tax code + a TreatyAct resolving by the same
  ladder rules) and a corporation board mid-run; load reattaches stock
  controllers (occupation is client state, never persisted). NOTE: no
  such test existed before J despite prior claims.
- **The delta boundary** (J5, `DeltaSerializer`): a save = base artifact
  + changed layers + the log's continuation. Layer-granular text diff
  over the artifact's own grammar; unchanged layers absent (genesis
  strata never re-record); the events layer ships only its
  continuation; FNV-64 fingerprint refuses foreign bases; refuses a
  section for a layer the base lacks. Byte-exact round trip certified
  at both ticks. REPL `edsave <base> <delta>` / `edload`.
- **Handoff framing** (J6): `HandoffView.OpenThreads` — half-won wars,
  loaded tensions with live casus belli, old thrones by species span,
  standing succession claims, leveraged corporations, burning plagues,
  quarantines, unanswered offers — all computed, nothing stored.
  `EventLog.ForWar` via `IWarPayload` completed the four chronicle
  indexes (place/actor/war/character).
- **Full-design acceptance pass** (J7): P1–P8 certified with evidence
  (P2 partial: character personal acts unarmed; P3 one filed exception:
  freight clears on true prices — Move 2 resolution, doc amended).
  13-item gap list — headline: **11 contract acts are type-only**
  (Sanction, Charter, ProcurementContract, CharterApplication,
  MajorAcquisition, RelocateHQ, Patronize, Defect, RoleResponse, Marry,
  LeadExpedition) — the player-verb backlog. Six files' stale
  perfect-info/stub comments fixed; perception-and-news.md amended to
  the landed per-subject snapshot model.
- **Registries**: 22 layers (no new ones; 5 version bumps: config 6,
  interior 6, corporations 2, relations 5, wars 2). Events next free
  unchanged — economic 211, political 314, military 409, diplomatic
  511, corporate 605, character 704. `RollChannel` next free: **74**
  (72 BattleLosses, 73 YardSlots). New knobs: `Poi.LawlessnessReachHexes`,
  `Poi.LawlessRaidFactor`, `Poi.MemorialStanceAnchor` + structural
  `Sim.GenerationYears` — all in TUNING.
- **REPL**: `threads` (the world in motion) · `estep [n] [years]`
  (fine tick: `estep 25 1` plays a generation year by year) ·
  `edsave`/`edload` (delta saves) · `equarantine <laneId>` (the
  player's polity verb by hand) · `watch` intact.
- Fresh-eyes review: 1 confirmed + 2 plausible + 3 notes, one fix wave,
  all addressed. Eyeball accepted 2026-07-11: seed 42 hands off y1000
  with ~26 open threads; 20 years at 5-year ticks settle the Alloys War
  and ignite two NEW liberation wars; a hand-quarantined lane shows in
  `threads`; delta round-trips 556KB against an 886KB base.

## Deliberately deferred / accepted (flagged, not silent)

- The acceptance record's 13-gap backlog (the design tree promises them;
  most are live-game player verbs) — see
  `docs/superpowers/specs/2026-07-11-design-acceptance.md`.
- Regional-news delay churn at fine tick can double- or skip-judge one
  event when lane topology shifts a delay across a window boundary —
  deterministic, bounded, documented in `SpreadRegional`.
- Coarse/fine macro divergence is keyed-roll path divergence (rolls key
  step index) — the band tests bound it; byte-equivalence across
  resolutions is not a goal.
- Non-divisor fine steps (e.g. 7y into a 25y generation): threshold
  crossings land up to step−1 years late — accepted; the REPL default
  paths (1/5/25) divide evenly.

## Next up

1. **Slice K (Unity atlas rebuild)** — the LAST slice, now decomposed
   into five sub-slices K1–K5 (planning session 2026-07-11): governing
   plan `docs/superpowers/plans/2026-07-11-slice-k-roadmap.md`. Fresh
   session, point it at
   `docs/superpowers/plans/2026-07-11-slice-k1-kickoff-prompt.md`
   (skeleton instrument; the original whole-K kickoff prompt remains the
   inherited context).
2. **User read-through of the design specs** — still outstanding.
3. Generation-flow diagram republished to its artifact URL this session
   (source synced at `docs/diagrams/generation-flow.html`).

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · REPL eyeball · merge
decision; kickoff-prompt chaining); hex-tier suite never breaks;
ProjectSettings stays uncommitted; bash printf for REPL piping; parallel
slices never share a checkout — take a `git worktree` each; every
calibration constant goes in a knob registry + TUNING.md; every new
`src/Core` file gets a two-line `.meta` with a fresh guid (src/Core is
the Unity package `com.stargen.core`); any new goods consumer needs a
demand pull at Markets or it starves; grace periods precede death
clocks; step-transients provably zero at epoch boundaries; histories
churn — tests pick live subjects via `EpochTestKit.FirstLiveRelation`;
belief state must serialize or LoadThenContinue diverges; immutable
residue registries anchor once. New this slice: **every clock is a
world-year — a counter that increments per step is a P7 bug**
(accumulate `YearsPerEpoch`; compare against knob × `GenerationYears`);
**per-step flows never compare to stocks or per-generation floors
without ÷ `StepFraction`**; fine-step integer granularity hash-rounds
on a dedicated RollChannel while the coarse path keeps its legacy
rounding so goldens hold; golden regen one-liner: `printf 'epoch 42 40
12\nesave tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt\nquit\n'
| dotnet run --project src/Inspector`. Older carried minors:
`git show a1f5843~40:docs/HANDOFF.md`.
