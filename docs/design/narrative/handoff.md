# World-State Handoff

*Status: awaiting design pass (6). This stub states scope only.*

The bridge from genesis to play: the final generational state as the live world's
initial conditions.

Defines, when designed:

- The handoff contents: registries (polities, corporations, characters, fleets,
  infrastructure), relations, stances, wealth, markets, per-cell ownership,
  population, and the open threads (live wars, pending successions).
- The four-clock integration contract: how the generational state machine resumes at
  play-tick resolution (P7) — same rules, finer sampling.
- Controller handover: how a player assumes any controller slot (character,
  corporation, polity) over the inherited state (P2).
- The delta layer boundary: what the live game mutates vs. what stays procedural.
