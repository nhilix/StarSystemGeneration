# Locality — bodies become addressable — design (2026-07-14)

Three findings from the 2026-07-13 market/locality research pass
(`docs/superpowers/plans` conversation record; artifact
https://claude.ai/code/artifact/6686121e-67e7-4c04-997a-eeb813c58dae),
judged there to be "the same fix wearing different clothes":

1. **Population/fleet sub-domain locality** — `PopulationSegment`
   (src/Core/Epoch/PopulationSegment.cs) carries only `PortId`; a port's
   service radius spans many hexes (`frame/space-and-travel.md`), so a
   segment has no location finer than "somewhere in this domain."
   `FleetRecord.Hex` is only load-bearing for the Expedition/Convoy
   posture — every other posture resolves off `HomePortId` alone.
2. **Genesis-vs-simulation body disconnect** — `SystemQuery.At`
   (src/Core/Atlas/SystemQuery.cs) regenerates a hex's full system fresh
   on every query (correct — matches the hex-tier-never-persisted hard
   rule), but `Facility`/`Project` carry only a whole-hex reference.
   `FacilityOrbit()` reverse-engineers a plausible body for display only,
   by deterministic type-affinity, first-match — two same-type
   facilities at one hex collapse onto the same body, and the guess
   never feeds back into the sim: extraction reads hex-aggregate genesis
   fields, not the specific body picked.
3. **Off-lane movement is a fallback formula, not a place** —
   `ShipmentOps.PlanRoute` only computes an off-lane leg when no lane
   path exists at all; a blockaded lane just stalls freight in place.
   `grep Smuggl src/Core` returns zero matches — smuggling is named
   twice in the design (`markets.md`'s black book, the interdiction
   strain formula's "minus smuggling leakage") and built nowhere.

Brainstormed 2026-07-14 (superpowers:brainstorming). Scope, per that
session: all three threads together, in one design, even though
implementation may split into phases. A fourth question — how fleets and
freight actually traverse *within* a settled system once bodies are
addressable, since a body reference alone would make movement instant
and teleporting — surfaced mid-session and is folded in as §2.

## 1. The data model: `SettledSystems` and body refs

A new epoch-tier registry, keyed by hex coordinate. The first time
*anything* touches a hex — a facility breaks ground, a fleet crosses it,
a population segment settles — the sim calls the existing hex-tier
generator once and freezes the result (stars, orbit slots, bodies, same
shape genesis already produces) as real state. Every later read of that
hex — siting, extraction, population placement, fleet docking, atlas
rendering — resolves against this one frozen record instead of
re-deriving or guessing independently.

A body is addressed `(StarIndex, SlotIndex)` — the shape `SystemQuery`'s
`OrbitRef` already uses. The type moves down into the Epoch layer (Atlas
depends on Epoch, never the reverse); Atlas's `OrbitRef` becomes a reuse
of the Epoch-owned type.

`Facility`, `Project`, `PopulationSegment`, and `Fleet` (for any posture
except Expedition/Convoy) each gain a body reference field.

Unsettled hexes are unaffected: `SystemQuery` falls back to live
generation for any hex with no registry entry, so the unvisited galaxy
stays exactly as cheap and non-persisted as today. State only grows with
what actually gets visited (see §7 on the growth this implies).

## 2. Intra-system movement: the local-hop leg type

A new discrete distance function, `OrbitDistance(bodyA, bodyB)`: same
star → `|SlotIndexA − SlotIndexB|`; different stars in a multi-star
system → a fixed cross-star hop constant plus each body's distance to
its own star's innermost slot. Same trick `HexGrid.Distance` already
uses at the galaxy scale, one level down — deliberately discrete, not
continuous orbital mechanics, to stay consistent with every other
distance metric in the sim (P6/P7 discipline) and because continuous
coordinates would be the one outlier layer buying realism nothing else
needs.

This is a fourth leg type, or more precisely splits the existing
"intra-domain" leg (`frame/space-and-travel.md`'s leg-type table,
currently "facility hex ↔ its port, hex distance, local") into two
composable pieces: the existing hex-hop between hexes in a domain, plus
a new **local hop** between bodies within the arrival hex, priced by
`OrbitDistance × a local-hop rate knob` — kept cheap relative to a
lane-hop. Any leg resolving to a specific body (freight to a facility, a
fleet relocating its dock, a courier's origin/destination) composes
hex-hop + local-hop.

Two consequences, both load-bearing for §5, not flavor:

- **Patrol coverage falls off with orbital distance.** A Patrol fleet's
  domain-wide enforcement weakens with `OrbitDistance` from wherever
  it's docked, instead of applying as a flat domain-wide multiplier.
- **Smuggling cache value falls out for free.** A body far from a
  patrol's dock, or in an under-covered hex, is mechanically a better
  place to stage off-lane freight — no separate mechanic needed once
  the above exists.

Local-hop cost scaling down with port tier / astrogation tech (mirroring
how lane speed and service radius already scale with investment) is a
tuning detail for the implementation plan, not a design-level fork.

## 3. Population locality: body assignment and staffing

Segments gain a body ref, assigned at creation the way migration already
picks a destination — a new segment (organic growth, migration arrival,
conquest split) settles at the body with the best local opportunity
signal within its domain. A port-domain can hold several segments spread
across different bodies (homeworld population, a mining-outpost crew, a
habitat-ring community) instead of one aggregate per port.

**Staffing** becomes nearest-by-(hex-hop + local-hop), mirroring how
`AttachedMarketIndex` already finds a facility's nearest port. A
facility doesn't require a segment on its exact body — an airless mine
can be staffed by commute from a habitat one local-hop away — but
distance now genuinely weights who works where.

**Organic baseline** (unserviced subsistence farming/crafting) runs
per-body rather than per-port — same formula, finer address.

**Explicitly out of scope**: segments physically relocating between
bodies within one domain over time. Domain-to-domain migration
(SoL/safety/affinity/opportunity gradients) is unchanged; only the
arrival address gets finer. Flagged as the natural seed for a later,
deeper mechanic — passenger ships and migrating population as cargo —
not decided here.

## 4. Facility siting hookup

Resolves a chicken-and-egg problem: siting ranks candidate hexes before
anything is built at any of them; committing a full body snapshot just
to score a candidate would commit far more hexes than ever get built on.

- **Ranking is unchanged.** `Siting.Score` keeps scoring candidate cells
  off the cheap raster potentials (`Potentials.Ore`, etc.) — no real
  bodies needed for relative comparison, so unsettled candidates stay
  cheap to evaluate.
- **Body assignment moves from "guessed at every atlas render" to
  "decided once, at groundbreaking."** The moment a project breaks
  ground at the chosen hex, that's the §1 commit trigger: generate and
  freeze the hex's real system, then run the *existing* `FacilityOrbit`
  type-affinity logic (mine→belt/rock, skimmer→gas giant, agri→richest
  biosphere) against the real body list — once, permanently, as state.

This fixes the two-mines-one-belt bug for free: assignment is now real
state, so a second mine's placement can see the first mine's claimed
slot and pick a different belt or rock if the system has one.

Extraction grade reads the specific claimed body's fields instead of
hex-aggregate genesis fields — the throughline of the whole slice: real
body-level richness variance finally reaches the price signal.

## 5. Off-lane routing as a real alternative

Extends `ShipmentOps.PlanRoute` (and the courier job board's routing) so
an off-lane hex-by-hex alternative is computed *alongside* the lane
path, not only when a lane path is completely absent. A closed leg
(blockade, quarantine, dead gate) becomes a real second option — cross
off-lane instead of stalling — at real time cost and real risk.

Risk composes from the existing piracy roll per sail plus a new
**detection roll** modulated by §2's patrol orbital-distance coverage: a
route through hexes far from any Patrol fleet's dock is safer to run
than one passing close to a well-covered body.

Who elects it, and when, is an implementation-level tuning question, not
a design fork here — the mechanical commitment is that both options get
computed and a real choice gets made (urgency / cargo value / risk
tolerance), not that a specific formula is fixed now. **No new actor
type** — any shipment or courier can take this path. Cartels
(`corporations.md`: chartered nowhere, "operating through black books
and off-lane freight") lean on it hardest since it's already their
niche; War-priority couriers reach for it when a front's lane is fully
severed; ordinary commerce mostly still waits, since waiting is usually
cheaper than the risk. This is also the first real mechanism behind the
black book — "prohibited goods... smuggler-supplied" has been a
description with no supply path until now.

## 6. Atlas integration

`SystemQuery.At` checks the §1 registry first; if a hex is settled, it
renders the frozen bodies plus each facility/fleet/segment's *real*
placement. `FacilityOrbit`'s type-affinity logic doesn't disappear — §4
moves it to fire once, at groundbreaking, instead of on every render —
but its role as a per-query *guess* is retired: `SystemQuery` reads the
decided placement from state, it no longer calls the matching logic
itself. Unsettled hexes render exactly as today. Population segments gain a real system-stage
position for the first time (currently no spatial representation below
the port). Local-hop travel visualization (fleets crossing between
bodies within a system stage) is a natural K6-or-later addition, not
required for this slice's eyeball gate.

## 7. Determinism, conservation, testing

**Determinism**: the commit is trivially safe — the hex-tier generator
is already a pure function of `(GalaxyConfig, hex)`, so freezing its
result the first time anything touches the hex is deterministic
regardless of what touched it first. The operation needs to be
idempotent (memoize-once); trigger order within a step doesn't matter
for correctness.

**State growth**: committed hexes accumulate with no eviction proposed —
add a tracked `SIMHEALTH.md` metric (settled-hex count over a sweep) so
growth gets the same evidence-based scrutiny as everything else, per
project convention, rather than an assumption.

**Conservation (P4)**: this slice mints or sinks nothing — it's a
locality/addressing change. Aggregate production shifting once grade
varies per body instead of per-hex-average is an intended economic
consequence, not a conservation violation; existing money-conservation
tests are unaffected.

**Testing**: goldens re-freeze once at slice end (siting/extraction
output legitimately changes) — same discipline as past slices. New
coverage: `OrbitDistance`, hex-commit idempotency, the
two-mines-different-bodies case, off-lane route selection.

## Boundary (deferred, not decided here)

- Intra-domain population migration / passenger ships as cargo (§3).
- Local-hop travel visualization in the Unity atlas (§6) — K6-or-later.
- The exact off-lane election formula (§5) — implementation-plan detail.
- Local-hop cost scaling with port tier/astrogation tech (§2) —
  implementation-plan detail.
- Multi-hop actor runs / retiring relay bids — separate economy slice,
  unrelated to this one (carried from the ME kickoff's boundary note).

## Provided interface

- `SettledSystems` registry: frozen per-hex body state, queryable by
  hex, the one source of truth for siting/extraction/population/fleet/
  atlas.
- Body refs on `Facility`, `Project`, `PopulationSegment`, `Fleet`.
- `OrbitDistance(bodyA, bodyB)` — the local-hop cost basis.
- A real off-lane leg option on shipments and couriers, with its own
  risk model (piracy + detection).
- A `SIMHEALTH.md` settled-hex-count metric.
