# Session Handoff — 2026-07-09 (Slice C: Substrate catalogs — merged)

State: `main`, **not pushed** (push on user say-so). Tests 256/256 green
(hex-tier suite untouched at 100%). ProjectSettings churn remains uncommitted
as always.

**⚠ Parallel-session note:** a Slice B session is (or was) running in the
*original* checkout on `slice-b-two-plane-state` — branched off slice C's tip
`65446a5`, so B's history contains C's pre-fix-wave commits; C's fix wave
(`b6b2dd5`+) is on main only. Slice C wrapped from the dedicated worktree
`../StarSystemGeneration-sliceC` to avoid touching B's uncommitted tree. That
worktree ends parked on the `slice-c-substrate-catalogs` branch so `main` is
free; remove it once pushed (`git worktree remove ../StarSystemGeneration-sliceC`).
B's wrap-up must merge/rebase onto this main (HANDOFF + Slice D kickoff will
conflict; take both halves) and fill in its half of the D kickoff prompt.

## What this session did: Slice C of the epoch-sim rebuild, merged

Pure Core data + functions in `src/Core/Substrate/` (`StarGen.Core.Substrate`)
— no sim contact, roll-free, unit-tested standalone (50 new tests):

- **Goods** (`Goods.cs`) — the 17-good vocabulary, frozen int ids 0–16 (the
  meaning of `Epoch/Policies.cs` goods-keyed dictionaries), tiers
  raw/processed/capital, recipes with per-unit input quantities and
  Standard/Advanced variants (standard exotics-free; advanced exotics-gated,
  higher grade base, tech-gated). Recipe graph acyclic, deepest chain
  Ore→Alloys→Machinery→Ship Components (4 nodes, pinned by test).
- **Grade & Stock** (`Grades.cs`) — grade scalar [0,1]; output grade = recipe
  base × input blend × facility tier × tech tier, capped at tech ceilings
  (0.55/0.75/0.90); precursor floor 0.92 above every ceiling; display bands
  crude→precursor-grade; `Stock` blend (qty-weighted mean) +
  `Effective(useCase)` with per-use-case grade sensitivity.
- **Demand & legality** (`Demand.cs`) — population bands per embodiment
  (machine pops draw Fuel/Machinery/Compute, never Provisions/Medicine;
  lithics eat ×0.4 but demand Machinery), institutional profiles, the design's
  priority order; `GoodLegality(Level, Tariff)` schema. Weights are normalized
  shares — absolute rates are economy-config knobs D applies.
- **Infrastructure** (`Infrastructure.cs`) — 15-row catalog (keystone Port +
  14 buildable; design prose amended from "fourteen" to name the keystone),
  frozen ids 0–14, build cost/upkeep in real goods, construction years.
  Luxuries deliberately has no producing facility (corporate niche, Slice G).
- **Production** (`Production.cs`) — output = base(type,tier) × terrain ×
  labor(pop × affinity, compute substitutes) × machineryGrade; tier factors
  ×1/×2.5/×6 output, ×1/×3/×8 cost; organic subsistence baseline.
- **Potentials & siting** (`Potentials.cs`, `Siting.cs`) — ore/volatiles/
  biosphere/exotics richness + raw grade over **plain raster args**
  (`CellFields`: density, lean, metallicity, anchors — class records, not
  record structs: Unity 6000.5 compiles src/Core as C# 9); embodiment
  affinity mirrors the design's species-terrain table; per-type siting scores
  over `CellSite` (connectivity/port/dev context caller-supplied — B owns it).
- **REPL**: `goods`, `infra`, `infra <q> <r>` (`SubstrateView`,
  invariant-culture + culture-flip test). Eyeball-accepted 2026-07-09; review
  subagent ran, one fix wave applied (ledger notes).

Ledger: `docs/superpowers/plans/2026-07-09-slice-c-ledger.md` — Notes /
surprises are required reading for D (design-doc calls: 15-type catalog,
producer-less Luxuries, automation-as-substitution).

## Next up

1. **Slice B (Two-plane state)** — in flight in the parallel session; its
   wrap-up reconciles with this main (see the parallel-session note).
2. **Slice D (Segments & markets)** — needs B+C. Kickoff prompt drafted at
   `docs/superpowers/plans/2026-07-09-slice-d-kickoff-prompt.md` with the C
   half concrete; **B's session fills in the B half at its wrap-up**.
3. **Push** — main carries slices A+C unpushed; push on user say-so.
4. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · REPL eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings stays
uncommitted; bash printf for REPL piping; HANDOFF.md is uppercase in git.
New this session: parallel slices must not share a checkout — take a
`git worktree` each. Older carried minors: see
`git show a1f5843~40:docs/HANDOFF.md`.
