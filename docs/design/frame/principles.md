# Design Principles

The epoch simulation is a **deterministic history engine** whose output is a world
that can be *read* (map, chronicle, ruins) and *inhabited* (at any scope, at any
timescale). Every mechanic in every subsystem is judged against these eight
principles.

**P1. The two-customer test.** Every mechanic must produce both: (a) legible residue
at genesis — something visible on a map layer, in the chronicle, or as a POI; and
(b) inhabitable state at play — a quantity or relationship a player at some scope
could observe, exploit, or change. A mechanic that only tunes internal numbers fails.

**P2. Replaceable controllers.** Every decision-making actor decides through a single
interface: *perceived state in, intents out*. The genesis AI, a future smarter AI,
and the player are interchangeable controllers at every scope (character,
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
Corporations are founded by the simulation when conditions warrant; precursor
archaeology is the residue of actually-simulated earlier civilizations.

**P6. Determinism and the artifact discipline.** Stateless hash rolls keyed by
(step, actor id), fixed iteration order, versioned pass schemas, persisted artifact,
hex tier never persisted.

**P7. World-time rates, multiple clocks.** All rates are expressed in world-years.
One state machine per clock layer, integrated at that layer's step size; the
generational machine also ticks fine-grained at play. Mechanics that only work at one
clock speed are misdesigned.

**P8. Story at every zoom.** The history must read coherently at galaxy scale (rise
and fall of empires), polity scale (a nation's arc), and character scale (a life
inside those events). This is the narrative counterpart of P2's scope ladder — and
the test that decides how deep characters need to be.
