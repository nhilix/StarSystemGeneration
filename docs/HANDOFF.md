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

1. **Slice A (Foundations)** — start a fresh session and point it at
   `docs/superpowers/plans/2026-07-09-slice-a-kickoff-prompt.md`; it contains the
   full reading list, scope, rules, and definition of done. The governing
   meta-plan is `docs/superpowers/plans/2026-07-09-implementation-roadmap.md`
   (11 greenfield slices A–K; prototype sim + atlas are reference-only PoC,
   hex-tier pipeline stays green).
2. **User review of the design specs** (approved conversationally
   section-by-section; a read-through of the files themselves hasn't happened —
   can proceed in parallel with Slice A).
3. Unity atlas: superseded as PoC — rebuild is roadmap Slice K (batched late,
   post-H). The old parity/polish ticket list is retired with it.

## Carried process conventions (unchanged)

Superpowers flow (brainstorm → spec → user review → plan → subagent execution →
final review → merge); implementer reports include verbatim test-summary lines;
golden re-freeze discipline; ProjectSettings stays uncommitted; PowerShell REPL
BOM gotcha (use bash printf); HANDOFF.md is uppercase in git.

Older carried minors (REPL/atlas/orbit-diagram tickets, perf parkings): see
`git show a1f5843~40:docs/HANDOFF.md` and the ledger — none were touched this
session; the design supersedes several of them structurally (noted in the specs'
"amendments" sections).
