# Slice F Ledger — Deep Genesis (slice-f-deep-genesis)

Ordered task checklist per the kickoff prompt
(`2026-07-09-slice-f-kickoff-prompt.md`). Updated and committed as tasks
complete — the resumability record. F makes the past real: the cosmic clock
(structure formation over the region-cell lattice) and the evolutionary
clock (life, sapience, precursors) replace the painted seeding passes with
simulated history. Design sources: `genesis/cosmic-genesis.md`,
`genesis/life-and-precursors.md`, `frame/time.md`,
`frame/space-and-travel.md`.

**Scope addition (user, at scope nod): watch mode** — the REPL can render
map frames as genesis steps (and epoch steps) execute, so the eyeball phase
sees the evolution happen instead of deducing it from the final state.
Observation is pure: a watched run is byte-identical to an unwatched one.

Architecture decisions (made at kickoff, flag deviations):

- **New namespace `StarGen.Core.Genesis`** (`src/Core/Genesis/`): the two
  deep-time sims live galaxy-side, upstream of the epoch sim —
  `CosmicSim`, `EvolutionSim`, `PrecursorArcs`, shared `GenesisObserver`
  hook. `SkeletonBuilder.Build(config)` stays the single entry: shape →
  cosmic sim → evolution sim → derived seeding. `BuildShape` (atlas
  preview) runs the cosmic sim too — the pixel-identical-preview promise
  holds because present-day density *is* the simulated field now (budget
  ~1s is acceptable for preview).
- **Working state vs. residue**: the field stack (`Gas`,
  `StarsYoung/Mid/Old`, `Remnants`, `Metals`) is sim-internal working
  state (arrays indexed by spiral index). Present-day values are
  compressed onto `RegionCell` at finalization (raster v2): gas fraction,
  cohort mix, metallicity, mineral richness, SF activity, habitability
  history scalars (life-viable year, last sterilization, stability since),
  biosphere fields (age, richness, life character). The hex tier stays a
  pure never-persisted function; Tier-1 per-hex density = interpolated
  present-day cell density × hex-scale clumping noise
  (cosmic-genesis.md §Tier-1 consequence).
- **Skeleton gains genesis registries**: `Features` (mergers, globulars,
  nebulae, AGN epochs — identity, date, cell footprint, hex-tier override
  tag), `Origins` (the emergence schedule: every sapient origin, current +
  precursor, with homeworld hex, species-profile seed, spaceflight date),
  `PrecursorWaves` (vigor class, capital, extent, end cause/date, typed
  site list), and the deep-time event list. `EpochGenesis.Seed` copies
  deep-time events into the sim's event log so one chronicle reads
  bottom-to-top (cosmic 0–99, evolutionary 100–199).
- **Knob families**: `Cosmic` + `Evolution` are *galaxy-side* calibration
  (they shape the skeleton, not the epoch sim), so they live on
  `GalaxyConfig` with a registry mirroring the E pattern (name-sorted,
  docs, tests enforce; TUNING.md rows) and are artifact-stamped in the
  config layer. Catalog-style data (vigor classes, end-cause weights,
  cohort aging rates) is data-as-code with a TUNING structural note.
- **Roll channels**: cosmic block appends from 41, evolutionary/precursor
  block follows — append-only, keyed (step, cell/actor, channel), fixed
  spiral-index order everywhere. Channel 40 (`EpochEntrySchedule`) retires
  when the causal schedule lands (value never reused).
- **Anchors survive as the hex-tier contact surface, derived causally**:
  homeworld anchors at sapient-origin hexes (species profiles seeded from
  origin context; machine species descend from precursor capitals),
  precursor-site anchors from the wave site lists (pre-commitment
  mechanism), mineral-rich anchors from the simulated mineral-richness
  field. Passes 2–4 (`PassStellarPopulation`, `PassResourceAnchors`,
  `PassHomeworlds`) are deleted with their stub tests — the one legitimate
  test-replacement zone.
- **Watch mode**: `CosmicSim.Run`/`EvolutionSim.Run` accept an optional
  per-step observer (step index, world-year, read-only field view); the
  REPL `gwatch <layer> [every N]` re-runs genesis rendering frames, and
  the epoch runner gets `ewatch <epochs> <layer>` rendering `emap` per
  epoch. Frames print sequentially (pipe-safe, scrollback-friendly).
- **Entry starting conditions**: entry epoch, homeworld, species seed from
  the schedule; maturation quality scales starting conditions with the
  late-emerger contact bonus (elevated starting Astrogation/Industrial
  feeding E's entry designs + starter fleets). May write TechTierStub-
  adjacent state; real tech domains are G.
- **E lessons carried**: bootstrap by furniture where a chain can't
  self-start; check availability before magnitude; report volumes not
  counts; every new `src/Core` file gets a two-line `.meta` with a fresh
  guid; REPL piping via bash `printf`; Read before fixing phantom `\ `
  comments; golden regenerated deliberately per history-changing task,
  frozen once at slice end.

**Boundary**: no archaeology/salvage consumption or POI compilation (I);
native-emergence crises recorded, not resolved (H); no real tech domains
(G); dormant remnants are registry entries, not encounters; no epoch-sim
mechanics changes beyond genesis inputs and entry-time state.

## Tasks

- [x] 0. **Branch + ledger** — branch `slice-f-deep-genesis` from main;
      this file.
- [ ] 1. **Potential prior + Cosmic knobs** — `GalaxyPotential` (shape
      function as time-varying potential: fixed arm pattern, deepening
      core, merger perturbation slots), `Cosmic` knob family +
      galaxy-side knob registry + TUNING rows. Gate: potential unit
      tests; registry tests (order, uniqueness, round-trip).
- [ ] 2. **Cosmic field stack + step loop** — working field arrays;
      inflow → transport → star formation → aging → death/enrichment
      over ~100–150 steps; observer hook. Gate: mass/metals conservation
      (P4 ledger); determinism (identical fields for same config); runs
      in the ~1s class.
- [ ] 3. **Discrete features** — merger schedule (trail injection,
      traveling starburst, potential perturbation, stream residue),
      early globulars, AGN accretion epochs + sterilization waves,
      emergent nebulae at finalization; feature registry; deep-time
      cosmic chronicle (events 0–99). Gate: bounded feature counts
      across seeds; dated entries with world-years; determinism.
- [ ] 4. **Present-day derivations replace paint** — RegionCell v2
      fields written at finalization; `PassDensitySummary` + voids +
      chokepoints read simulated mass; `PassStellarPopulation` deleted;
      Tier-1 per-hex density reads the cell layer × clumping noise;
      habitability history scalars. Gate: hex-tier mechanics suite
      green; stub tests replaced; density invariants (voids where gas
      never gathered, `MeanDensityTarget` normalization).
- [ ] 5. **Evolution loop + emergence schedule** — biosphere fields;
      abiogenesis/aging/catastrophe/spread/sapience steps; sapient
      origins with maturation clocks → spaceflight dates; `Evolution`
      knob family; evolutionary chronicle (events 100–199). Gate:
      schedule shape bands (dates spread across the window, homeworld
      count sane); determinism.
- [ ] 6. **Precursor waves** — vigor classes, domain-budget wave count,
      coarse civ-arc sim on the raster (rise/peak/decline, cause-typed
      endings, inter-wave contact); four living-residue channels
      (machine descendants, biosphere engineering, sterilization scars
      feeding the evolution loop, dormant remnants); typed sites →
      anchors via pre-commitment. Gate: waves/sites bounded; arcs
      chronicle end-to-end; scars visibly delay downstream emergence.
- [ ] 7. **Seeding + entry integration** — homeworld anchors + species
      profiles from origins (machine species from precursor capitals);
      mineral anchors from the field; `PassResourceAnchors`/
      `PassHomeworlds` deleted; `EpochGenesis.Seed` consumes the
      schedule (channel 40 retired): entry epoch, starting conditions,
      late-emerger contact bonus into entry designs/starter fleets.
      Gate: full 40-epoch histories run; staggering is causal; stub
      tests replaced.
- [ ] 8. **Artifact complete + load gates** — raster layer v2; appended
      layers (features, biospheres/origins, precursor registry);
      deep-time event payloads + `SimTraceView` cases; version refusal.
      Gate: round-trips; byte-identity; load-vs-rebuild;
      `LoadThenContinue_EqualsTheStraightRun`; golden regen.
- [ ] 9. **REPL surface + watch mode** — `map` layers: gas, metallicity,
      stellar age, mineral richness, biosphere, emergence, features;
      `features`/`precursors` dumps; deep-time chronicle beneath the
      epoch chronicle; `gwatch` + `ewatch`. Gate: piped-stdin smoke via
      bash printf; watched run byte-identical to unwatched.
- [ ] 10. **Shape acceptance + calibration** — multi-seed bands:
      emergence spread, homeworld count, precursor sites bounded, void
      fraction sane, genesis budget held; TUNING consequences rows.
      Gate: full `dotnet test` green, hex tier untouched.
- [ ] 11. **Fresh-eyes whole-branch review** subagent + one fix wave.
- [ ] 12. **USER: REPL eyeball** — maps that visibly tell the formation
      story (a pointable merger stream, metal-rich arms vs burned-out
      core, life clustered where stability allowed) and a precursor arc
      readable end-to-end (rise, extent, dated fall, ruins at its ports,
      a polity entering late because that wave's war sterilized its
      cradle) — watched live via `gwatch`.
- [ ] 13. **Golden freeze + wrap-up** — golden frozen at final format ·
      USER merge decision · HANDOFF · **write Slice G kickoff prompt**
      (interior & corporations — consumes E's commander slots and D's
      niches; read the roadmap row) · flip the kickoff checkbox · push
      only on user say-so.

## Notes / surprises

- (running log)
