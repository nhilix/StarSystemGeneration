# Cosmic Genesis

The cosmic clock: a deep-time structure simulation over the region-cell lattice.
The galaxy's present-day structure and composition are the residue of a simulated
formation history — gas gathers, stars form and age, supernovae enrich, mergers
disrupt — rather than analytic paint (P5). Runs once per galaxy as the first
artifact layer; everything downstream reads its outputs.

## The potential prior

The analytic shape function (core + disc + log-spiral arms, with the shape knobs)
is the galaxy's **gravitational potential** — where matter *wants* to be. The
simulation decides where matter actually ends up. The potential is mildly
time-varying: the arm pattern is fixed (a density wave matter moves through), the
core deepens slowly, and merger events add transient decaying perturbations.

## The field stack

Per region cell, conserved quantities (P4):

| Field | Meaning |
|---|---|
| `Gas` | Star-forming material: primordial inflow early, consumed and partially recycled after |
| `StarsYoung` / `StarsMid` / `StarsOld` | Age cohorts; the present-day mix derives `StellarLean` |
| `Remnants` | Accumulated stellar corpses — graveyard terrain, mineral-richness contributor |
| `Metals` | Cumulative enrichment; derives present-day `Metallicity` |

## The step loop

~100–150 deep-time steps across the compressed ~14 Gyr; fixed spiral-index order;
rolls keyed by (step, cell, channel) (P6):

1. **Inflow** (early steps): primordial gas lands potential-weighted, noise-clumped.
2. **Transport**: gas drifts along the potential gradient plus slight diffusion —
   arms and core collect gas.
3. **Star formation**: rate ∝ gas × potential compression × trigger noise;
   `Gas → StarsYoung`; new stars record the cell's current `Metals` — when a region
   formed its stars determines how metal-rich its worlds are.
4. **Aging**: `Young → Mid → Old → Remnants` at fixed world-time rates.
5. **Death & enrichment**: the massive fraction of young cohorts dies fast —
   returning gas, adding `Metals`, spilling enrichment to neighboring cells.

Present-day quantities are derivations, not paint: `MeanDensity` from total mass;
`StellarLean` from cohort mix (young-bright where star formation is still active,
old-dim where gas burned early, remnant-graveyard where it burned early and hard);
`Metallicity` from `Metals`; voids where potential plus clumping never gathered gas;
plus gas fraction, mineral richness (metals × remnant processing), and
star-formation activity.

## Discrete features

Sparse, identified, dated objects that interact with the field stack rather than
bypassing it. The spatial model is strictly 2D hex-radial: off-plane phenomena
appear as their projections onto the lattice, with off-plane-ness as flavor only.

- **Mergers** — infalling dwarfs (entry bearing, mass, epoch; count/scale from
  knobs, schedule seeded per galaxy). Active: gas + star injection along a trail of
  cells, a traveling starburst multiplier, a decaying potential perturbation.
  Residue: a stellar stream with foreign metallicity signature, a datable starburst
  cohort, possibly a secondary-core knot. The biggest source of seed-to-seed
  structural variety.
- **Globular clusters** — placed in the earliest steps: ancient, compact,
  metal-poor single-cell features with near-zero gas and their own hex-tier
  star-table bias. Rare exotic terrain.
- **Nebulae** — emergent at finalization, never placed: contiguous high-gas regions
  become named emission nebulae (star formation active) or dark clouds (inactive);
  recent massive-cohort deaths become supernova remnants.
- **Active nucleus** — hosted by the core cell. Accretion epochs trigger on
  merger/gas feeding; each outburst emits a sterilization/enrichment wave over an
  inner radius. Quiescent at present day. Life near the core starts late — the
  evolutionary clock reads this directly.

## Provided interface — "the physical galaxy"

Persisted artifact layers (P6); consumers: the evolutionary clock
([life-and-precursors.md](life-and-precursors.md)), the skeleton seeding passes,
and Tier-1/Tier-3 hex generation.

1. **Present-day cell fields**: density, gas fraction, cohort mix, metallicity,
   mineral richness, star-formation activity.
2. **Feature registry**: mergers, globulars, nebulae, AGN epochs — identity, date,
   cell footprint. Feature cells carry pre-commitment-style overrides for the hex
   tier (a globular-cluster hex rolls on a different star table).
3. **Habitability history per cell**, compressed to scalars: when metallicity first
   crossed the life-viable floor, last sterilization event, stability since. This
   makes the emergence schedule causal instead of rolled.
4. **Deep-time chronicle**: cosmic events in the standard event grammar with
   deep-time world-years ("the Heron Merger, −6.2 Gyr") — the deepest stratum of
   the one history in-game science reads.

**Tier-1 consequence:** per-hex density = interpolated present-day cell density ×
hex-scale clumping noise. The density field reads a persisted cell-resolution
layer; the hex tier itself remains a pure, never-persisted function of
(config, coordinate).

## Knobs

Shape knobs (`CoreRadius`, `DiscFalloff`, `ArmCount`, `ArmTightness`, `ArmWidth`,
`ArmStrength`) keep their meaning as potential parameters; `MeanDensityTarget`
normalizes the present-day field. New: merger count/scale, star-formation
efficiency, enrichment rate, globular count, AGN activity. Seeded defaults,
artifact-stamped.

## P1 evidence

- **Legible residue**: gas / metallicity / stellar-age / feature map layers; named,
  dated map features (nebulae, streams, clusters); deep-time chronicle entries.
- **Inhabitable state**: terrain diversity with mechanical teeth — graveyards,
  nebulae, and cluster cells as strategic and exotic regions; mineral geography
  whose ore economics trace to actual ancient supernovae; a deep history in-game
  astronomy and archaeology can uncover.

## Cost

~1,600 cells × ~150 steps × a handful of fields: well under a second of the genesis
budget.
