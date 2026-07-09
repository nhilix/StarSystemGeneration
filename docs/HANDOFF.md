# Session Handoff — 2026-07-08 (late)

State: `main` at `3fcf370` (sim-economy merged ff, branch deleted; push to origin is manual and pending — now 31 commits ahead).
Core tests 161/161 (`dotnet test StarSystemGeneration.sln`); Unity untouched this slice (edit-mode suite unchanged at 50/50, editor not needed).
Durable task ledger: `.superpowers/sdd/progress.md` (git-ignored — read it before re-doing anything).
Living flow diagram (artifact, updated): https://claude.ai/code/artifact/67f20b6b-4e8c-4941-b88b-fc071c1c64f4 — source `docs/diagrams/generation-flow.html`; update + republish per the memory note when layers change.

## Just shipped (context, don't redo)

- **Sim economy slice** — regional spec stages 2–3 complete. Spec
  `docs/superpowers/specs/2026-07-08-sim-economy-design.md`, plan
  `docs/superpowers/plans/2026-07-08-sim-economy.md`. `EpochSim` is now the
  income→allocation→action→resolution phase pipeline (`src/Core/Galaxy/Sim/`, pure math in
  `Economy.cs`): commodities (Provisions/Ore/Exotics) with BFS-pathed intra-polity flows +
  cross-polity trade, per-cell `RouteThroughput`, blockade = contested/belligerent transit,
  famine/population dynamics (species-tagged, growth-guarded — feeding never shrinks),
  four-way budgets, military stockpile, exotics→tech ladder, persistent `War` registry
  (deficit-driven goals, weariness, victory/white-peace, extinct retention). Serializer
  **schema v4** (no v3 loader). REPL: `trade`/`economy`/`war` layers (trade shading is
  relative to busiest cell), `polity`, `chronicle`, economy stats block.
  Acceptance-tuned knob defaults: `ProvisionsPerPop` 0.5, `TechThresholdBase` 5;
  `AllocationPhase.DevIncomeBase` 5.5 (plan said 1.5 — budgets don't carry epochs).
  Golden (seed 7 r3): polities 2, events 36 — re-frozen 3× this slice, history in the test comment.
- **Orbit-diagram system view** (roadmap phase 2), **setup knobs**, **Unity atlas** — see git
  history / previous handoff (`git show 35fb4dd:docs/HANDOFF.md`).

## Next up (pick one)

1. **Sim stages 4–6** (biggest): relations ladder/federations/schisms/vassalage (4), news &
   stances (5), event→POI compiler + world-state handoff (6). Parent spec §7.4–7.7.
   Brainstorm→spec cycle per slice; stage 4 should absorb the exotics-deficit and war-goal
   findings below.
2. **Unity economy parity + atlas polish**: trade/economy/war layers and polity/econ panel in
   the Unity atlas (editor-open live acceptance); smooth pan/zoom; cell hover highlight;
   Atlas.unity as build scene 0; orbit-diagram §8 follow-ups.
3. **Deferred ticket batch** — see below.

## Deferred tickets (details in ledger + `.superpowers/sdd/final-review-report.md`)

Sim-economy (final review triage, all carried):
- **Exotics deficit structurally impossible** (`ExoticsBalance` = raw production, never < 0) →
  `WarGoal.Exotics`, exotics stagnation, and exotics imports are dead code. Design decision
  for stage 4 (e.g., tech investment as consumption before balance).
- **TradeBlocked semantics drift**: `HasLiveWar` gate undocumented; fired 0× on all sampled
  seeds; untested. Needs a semantics decision + test.
- War-goal variety: mostly Punitive at healthy economies (one Ore war at seed 42 e9); ties
  into the **non-deficit war causes** ticket (ideology clashes, spark events — user note,
  spec §10) for stages 4/5.
- Smaller: famine/war-scar shrink don't stack (spec prose ambiguous); `SharesBorder`
  O(cells×pairs) + per-good capital-path recompute; unpaid-upkeep/ore-deficit decay branches
  untested; landless-but-alive polity keeps stale Wealth; `double.MinValue` sentinel in goal
  pick; WhitePeace can mislabel same-epoch defender extinction (+ pre-extinction captures not
  returned on DefenderVictory); double LostCapital per epoch possible; shared-front Contested
  handling (M-1 in final report).

Older (carried): ClearHexSelection screen guard; multi-primary doc note; companion-moon pad
scaling untested; SystemPanel.Highlight no-op on unknown BodyRef; orbit-diagram §8 follow-ups
(labels, motion, hover recolor, pan/zoom); golden v3 CONFIG-line literal test; sim-only knobs
rebuild preview; `_setupErrorLabel` ScrollView; IsShapeOnly guard; dedupe show-preview;
`_radiusField.isDelayed`; REPL `galaxy` knob parity; WarStarted dedup (obsolete? one live war
per pair now enforced — verify then close); REPL goto wrap; StatsReport ignores satellites.

## Process conventions (established, keep following)

- Superpowers flow: brainstorm → spec (user reviews) → plan → subagent-driven execution →
  final whole-branch review (fable) + one fix wave → finishing-a-development-branch →
  merge to main locally, verify tests, delete branch. User pushes manually.
- Implementer reports MUST include verbatim test-summary lines; "gate mandatory, never kill
  processes" language stays in every dispatch prompt. This slice had zero gate incidents
  (first clean slice) — the plan-contains-full-code + explicit-contingency-rules pattern
  worked; keep it.
- New pattern that paid off 3×: brief-provided tests can fail against brief-provided code
  (plan bugs). Dispatch language: "diagnose, apply the MINIMAL fix, prefer fixing
  implementation over weakening asserts, report DONE_WITH_CONCERNS with arithmetic."
  Reviewers then re-derive the arithmetic independently.
- Cheap model (haiku) for transcription tasks, sonnet for integration/judgment tasks and all
  task reviews, fable for the final whole-branch review.
- Mid-acceptance visual/tuning feedback is handled controller-inline (fix waves committed
  after gates), not via subagent.
- ProjectSettings churn stays uncommitted; never let it ride into commits.

## Gotchas (hard-won)

- PowerShell piping to the REPL mangles the FIRST stdin line (BOM) — use bash
  `printf 'cmd\n...' | dotnet run --project src/Inspector`.
- REPL syntax: `galaxy <seed> [radiusCells]` (defaults r21 ≈ 1615 cells, ~230 ms build),
  `polity <id>`, `chronicle [polityId]`, `map trade|economy|war|polity|zone|dev|lean|density`.
- Golden re-freeze rule: literals update ONLY for intentional generation changes, say so in
  the commit; the test comment carries the freeze history.
- Unity batchmode gates need the editor closed; live acceptance needs it open with the MCP
  bridge (not used this slice; see previous handoff via git for the full Unity gotcha list).
