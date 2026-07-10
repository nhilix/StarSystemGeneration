# Session Handoff — 2026-07-10 (Slice F: Deep genesis — merged)

State: `main`, merged locally, **not yet pushed** (push on user say-so).
Tests 374/374 green — hex-tier suite untouched at 100%. ProjectSettings
churn remains uncommitted as always.

## What this session did: Slice F of the epoch-sim rebuild, merged

The galaxy's past is real: the cosmic clock (structure formation over the
region-cell lattice) and the evolutionary clock (life, sapience,
precursors) replaced the painted seeding passes with simulated history.
Density, metallicity, leans, minerals, biospheres, homeworlds, species,
and *when each polity enters* are all residue of causes — and the galaxy
has archaeology. Ledger (the full task/decision/surprise record):
`docs/superpowers/plans/2026-07-09-slice-f-ledger.md`.

- **Cosmic sim** (`src/Core/Genesis/`: `CosmicSim`, `CosmicState`,
  `CosmicFeatures`, `CosmicResidue`, `GalaxyPotential`): the shape knobs
  became a time-varying potential prior; 140 deep-time steps of inflow →
  transport → star formation → aging → death/enrichment on conserved
  mass/metals ledgers (P4, tested exact); discrete features that interact
  with the field stack — rolled mergers (foreign-metallicity trail
  injection, traveling starburst, decaying potential perturbation), early
  globulars (actively kept metal-poor: inflow/transport/spill/AGN all
  skip them), AGN accretion epochs (sterilization waves, conserved
  accretion, quiescent tail), emergent nebulae + supernova remnants at
  finalization. `CosmicResidue.Compress` writes RegionCell v2 fields
  (gas fraction, cohort mix, mineral richness, SF activity, habitability
  scalars); MeanDensity maps mass^0.45 (heavy-tailed distribution — the
  1.0-exponent map was 74% voids); lean derives from SF activity + gas
  per the design doc, NOT raw young-mass share (young ≈ 0.2% of 14 Gyr
  everywhere).
- **Tier-1 rewired**: `DensityField.At(skeleton, hex)` = inverse-distance
  cell interpolation × the old clumping noise; the analytic paint is
  deleted (its math lives on in `GalaxyPotential`); `BuildShape` runs the
  cosmic sim (preview parity holds, ~50–90 ms full genesis at r21).
  Globular feature cells override the hex-tier star table via
  `RegionContext` (cache invalidated on re-run).
- **Evolutionary sim** (`EvolutionSim`, 400 × 35 Myr steps): abiogenesis
  gated by the cosmic metallicity-floor crossing; richness growth;
  catastrophes (AGN waves land at their cosmic step); slow panspermia;
  sapience registration → **the emergence schedule** (`SapientOrigin`:
  abio/sapience/spaceflight dates, era striping Precursor < −0.05 Gyr <
  Current ≤ +0.35 Gyr < native ≤ 0.7 Gyr; beyond doesn't register).
  Dates are causal: viability + growth + setbacks decide (key lesson: if
  every stage is a slow Poisson lottery, dates are roll noise — rates
  are fast-once-allowed). **The playable floor**: <2 current polities
  stretches the era over the nearest natives (never precursors).
- **Precursor arcs** (`PrecursorArcEngine`) run *inside* the evolution
  loop so residue is causal: every deep-time origin waves (era-cutoff
  activation sweep — no date dead zones); vigor classes under a domain
  budget (grand ≤ 3); terrain-following expansion trees with real lane
  networks; cause-typed endings (class-conditioned weights); war/plague
  **sterilization scars** (ScarPenalty 0.25) that delay downstream life
  — late bloomers have causes; peak-phase biosphere engineering;
  inter-wave contact (war/absorb/partition); typed site registry
  (capital/ruins/battlefield/scar/engineered/megastructure, dormant
  flags); **machine descendants**: transcendence can seed a current-era
  machine origin at the old capital.
- **Seeding derives**: passes 1–4 deleted. Species from current-era
  origins (machine embodiment ONLY via precursor descent; setbacks raise
  militancy floor); homeworld anchors at origin hexes; precursor-site
  anchors from wave sites; mineral anchors roll against simulated
  richness. `HomeworldRatePerCell` retired — polity count is causal and
  **seed personality** (5–16 at r12; SapienceRate moves it
  non-monotonically, see TUNING).
- **Entry is schedule-driven** (`EpochGenesis`): channel 40 retired;
  actor order = spaceflight-date order projected onto the emergence
  window (spacing preserved); `PolityRecord.EntryGradeBonus`
  (0.05×richness + 0.10×lateness — the late-emerger contact bonus) lifts
  entry design grades. The deep-time chronicle seeds the event log floor
  — one history reads bottom-to-top (`WorldEvent.WorldYear` is **long**
  now; deep years render as "−6.20Gy" via `SimTraceView.YearLabel`).
- **Artifact**: config v5 (GKNOB lines, `GalaxyKnobRegistry` — the
  galaxy-side knob twin; GCONFIG dropped the retired rate), raster v2
  (residue fields on CELL), actors v3 (EntryGradeBonus), + appended
  features/origins/precursors layers. Load restores the full physical +
  living galaxy without re-running genesis; byte-exact round-trips,
  version refusal, LoadThenContinue green. Golden frozen at the final
  format.
- **Knobs**: `Cosmic` (6) + `Evolution` (10) families on GalaxyConfig
  behind `GalaxyKnobRegistry` + TUNING.md rows. Events: cosmic 0–2,
  evolutionary 100–105 (next evolutionary free: **106**); economic next
  free 207, military 403. `RollChannel` next free: **60** (41–59 used;
  30 + 40 retired).
- **REPL**: `map` layers gas/metal/age/minerals/bio/emergence/features ·
  `features` / `precursors [id]` dumps (a wave arc readable end-to-end) ·
  `chronicle deep` · **`watch <seed> [radius] [epochs] [frameMs]`** —
  the whole story (cosmic gas → life+waves → domains) as an **in-place
  ANSI animation**, every sim step a frame (`FrameAnimator`: cursor-up +
  erase-per-line, lines clipped to terminal width — wrap = drift;
  sampled sequential fallback when piped); `gwatch`/`ewatch` animate in
  place too. Observation is byte-neutral (tested).
- Eyeball-accepted 2026-07-10 after two watch waves (in-place animation
  rebuild per user; header/wrap-drift fix). Flagged as knob territory:
  famines-per-port ~2× pre-F (more polities crowd the same disc), polity
  count variance as seed personality.

## Next up

1. **Slice G (Interior & corporations)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-10-slice-g-kickoff-prompt.md`
   (complete: reading list, contact surfaces F/E left, scope, boundary).
2. **Push main** when ready (this merge is local-only).
3. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · REPL eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings stays
uncommitted; bash printf for REPL piping; parallel slices never share a
checkout — take a `git worktree` each; **every calibration constant goes in
a knob registry + TUNING.md** (epoch-side `KnobRegistry`, galaxy-side
`GalaxyKnobRegistry`); every new `src/Core` file gets a two-line `.meta`
with a fresh guid. Older carried minors: see
`git show a1f5843~40:docs/HANDOFF.md`.
