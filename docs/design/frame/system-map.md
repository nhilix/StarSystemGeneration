# System Map — Five Levels and Cross-Cutting Interfaces

Runtime has cycles (economy reads war state, war reads prices); the *design*
dependency order is linear. Each level owns state, provides interfaces, and reads
others' interfaces — never their internals.

| Level | Owns (state) | Provides (interface) | Reads |
|---|---|---|---|
| **L0 Substrate** | Commodity vocabulary (goods, use-cases, production recipes), infrastructure vocabulary & siting rules, per-cell production potentials, population stores (species/culture-tagged), connectivity graph, market geography | `Potential(cell, good)`, habitability per embodiment, demand profiles, buildable-infrastructure catalog | Deep-genesis outputs (board + emergence schedule) |
| **L1 Economy** | Markets (price state), trade flows, wealth ledgers, corporate registry, infrastructure ownership & condition, tariffs/sanctions state | **Prices**, tax income, corporate revenue, route throughput, freight capacity, blockade strain, asset effects | L0 potentials & demand; L3 constraints (wars, blockades, borders); the fleet model; policies from Intent |
| **L2 Polity interior** | Factions, ideology state, government form, characters & roles, succession, cohesion, demographics (growth, migration) | Temperament composition (feeds Intent AI), stability/schism risk, **graduations**, leadership personality | L1 (standard of living, faction wealth); L3 (war outcomes → faction anger); L4 (news → opinion) |
| **L3 Inter-polity** | Relations matrix, wars, treaties/federations/vassalage, military fleet postures, fronts, battles | Constraint surfaces for L1 (blockades, borders, sanctions), war outcomes for L2, contact events | L4 perception (stances); L1 prices (war-goal value); L2 composition (militancy, faction pressure); the fleet model |
| **L4 Narrative** | Event log, news pulses, per-actor perception states, reputation, chronicle/era views, POI compiler, world-state handoff | Perceived state (Phase 1), chronicle queries, POIs to the hex tier, the handoff | Everything — via events only |

The **fleet model** (ship classes, production, movement, capacity) is a shared
component consumed by both L1 (freight) and L3 (war) — deliberately owned by
neither. See [../fleets/](../fleets/).

## Cross-cutting interfaces

Defined by the frame because every subsystem builds against them:

1. **Controller** (P2): `Decide(perceivedState) → (policies, acts)`. The frame
   defines the shape; each subsystem designs the AI implementation for its actor
   kinds.
2. **The price signal as the universal value language.** Expansion attractiveness,
   war-goal selection, migration pull, corporate investment, and infrastructure
   siting all read the same market-price-derived valuations. One number system, and
   an emergent one.
3. **Event grammar** (P4): every subsystem emits events in one schema — actor ids,
   location, world-year, magnitude, typed payload. L4 owns the grammar; emitting
   well-formed history is a requirement on every mechanic.
4. **Pressure → graduation**: L2's faction machinery is the sole factory for new
   institutions; L3 consumes schisms (successor polities), L1 consumes charters
   (corporations). The emergence schedule is the one non-faction institutional
   origin.

## Artifact discipline (P6)

Each level's state is an artifact section with its own schema version; the
simulation phases are the passes; the deep-genesis clocks are upstream artifact
layers. The hex tier remains a pure function — never persisted.
