# Slice C Ledger — Substrate Catalogs (slice-c-substrate-catalogs)

Ordered task checklist per the kickoff prompt
(`2026-07-09-slice-c-kickoff-prompt.md`). Updated as work proceeds — the
resumability record. New code in `src/Core/Substrate/`
(`StarGen.Core.Substrate`): pure data + pure functions, roll-free, no sim
contact. Reuses `StellarLean` (`RegionCell.cs`) and `Embodiment`
(`SpeciesProfile.cs`) as natural-substrate vocabulary; everything else taken
as plain arguments (B runs parallel and may reshape `RegionCell`).

## Tasks

- [ ] 1. **Goods catalog** — 17 goods, stable int ids (`GoodId`), tiers
      (raw/processed/capital), recipes with Standard/Advanced variants
      (advanced = exotics-gated, higher grade base, tech-tier gated), input
      quantities per output unit. Tests: 17 ids stable + unique, recipe
      closure (every input a catalog good), advanced variants contain
      exotics-lineage inputs, raw goods recipe-free.
- [ ] 2. **Grade & Stock** — `Stock(good, qty, grade)`, quantity-weighted
      blend, `Effective(useCase) = qty × GradeMultiplier`, processed-grade
      formula (recipe base × input blend × facility tier, tech-tier ceiling),
      display bands (crude → precursor-grade; precursor above every tech
      ceiling). Tests: blend math, ceiling caps, band edges, adv > std grade
      for same inputs.
- [ ] 3. **Demand profiles** — population bands (subsistence / SoL / luxury)
      per embodiment (machine pops draw Fuel/Machinery/Compute, no
      Provisions/Medicine; lithics eat little, want machinery) + institutional
      use-cases (industry, movement, military, technology) with the design's
      priority order. Legality schema (`LegalityLevel` reuse + tariff pair).
      Tests: profile invariants per design table.
- [ ] 4. **Infrastructure catalog** — 15 rows (keystone port + 14 buildable;
      design-doc prose says "fourteen" — amend prose in same branch, flag to
      user), stable ids, families, tiers 1–3, build cost in real goods +
      construction time + upkeep. Production formula as pure function
      (base × terrain × labor(pop × affinity, compute automation) ×
      machineryGrade multiplier); organic baseline. Tests: id/count stability,
      cost-goods closure, formula fixtures.
- [ ] 5. **Potentials & siting** — richness functions over raster fields
      (ore/volatiles/biosphere/exotics from density, lean, metallicity,
      anchors), embodiment affinity, raw-grade-from-terrain, per-type siting
      scores over a plain `CellSite` struct. Tests: belt cell out-scores
      garden world for Mine and vice versa for Agri, richness bounds,
      affinity fixtures.
- [ ] 6. **REPL surface** — `goods` / `infra` commands dumping the catalogs;
      potentials + siting evaluated for a sample cell (real cell if a galaxy
      is loaded). Piped-stdin smoke via bash printf.
- [ ] 7. **Fresh-eyes whole-branch review** subagent + one fix wave.
- [ ] 8. **Gates**: `dotnet test` green incl. hex-tier suite · Unity `.meta`
      files for every new file/folder under `src/Core` · REPL smoke.
- [ ] 9. **User gate: REPL eyeball acceptance** — catalog reads like the
      design doc's tables.
- [ ] 10. **Wrap-up**: merge on user nod · HANDOFF · update Slice D kickoff
      expectations (D needs B+C) · flip kickoff checkbox · push on user
      say-so.

## Notes / surprises

- Design-doc discrepancy: `infrastructure.md` prose says "Fourteen types in
  five families" but the table lists 15 rows (keystone Port + 4+4+4+2).
  Reading: 14 *buildable* types plus the keystone. Implementing all 15 table
  rows; prose amended in this branch. **Flag to user at eyeball gate.**
