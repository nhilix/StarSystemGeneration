# Sim-Economy Deferred Ticket Batch — Design

Date: 2026-07-09. Parent spec: `2026-07-08-sim-economy-design.md`; tickets from
the sim-economy final review (`.superpowers/sdd/final-review-report.md` I-3,
M-1, and the carried minors in the ledger). Goal: finish the shipped economy
slice's known debts before stage 4 adds relations on top of them.

## 1. Overview

Seven fixes to the epoch simulation, one of them design-bearing: blockade
strain becomes a measured, serialized, consequence-bearing quantity
(`Polity.BlockadeLoss`, schema v5) instead of a TradeBlocked event that never
fires. The rest: extinction-correct war termination with territorial
restoration on defender victory, famine/war-scar stacking, safe capital
relocation, a shared-front Contested guard, and a sweep of small mechanical
items (landless wealth, sentinel refactor, decay-branch coverage, WarStarted
dedup verify-then-close).

## 2. Scope

**In:** blockade-loss classification + `Polity.BlockadeLoss` + serializer
schema v5 + weariness hook + REPL surfacing; war termination extinction rules
+ DefenderVictory front restoration; famine/war-scar shrink stacking; capital
relocation contest-avoidance; end-of-war Contested clear guard; landless-alive
wealth zeroing; `double.MinValue` sentinel refactor; stockpile-decay branch
tests; WarStarted-dedup verification and ticket closure.

**Out (parked, with owners):** exotics-deficit design, non-deficit war causes,
sanction (non-war) blockades, relations impact of blockade strain, provisions
stockpiles buffering sieges (all stage 4 — see §10); `SharesBorder` /
per-good capital-path perf (parked until galaxy sizes grow); REPL cosmetic
spec-drift notes M-2/M-3/M-4; all older non-econ tickets.

## 3. Blockade Strain (final-review I-3)

### Classification

`Economy.Route` returning null currently conflates "surplus exists but the
path is blockaded" with "no surplus exists anywhere". The fix: whenever a
routing attempt fails under the owner's `Economy.Passable` predicate, re-run
the identical BFS with the unblockaded predicate `c => !c.IsVoid`.

- Unblockaded path **exists** → the loss is blockade-induced; accrue it.
- No path even unblockaded → nothing to block (no surplus / geographically
  disconnected); accrue nothing. The unfed→famine path is unchanged.

Applied in both places routing can fail in `IncomePhase`:

- **Intra-polity deficit fill:** the unfilled `need` accrues to the owner.
- **Cross-polity trade:** a failed both-parties-passable capital-capital path
  accrues the matched `give` to **both** partners.

The undocumented `HasLiveWar` gate on the TradeBlocked event is **deleted**.
A neutral polity severed by a third-party war front now accrues strain and
can fire the event; a warring polity with a plain no-surplus famine cannot.

### State: `Polity.BlockadeLoss`

New `double` on `Polity`: last-epoch blockade-induced loss. Reset and
recomputed every income phase, mirroring the balance fields' last-epoch-net
semantics. Zero for extinct/landless polities.

**Serializer: schema v5.** `BlockadeLoss` appends to the polity record. No v4
loader (pre-release; consistent with v2→v3→v4 precedent). Version-literal
fixtures update in the final task alongside the golden re-freeze.

### Event

`TradeBlocked` fires when `BlockadeLoss > TradeBlockedFloor` (2.0, existing
constant); actor: the strained polity; location: its capital; magnitude: the
loss. Semantics now match parent spec §5's canonical scenario.

### Consequence: weariness hook

`ResolutionPhase.Weariness`'s hardship test widens to

```
ProvisionsBalance < 0 || OreBalance < 0 || BlockadeLoss > TradeBlockedFloor
```

(same 1.5× multiplier, same floor constant — one "strained" concept; a
separate dial only if tuning later demands it). Blockading an enemy now
hastens their breaking — the action→consequence loop the mechanic exists for.

### Surface

- REPL `polity` panel: a blockade-loss line.
- REPL economy `stats` block: total strain and strained-polity count.

### Stage-4 hooks (recorded, not built)

- **Sanction blockades:** non-war transit refusal is a relations action;
  `Economy.Passable` remains the single place transit legality lives, so
  stage 4 extends that predicate. Strain measurement then works unchanged.
- **Relations impact:** punitive relations consequences of strain read
  `Polity.BlockadeLoss` directly.

## 4. War Termination — Extinction Rules + Restoration

At the top of each war's resolution pass: if either belligerent is already
extinct, skip the front-contest loop entirely and go straight to termination
(extinct polities fight no fronts; closes the last window where a
same-epoch-extinct attacker could still capture cells).

**Outcome labeling** (replaces the current aBroke/dBroke-only mapping):

| Condition | Outcome |
|---|---|
| Defender extinct, attacker not | AttackerVictory |
| Attacker extinct, defender not | DefenderVictory |
| Both extinct | WhitePeace |
| Neither extinct | existing weariness/stockpile-floor logic |

**Territorial settlement on DefenderVictory:** front cells owned by the
attacker revert to the defender (the mirror of AttackerVictory's annexation —
front cells are by construction originally the defender's). Reverts emit
CellTaken events (actor: defender, magnitude 0) and run the capital/extinction
handler, so an attacker whose capital relocated into a captured front cell
relocates again. WhitePeace remains uti possidetis — keeping what you hold is
what distinguishes it.

## 5. Famine + War-Scar Stacking

The war-scar shrink (×`ScarShrink` 0.95) now applies to contested+scarred
owned cells **regardless** of whether the cell starved this epoch; growth
still skips starving cells. A besieged starving cell nets ×0.76 — famine and
siege are separate population pressures (user adjudication). The task-10
growth guard (feeding never shrinks) is untouched. Shape bands (famine
rarity, survival count) are re-verified at reference config during acceptance
and deltas reported; provisions stockpiles that would let a well-fed system
weather a siege are a stage-4 note (§10).

## 6. Capital Relocation Prefers Safe Cells

`HandleCapitalAndExtinction`'s relocation ordering becomes: **non-contested
first**, then highest development tier, then lowest spiral index; contested
cells are eligible only when every owned cell is contested. Double
LostCapital per epoch remains possible only when genuinely unavoidable.

## 7. Shared-Front Contested Guard (final-review M-1)

When a war ends, `Contested` is cleared on a front cell **only if no other
live war lists that cell in its `FrontCells`**. Chosen over filtering
already-contested cells out of goal candidates at declaration: the guard is
correct however overlaps arise in future stages and does not perturb goal
selection.

## 8. Mechanical Items

- **Landless-alive wealth:** the `owned.Count == 0` allocation branch zeroes
  `Wealth` (stockpile already decays there). Post-ghost-attacker-fix this is
  test-only state; kept correct anyway.
- **Sentinel refactor:** war-goal candidate pick replaces the
  `double.MinValue` sentinel with an explicit found-flag. No behavior change.
- **Decay coverage:** unit tests for the unpaid-upkeep ×2 and ore-deficit ×2
  stockpile-decay branches in `AllocationPhase`.
- **WarStarted dedup (verify-then-close):** confirm the `AtWar` candidate
  gate plus `ActionPhaseTests` one-live-war-per-pair assertion cover the old
  ticket; strengthen the test if thin; close it in the ledger.

## 9. Test Strategy

- **Constructed fixtures:** blockade-twin scenario where a *neutral* polity's
  corridor is severed by a third-party front → strain accrues, TradeBlocked
  fires; a warring polity with a no-surplus famine → no strain, no event.
  Weariness hardship multiplier engages at strain > floor. Two-war
  shared-front-cell fixture → ending one war leaves the cell Contested.
  Defender-extinct-while-attacker-breaks fixture → AttackerVictory, not
  WhitePeace. DefenderVictory restoration fixture → attacker-held front cells
  revert, CellTaken emitted. Stacked famine+scar cell → ×0.76 net. Capital
  relocation with a contested higher-dev cell available → picks the safe one.
- **Existing suites stay green** except where generation intentionally moves;
  §3–§7 all change generation, so the golden re-freezes **exactly once**, in
  the final task, together with the v5 version-literal fixtures (red-window
  convention if intermediate tasks go red).
- **Acceptance (REPL eyeball):** on a blockaded seed, strain visible in
  `polity`/`stats`; chronicle coherent (no white peace with an extinct
  polity, restored borders after defender victories); shape bands at
  reference config within reason, deltas reported.

## 10. Deferred to Stage 4 (recorded here so the brainstorm inherits them)

- Exotics-deficit design (final-review I-2: `ExoticsBalance` never negative →
  exotics wars/stagnation/imports dead code) and non-deficit war causes.
- Sanction blockades: relations-driven transit refusal via `Economy.Passable`.
- Relations impact of blockade strain (reads `Polity.BlockadeLoss`).
- Provisions stockpiles: stored food lets a system weather a siege the way a
  starving one cannot (user note, this brainstorm).
