# Regional / Spatial Generation Design — Galaxy Structure, History Simulation, and Per-Hex Integration

Status: **draft — awaiting user review**
Date: 2026-07-07

## 1. Overview

Phase 1 generates every hex homogeneously: a flat 50% presence probability, flat
settlement rates, and no spatial correlation between neighbors. This spec replaces
homogeneity with structure. It designs one shared foundation — the determinism-safe
way for a hex to be influenced by things beyond its own coordinate — and four
consumers of it:

1. **Noise-driven density** — clumps, filaments, voids, and a recognizable galactic
   shape (fully specced here).
2. **Region/zone typing** — war zones, dead zones, volatile zones, stable cores,
   trade gradients (fully specced here; zones are *emergent* from simulated history,
   not painted).
3. **Settlement highways** — populated corridors between developed centers
   (cell-resolution graph specced here; hex-level rendering deferred to a follow-up
   spec).
4. **Political geography** — kingdoms, borders, federations, and their history
   (registry and world-state handoff specced here; live-play faction dynamics
   deferred to the game-layer spec).

The heart of the design is a **deterministic galactic history simulation**: seeded
homeworlds expand across the density terrain over discrete epochs, trade, ally,
federate, fight resource wars, fracture, and collapse — leaving a galaxy whose map
features (borders, ruins, trade corridors, unclaimed wilds) are the *residue of
simulated events* rather than random paint.

Relationship to prior docs: builds on
`docs/superpowers/specs/2026-07-07-generation-rules-design.md` (the system/body
layer, unchanged in its internals) and supersedes the regional-generation sketch in
`docs/DESIGN.md` §4. Amendments to DESIGN.md required by this spec are listed in §12.

## 2. Goals

- Break up homogeneous generation with **spatial structure at every scale**: galactic
  shape at the top, regional clumps/voids in the middle, coherent per-hex character at
  the bottom.
- Make political geography **genuinely historical**: kingdoms' shapes, ruins, and
  reputations trace to simulated events a player can later discover in-world.
- The map's richness must remain **deterministic and generate-on-demand**: naive
  neighbor-influence creates unbounded generation cascades; this design bounds all
  spatial influence through a precomputed, cached, persistable artifact.
- The simulation's final state doubles as the **initial conditions of a live game
  world** — the player inherits the economy, wars, and reputations the sim produced.
- Every simulation mechanic must pass the **map-legibility rule** during genesis: if
  it cannot surface as something a player could see (a zone character, a POI, a
  history line, a society flavor), it does not belong in the sim.

## 3. Architecture: Three Tiers

Generation input changes from a bare `masterSeed` to a **`GalaxyConfig`**:

- **Identity:** master seed, galaxy size in sectors (default ~100; a first-class knob
  — the same seed at size 10, 100, or 1,000 legitimately produces different galaxies,
  since history plays out on a different stage).
- **Tuning knobs** (seeded defaults, all recorded in the artifact stamp): mean
  density target, spiral arm count/tightness/width, epoch count, years-per-epoch,
  news propagation speed, settlement-rate targets, and future knobs as they arise.

Three tiers, each deterministic from what's below:

| Tier | Nature | Contents |
|---|---|---|
| **1. Field tier** | Pure, stateless, per-hex | Galactic shape × local noise → density field. No storage; works at any scale. |
| **2. Skeleton tier** | Bounded precompute over a coarse lattice; persisted artifact | Seeding passes (resources, precursors, homeworlds), the epoch history simulation, its outputs (polities, zones, routes, events), and the world-state handoff. |
| **3. Hex tier** | Existing Phase 1 pipeline + regional reads; on-demand | Per-hex generation reads the field (presence) and its region's skeleton state (modifier bundle + pre-commitments) before rolling. |

End-to-end the contract stays **hex = f(GalaxyConfig, coordinate)** — the skeleton is
a memoized intermediate term of that function.

### 3.1 The artifact chain (persistence)

"Reconstructable in principle" is not the same as "safe to reconstruct": rebuild cost
grows with galaxy size and future sim tiers, and — more fundamentally — determinism
holds only *per code version*. A generator update would silently rearrange a
regenerated galaxy under a long-lived save. Therefore:

- Each simulation pass is a pure function `LayerN = PassN(LayerN₋₁, config)` with a
  **defined, versioned, serializable output schema**. Passes never reach around each
  other; the stack is explicitly extensible (future tiers — deeper economics,
  migration waves — append as new passes).
- The final composite (plus any intermediate layers hex generation reads) is the
  **galaxy structure artifact**: built once when a galaxy is created, persisted to
  disk, loaded thereafter. Hex generation is a lookup against it, never a
  resimulation.
- The artifact carries stamps: full GalaxyConfig + per-pass schema/code versions. On
  load-with-mismatch nothing silently rebuilds — it is an explicit choice between
  *keep the artifact* (galaxy stays stable under newer code; the normal case for a
  real save) and *regenerate* (a new galaxy).
- In-memory-only operation remains available for throwaway exploration (inspector
  seed-hopping); persistence is the default for any galaxy intended to be kept.
- The hex tier is never persisted — it is genuinely a pure function of
  (config, skeleton, coordinate).

## 4. Tier 1: The Density Field

`DensityField.At(config, hex) → [0,1]`, a pure function composed of two multiplied
layers:

- **Galactic shape** — parametric in normalized galactic position (distance/angle
  from center): dense core, log-spiral arm ridges (count/tightness/width knobs),
  falloff to the rim, hard zero beyond the galaxy's extent. Coordinates gain a
  defined extent; beyond it is empty intergalactic space.
- **Local structure noise** — hash-based value noise, a few octaves plus domain
  warping, built directly on the existing `StableHash` (no external noise library;
  Core stays dependency-free). Noise lattice draws use new `RollChannel`s per the
  registry discipline. This produces clumps, filaments, and voids at
  sector/subsector scale.

The hex presence roll keeps its existing `Presence` channel draw and compares it
against this composed probability instead of the flat constant — minimally invasive
to the Phase 1 pipeline.

Consumers: **hexes** (presence) and the **skeleton builder** (per-cell density
summaries — the terrain the history sim plays on: dense corridors invite expansion,
voids are natural borders and chokepoint-makers).

Sanity anchor (a config knob, like all such targets): mean density inside the
inhabited disc ≈ the configured target (default ~50%, preserving Phase 1 tuning
intuitions), with local values ranging ~0 (voids, rim) to ~0.9 (core, arm ridges).

## 5. Tier 2a: Skeleton Lattice and Seeding

**Region lattice.** The galaxy divides into region cells aligned to Traveller
subsectors (8×10 hexes; ~1,600 cells at 100 sectors). The cell is the resolution of
all skeletal state. Hexes belong to exactly one cell; smooth cross-cell gradients
come from read-time interpolation, never finer sim resolution.

**Seeding passes** (pre-history artifact layers, in order):

1. **Density summary** — each cell samples Tier 1: mean density,
   void/corridor/chokepoint classification (chokepoints = graph articulation cells in
   the density connectivity graph; their strategic value feeds the sim).
2. **Resource anchors** — strategic features placed as **anchored hexes**: the
   skeleton picks a specific hex in a cell (hash-draw from cell coordinates) and
   records what must be true there. Anchor types are a closed, versioned vocabulary —
   initially exactly: *mineral-rich system*, *precursor site*, *homeworld* (pass 3),
   plus the event-POI types compiled in §7.6. New types extend the vocabulary with a
   schema version bump. Placement is density-weighted with deliberate rare exceptions
   (a precursor site deep in a void is a story).
3. **Homeworlds** — sapient-origin systems, seeded density- and spacing-aware so
   nascent polities don't all start adjacent. Each homeworld generates a species
   profile (§6).

**The pre-commitment mechanism** — the single top-down channel by which the skeleton
dictates hex-level facts: an anchored hex's record ("system present, mineral-rich";
"precursor site"; "homeworld: sapient, flourishing"; later, event POIs from §7.6)
**overrides the corresponding baseline rolls** when that hex generates. All unpinned
fields roll normally on their normal channels, so anchored hexes remain mostly
organic. Pre-commitments are few (a handful per cell at most) and strongly typed.
Everything else the skeleton does is soft modifier bundles (§8).

Phase 1's random `Sapient` biosphere roll survives as rare "undiscovered natives";
homeworld-grade civilizations are skeleton-driven. The Phase 1 sapient rate (0.9% of
bodies / 7% of systems) should be retuned downward once homeworlds exist.

## 6. Homeworlds and Sapient Species

Geography is the board; species are the players; the same board plays out differently
under different players. Each homeworld generates a **species profile**
(deterministic, on skeleton channels). Cardinal rule: **every trait must be
simulation-legible** — nothing enters the profile unless the epoch sim reads it.

**6.1 Embodiment** — what kind of being, expressed mechanically as a **habitability
profile**: which world types are comfortable / marginal / hostile for settlement.
Illustrative types (weighted, rarest last):

| Embodiment | Comfortable worlds | Notes |
|---|---|---|
| Terran-analog organics (common) | breathable rocky worlds | the baseline |
| Aquatic | ocean worlds | dry worlds hostile |
| Cryophilic | ice worlds | habitable-band worlds merely marginal |
| Lithic/subterranean | airless rocks | atmosphere nearly irrelevant |
| Hive organisms (rare) | broad tolerance | high-density preference |
| Machine intelligences (very rare) | near-universal tolerance | plausibly precursor-descended; spawn odds tied to precursor-site proximity |

Consequence: **terrain is species-relative.** Expansion cost (§7.2) is computed
against the expander's habitability profile — aquatic empires flow along water-world
chains while lithic ones spread through asteroid-rich dead systems the aquatics
ignored. Borders emerge where preferred corridors collide; interleaved empires can
share space where they don't.

**6.2 Temperament** — a small set of axes, each mapping to a specific sim parameter:

| Axis | Sim effect |
|---|---|
| Expansionism | expansion budget share and reach appetite |
| Cohesion | schism/civil-fracture odds; federation stability |
| Militancy | conflict initiation at contact; military budget share |
| Openness | trade-pact/federation odds; reaction to news |
| Industry | development growth; precursor-exploitation multiplier |
| Adaptability | discount on hostile-world settlement |

**6.3 Ideology seed** — government-archetype biases derived from embodiment +
temperament (hive → collectives; lithic longevity → steward dynasties;
high-openness → free assemblies). Feeds both sim behavior and per-hex society flavor
inside the polity's territory (§8).

Traits draw with correlations, not independently (hive strongly implies high
cohesion). A profile is compact — roughly 6–8 scalars plus two enum tags — keeping
the sim cheap.

## 7. Tier 2b: The Epoch Simulation

**Mental model: each epoch is a macro-turn of a 4X strategy game** (Stellaris at
generational timescale), with phases **income → allocation → action → resolution →
news**. The sim's only customer *during genesis* is the map (the legibility rule,
§2); its final state is then handed to the player (§7.7).

**Actor model.** The **polity** is the first-class actor from epoch zero; species is
a composition property. Every polity starts as (one homeworld, one species,
membership 100%); events change composition over time. This — rather than a post-hoc
consolidation layer — is what lets federations act as federations: merged entities
keep playing subsequent epochs as themselves. A post-hoc layer could only paint
mergers over species-shaped history; pooled budgets would never fund expansion and
multi-species empires would never conquer anything as themselves.

**State.** Per cell: owning polity (or none), development tier, contested flag,
resident species mix, event log. Global: the **polity registry** — id, name, species
membership, blended temperament, capital, tech tier, military stockpile, commodity
balances, relations matrix (war / neutral / trade / alliance / federation / vassal),
stances (§7.5).

**Determinism.** Fixed iteration order (cells by index, polities by id); every roll
on skeleton `RollChannel`s keyed by (epoch, cell/polity id). The stateless hash
discipline extends into the sim unchanged.

**Timescale.** All rates are expressed in **world-time units** (an epoch = N years, a
config knob), not "per epoch" — so the same dynamics can later integrate at smaller
timesteps (§7.7).

### 7.1 Commodities, value, and income

Three strategic goods plus abstract wealth, each map-legible:

| Commodity | Produced by | Shortage effect |
|---|---|---|
| **Provisions** | cells with rich biospheres *relative to the owner's embodiment* (aquatics farm ocean worlds; lithics barely need provisions) | growth stalls, unrest, cohesion damage |
| **Ore** | mineral anchors, asteroid-dense cells | development slows, military stockpile decays |
| **Exotics** | precursor sites, anomaly cells | tech stagnation; the scarcest and most fought-over |

Each cell's production profile + **route throughput** + strategic position
(chokepoint status) composes its **system value** — and value is what wars are about.
Income phase: surpluses flow along the trade graph to deficits; complementary
economies mechanically strengthen relations (the real basis of the trade→alliance
ladder). **Blockade** is war's economic weapon: cutting routes starves flows without
taking cells.

### 7.2 Allocation and expansion

Polity income splits four ways — **expansion / development / military / war
upkeep** — weighted by temperament (militancy → military, expansionism → expansion,
industry → development) and overridden by situation (a polity at war forcibly shifts
toward military and pays upkeep per contested front).

Expansion spends its budget on frontier cells, cheapest first. Cost = distance +
density terrain *seen through the species' habitability profile* + void penalties.
**Military strength is a tracked stockpile** (grows with spending, decays without)
and is what war resolution uses — not raw development. Peaceful industrialists
out-develop everyone but can be caught undefended; long wars exhaust both sides into
white peace, and exhausted victors are prime schism candidates.

### 7.3 Contact, war, and war goals

Expansion fronts meeting a neighbor trigger the **contact matrix** (both parties'
militancy × openness, modified by stances from news §7.5) → relations. War goals
derive from deficits and value: ore wars, chokepoint seizures, exotics grabs,
punitive blockades — and history annotations inherit them ("the Ore War of epoch 6,
fought over the Kessuline Belt"). Contested cells flip by relative strength (military
stockpile + tech + ally support); multi-epoch contests accumulate war scarring.

### 7.4 Relations ladder, federations, and composition

Sustained positive relations climb a ladder with mechanical teeth:

- **Trade** — income multiplier on connecting routes (also what strengthens the
  highway graph).
- **Alliance** — military pact: attack one and the ally's strength joins the
  defense; partial resource pooling.
- **Federation merge** — gated on sustained alliance + openness + ideological
  compatibility + cohesion: the polities fuse into a *new* polity (multi-species
  membership, population-weighted blended temperament, fresh federation-style name
  and government flavor, combined territory and budget) which plays all subsequent
  epochs as itself.

**Composition through conquest:** conquered cells keep their original species as
resident population — membership without power. That asymmetry feeds cohesion
penalties and schism odds: empires built by the sword carry the seeds of their own
successor states; federations built by treaty are stabler. Resident species mix is
also read by per-hex society flavor (§8).

**Internal events:** schism checks (size vs cohesion — sprawling low-cohesion empires
fracture into successor polities with mutated profiles) and rare collapses (plague,
uprising, precursor catastrophe) leaving **dead husks**: formerly developed cells,
now ruins.

### 7.5 News propagation and reputation

Reputation-worthy events (treaty broken, unprovoked war, atrocity — razing developed
cells, alliance honored, liberation) emit **news pulses**: origin cell, magnitude,
valence. A pulse travels the same connectivity graph trade uses — fast along routes,
slow through wilds, barely across voids — at a configurable speed (cells/epoch knob).
A polity learns when the pulse reaches its territory and **reacts on arrival, not on
occurrence**; reaction is temperament-filtered into a **stance** update toward the
actor (open traders sanction treaty-breakers; militant cultures may respect bold
conquest). Magnitude attenuates with distance — far polities hear diminished rumors.
(No distortion modeling now; noted as future depth.) Stances feed the contact matrix
and the ladder's gates, so a treaty-breaker finds federation doors closing across the
galaxy as news spreads.

**Loop order:** each epoch opens with news arrival and stance updates *before*
contact/war/merge decisions — this epoch's diplomacy runs on last epoch's (possibly
stale) information. At galactic scale this is a feature: the rim reacts years late,
and in a 1,000-sector galaxy there are wars nobody important has heard of yet.

### 7.6 The event→POI compiler (history becomes archaeology)

At artifact finalization, a compiler pass walks the complete event log and converts
dramatic events into **anchored POIs** via the §5 pre-commitment mechanism:

| Event | POI seeded |
|---|---|
| Major battle (destroyed stockpile above threshold) | battlefield ruins — wreckage fields, scaled by magnitude, tagged with epoch and belligerents |
| Collapse | dead cities, ruined worlds |
| Abandoned/lost capital | ruined metropolis |
| Precursor catastrophe | anomaly site |
| Depleted resource anchor / famine | depleted mines, famine ruins |

Phase 1's random `derelict_fleet` overlay already models wreckage mechanically; the
compiler is a second, *historical* source of the same content — random overlays are
mystery debris, compiled POIs are debris with a name, a date, and two factions you
can look up.

### 7.7 The world-state handoff

The final epoch's **complete state** — registry, relations, stances, budgets,
stockpiles, commodity production/deficits, route throughputs, per-cell ownership and
development — is a first-class layer of the persisted artifact: the **live world's
initial conditions**. A fledgling trader reads real deficits on real routes;
conscription joins a military with an actual stockpile and actual wars.

Consequences: the sim state schema is **resumable and steppable** (no "runs exactly
once" assumptions); rates in world-time units allow the same state machine to tick at
two clock speeds — coarse epochs during genesis, finer ticks (or event-driven
advancement paced by player travel) during play. At play speed, news travels at the
speed of ships, and the player can *be* the news.

Out of this spec (contract-level only, owned by the future game-layer spec): the live
tick system, player interaction with the sim, and reconciliation of live mutations
with the delta layer.

### 7.8 Emergent zone typing

Zones are **emergent, not painted** — the original zone list falls out of the sim:

| Zone tag | Emerges from |
|---|---|
| War zone | contested at final epoch |
| Dead zone | collapse ruins, or natural voids |
| Volatile zone | frequent ownership flips across epochs |
| Stable core | old heartland unchanged for many epochs |
| High/low trade | route throughput × development |
| Frontier / unclaimed wilds | never claimed — expansion budgets + void barriers guarantee these exist (tunable via epoch count) |
| Chokepoint fortress | articulation cells with military investment |
| Breadbasket / mining belt / blockade-scarred | commodity production and war history |

### 7.9 Implementation staging

The sim is built in shippable stages, each independently testable and playable:
**(1)** core loop — expansion/development/war with flat budgets; **(2)** budget
allocation + military stockpile; **(3)** commodities, flows, value, blockades;
**(4)** relations ladder, federations, conquest composition, schisms; **(5)** news,
stances, reputation; **(6)** event→POI compiler + world-state handoff finalization.

## 8. Tier 3: Per-Hex Integration

Phase 1's architecture bet pays off here: cross-influence was already call-site
`Func<T,double>` weight modifiers on `WeightedTable`, so regional influence composes
in without a pipeline rewrite.

Hex generation resolves its region cell (+ neighbors) into a **RegionContext**:

1. **Modifier bundle (soft influence)** — pure functions of cell state:
   - *Settlement odds* scale with development tier and polity presence (heartland ↑↑,
     unclaimed wilds ≈ baseline frontier rates, dead zones ≈ 0).
   - *Society rolls* take polity flavor: government archetypes biased by the polity's
     ideology bundle (with organic exceptions), infrastructure/port tiers pulled
     toward polity tech, population boosted in developed cells, resident species mix
     as society flavor.
   - *Overlay mix* by zone tag: war zones boost wreckage overlays, dead zones boost
     ruins, stable cores suppress hazards.
   - **Nature stays natural:** biosphere, atmosphere, and body kinds are never
     politically modified — civilization-facing rolls only.
2. **Pre-commitments (hard facts)** — anchored hexes override their specific fields;
   unpinned fields roll normally. Anchored hexes are excluded from the random overlay
   roll (historical POIs and random mysteries don't pile up).
3. **Annotations (display)** — history lines, polity identity, zone naming; exposed
   for inspection/UI, never affecting rolls.

**Smoothing:** continuous modifiers (settlement scale, development) interpolate
across neighboring cell centers so cell edges don't seam; discrete facts (polity id,
zone tag) do not interpolate — political borders are hard lines, correctly.

**API:** `Generate(GalaxyContext, coord)` where GalaxyContext = config + skeleton. A
**flatspace mode** (null RegionContext → neutral modifiers + flat density) is
retained deliberately: the isolation harness for Phase 1 pipeline tests and the
back-compat path for the existing suite.

**Determinism:** modifiers are pure functions of skeleton state; existing
RollChannels untouched; new draws get new channels.

**Naming hook (contract-level):** systems inside a polity eventually draw per-culture
syllable flavors; polity names extend the existing syllable generator in this phase.

## 9. Tooling: the Inspector Becomes a Galaxy Atlas

- `galaxy <seed> <sectors>` — build or load a skeleton; prints build stats (polity
  count, epochs, zone mix, unclaimed %, wall-time).
- **ASCII map view** — the flagship: `map` renders the galaxy at cell resolution with
  togglable layers (density shading / polity ownership / zone tags / development /
  trade routes); `map <sector>` zooms to hex resolution (presence + settlement).
  This is how we eyeball whether galaxies look like galaxies and kingdoms like
  kingdoms — the visual counterpart of `stats`.
- `polity <id>`, `cell <x> <y>`, `chronicle [polity|cell]` — registry inspection and
  event-log browsing ("show me the Ore War").
- `stats` gains per-zone and per-polity breakdowns.
- Artifact `save` / `load`.

## 10. Testing Strategy

- **Determinism** — same GalaxyConfig → byte-identical serialized skeleton (hash
  compare); same skeleton → identical hex output; version stamps verified;
  load-vs-rebuild equivalence within one code version.
- **Sim invariants** — budgets never negative/NaN; every owned cell traces to a live
  registry polity; event log referentially intact (every POI's cells/polities
  exist); merges conserve territory and membership; news never arrives before it
  happens; anchored-hex pre-commitments always honored by hex output.
- **Shape acceptance bands** — the Phase 1 `stats` lesson institutionalized: polity
  count, unclaimed-wilds %, zone-tag mix, and mean density asserted within tunable
  bands over a reference config, so a weight change that quietly paints the whole
  galaxy claimed fails CI.
- **Golden snapshots** — one small fixed-config galaxy's skeleton summary plus a
  handful of hex dumps as literal expected strings, catching unintended drift across
  refactors (adopting the Phase 1 final-review recommendation).
- **Field tests** — bounds [0,1], zero beyond the rim, mean near the density knob.
- **Flatspace regression** — the existing Phase 1 suite keeps passing in flatspace
  mode.

## 11. Deferred / Follow-Up Work

- **Hex-level highway rendering** — which specific systems form a route chain
  (follow-up spec; cell-resolution graph + throughput ship in this phase).
- **Live-play simulation** — fine-grained ticking, player interaction, delta
  reconciliation (game-layer spec; this phase delivers the steppable state and
  world-time rates it needs).
- **Per-culture naming flavors**; news distortion modeling; region-varying overlay
  rarity beyond zone tags; additional embodiment types.
- **Additional sim tiers** — deeper economics, migration waves, espionage — append
  as new artifact passes on the proven foundation.

## 12. Amendments to DESIGN.md

1. Save-file philosophy: from "seed + deltas only" to **"seed/config + galaxy
   structure artifact + deltas."**
2. Game-layer readiness: mutable state = deltas *plus a continuing simulation* seeded
   by the world-state handoff, not deltas over a static baseline.
3. Generation input: `masterSeed` → `GalaxyConfig` (seed + size + tuning knobs).
4. Coordinates gain a defined galaxy extent; beyond-rim hexes are empty space.
5. The roadmap's regional-generation sketch is superseded by this spec.
