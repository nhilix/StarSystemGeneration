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
- [x] 1. **Potential prior + Cosmic knobs** — `GalaxyPotential` (shape
      function as time-varying potential: fixed arm pattern, deepening
      core, merger perturbation slots), `Cosmic` knob family +
      galaxy-side knob registry + TUNING rows. Gate: potential unit
      tests; registry tests (order, uniqueness, round-trip).
- [x] 2. **Cosmic field stack + step loop** — working field arrays;
      inflow → transport → star formation → aging → death/enrichment
      over ~100–150 steps; observer hook. Gate: mass/metals conservation
      (P4 ledger); determinism (identical fields for same config); runs
      in the ~1s class.
- [x] 3. **Discrete features** — merger schedule (trail injection,
      traveling starburst, potential perturbation, stream residue),
      early globulars, AGN accretion epochs + sterilization waves,
      emergent nebulae at finalization; feature registry; deep-time
      cosmic chronicle (events 0–99). Gate: bounded feature counts
      across seeds; dated entries with world-years; determinism.
- [x] 4. **Present-day derivations replace paint** — RegionCell v2
      fields written at finalization; `PassDensitySummary` + voids +
      chokepoints read simulated mass; `PassStellarPopulation` deleted;
      Tier-1 per-hex density reads the cell layer × clumping noise;
      habitability history scalars. Gate: hex-tier mechanics suite
      green; stub tests replaced; density invariants (voids where gas
      never gathered, `MeanDensityTarget` normalization).
- [x] 5. **Evolution loop + emergence schedule** — biosphere fields;
      abiogenesis/aging/catastrophe/spread/sapience steps; sapient
      origins with maturation clocks → spaceflight dates; `Evolution`
      knob family; evolutionary chronicle (events 100–199). Gate:
      schedule shape bands (dates spread across the window, homeworld
      count sane); determinism.
- [x] 6. **Precursor waves** — vigor classes, domain-budget wave count,
      coarse civ-arc sim on the raster (rise/peak/decline, cause-typed
      endings, inter-wave contact); four living-residue channels
      (machine descendants, biosphere engineering, sterilization scars
      feeding the evolution loop, dormant remnants); typed sites →
      anchors via pre-commitment. Gate: waves/sites bounded; arcs
      chronicle end-to-end; scars visibly delay downstream emergence.
- [x] 7. **Seeding + entry integration** — homeworld anchors + species
      profiles from origins (machine species from precursor capitals);
      mineral anchors from the field; `PassResourceAnchors`/
      `PassHomeworlds` deleted; `EpochGenesis.Seed` consumes the
      schedule (channel 40 retired): entry epoch, starting conditions,
      late-emerger contact bonus into entry designs/starter fleets.
      Gate: full 40-epoch histories run; staggering is causal; stub
      tests replaced.
- [x] 8. **Artifact complete + load gates** — raster layer v2; appended
      layers (features, biospheres/origins, precursor registry);
      deep-time event payloads + `SimTraceView` cases; version refusal.
      Gate: round-trips; byte-identity; load-vs-rebuild;
      `LoadThenContinue_EqualsTheStraightRun`; golden regen.
- [x] 9. **REPL surface + watch mode** — `map` layers: gas, metallicity,
      stellar age, mineral richness, biosphere, emergence, features;
      `features`/`precursors` dumps; deep-time chronicle beneath the
      epoch chronicle; `gwatch` + `ewatch`. Gate: piped-stdin smoke via
      bash printf; watched run byte-identical to unwatched.
- [x] 10. **Shape acceptance + calibration** — multi-seed bands:
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

- **Metal units are narrative units** (task 2): with MetalYield 0.15,
  end-of-history stellar metallicity StarZ lands at median ≈ 0.017, core
  max ≈ 0.06 (seed 42, radius 8). `LifeViableZFloor` = 0.012 sits inside
  that distribution: ~62% of cells cross, staggered from step ~20 to the
  final step — the causal-staggering shape 0b consumes. If yield or
  enrichment mechanics change, re-place the floor (diagnose with a
  crossing histogram, don't trust the old constant).
- **Potential has no upper clamp** (task 1): the density paint's [0,1]
  clamp saturated the core and hid the core-deepening term; the potential
  is relative (gradients + normalized residue), so only the zero floor
  survives. Core peaks ≈ 1.45.
- Cosmic budget: radius 21 (1,615 cells) full run = **53 ms** Release —
  well inside the ~1s class with evolution still to come.
- **`WorldEvent.WorldYear` is long now** (task 3): deep-time strata write
  true world-years (−1.4e10 overflows int). EventLog.Append + the EVENT
  load path follow; the generational clock stays int.
- **Globulars are actively defended terrain** (task 3): staying metal-poor
  required *three* exclusions — inflow ×0.05, transport flows around them,
  and enrichment spill + AGN wave deposits skip them. Any future metal
  source must skip globular cells too, or tiny gas mass concentrates it
  into poisoned stellar Z (0.6 mass at Z 0.002 is easy to contaminate).
- **AGN cooldown bug**: `step - int.MinValue` overflows — sentinel "never
  fired" values near int.MinValue are a trap; use a small negative.
- AGN feed threshold set against the *simulated* core trajectory (peaks
  ~1.3 at radius 8, not the naive "core hoards gas" guess — the deep well
  burns as fast as it collects); nebula membership at 1.8× mean gas so
  arm-ridge concentrations connect instead of reading as isolated peaks.
- **Task-4 map-shape lessons** (all found by diagnosing distributions, not
  guessing): (1) 140 compounding steps of DriftRate 0.10 funneled the
  whole disc onto ridge cells — 74% voids; drift 0.04 + diffusion 0.04 +
  inflow exponent 1.5 restores a broad disc (38% voids at radius 8).
  (2) The simulated mass distribution is heavy-tailed; MeanDensity maps
  it through mass^0.45 (presentation only, mass stays raw/conserved).
  (3) Lean cannot derive from raw young-mass share (young ≈ 0.2% of
  14 Gyr of mass everywhere) — per the design doc it derives from
  SfActivity (young-bright > 0.5 normalized) and burned-early gas
  (old-dim: activity < 0.12 ∧ gas < 0.08); graveyard stays
  remnant-share > 0.45.
- Golden regenerated at task 4 (the painted galaxy became the simulated
  one — every downstream fact legitimately changed). Radius-12 seed-42
  history stays alive: 4 polities, 55 ports, 117 lanes, top tier 3.
- MeanDensityTarget's meaning shifted slightly: it normalizes the *cell
  mean* pre-clamp; the hex-level mean inherits clamp losses (band-tested
  ±0.15). GalaxyContext without a skeleton now generates as flatspace
  (mirrors RegionContext.For's null) — documented in Generator.
- **Task-5 emergence-schedule lessons**: (1) each pipeline stage being a
  slow Poisson lottery (low abio rate, low sapience rate) destroys
  causality — dates were pure roll noise and everything clustered
  precursor. Fixed: fast registration once richness allows; dates now
  trace to viability + growth + setbacks. (2) MaturationScaleGyr must
  exceed the abio→sapience lag or the "never before sapience" clamp sets
  every date. (3) The NativeHorizonGyr (0.7) re-roll trick both keeps
  natives *rare* and pulls richer late registrations into the current
  band. Counts: Current r8 3–7, r12 6–14, r21 21; precursors 8–86;
  natives 4–24.
- Era projection: precursor < −0.05 Gyr < current ≤ +0.35 Gyr < native ≤
  0.7 Gyr (structural cuts); the current band compresses onto the
  emergence window at EpochGenesis.Seed (task 7) — honest narrative
  compression per frame/time.md.
- **Task-6 architecture**: precursor arcs run *interleaved inside the
  evolution loop* (PrecursorArcEngine.Step per evo step), not as a
  post-pass — so sterilization scars (ScarPenalty 0.25 on hospitability)
  genuinely delay downstream abiogenesis/growth/spread, engineering
  bumps richness mid-history (uplift emerges on the schedule), and
  machine descendants append to the one origins list. Every precursor
  origin waves; extent is budget/class-limited (grand ≤ 3, 117-cell
  grand wave seen at r21). End-cause weights per class are a structural
  catalog (pockets collapse, grands transcend/war). Lane networks are
  expansion trees (cells−1 lanes, adjacency-verified).
- Wave shapes (seed 42): r8 28 waves / r12 44 / r21 81; sites 117–342
  (scars ~30%, dormant <10%); machine descendants 1–4 per galaxy;
  genesis total 127 ms at r21 — budget holds. Channels 52–59; events
  103–105.
- **Task-7 integration**: passes 3–4 deleted (species from origins,
  machine embodiment only via precursor descent; homeworld anchors at
  origin hexes claim first, site anchors dedupe by hex, mineral anchors
  roll against simulated MineralRichness). Channels 30 + 40 retired.
  `HomeworldRatePerCell` retired (config layer v5, GCONFIG reshaped);
  actors layer v3 (POLITY carries EntryGradeBonus = 0.05×richness +
  0.10×lateness — entry designs register at 0.5 + bonus, the contact
  bonus within F's boundary). EpochGenesis: current origins project
  date→window preserving spacing, actor order = schedule order,
  deep-time chronicle seeds the log floor. Globular cells override the
  hex-tier star table via the feature registry (RegionContext).
- Golden regen (task 7): seed 42 r12 = **8 polities** staggered epochs
  0–20; the epoch-0 elder ends with 27 ports vs 8 for the y475-entry
  latecomers — asymmetric emergence with visible compounding.
- **Task-10 volumes** (r12, 40 epochs, Release): seed 42 — 8 polities /
  102 ports / 313 lanes / 94 foundings / 665 famines / 2 machine
  polities; seed 99 — 16 / 152 / 391 / 136 / 849 / 4; seed 7 — 14 /
  155 / 445 / 141 / 689 / 1. Genesis 43–88 ms; full 40-epoch history
  under 400 ms. Famines-per-port run ~2× the pre-F baseline — more
  polities crowd the same disc; flagged for the eyeball (knob
  territory: SapienceRate, era horizons, or later-slice mechanics).
- **SapienceRate is not a clean polity-count dial** (tried 0.05→0.035:
  seed 42 went UP 8→12): changing the rate changes *when* cells
  register → which era band they land in. Current-era count is seed
  personality (5–16 at r12) — crowded and sparse galaxies both real.
  Documented in TUNING; kept 0.05.
