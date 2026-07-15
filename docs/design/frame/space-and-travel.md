# Space & Travel — The Two-Plane Model

What space *is*, and what movement, presence, and interdiction concretely mean.
Every subsystem builds on this model; every political abstraction here has a
physical hex address.

## The two planes

- **Hexes are physical space.** Systems, facilities, fleets, and events all have
  hex addresses. There is no political fact without a physical carrier at a hex.
- **The cell lattice is the natural raster only.** Nature's fields — density,
  metallicity, habitability history, the cosmic and evolutionary outputs — live at
  fixed cell resolution, as a sampling grid that exists before any civilization.
  Terrain facts (voids, chokepoints) are raster-derived. The lattice carries **no
  political meaning**.
- **Political and logistical geography is emergent port domains.** Civilization's
  spatial structure derives from the port registry, not from the grid.

## Ports and domains

A **port** is the keystone infrastructure: a starport at a specific hex, with two
independent growth axes:

- **Local service radius** — the hexes it administers and economically services:
  the local hand of its polity. Grows with port tier (investment) and technology;
  shaped by terrain (voids and empty reaches dilute effective range). An early
  exploration hub services a few hexes; a late imperial megahub covers a swath.
- **Gate slots** — the second growth axis: each tier grants slots for **gate**
  facilities, one gate per lane end. Lane *degree* is physically capped by port
  investment: a port that wants to be a hub must grow first. Reach, capacity,
  and transit speed live in the gates themselves, not the port.

**Claiming space is building a port.** A colonization convoy travels (off-lane) to
a frontier system and establishes the port; the domain starts small and grows with
investment. Homeworlds are simply the first ports. Space without port coverage is
**wilds** — visitable, even inhabited, but off the network.

**Territory is derived, never stored.** A polity's territory = the union of its
ports' service areas, computable from the port registry. Political state lives in
**sparse, hex-addressed registries** (ports, infrastructure, fleets) over the
natural raster — there is no per-hex or per-cell ownership paint (P4, P5).

**Domain overlap is allowed and meaningful.** Where two polities' service ranges
overlap, the overlap is a **contested-influence zone** — border friction, dispute
fuel, war goals. Borders are organic reach-collision shapes, not tile edges.

### Colonization, end to end

The chain, stitched across its owners: **decision** — an Intent act picks a
target from price-signal valuations × terrain potentials × reach; **assembly** —
a convoy fleet forms (colony hulls with berths, construction goods, volunteer
population segments, escort); **journey** — off-lane legs at the convoy's
endurance floor, at real risk; **founding** — the tier-1 port establishes, the
domain starts small, the founder may be minted; **growth** — investment raises
the port, migration flows in, facilities site. Every step is ordinary machinery;
colonization is just the machinery pointed outward.

## Lanes

A **lane** is a linked pair of **gate facilities**, one standing in each port
system — the bulk-economy channel, mass-driver fiction made registry fact. Gates
are tiered (1–3) like every facility, built from real goods drawn across the pair
(each end's market, the partner's surplus, the funder's reserves — state
logistics ship the difference), owned by whoever paid: polity **or corporation**.
The lane is live only while both gates stand and function; a raided gate severs
the lane without touching the port, and the survivor pointing at nothing is a
visible wound.

- **Reach comes from gate tier** (min of the two ends): tier-1 gates link short
  hops, tier-3 gates span long corridors — and facility tier costs are
  superlinear, so length is priced steeply. Astrogation stretches reach.
- **Capacity and transit speed** derive from the gate tiers; the weaker gate
  bounds both.
- **The anti-web rule**: a builder considers a direct lane only when the network
  can't already get there within a detour factor of the direct distance — or
  when every lane on that path has run **saturated** long enough (a world-year
  clock) to earn a congested corridor its express bypass. A→B→C→D chains carry
  the traffic, intermediate ports get their hauler wages and market flow, and
  hubs emerge from geometry instead of subsidy.
- **Founding links**: an isolated port's first lane is the colonization
  chain's last step — colony ship arrives, the foothold establishes (port +
  essential industry), then the connecting gate joins the new system to the
  polity network for import/export/migration. The builder links every isolated
  port to its nearest eligible partner (preferring one already on the network)
  before any densification project; no port is left off the web while a
  reachable, affordable partner exists.
- **Cross-border lanes**: polities pair with trade-pact partners' ports;
  **freight-line corporations** bridge any profitable, non-hostile border on
  their own books, owning and tolling both gates — no treaty required, so
  profit walks across the border before diplomats do.
- **Crossing fees** are decided by the destination-side gate's owner: your own
  gate is free (vertical integration pays), a corp gate tolls, a foreign polity
  gate collects its tariff schedule as customs — once, at entry
  (economy/markets.md §Sanctions and tariffs).
- **Piracy prices length**: more hexes, more ambush points — longer lanes tempt
  raiders at thinner cargo.

Terrain and reach define *potential* geometry; the actual network is **built**,
polity by polity, epoch by epoch — the map's highways are somebody's investment
(P5). Freight is only economical on-lane.

## Off-lane movement

Space is open: any ship can cross hex-by-hex at heavy time cost — *if it can*.
Off-lane endurance is a **ship-class axis** and a real investment trade-off (cheap
lane-bound freighters vs. expensive long-range hulls). What this yields without
special-case rules: smugglers (blockade leakage), scouts and flanking fleets,
explorers in the wilds, colonization convoys, and raiders — all the same movement
model, priced differently.

## Movement and time

Journeys compose from three leg types:

| Leg | Scale | Cost basis |
|---|---|---|
| **Hex hop** | facility hex ↔ its port | hex distance, local |
| **Local hop** | body ↔ body within an arrival hex | `OrbitDistance × Economy.LocalHopYearsPerOrbitStep` |
| **Lane hop** | port ↔ port | fast; lane quality |
| **Off-lane crossing** | anywhere ↔ anywhere | slow; hex distance × ship endurance |

The former "intra-domain" leg is really two composable pieces: the **hex hop**
between hexes in a domain, and the **local hop** between bodies within the
arrival hex — same star or across a multi-star system's stars, priced by the
discrete `OrbitDistance` metric (locality slice §2), kept cheap relative to a
lane hop. Any leg that resolves to a specific body (not just a hex) composes
hex-hop + local-hop.

At the generational clock these are throughput and delay rates; at the play clock
they are literal journeys (P7). News rides the same traffic: fast along busy lanes,
slow through wilds (P3).

## Interdiction

A **blockade** is a fleet stationed at a port's approaches — one hex address —
cutting specific lanes. The economy strangles because bulk freight is lane-bound;
interdiction leaks because off-lane smuggling slips a trickle past any siege, so
blockade strain is a continuous quantity measured per lane. Piracy preys on lanes;
sieges are fought at ports; military surprise moves off-lane through the wilds.

## Inside a domain

Every claimed region has a reason-structure: the **port hex**, **infrastructure
hexes** (facilities placed by investment at chosen systems — two mines among six
candidate belts), and **organic systems** that are simply there, settled sparsely,
served by the port. Development is literally proximity-to-port plus what has been
built — not an abstract per-cell scalar.

## P1 evidence

- **Legible residue**: the atlas shows empires as port-domain glows with organic
  borders; lanes render as literal highways; blockades render as fleets at
  addresses; the wilds are visibly dark.
- **Inhabitable state**: the player flies the same legs the sim prices — hauls
  freight on lanes, runs blockades off-lane, scouts the wilds, escorts a
  colonization convoy, besieges a port.

## Consumers

Substrate (port/lane in the infrastructure vocabulary, siting rules); economy
(freight and tariffs on lanes, strain as lane interdiction); fleets (endurance as
a class axis, convoy mechanics); inter-polity (sieges at ports, off-lane
maneuver); narrative (news propagation from lane traffic).
