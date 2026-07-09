# Session Handoff — 2026-07-09 (design-phase session)

State: `main`, pushed to origin. Core tests unchanged (175/175, prototype code
untouched — this was a documentation/design session). ProjectSettings churn remains
uncommitted as always.

## What this session did: the epoch-sim design phase, complete

Stepped back from implementation and ran the full top-down design of the epoch
simulation. **All eight design passes are specced and written into the design
tree.** No code changed; stages 1–3 code is reclassified as the running prototype,
to be replaced subsystem-by-subsystem when implementation resumes.

**The documentation planes (new convention, follow it):**
- `docs/design/` — the **living product**: final feature designs, present tense,
  no process. Frame docs (principles P1–P8, actors, four clocks, seven phases,
  space-and-travel, system map) + one directory per subsystem. Conventions in
  `docs/design/README.md`.
- `docs/superpowers/specs/2026-07-09-*.md` — the dated decision records (9 specs
  this session): master frame, space-and-travel amendment, cosmic genesis (0a),
  life & precursors (0b), substrate & commodities (1), markets/wealth/corporations
  (2), ships & fleets (3), polity interior (4), inter-polity (5), narrative &
  handoff (6).
- Living flow diagram rebuilt around the new frame and current:
  https://claude.ai/code/artifact/67f20b6b-4e8c-4941-b88b-fc071c1c64f4
  (source `docs/diagrams/generation-flow.html`; republish per memory note).

**Load-bearing frame decisions** (details in the master frame spec + design tree):
four clocks (cosmic → evolutionary → generational → play); seven phases per step
with ONE controller touchpoint (Intent: policies + acts — AI/player interchangeable
at every scope, P2); two-plane space (hexes physical, cell lattice = natural raster
only, political geography = emergent variable port domains; territory derived from
the port registry, never stored); lanes economical-not-exclusive with off-lane
ship-class endurance; 17-good commodity vocabulary with the Grade system; local
price adjustment + freight arbitrage; simple credit; emergent corporations via
persistent-profit-niche charters; two-layer identity (culture/ideology) on
population segments; graduation as the institutional origin mechanism; temperament
composition (species × ideology × ruler × factions, weighted by government form);
theater/objective war with politics-driven termination; compressed-belief
perception; **incremental POI compiler — POIs are live sim objects from the epoch
they form** (salvage niches, claim anchors).

## Next up

1. **User review of the specs** (all marked "awaiting user review" — approved
   conversationally section-by-section during the session, but a read-through of
   the spec files themselves hasn't happened).
2. **Implementation re-planning across the whole design** (master frame §7:
   design-first, then re-slice). The old stage-4/5/6 numbering is retired; slices
   should be planned against the design tree. Big early items: world-year rate
   conversion, the raster/registry state-model inversion (per-cell political
   state → sparse port/facility/fleet registries), deep-genesis clocks.
3. Unity atlas economy parity + polish items (carried from previous sessions,
   unchanged — see git history for the older handoff ledger).

## Carried process conventions (unchanged)

Superpowers flow (brainstorm → spec → user review → plan → subagent execution →
final review → merge); implementer reports include verbatim test-summary lines;
golden re-freeze discipline; ProjectSettings stays uncommitted; PowerShell REPL
BOM gotcha (use bash printf); HANDOFF.md is uppercase in git.

Older carried minors (REPL/atlas/orbit-diagram tickets, perf parkings): see
`git show a1f5843~40:docs/HANDOFF.md` and the ledger — none were touched this
session; the design supersedes several of them structurally (noted in the specs'
"amendments" sections).
