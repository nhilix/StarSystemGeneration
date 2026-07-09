# Life & Precursors (Design Pass 0b) — The Evolutionary Clock

Status: **draft — awaiting user review**
Date: 2026-07-09
Parent: `2026-07-09-epoch-sim-master-frame-design.md` (pass 0b). Consumes pass 0a
(`2026-07-09-cosmic-genesis-design.md`) and the space-and-travel amendment
(`2026-07-09-space-and-travel-design.md`). Product doc:
`docs/design/genesis/life-and-precursors.md`.

## 1. Overview

The evolutionary clock: life across the galaxy over ~Gyr in ~Myr steps, running on
0a's physical galaxy. It threads between two existing seeding concerns — it consumes
0a's per-cell habitability history and produces the homeworlds and biosphere
character that the prototype's seeding passes 3–4 currently paint. Its headline
output is the **emergence schedule**: which sapient origins reach spaceflight at
which generational epoch, making the frame's asymmetric polity entry causal rather
than rolled. It also simulates **precursor civilizations** as earlier entries on that
same schedule, producing the galaxy's archaeology.

Output contract: **the living galaxy** — present-day biosphere fields, the emergence
schedule, a precursor registry (arcs + typed sites), and the evolutionary stratum of
the chronicle.

## 2. Decisions

- **Coarse civ-arc precursor sim** (over full-generational-compressed and
  parameterized-arcs): each live wave runs a simplified spatial civilization sim on
  the raster — origin, port-planting expansion along real terrain, lane-network
  growth, cause-typed decline — reusing the space-and-travel model at low fidelity
  (no markets, characters, or factions). Ruins therefore have real geography (dead
  lane networks, capitals, frontier outposts, battle sites), at moderate cost.
- **All four living-residue channels in scope**: machine descendants, biosphere
  engineering, sterilization scars, dormant remnants (§6).
- **Waves may overlap in time and interact** (over strictly-sequential and
  single-wave): when two live waves meet, contact resolves crudely (war / absorption
  / partition), producing inter-precursor battle strata, contested ruins, and
  mixed-provenance sites — the deepest political history.
- **Wave count is emergent from scale, not a flat cap** (user refinement): waves draw
  a **vigor class** — *grand* (rare, 1–3, slow arcs, galaxy-scale extent) and *minor*
  / pocket (more numerous, fast rise/fall, spatially isolated) — and are generated
  against a **total-galactic-domain-ever budget** plus per-class count limits, so the
  number of waves falls out of how much galaxy each actually claims. End-cause weights
  condition on vigor class (pocket civs collapse/burn out more than they transcend or
  fight galaxy-spanning wars).

## 3. The Biosphere Field

Per cell, layered on 0a's physical galaxy:

| Field | Driven by | Meaning |
|---|---|---|
| `LifeViability` | 0a metallicity-floor crossing, star cohort, stability window | when/whether life could start |
| `BiosphereAge` | steps since local abiogenesis | older → richer, more complex |
| `BiosphereRichness` | age × world mix × catastrophe history | provisions potential, sapience substrate |
| `SapiencePotential` | richness × stability × trigger noise | proximity to a sapient origin |

## 4. The Step Loop

~a few hundred evolutionary steps; fixed spiral-index order; rolls keyed
(step, cell, channel):

1. **Abiogenesis** — fires where `LifeViability` crosses threshold (seeded,
   terrain-gated): bright stable cells early, rim and disturbed cells late or never.
2. **Aging & enrichment** — biospheres accumulate `BiosphereAge`/`Richness`.
3. **Catastrophes** — 0a sterilization events (AGN waves, local starbursts) plus
   seeded mass extinctions knock richness back, occasionally resetting a cell.
   Precursor sterilization scars (§6) also write here.
4. **Spread** — life propagates to neighbors along habitability gradients
   (panspermia; slow).
5. **Sapience registration** — where `SapiencePotential` crosses threshold and holds,
   register a **sapient origin** with a maturation clock.

## 5. The Emergence Schedule

Each sapient origin gets a **spaceflight date** = abiogenesis time + maturation
duration scaled by biosphere richness, world hospitability, and catastrophe setbacks
(a species clawing up through repeated extinctions emerges late). Projected onto the
generational timeline, these are the staggered epochs at which polities enter the
epoch sim (frame §4).

Consequences: early-viable core-adjacent cells the AGN didn't sterilize produce
ancient powers; twice-scarred rim biospheres produce late bloomers born into a
claimed galaxy. Retires the prototype's spacing-aware random homeworld pass and the
flat sapient-biosphere roll (homeworlds are where/when life got there first);
Phase-1's rare random `Sapient` roll survives only as genuine undiscovered
pre-spaceflight natives.

## 6. Precursor Waves

A wave is a sapient origin whose spaceflight date lands in deep time. Vigor class is
drawn (§2); grand waves are rare and galaxy-scale, minor waves numerous and isolated.

**The coarse civ-arc sim** (per live wave, on the raster, low fidelity):

1. **Rise** — plant a capital port at the origin; expand by planting ports along
   traversable terrain (0a density + biosphere richness pull), growing a real lane
   network.
2. **Peak** — extent set by vigor and available room; optional biosphere engineering
   within territory.
3. **Decline** — cause-typed ending (weights condition on vigor class):

| End cause | Residue signature |
|---|---|
| War (inter-wave or civil) | battle strata, shattered ports, contested ruins |
| Cascade collapse | intact-but-dead cities, abandoned lane networks |
| Transcendence | empty megastructures, silent intact capital, no bodies |
| Plague / catastrophe | mass graves, quarantine relics, sterilization scar |

**Contact** (overlapping live waves): crude war / absorption / partition →
inter-precursor battlefields, mixed-provenance sites, borders predating current life.

**Living-residue channels:**

- **Machine descendants** — a transcendence/autonomous-systems ending can seed a
  *current-era* machine-intelligence origin (homeworld = precursor capital; emergence
  date set to make it a present-day player). Grounds the machine-species table hint.
- **Biosphere engineering** — peak-phase seeding/terraforming/uplift: anomalously
  early-rich biospheres, transplanted species sharing a genetic lineage across
  distant worlds, uplift candidates maturing on the schedule.
- **Sterilization scars** — precursor wars/collapses write back into the §4
  catastrophe layer, delaying or erasing downstream life; the present emergence map
  carries their wars' shadows.
- **Dormant remnants** — a fraction of end-state sites stay live: dormant war
  machines, active defense grids, functioning megastructures — encounter content and
  strategic prizes, flagged distinctly from inert ruins.

## 7. Outputs Contract — "The Living Galaxy"

Persisted artifact layers; consumers: skeleton seeding passes and the epoch sim.

1. **Biosphere fields** (present-day): richness, age, world-life character per cell —
   replacing painted biosphere/provisions potential.
2. **Emergence schedule**: every sapient origin (current + precursor) with homeworld
   hex, species-profile seed, spaceflight date.
3. **Precursor registry**: per wave — vigor class, capital, extent, lane network, end
   cause/date, and a typed site list (ruins, scars, dormant remnants, engineered
   biospheres, descendant links). Sites become hex-tier anchors via the
   pre-commitment mechanism, like 0a's features.
4. **Deep-time chronicle**: wave rise/peak/fall and inter-wave events in the standard
   event grammar with deep-time world-years — continuous with 0a below and the epoch
   sim above.

## 8. Config Knobs

Abiogenesis rate, maturation scale, catastrophe frequency, grand/minor vigor
distribution, total-galactic-domain-ever budget, per-class count limits,
biosphere-engineering rate, dormant-survival rate. Seeded defaults, artifact-stamped.

## 9. Determinism & Performance

Origins iterate by id, waves by index, arc-sim rolls key (wave, step, cell); fixed
order throughout. A few hundred evolutionary steps plus a bounded set of short arc
sims stays well within the genesis budget.

## 10. Testing Strategy

- **Invariants**: every emergence-schedule entry traces to a viable origin cell;
  spaceflight dates are finite and ordered; precursor site cells exist and honor
  one-anchor-per-hex; machine-descendant links resolve to a real wave; sterilization
  scars never resurrect erased life.
- **Acceptance bands** (reference config): count of current-era homeworlds within the
  polity-count band the frame targets; emergence-date spread (some early, some late);
  wave count and total precursor domain within budget; residue-site density in range.
- **Goldens**: reference-config emergence schedule + precursor registry summary as
  frozen literals.
- **Determinism**: byte-identical layers per config; load-vs-rebuild equivalence.
- **Downstream regression**: seeding passes consuming biosphere fields (rather than
  painted rolls) stay within existing shape bands.

## 11. Frame-Consistency Check (master frame §9)

Additions only: new upstream artifact layers feeding L0; no phase-order, taxonomy, or
cross-cutting-interface reshape. The arc sim reuses the space-and-travel model rather
than defining a new one. Event grammar reused with deep-time world-years. P1/P4
evidence in §6–§7. Controller interface untouched (arc-sim actors use a fixed
low-fidelity policy, not the full controller).

## 12. Deferred / Follow-Up

- Precursor *technology* as recoverable tech-tree content (which ruins yield what):
  a play-layer / economy concern, not genesis.
- Living precursor holdouts (a grand wave that never fully died, present at epoch 0
  as an actual polity): possible via an emergence-schedule entry landing in-era;
  parked until the epoch sim's actor entry is designed (pass 4).
- Multi-lineage biology depth (ecosystem simulation beyond richness scalars): out of
  scope; richness suffices for the sim's needs.

## 13. Amendments to Prior Docs

- `2026-07-07-regional-generation-design.md` §5 seeding passes 3–4 (resource-anchor
  climate weighting, spacing-aware homeworlds) and the flat sapient rate are
  superseded by 0b outputs.
- `docs/design/genesis/life-and-precursors.md`: stub replaced by the final design.
- Flow diagram: 0b node FUT → SPEC on merge.
