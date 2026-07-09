# Actor Taxonomy

Six kinds of entity in three fundamentally different categories: **institutions and
characters decide; populations respond; assets are acted through.**

## The common actor substrate

Every decision-making actor — polity, corporation, character — shares one shape:

- an **identity** (registry id, participation in the event log),
- a **perception state** — what it currently believes about the world (P3),
- a **controller slot** — AI or player, interchangeable (P2).

Institutions additionally have **roles** (ruler, board, faction head, admiral,
captain) that characters occupy. Roles are the character↔institution bridge:
characters matter by coloring the decisions of the institutions they lead, so the
simulation never micro-simulates thousands of individuals.

## Population *(substrate, never an actor)*

Species-tagged, hex-addressed quantities (administered per port domain — see
[space-and-travel.md](space-and-travel.md)) carrying attribute distributions:
culture, ideology leanings, standard of living. Population is:

- the **demand side** of the economy — consumption by use-case is what gives
  commodities value;
- the **legitimacy base** of internal politics — factions draw strength from
  population segments;
- the thing that **migrates** — pressure-driven: famine, war, opportunity gradients.

Populations respond statistically to conditions; they never hold a controller.
Local populations are mixed: conquest, migration, and diaspora layer species and
cultures in one place.

## Polity *(territorial institution)*

The sovereign actor: its territory is the union of its ports' service domains
(derived from the port registry, never stored); it taxes economic activity,
budgets, expands by establishing ports, declares war, conducts diplomacy. A polity has an interior: government form, ideology (a
drifting quantity, not a fixed seed), internal factions, and a leadership role
occupied by a character. A polity's temperament is a **composition** — species
disposition × ideology × current leadership — so the same nation can turn aggressive
under a new ruler.

## Corporation *(non-territorial institution)*

Emerges — founded by the simulation when commodity concentration, trade volume, and
peace allow (P5); never seeded. Owns **assets**, not territory: extraction
infrastructure, freighter fleets, route contracts, depots, charters granted by
polities. Operates across borders; its interests (route security, low tariffs,
resource access) are distinct from any polity's, and it exerts **influence** —
lobbying factions, funding development, evading sanctions, occasionally
out-wealthing small states. Can be chartered, taxed, nationalized, or expelled —
each a legible event.

## Character *(individual)*

Sparse by design: characters exist only where story needs them (P8) — occupants of
institutional roles, plus event-born notables (war heroes, founders, prophets,
pirate lords, famed admirals). Characters carry:

- **personality** — colors the institution's decisions while they hold its role;
- **lifespan** — mortality forces succession, a native drama generator;
- **lineage** — dynasties.

At genesis resolution characters are generational (sampled at role changes:
succession, founding, death); at play resolution a character is a full controller
scope.

## Internal faction *(semi-actor)*

An interest bloc inside a polity — ideological, regional, species-based, or
corporate-aligned — with an agenda and a strength drawn from population segments and
patrons. Factions exert pressure (steer budgets, force wars or peace, trigger
succession crises) but hold no controller slot **until they graduate**:

- a schism makes one a **polity**;
- a chartered venture makes one a **corporation**;
- a coup puts its leader in the **ruler role**.

Graduation is the unified origin story for new institutions. The one non-faction
institutional origin is the emergence schedule: a new species arriving at
spaceflight enters as a new polity (see [time.md](time.md)).

## Assets *(owned physical things; acted through, never deciding)*

Two families:

- **Infrastructure** (anchored, immobile): the **port** — the keystone that
  projects a polity's territory and carries its lanes (see
  [space-and-travel.md](space-and-travel.md)) — plus mines, shipyards, depots,
  stations, fortresses. Built by polity or corporate investment, sited by rules
  (mines want belts, ports want the heart of a system cluster), each with
  mechanical effects on its domain and owner (extraction multipliers, ship
  production, service radius, lane capacity). Infrastructure anchors into per-hex
  generation as pre-commitments: the port a player visits is the one the
  simulation built in epoch 31, not a random roll (P1).
- **Fleets** (mobile): freighters carry goods, warships project power. Everything
  abstract is physical — trade flows need hulls (freight capacity constrains
  throughput), military strength *is* fleet composition, information travels with
  traffic (news propagation speed is emergent from shipping density), and reach is
  literally where your ships can be. Notable fleets take **commanders** through the
  role bridge. Ship classes are species- and culture-flavored.

Assets make power concrete and conserved (P4): no strength exists that wasn't built
at a shipyard that consumed real ore.

## Not actors

Species and culture (property layers on population), wars and relations
(relationships between actors), markets (arenas actors meet in).
