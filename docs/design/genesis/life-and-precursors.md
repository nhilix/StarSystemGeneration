# Life & Precursors

The evolutionary clock: life across the galaxy at ~Myr steps, running on the
physical galaxy from [cosmic-genesis.md](cosmic-genesis.md). It produces the
homeworlds and biosphere character that seed the epoch sim, and — as its headline
output — the **emergence schedule** that makes asymmetric polity entry causal. It
also simulates **precursor civilizations** as earlier entries on that same schedule,
giving the galaxy its archaeology (P5).

## The biosphere field

Per cell, layered on the physical galaxy:

| Field | Driven by | Meaning |
|---|---|---|
| `LifeViability` | 0a metallicity floor, star cohort, stability window | when/whether life could start |
| `BiosphereAge` | steps since local abiogenesis | older → richer, more complex |
| `BiosphereRichness` | age × world mix × catastrophe history | provisions potential, sapience substrate |
| `SapiencePotential` | richness × stability × trigger noise | proximity to a sapient origin |

## The step loop

A few hundred evolutionary steps; fixed order; rolls keyed (step, cell, channel) (P6):

1. **Abiogenesis** — fires where viability crosses threshold (terrain-gated: bright
   stable cells early, rim and disturbed cells late or never).
2. **Aging & enrichment** — biospheres accumulate age and richness.
3. **Catastrophes** — 0a sterilization events (AGN waves, starbursts) plus mass
   extinctions and precursor scars knock richness back, sometimes resetting a cell.
4. **Spread** — life propagates to neighbors along habitability gradients
   (panspermia; slow).
5. **Sapience registration** — where potential crosses threshold and holds, a
   **sapient origin** is registered with a maturation clock.

## The emergence schedule

Each sapient origin gets a **spaceflight date** = abiogenesis time + a maturation
duration scaled by biosphere richness, world hospitability, and catastrophe setbacks.
Projected onto the generational timeline, these are the staggered epochs at which
polities enter the epoch sim (frame [time.md](time.md)).

Asymmetry is causal: early-viable core-adjacent cells the AGN didn't sterilize
produce ancient powers; twice-scarred rim biospheres produce late bloomers born into
a claimed galaxy. Homeworlds are where and when life got there first; the old random
homeworld placement and flat sapient-biosphere roll are retired, leaving only rare
genuine undiscovered pre-spaceflight natives.

**Starting conditions at emergence**: a new polity enters with its homeworld domain
(tier-1 port), seeded population segments, and base tech tiers reflecting its
maturation quality — with a **contact bonus** for late emergers who matured under a
sky full of foreign traffic (elevated starting Astrogation/Industrial): latecomers
are behind, not hopeless. Emergence *inside claimed space* resolves through the
host's native policy
([../interpolity/relations.md](../interpolity/relations.md)).

## Precursor waves

A precursor wave is a sapient origin whose spaceflight date lands in deep time. Each
draws a **vigor class**, and the number of waves is emergent from how much galaxy
each claims (a domain budget), not a flat cap:

- **Grand waves** — rare (1–3), high vigor, slow arcs, galaxy-scale extent. The
  elder races; few because the galaxy has room for few at that scale, and their long
  arcs make overlap-contact meaningful.
- **Minor / pocket waves** — more numerous, low-to-moderate vigor, fast rise/fall,
  spatially isolated (a rich cluster, a globular, an arm segment). Many coexist
  across deep time without crowding.

### The coarse civ-arc sim

Each live wave runs a low-fidelity spatial civilization sim on the raster — reusing
the [space-and-travel.md](../frame/space-and-travel.md) model without markets,
characters, or factions, so ruins have real geography:

1. **Rise** — plant a capital port at the origin; expand by planting ports along
   traversable terrain, growing a real lane network.
2. **Peak** — extent set by vigor and available room; optional biosphere engineering
   in territory.
3. **Decline** — a cause-typed ending (weights condition on vigor class — pocket
   civs collapse and burn out more than they transcend or fight):

| End cause | Residue signature |
|---|---|
| War (inter-wave or civil) | battle strata, shattered ports, contested ruins |
| Cascade collapse | intact-but-dead cities, abandoned lane networks |
| Transcendence | empty megastructures, silent intact capital, no bodies |
| Plague / catastrophe | mass graves, quarantine relics, sterilization scar |

When two live waves overlap, contact resolves crudely (war / absorption / partition),
writing inter-precursor battlefields, mixed-provenance sites, and borders that
predate all current life.

### Living residue

Precursors touch the living present through four channels:

- **Machine descendants** — a transcendence or autonomous-systems ending can seed a
  current-era machine-intelligence origin: its homeworld is the precursor capital,
  its emergence timed to make it a present-day player. Grounds the machine-species
  lineage in real history.
- **Biosphere engineering** — peak-phase seeding, terraforming, and uplift:
  anomalously early-rich biospheres, transplanted species sharing a genetic lineage
  across distant worlds, uplift candidates maturing on the schedule. Archaeology
  readable in the biology itself.
- **Sterilization scars** — precursor wars and collapses delay or erase downstream
  life; the present emergence map carries their wars' shadows.
- **Dormant remnants** — a fraction of end-state sites stay live: dormant war
  machines, active defense grids around old capitals, functioning megastructures.
  Encounter content and strategic prizes, flagged distinctly from inert ruins.

## Provided interface — "the living galaxy"

Persisted artifact layers (P6); consumers: the skeleton seeding passes and the
epoch sim.

1. **Biosphere fields** (present-day): richness, age, world-life character per cell —
   replacing painted biosphere/provisions potential.
2. **Emergence schedule**: every sapient origin (current + precursor) with homeworld
   hex, species-profile seed, and spaceflight date.
3. **Precursor registry**: per wave — vigor class, capital, extent, lane network, end
   cause and date, and a typed site list (ruins, scars, dormant remnants, engineered
   biospheres, descendant links). Sites become hex-tier anchors via the
   pre-commitment mechanism.
4. **Deep-time chronicle**: wave rise/peak/fall and inter-wave events in the standard
   event grammar with deep-time world-years — continuous with the cosmic stratum
   below and the epoch sim above.

## Knobs

Abiogenesis rate, maturation scale, catastrophe frequency, grand/minor vigor
distribution, total-galactic-domain budget, per-class count limits,
biosphere-engineering rate, dormant-survival rate. Seeded defaults, artifact-stamped.

## P1 evidence

- **Legible residue**: biosphere and emergence map layers; dated precursor arcs in
  the chronicle; anchored sites (ruins, scars, dormant remnants) the atlas marks.
- **Inhabitable state**: staggered emergence *is* the strategic starting condition;
  ruins, dormant remnants, and engineered-biosphere worlds are explorer and
  archaeologist content with real provenance; machine descendants are live neighbors
  with a backstory.

## Cost

A few hundred evolutionary steps plus a bounded set of short arc sims: well within
the genesis budget.
