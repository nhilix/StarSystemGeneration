# Space & Travel Model — Frame Amendment E

Status: **draft — awaiting user review**
Date: 2026-07-09
Parent: `2026-07-09-epoch-sim-master-frame-design.md` (amends the frame with a sixth
frame document). Product doc: `docs/design/frame/space-and-travel.md`.

## 1. Overview

The master frame defined actors, clocks, phases, and levels but not the **physical
model of space** — what travel, presence, and interdiction concretely mean across
the hex/cell boundary. The gap surfaced as a user design question: a cell-level
blockade has no hex-level meaning (which systems? where are the ships?), travel
time had no defined scale, and hexes inside politically-coherent cells read as
random. This amendment defines the two-plane space model that every subsequent
design pass builds on.

## 2. Decisions

1. **Hub-and-spoke logistics** (user proposal, adopted): the port is the keystone
   infrastructure; claiming space *is* building a port; production routes
   facility → port → lanes; blockades are enforced at port approaches — every
   political abstraction gains a hex address.
2. **Economic highways + ship-class capability** (chosen over hard gates and over
   pure highways): lanes (paired port infrastructure) are the only *economical*
   bulk channel, but space is open — off-lane crossing is possible at heavy time
   cost, gated by a ship-class endurance axis that is itself an investment
   trade-off. Smuggling, scouting, exploration, flanking, and colonization
   convoys fall out of one movement model priced differently; blockades strangle
   but leak (strain is continuous). Hard gates were rejected (kills smuggling and
   off-lane maneuver, needs bootstrap exceptions anyway).
3. **Variable port domains; the lattice retained as natural raster only** (user
   extension, adopted): "cell" had conflated two jobs — terrain sampling grid and
   political unit. The fixed lattice keeps only the first: nature's fields
   (cosmic/evolutionary outputs, density, habitability) stay at cell resolution.
   Political/logistical geography becomes **emergent port domains**: local service
   radius (investment + tech, terrain-shaped) and inter-port range/efficiency
   (separate tech axis governing lane reach, count, speed) grow over epochs.
4. **Territory is derived, never stored**: polity territory = union of port
   service areas, a pure function of the port registry. Political state inverts
   from dense per-cell paint to sparse hex-addressed registries (ports,
   infrastructure, fleets) over the natural raster.
5. **Domain overlap is allowed and meaningful**: overlapping service ranges are
   contested-influence zones — border friction, dispute fuel, war goals — rather
   than forbidden by first-claim partitioning. Borders are organic reach-collision
   shapes.

## 3. Consequences

- **Standing tickets absorbed**: blockade-strain under-fire/multi-count becomes
  per-lane interdiction measurement with off-lane leakage; "hexes feel random
  inside logical cells" is resolved by the domain reason-structure (port hex,
  invested facility hexes, organic systems).
- **Travel time defined at both clocks**: three composable leg types
  (intra-domain, lane hop, off-lane crossing) are rates at the generational clock
  and literal journeys at play (P7); news rides the same traffic (P3).
- **Prototype migration cost accepted**: per-cell political state (owner,
  development tier, contested, population) migrates to per-domain/registry state
  during implementation. The prototype was already slated for subsystem-by-
  subsystem replacement; this deepens the economy/war data-model change, not the
  schedule.
- **Pass 0a untouched**: the cosmic design already operates on the natural raster.
- **Design passes inherit**: substrate (port/lane vocabulary, siting), economy
  (lane freight/tariffs, interdiction strain), fleets (endurance class axis,
  convoys), inter-polity (port sieges, off-lane maneuver), narrative (news from
  lane traffic).

## 4. Frame-Consistency Check (master frame §9)

Additions only: a sixth frame document; no phase-order, taxonomy, or cross-cutting
interface reshape. The controller interface, price signal, event grammar, and
graduation mechanism are unchanged; assets gain the port as their keystone type
(already an infrastructure family member). P1/P4 evidence recorded in the product
doc.

## 5. Doc Amendments (applied with this spec)

- `docs/design/frame/space-and-travel.md` — new frame document (the product).
- Terminology pass over frame docs: "cell" retains only its natural-raster
  meaning; political references become domains/registries
  (`actors.md`, `system-map.md`, `simulation-flow.md`, `README.md` index row).
- Flow diagram: polity/population wording updated; republished.
- The master frame spec is unamended (it remains the dated record); this spec is
  the amendment record.
