# Epoch Simulation Master Frame — Vision, Actors, Clocks, Flow, and the Design-Pass Roadmap

Status: **draft — awaiting user review**
Date: 2026-07-09

## 1. Overview

This document is the **master frame** for the epoch simulation: the constitution every
subsystem design must satisfy, the actor taxonomy, the time model, the per-step phase
flow, the subsystem map with interfaces, and the roadmap of deep-dive design passes
that follow. It is the product of a deliberate step back: stages 1–3 of the simulation
were implemented from a spec (`2026-07-07-regional-generation-design.md` §7) that was
deep enough to slice implementation from but not deep enough to be a mechanics design.
This frame supersedes that spec's §7; the shipped stage 1–3 code is hereby reclassified
as the **running prototype** — kept green until each subsystem's designed replacement
lands.

Scope of the redesigned simulation (everything below gets a dedicated design pass):

- A real economy: an expanded commodity vocabulary with use-case-driven demand,
  supply/demand price formation, true wealth, and **corporations** — emergent
  trans-polity economic institutions.
- **Infrastructure**: investment builds concrete facilities (mines, shipyards,
  spaceports, stations) with mechanical effects, replacing abstract development
  scalars and random hex outputs.
- **Ships and fleets**: the physical carriers of goods, power, information, and
  influence; military strength, trade throughput, and news propagation all become
  fleet-mediated.
- Polity interiors: demographics and migration, culture and ideology drift, internal
  factions, leaders/characters/dynasties, government forms, and a unified
  **graduation** mechanism for the birth of new institutions.
- Inter-polity dynamics: contact, relations ladder, federations, vassalage, and a war
  redesign with non-economic causes, fought with fleets over infrastructure.
- Narrative: explicit perception (news, stances, reputation), a shared event grammar,
  chronicle and era views, the event→POI compiler, and the world-state handoff.
- **Deep genesis**: a cosmological structure simulation and an evolutionary life
  simulation (including precursor civilizations as previous emergence waves) that
  replace the current single-pass analytic seeding and produce a staggered
  **emergence schedule** — polities enter history asymmetrically.

Design decisions ratified in this frame (session 2026-07-09):

- Everything is on the table: stages 1–3 are a prototype that informed this design,
  not a constraint on it.
- Depth serves **both customers equally**: the readable history (map, chronicle,
  ruins) and the inhabitable world (the live game's inherited state).
- Player scope ladder: trader, explorer, mercenary, political agent, and
  dynastic/faction/polity control — a player can scale from one character to a full
  polity (4X style) and back. The world is a story sandbox at every timescale and
  perspective scope.
- Genesis budget: **up to a minute or more** at reference scale (~1,600 cells) — a
  "generate world" moment, Dwarf Fortress worldgen-style.
- Design structure: top-down layered — this frame first, then one deep design pass
  per level, interfaces before internals. Design completes before implementation
  resumes.

## 2. Design Principles (the constitution)

Every mechanic in every design pass is judged against these.

**P1. The two-customer test** *(strengthened map-legibility rule).* Every mechanic
must produce both: (a) legible residue at genesis — visible on a map layer, in the
chronicle, or as a POI; and (b) inhabitable state at play — a quantity or relationship
a player at some scope could observe, exploit, or change. A mechanic that only tunes
internal numbers fails.

**P2. Replaceable controllers.** Every decision-making actor decides through a single
interface: *perceived state in, intents out*. The genesis AI, a future smarter AI, and
the player are interchangeable controllers at every scope (character,
corporation/faction, polity). Nothing inside the sim may care who is driving. This is
what makes "start as a trader, end running an empire, drop back into one character"
architecturally free.

**P3. Perception is explicit.** Because controllers act on perceived state (P2), what
an actor knows is modeled, not assumed — news arrival, stale information, rumor
attenuation. At genesis this gives the rim its lag; at play speed it makes the
player's information position (and being the news) real. No actor reads global truth
when deciding.

**P4. Conservation and causality.** Goods, wealth, population, ships, and
infrastructure are conserved quantities — produced, transformed, moved, consumed,
destroyed, never minted by fiat. Every dramatic outcome must be traceable backward
through the event log to its causes ("why did this empire fall?" always has an
answer). Supply/demand economics is only meaningful on top of conservation; chronicle
quality is only trustworthy on top of causality.

**P5. Emergence over paint.** Zones, trade corridors, corporations, reputations,
borders, ruins, precursor sites — all residue of simulated events, never decorative.
Corporations are founded by the sim when conditions warrant; precursor archaeology is
the residue of actually-simulated earlier civilizations (§4, evolutionary clock).

**P6. Determinism and the artifact discipline.** Non-negotiable and unchanged:
stateless hash rolls keyed by (step, actor id), fixed iteration order, versioned pass
schemas, persisted artifact, hex tier never persisted.

**P7. World-time rates, multiple clocks.** All rates are expressed in world-years.
One state machine per layer, integrated at that layer's step size; the generational
machine must also tick fine-grained at play. Mechanics that only work at one clock
speed are misdesigned.

**P8. Story at every zoom.** The history must read coherently at galaxy scale (rise
and fall of empires), polity scale (a nation's arc), and character scale (a life
inside those events). This is the narrative counterpart of P2's scope ladder — and the
test that decides how deep characters need to be.

## 3. Actor Taxonomy

Six kinds of entity in three fundamentally different categories: **institutions and
characters decide; populations respond; assets are acted through.**

**The common actor substrate.** Every decision-making actor — polity, corporation,
character — shares one shape: an identity, a **perception state** (what it currently
believes about the world, per P3), a **controller slot** (AI or player, per P2), and
participation in the event log. Institutions additionally have **roles** (ruler,
board, faction head, admiral, captain) that characters occupy. Deep-dive passes design
against this substrate.

**3.1 Population** *(substrate, never an actor).* Species-tagged, cell-local
quantities carrying attribute distributions: culture, ideology leanings, standard of
living. Population is the **demand side** of the economy (consumption by use-case is
what gives commodities value), the **legitimacy base** of internal politics (factions
draw strength from population segments), and the thing that **migrates**
(pressure-driven: famine, war, opportunity gradients). Populations respond
statistically to conditions — no controller, ever. Conquest, migration, and diaspora
make cell populations mixed, replacing the prototype's single-species-per-cell
simplification.

**3.2 Polity** *(territorial institution).* The sovereign actor: owns cells, taxes
economic activity, budgets, expands, declares war, conducts diplomacy. Gains an
interior: government form, ideology (drifting, not a static seed), internal factions,
and a leadership role occupied by a character. A polity's temperament stops being a
fixed species vector and becomes a **composition**: species disposition × ideology ×
current leadership — the same nation can turn aggressive under a new ruler.

**3.3 Corporation** *(non-territorial institution).* Emerges — founded by the sim
when commodity concentration, trade volume, and peace allow (P5); never seeded. Owns
**assets**, not territory: extraction infrastructure, freighter fleets, route
contracts, depots, charters granted by polities. Operates across borders; its
interests (route security, low tariffs, resource access) are distinct from any
polity's, and it exerts **influence**: lobbying factions, funding development, evading
sanctions, occasionally out-wealthing small states. Can be chartered, taxed,
nationalized, or expelled — each a legible event.

**3.4 Character** *(individual).* Sparse by design: characters exist only where story
needs them (P8) — occupants of institutional roles, plus event-born notables (war
heroes, founders, prophets, pirate lords, famed admirals). Characters carry
personality (colors the institution's decisions while they hold its role), lifespan
(mortality forces succession — a native drama generator), and lineage (dynasties). At
genesis resolution characters are generational; at play resolution a character is a
full controller scope.

**3.5 Internal faction** *(semi-actor).* An interest bloc inside a polity —
ideological, regional, species-based, or corporate-aligned — with an agenda and a
strength drawn from population segments and patrons. Factions exert pressure (steer
budgets, force wars or peace, trigger succession crises) but hold no controller slot
**until they graduate**: a schism makes one a polity; a chartered venture makes one a
corporation; a coup puts its leader in the ruler role. Graduation is the unified
origin story for new institutions, replacing the prototype's flat schism odds.

**3.6 Assets** *(owned physical things; acted through, never deciding).* Two
families:

- **Infrastructure** (anchored, immobile): mines, shipyards, spaceports, depots,
  stations, fortresses. Built by polity/corporate investment, sited by rules (mines
  want belts, spaceports want route junctions), each with mechanical effects on its
  cell and owner (extraction multipliers, ship production, trade cost/efficiency).
  Infrastructure anchors into per-hex generation as pre-commitments: the spaceport a
  player visits is the one the sim built in epoch 31, not a random roll (P1).
- **Fleets** (mobile): freighters carry goods, warships project power, and everything
  abstract becomes physical — trade flows need hulls (freight capacity constrains
  throughput), military strength *is* fleet composition (the prototype's stockpile
  scalar dissolves), information travels with traffic (news propagation speed becomes
  emergent from shipping density rather than a knob), and reach is literally where
  your ships can be. Notable fleets take **commanders** through the role bridge. Ship
  classes are species- and culture-flavored (hive ships, machine swarms, dreadnoughts;
  civilian/corporate/military/state classifications) — designed in the ships pass.

Assets make power **concrete and conserved** (P4): no strength exists that wasn't
built at a shipyard that consumed real ore.

**Not actors:** species and culture (property layers on population), wars and
relations (relationships between actors), markets (arenas actors meet in).

Load-bearing choices: **roles as the character↔institution bridge** (characters matter
by coloring institutional decisions; thousands of individuals are never
micro-simulated), **faction graduation** as the single institutional origin mechanism,
and **assets as the physical form of power**.

## 4. Time Model — Four Clocks

Each clock's step size matches the granularity at which its *stories* happen; each
hands the next a finished board plus latent story material. Durations are honest
narrative compression, not physics.

| Clock | Span / step | Subject | Hands to the next |
|---|---|---|---|
| **Cosmic** | ~14 Gyr in coarse steps | Structure: nebulae, coalescence, stellar generations, supernova enrichment → element/metallicity distribution, density pockets, voids, mineral-rich vs. gas-rich regions | The physical galaxy |
| **Evolutionary** | ~Gyrs in ~Myr steps | Life: biosphere seeding and spread, evolutionary progressions, extinctions and catastrophes, sapience maturation, **precursor civilizations** | The living galaxy + **emergence schedule** + archaeology layer |
| **Generational** | ~1,000y in ~25y epochs | History: polities, economies, wars, culture | The political galaxy (world-state handoff) |
| **Play** | days–weeks per tick | Experience: the player at any scope | — |

**The epoch is a generation** (~25 world-years, a knob). This is the unit at which
history is legible: a ruler reigns one to three epochs, a two-epoch war is a long war,
culture drifts noticeably across a few. Default history depth: **~40 epochs ≈ 1,000
years** — enough for empires to rise, calcify, and leave successor states several
times over. The minute+ genesis budget pays for this.

**All rates in world-years** (P7). An epoch integrates 25 years of each rate; the
live game integrates the same rates at fine ticks. Nothing is expressed "per epoch"
internally — the epoch is an integration step, not a unit. (Retrofit note: the
prototype keys rolls by epoch index and holds per-epoch rates in places; the
world-year conversion is a mechanical but real migration, and belongs in the first
implementation slice after the design passes.)

**Generational vs. play differ in sampling, not rules:**

| | Genesis (coarse) | Play (fine) |
|---|---|---|
| Step | one generation | days–weeks |
| Characters | sampled at role changes: succession, founding, death | continuous individuals |
| News (P3) | pulse arrival quantized to epochs | travels with actual ships; the player can be the news |
| Markets | epoch-clearing | continuous drift between clearings |
| Determinism keys | (epoch, actor id) | (tick, actor id) — same hash discipline |

**Slow variables integrate every step; discrete events punctuate.** Continuous
quantities (population, prices, ideology, weariness, infrastructure condition)
accumulate each step from their rates. Discrete events (war declared, succession,
corporation founded, schism, emergence) are threshold-crossings of those continuous
quantities plus seeded rolls — every event is explainable by the pressures that
preceded it (P4), at any clock speed.

**Asymmetric emergence.** The evolutionary clock produces homeworld evolutionary
states maturing at different rates, so sapients reach spaceflight at different epochs
and polities **enter the generational sim staggered**. Early risers expand into
uncontested space and compound their advantage; late emergers are born into a
colonized galaxy — possibly inside someone's border. Pre-emergence homeworlds exist on
the map as terrain (pre-spaceflight sapients a polity or player can encounter).

**Precursors are previous emergence waves.** At evolutionary timescale, earlier
sapient civilizations rise, spread, and end before the present era. Their residue is
generated by the same machinery that produces the current era's civilizations:
precursor sites are their infrastructure, machine intelligences are plausibly their
descendants, and archaeology digs up actually-simulated history from a deeper stratum
(P5 reaches all the way down).

**The chronicle is timescale-aware from birth.** Every event carries its world-year
(not just a step index), so the story reads at any zoom (P8): galaxy-scale views name
eras, polity views read reign-by-reign, character views read a life. Era detection
(clustering epochs into named ages) is a narrative-pass concern; the data supports it
from day one.

## 5. The Simulation Flow — Seven Phases per Step

Two structural moves make the flow hang together.

**Move 1 — one controller touchpoint.** Decisions happen in exactly one phase
(Intent). Every controller emits two kinds of output there:

- **Standing policies** — budget weights, trade posture, diplomatic stances, military
  doctrine, shipbuilding priorities. Applied mechanically by *other* phases on
  subsequent steps.
- **Discrete acts** — declare war, offer alliance, charter a corporation, nationalize,
  found a colony, commission a fleet. Resolved this step.

Everything outside Intent is mechanical consequence. This is P2 with teeth: swapping
AI for player means swapping who answers one question — "given what you perceive, what
are your policies and acts?" — which is a 4X interface at polity scope, a tycoon
interface at corporate scope, and a character sheet at individual scope.

**Move 2 — decisions run on perception, consequences run on truth.** Phase 1 updates
each actor's believed world; Intent reads only that. Markets, battles, and migration
operate on actual state. The gap between the two is where stale-news drama lives (P3).

| # | Phase | What happens | Owning design pass |
|---|---|---|---|
| 1 | **Perception** | News arrives (carried by traffic); each actor's perceived state (stances, reputations, known prices, known wars) updates | Narrative |
| 2 | **Markets** | Production (cells + infrastructure) → demand (population use-cases, industry, military, tech) → **price formation per market** → trade flows route under freight capacity (tariffs, blockades, sanctions constrain) → revenues, tax take | Economy |
| 3 | **Allocation** | Standing policies applied mechanically: development and infrastructure investment, shipbuilding at shipyards (recipes consuming real goods), military upkeep, tech investment, corporate dividends/reinvestment, faction appeasement | Economy |
| 4 | **Intent** | The controller touchpoint: all institutions and role-holding characters emit policies + acts from perceived state | All (interface); per-actor AI in each pass |
| 5 | **Resolution** | Acts collide and resolve deterministically: expansion claims, fleet movement and positioning, war fronts and battles, blockades established, diplomacy matched (consent required both sides), annexations, capital falls | Inter-polity |
| 6 | **Interior & demographics** | Within polities: cohesion, ideology drift, faction strength and pressure, succession (aging, death), **graduations** (schism / coup / charter), corporation-founding checks. Globally: population growth, famine, migration flows. New polities enter per the emergence schedule | Polity interior |
| 7 | **Chronicle** | Events finalized with world-years; news pulses emitted (arriving in future steps by distance and traffic); map residue updated (scars, zone inputs, throughput snapshots) | Narrative |

**Ordering rationale:** perceive before deciding (P3); earn before spending (2→3);
budgets constrain intents (3→4); acts before consequences (4→5); interiors react to
what just happened — a lost war feeds faction anger this step (5→6); chronicle last so
every phase's events are captured and next step's news is this step's history (7→1).

**Continuity:** the prototype pipeline is a strict subset — income→Markets,
allocation→Allocation, action→Intent, resolution→Resolution. Existing mechanics
migrate into their phase; rewrite cost concentrates in Markets (price formation
replaces surplus→deficit routing) and the genuinely new phases (Perception, Interior).

## 6. System Map — Five Levels and Their Interfaces

Runtime has cycles (economy reads war state, war reads prices) — fine. The *design*
dependency order is linear, which is what lets the deep dives run in sequence without
rework.

| Level | Owns (state) | Provides (interface) | Reads |
|---|---|---|---|
| **L0 Substrate** | Commodity vocabulary (goods, use-cases, production recipes), infrastructure vocabulary & siting rules, per-cell production potentials, population stores (species/culture-tagged), connectivity graph, market geography | `Potential(cell, good)`, habitability per embodiment, demand profiles, buildable-infrastructure catalog | Deep-genesis outputs (board + emergence schedule) |
| **L1 Economy** | Markets (price state), trade flows, wealth ledgers, corporate registry, infrastructure ownership & condition, tariffs/sanctions state | **Prices**, tax income, corporate revenue, route throughput, freight capacity, blockade strain, asset effects | L0 potentials & demand; L3 constraints (wars, blockades, borders); fleet model; policies from Intent |
| **L2 Polity interior** | Factions, ideology state, government form, characters & roles, succession, cohesion, demographics (growth, migration) | Temperament composition (feeds Intent AI), stability/schism risk, **graduations**, leadership personality | L1 (standard of living, faction wealth); L3 (war outcomes → faction anger); L4 (news → opinion) |
| **L3 Inter-polity** | Relations matrix, wars, treaties/federations/vassalage, military fleet postures, fronts, battles | Constraint surfaces for L1 (blockades, borders, sanctions), war outcomes for L2, contact events | L4 perception (stances); L1 prices (war-goal value); L2 composition (militancy, faction pressure); fleet model |
| **L4 Narrative** | Event log, news pulses, per-actor perception states, reputation, chronicle/era views, POI compiler, world-state handoff | Perceived state (Phase 1), chronicle queries, POIs to the hex tier, the handoff | Everything — via events only |

The **fleet model** (ship classes, production, movement, capacity) is defined once in
its own pass and consumed by both L1 (freight) and L3 (war) — it is deliberately not
owned by either.

Four **cross-cutting interfaces** are defined by this frame because every pass builds
against them:

1. **Controller** (P2): `Decide(perceivedState) → (policies, acts)`. The frame defines
   the shape; each pass designs the AI implementation for its actor kinds.
2. **The price signal as the universal value language.** The prototype's hand-rolled
   "system value" heuristic is *replaced by market prices*: expansion attractiveness,
   war-goal selection, migration pull, corporate investment, and infrastructure siting
   all read the same price-derived valuations. One number system, and an emergent one —
   the single biggest unification the redesign buys, and why the economy passes come
   early.
3. **Event grammar** (P4): every subsystem emits events in one schema — actor ids,
   location, world-year, magnitude, typed payload. L4 owns the grammar; emitting
   well-formed history is a requirement on every mechanic, not a narrative
   afterthought.
4. **Pressure → graduation**: L2's faction machinery is the sole factory for new
   institutions; L3 consumes schisms (successor polities), L1 consumes charters
   (corporations). Emergence-schedule entries are the one non-faction institutional
   origin (new species arriving at spaceflight).

**Artifact discipline (P6):** each level's state is an artifact section with its own
schema version; phases are the passes; the deep-genesis clocks are upstream artifact
layers. Per-actor perception states are the one flagged design wrinkle — whether they
persist in full or compress to stances + knowledge horizons is an L4 design question.

## 7. Design-Pass Roadmap

Eight passes, each its own brainstorm→spec cycle, in dependency order. Each inherits
the parked tickets that belong to it, so the debt list gets structural answers rather
than patches.

| # | Pass | Covers | Inherits |
|---|---|---|---|
| 0a | **Cosmic genesis** | Deep-time structure simulation: nebulae, coalescence, stellar generations, enrichment → element distribution, density structure, voids, mineral-rich vs. gas-rich regions. Replaces the analytic Tier-1 shape + one-shot seeding | — |
| 0b | **Life & precursors** | Evolutionary clock: biosphere seeding/spread, extinctions, sapience maturation → **emergence schedule**; precursor civilizations as previous emergence waves → archaeology layer | precursor-site placement rules |
| 1 | **Substrate & commodities** | Goods vocabulary (well beyond Provisions/Ore/Exotics), use-cases and demand profiles, production recipes, embodiment relativity, **infrastructure vocabulary & siting rules**, market geography | exotics-deficit dead code (fixed at the root by real demand design) |
| 2 | **Markets, wealth & corporations** | Price formation and clearing, trade routing under prices, wealth/taxation, corporate lifecycle (founding → influence → death/nationalization), **infrastructure investment & asset effects**, tariffs/sanctions, commodity stockpiles | sanction blockades; blockade-strain tuning (under-fire, multi-count); provisions stockpiles; trade→relations hook |
| 3 | **Ships & fleets** | Ship classes (species/culture-flavored), production chains at shipyards, the fleet model (composition, movement, reach), freight capacity, information carriage, commanders (admirals/captains via roles) | military-stockpile abstraction (dissolved into fleets) |
| 4 | **Polity interior** | Demographics and migration, culture/ideology drift, factions and pressure, characters/roles/succession/dynasties, government forms, cohesion → graduation | static species temperament; conquest composition |
| 5 | **Inter-polity** | Contact, relations ladder, federations/vassalage, war redesign — causes (economic, ideological, faction-driven, spark events), conduct (fleets over infrastructure: fronts, battles, blockades), termination and settlement | non-deficit war causes; war-goal variety |
| 6 | **Narrative & handoff** | Perception/news/reputation (traffic-borne propagation), event grammar formalization, chronicle and era views, POI compiler, world-state handoff, four-clock integration contract | news-speed knob (becomes emergent) |

**Sequencing notes:**

- Passes 0a/0b are conceptually first but **separable**: the generational sim consumes
  only their outputs (board + emergence schedule), and today's seeding passes are the
  degenerate version of that interface. They may run in parallel with or after 1–2.
- Passes 1→2→3→4→5→6 run in order; each designs against the frame's cross-cutting
  interfaces and the outputs of its predecessors. One forward dependency is handled
  by stub: pass 2's trade routing consumes a **declared fleet-capacity interface**
  (capacity per route per owner); pass 3 designs its internals.
- **Design first, implement after.** All passes complete before implementation
  resumes; then implementation is re-planned as slices across the whole design. One
  pragmatic exception: L0/L1 implementation may start once passes 1–2 are stable,
  since they are strictly upstream.
- The prototype (stages 1–3 code) stays green as the running system until each
  subsystem's replacement lands.

## 8. Relationship to Prior Docs

- **Supersedes** `2026-07-07-regional-generation-design.md` §7 (the epoch simulation)
  and its §7.9 staging. Stage numbers 4–6 retire as design units; they survive in git
  history and old handoffs as implementation-era references. The rest of the regional
  spec (three-tier architecture, density field, skeleton lattice, seeding-pass
  mechanism, per-hex integration, artifact chain) **stands** — deep genesis (passes
  0a/0b) will amend its seeding sections when designed.
- `2026-07-08-sim-economy-design.md` and `2026-07-09-econ-deferred-tickets-design.md`
  describe the running prototype; their deferred-ticket lists are absorbed into §7's
  inheritance column.
- `docs/DESIGN.md` amendments on merge: the regional paragraph in §4 should note the
  master frame and the design-first phase; §2's game-layer readiness paragraph gains
  the replaceable-controller principle (P2) as the stronger form of the world-state
  handoff idea.
- The living flow diagram (`docs/diagrams/generation-flow.html` + artifact) is
  rebuilt around the four-clock / seven-phase / five-level frame once this spec is
  approved.

## 9. Acceptance for the Frame Itself

This frame succeeds if, during the eight design passes:

- No pass needs to change the phase order, the actor taxonomy, or a cross-cutting
  interface signature (additions are fine; reshapes mean the frame failed).
- Every mechanic designed in every pass passes P1 (two customers) and P4 (conservation
  + causality) without special pleading.
- The controller interface (P2) proves sufficient for: genesis polity AI, genesis
  corporate AI, character-scope actions, and a sketched player interface at each
  scope — checked as a paper exercise in each pass, not deferred to the game layer.
