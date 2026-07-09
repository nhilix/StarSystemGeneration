# Session Handoff — 2026-07-09

State: `main` at `93e2ca9` (econ-tickets merged ff, branch deleted; push to origin is manual and pending — now 37 commits ahead).
Core tests 175/175 (`dotnet test StarSystemGeneration.sln`); Unity untouched this batch (edit-mode suite unchanged at 50/50, editor not needed).
Durable task ledger: `.superpowers/sdd/progress.md` (git-ignored — read it before re-doing anything).
Living flow diagram (artifact, updated): https://claude.ai/code/artifact/67f20b6b-4e8c-4941-b88b-fc071c1c64f4 — source `docs/diagrams/generation-flow.html`; update + republish per the memory note when layers change.

## Just shipped (context, don't redo)

- **Sim-economy deferred-ticket batch** — spec
  `docs/superpowers/specs/2026-07-09-econ-deferred-tickets-design.md`, plan
  `docs/superpowers/plans/2026-07-09-econ-deferred-tickets.md`. Blockade strain is now
  measured state: `Polity.BlockadeLoss` (re-route classification replaces the `HasLiveWar`
  gate; failed routes count as blockade only when an unblockaded path exists), serialized
  **schema v5** (no v4 loader), feeds war weariness (1.5× hardship above
  `Economy.TradeBlockedFloor` 2.0) and the REPL (`polity` line + `stats` strain block).
  War termination: extinct side loses outright (both extinct → white peace), extinct
  belligerents fight no fronts, DefenderVictory restores attacker-held front cells,
  extinction zeroes strain. Famine (×0.8) and war-scar (×0.95) now stack (×0.76). Capital
  relocation prefers uncontested cells. Ending wars no longer un-contest cells another live
  war fights over (M-1). Landless polities zero Wealth; `double.MinValue` sentinel gone;
  decay branches covered; WarStarted dedup verified + closed.
  Golden (seed 7 r3): 2 polities / 36 events — counts UNCHANGED through the whole batch
  (only the header literal moved to v5); the plan's red window never opened. Reference
  acceptance 42/r21 identical to pre-batch baseline.
- **Sim economy slice (stages 2–3)**, **orbit-diagram system view**, **setup knobs**,
  **Unity atlas** — see git history / previous handoff (`git show 8e5023b:docs/HANDOFF.md`).

## Next up (pick one)

1. **Sim stages 4–6** (biggest): relations ladder/federations/schisms/vassalage (4), news &
   stances (5), event→POI compiler + world-state handoff (6). Parent spec §7.4–7.7.
   Brainstorm→spec cycle per slice; the stage-4 brainstorm inherits the parked design
   tickets below (exotics deficit, non-deficit war causes, sanction blockades, relations
   impact of strain, provisions stockpiles — see 2026-07-09 spec §10).
2. **Unity economy parity + atlas polish**: trade/economy/war layers and polity/econ panel in
   the Unity atlas (editor-open live acceptance); smooth pan/zoom; cell hover highlight;
   Atlas.unity as build scene 0; orbit-diagram §8 follow-ups.

## Deferred tickets

Parked for the stage-4 brainstorm (design-bearing, spec 2026-07-09 §10):
- **Exotics deficit structurally impossible** (`ExoticsBalance` never negative → exotics
  wars/stagnation/imports dead code) + war-goal variety / non-deficit war causes.
- **Sanction blockades** (non-war transit refusal via `Economy.Passable`) and **relations
  impact of strain** (reads `Polity.BlockadeLoss`).
- **Provisions stockpiles** (stored food buffers sieges — user note).
- **Blockade-strain tuning** (final-review note, same dial from both ends): strain
  under-fires in open space (routes detour small fronts; bites only at chokepoint/void
  pinches — organic TradeBlocked ≈ 0 at reference scale, nonzero strain seen at seed 99)
  AND can multi-count one surplus across several blocked partners.

Carried minors (econ, final-review triage): blockade-retry pattern duplicated
intra/cross-polity; TradeBlocked guard piggybacks on famine `unfed` dict membership
(equivalent today, wants a comment/direct guard before stage 4); `shortages` bool naming;
restore-test CellTaken assert (strengthen with `Magnitude == 0` next touch).

Perf (parked until galaxy sizes grow): `SharesBorder` O(cells×pairs); per-good capital-path
recompute; re-route classification doubles BFS on failure paths (new this batch).

Older (carried): ClearHexSelection screen guard; multi-primary doc note; companion-moon pad
scaling untested; SystemPanel.Highlight no-op on unknown BodyRef; orbit-diagram §8 follow-ups
(labels, motion, hover recolor, pan/zoom); golden v3 CONFIG-line literal test; sim-only knobs
rebuild preview; `_setupErrorLabel` ScrollView; IsShapeOnly guard; dedupe show-preview;
`_radiusField.isDelayed`; REPL `galaxy` knob parity; REPL goto wrap; StatsReport ignores
satellites.

## Process conventions (established, keep following)

- Superpowers flow: brainstorm → spec (user reviews) → plan → subagent-driven execution →
  final whole-branch review (fable) + one fix wave (controller-applied) →
  finishing-a-development-branch → merge to main locally, verify tests, delete branch.
  User pushes manually.
- Implementer reports MUST include verbatim test-summary lines; "gate mandatory, never kill
  processes" language stays in every dispatch prompt. Second consecutive zero-gate-incident
  batch — plan-contains-full-code + explicit-contingency-rules keeps working.
- Coverage-lock tests that pass first are fine when the plan says so explicitly — dispatch
  language must pre-declare which tests are TDD-red and which are locks, or reviewers flag it.
- Cheap model (haiku) for transcription tasks, sonnet for integration/judgment tasks and all
  task reviews, fable for the final whole-branch review.
- Plan defect class to avoid: name EVERY file a task must touch (task 7 promised a handoff
  ledger closure its file list didn't include — workers couldn't stage it; fix wave caught it).
- ProjectSettings churn stays uncommitted; never let it ride into commits.

## Gotchas (hard-won)

- PowerShell piping to the REPL mangles the FIRST stdin line (BOM) — use bash
  `printf 'cmd\n...' | dotnet run --project src/Inspector`.
- The file is tracked as `docs/HANDOFF.md` (uppercase); `git add docs/handoff.md` silently
  misses it on Windows.
- Grep/diff rendering can show `//` comments as `\ ` — two reviewers flagged phantom
  malformed comments this batch; verify with Read before "fixing".
- REPL syntax: `galaxy <seed> [radiusCells]` (defaults r21 ≈ 1615 cells, ~275 ms build),
  `polity <id>`, `chronicle [polityId]` (tails 60 events), `map trade|economy|war|polity|zone|dev|lean|density`.
- Golden re-freeze rule: literals update ONLY for intentional generation changes, say so in
  the commit; the test comment carries the freeze history.
- Unity batchmode gates need the editor closed; live acceptance needs it open with the MCP
  bridge (not used this batch; see the unity-atlas-era handoff via git for the full list).
