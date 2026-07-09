# Systems Design Documentation

This tree is the **product of the design passes**: the epoch simulation's systems
design as it currently stands. It describes what the systems *are* — not how we
decided them, not task flow, not progress records. Process lives elsewhere:

- `docs/superpowers/specs/` — dated design-decision records produced by each
  brainstorm (rationale, alternatives, scope negotiations, inherited tickets).
- `docs/superpowers/plans/` — implementation plans.
- `docs/HANDOFF.md` — session state.

When a design pass completes, its results are written **into this tree** as final
feature design; the spec that produced them remains behind as the decision record.
Documents here are living: later passes may deepen or revise earlier sections.

## Reading order

The frame first — it is the constitution every subsystem satisfies:

| Doc | Contents |
|---|---|
| [frame/principles.md](frame/principles.md) | The eight design principles (P1–P8) |
| [frame/actors.md](frame/actors.md) | Actor taxonomy: institutions, characters, populations, factions, assets |
| [frame/time.md](frame/time.md) | The four clocks: cosmic → evolutionary → generational → play |
| [frame/simulation-flow.md](frame/simulation-flow.md) | The seven phases of a simulation step |
| [frame/space-and-travel.md](frame/space-and-travel.md) | The two-plane space model: hexes, the natural raster, port domains, lanes |
| [frame/system-map.md](frame/system-map.md) | The five subsystem levels and cross-cutting interfaces |
| [frame/controller-contract.md](frame/controller-contract.md) | The canonical policies and acts per actor kind — the Intent-phase API |

Then the subsystems, in design-dependency order:

| Directory | Subsystem | Design pass |
|---|---|---|
| [genesis/](genesis/) | Cosmic structure formation; life, sapience, and precursors | 0a, 0b |
| [substrate/](substrate/) | Commodities, demand, recipes; infrastructure vocabulary | 1 |
| [economy/](economy/) | Markets and prices; corporations; trade and logistics | 2 |
| [fleets/](fleets/) | Ship classes, the fleet model, freight, commanders | 3 |
| [polity/](polity/) | Demographics, culture, factions, characters, government | 4 |
| [interpolity/](interpolity/) | Relations, diplomacy, federations, war | 5 |
| [narrative/](narrative/) | Perception and news, event grammar, chronicle, POIs, handoff | 6 |

Subsystem documents marked *awaiting design pass* contain only their scope; the
pass replaces the stub with the design.

## Conventions

- Present tense, final-design voice. No open questions, alternatives-considered, or
  scheduling — those belong in specs.
- Every mechanic documented here must state how it satisfies **P1** (its map/chronicle
  residue and its inhabitable state) — the two-customer test is part of the design,
  not commentary.
- Interfaces between levels are documented on the *providing* side; consumers link.
