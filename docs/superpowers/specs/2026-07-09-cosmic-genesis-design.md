# Cosmic Genesis (Design Pass 0a) — The Deep-Time Structure Simulation

Status: **draft — awaiting user review**
Date: 2026-07-09
Parent: `2026-07-09-epoch-sim-master-frame-design.md` (pass 0a of the eight-pass
roadmap). Product doc: `docs/design/genesis/cosmic-genesis.md` (filled by this pass).

## 1. Overview

The cosmic clock: a deep-time simulation over the region-cell lattice that replaces
the current analytic seeding of galactic structure and composition. Today the galaxy's
shape is a static potential-times-noise formula (`DensityField.ShapeAt` ×
`ValueNoise`) and cell composition (`StellarLean`, `Metallicity`) is painted by
independent noise channels (seeding pass 2). After this pass, structure and
composition are the **residue of a simulated formation history**: gas gathers, stars
form and age, supernovae enrich, mergers disrupt, and the present-day galaxy is
whatever ~14 compressed Gyr left behind (P5 all the way down).

Output contract: **the physical galaxy** — present-day cell fields, a dated feature
registry, per-cell habitability history, and the deep-time stratum of the chronicle —
consumed by pass 0b (life & precursors), by the skeleton seeding passes, and by
Tier-1/Tier-3 hex generation.

## 2. Decisions

- **Guided emergence** (chosen over fully-emergent structure and enrichment-only):
  the analytic shape survives as a **gravitational-potential prior** — where matter
  *wants* to be — and the sim decides where matter actually ends up. Astrophysically
  defensible (density-wave theory: arms are patterns matter moves through); keeps
  galaxies art-directable via the existing knobs; makes composition, ages, nebulae,
  and graveyards genuine residue. Fully-emergent was rejected for tuning risk and
  indirect knobs; enrichment-only because structure and composition would never
  causally connect.
- **Hybrid mechanism: fields + features** (chosen over pure field stack and
  agent/particle): continuous per-cell field stack for smooth causal evolution, plus
  sparse discrete feature objects (mergers, globulars, nebulae, the nucleus) for
  identity — nameable, datable, chronicle-referenceable. Matches the existing
  anchor/pre-commitment pattern.
- **Strictly 2D** (user constraint): the spatial model stays hex-radial. Off-plane
  phenomena (globular clusters, merger streams) enter as their projections onto the
  lattice — a cluster is a cell feature, a stream is a linear trail of cells —
  with off-plane-ness as descriptive flavor only, never a third coordinate.
- **Tier-1 architecture change** (consequence, flagged and accepted): the density
  field stops being a pure analytic function. Present-day per-hex density =
  interpolated evolved cell fields × hex-scale noise; Tier 1 reads a persisted,
  cell-resolution artifact layer, as the hex tier already reads the skeleton. The
  purity that matters is preserved: hex tier never persisted, everything
  deterministic from (config, coordinate). `ShapeAt` survives only as the potential
  inside the sim.

## 3. The Potential Prior

- Core + disc + log-spiral arms with the existing knobs (`CoreRadius`,
  `DiscFalloff`, `ArmCount`, `ArmTightness`, `ArmWidth`, `ArmStrength`), reinterpreted
  as potential parameters — unchanged meaning, unchanged defaults.
- Mildly time-varying: the arm pattern is fixed; the core deepens slowly over deep
  time; merger events add transient, decaying perturbations.

## 4. The Field Stack and Step Loop

Per region cell, conserved quantities (P4): `Gas`, `StarsYoung`, `StarsMid`,
`StarsOld`, `Remnants`, `Metals`.

~100–150 deep-time steps over the compressed ~14 Gyr; fixed spiral-index order;
rolls keyed by (step, cell, channel):

1. **Inflow** (early steps only): primordial gas lands potential-weighted,
   noise-clumped.
2. **Transport**: gas drifts along the potential gradient plus slight diffusion —
   arms and core *collect* gas.
3. **Star formation**: rate ∝ gas × potential compression × trigger noise;
   `Gas → StarsYoung`; new stars record the cell's current `Metals` — *when* a
   region formed its stars determines how metal-rich its worlds are (mineral
   geography's causal root).
4. **Aging**: `Young → Mid → Old → Remnants` at fixed world-time rates.
5. **Death & enrichment**: the massive fraction of each young cohort dies fast,
   returning gas, adding `Metals`, and spilling enrichment to neighboring cells —
   gradients emerge; supernova products don't respect cell borders.

Present-day derivations (replacing painted values): `MeanDensity` from total mass;
`StellarLean` from cohort mix (`YoungBright` = active star formation,
`OldDim` = burned out early, `RemnantGraveyard` = burned out early and hard,
`Balanced` otherwise); `Metallicity` from `Metals`; voids = where potential + clumping
never gathered gas; plus new derived quantities: gas fraction, mineral richness
(metals × remnant processing), star-formation activity.

## 5. Discrete Features

Each interacts with the field stack rather than bypassing it:

- **Mergers** (count/scale knob-governed; schedule seeded per galaxy): infalling
  dwarf with entry bearing, mass, deep-time epoch. Active effects: gas + star
  injection along a trail of cells (2D-projected infall path), traveling starburst
  multiplier, decaying potential perturbation. Residue: a stellar stream with a
  foreign metallicity signature, a datable starburst cohort, possibly a
  secondary-core knot. The biggest source of seed-to-seed structural variety.
- **Globular clusters** (placed in the earliest steps): ancient, compact, metal-poor
  single-cell features; near-zero gas; own hex-tier star-table bias. Rare exotic
  terrain for the epoch sim.
- **Nebulae** (emergent at finalization, never placed): contiguous present-day
  high-gas regions → named emission nebulae (star formation active) or dark clouds
  (inactive); recent massive-cohort deaths → supernova remnants.
- **Active nucleus**: hosted by the core cell. Accretion epochs trigger on merger /
  gas-inflow feeding; each outburst emits a sterilization/enrichment wave over an
  inner radius. Quiescent at present day (activity a knob). Primary consumer: 0b —
  life near the core starts late.

## 6. Outputs Contract — "The Physical Galaxy"

Persisted artifact layers:

1. **Present-day cell fields**: density, gas fraction, cohort mix (→ derived
   `StellarLean`), metallicity, mineral richness, star-formation activity.
2. **Feature registry**: mergers, globulars, nebulae, AGN epochs — identity, date,
   cell footprint; feature cells carry pre-commitment-style overrides for the hex
   tier (a globular-cluster hex rolls on a different star table).
3. **Habitability history per cell** (0b's primary input), compressed to scalars:
   when metallicity first crossed the life-viable floor, last sterilization event
   (AGN wave, local starburst), stability since. This is what makes 0b's emergence
   schedule causal instead of rolled.
4. **Deep-time chronicle**: cosmic events in the standard event grammar with
   deep-time world-years ("the Heron Merger, −6.2 Gyr") — the deepest stratum of the
   one history in-game science reads (P1, P8).

## 7. Config Knobs

Existing shape knobs → potential parameters (unchanged meaning/defaults);
`MeanDensityTarget` survives as present-day normalization. New: merger count/scale,
star-formation efficiency, enrichment rate, globular count, AGN activity. Seeded
defaults, artifact-stamped.

## 8. Determinism & Performance

Rolls keyed (step, cell spiral index, channel); features by (step, feature id);
fixed iteration order throughout. ~1,600 cells × ~150 steps × a handful of fields is
well under a second — the cosmic clock spends almost none of the minute+ genesis
budget.

## 9. Testing Strategy

- **Conservation invariants**: total mass across gas/cohorts/remnants/recycling
  balances every step; no negative or NaN fields.
- **Acceptance bands** (reference config): present-day mean density near target;
  lean mix within bands; core→rim metallicity gradient monotone within tolerance;
  nebula/void/feature counts in range — the sim provably still makes galaxies that
  look like galaxies.
- **Goldens**: reference-config present-day field summary + feature registry as
  frozen literals (red-window discipline on re-freeze).
- **Determinism**: byte-identical artifact layers for same config;
  load-vs-rebuild equivalence within a code version.
- **Downstream regression**: skeleton seeding passes consuming derived (rather than
  painted) lean/metallicity stay within their existing shape bands.

## 10. Frame-Consistency Check (master frame §9)

No phase-order, taxonomy, or cross-cutting-interface change required: the cosmic sim
is upstream artifact layers feeding L0 through the already-defined board interface.
Event grammar gains deep-time world-years (an addition, not a reshape). P1/P4
checked in §5–§6 without special pleading. Controller interface untouched (no
decision-making actors exist at this clock).

## 11. Deferred / Follow-Up

- **0b (life & precursors)** consumes the habitability history and feature registry;
  its design defines the life-viable metallicity floor and sterilization semantics
  precisely.
- Bar structures, ring galaxies, and multi-galaxy fields: out of scope; the
  potential prior could express them later as new potential terms (knob additions).
- Hex-level noise re-tuning: the hex-scale clumping noise persists under the
  interpolated cell field; retune its amplitude during implementation acceptance.
- Naming for features (nebulae, mergers) uses the existing syllable generator;
  culture-specific renaming of cosmic features is a narrative-pass concern.

## 12. Amendments to Prior Docs

- `2026-07-07-regional-generation-design.md` §4 (Tier 1): density field becomes
  "interpolated persisted cosmic fields × hex noise" (this spec §2, decision 4);
  §5 seeding pass 2 (stellar lean & metallicity paint) is superseded by derivation
  from cosmic outputs.
- `docs/design/genesis/cosmic-genesis.md`: stub replaced by the final design (this
  pass's product).
- Flow diagram: cosmic node moves FUT → SPEC on merge of this spec.
