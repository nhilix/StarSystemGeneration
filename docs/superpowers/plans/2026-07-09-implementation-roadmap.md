# Epoch-Sim Implementation Roadmap

> **This is the governing meta-plan.** It defines the slice decomposition, order,
> and gates for implementing the complete epoch-sim design
> (`docs/design/`, specs of 2026-07-09). Each slice gets its own full task-level
> plan (superpowers:writing-plans format) before execution. This document is not
> itself executable.

**Goal:** Replace the stage-1–3 prototype with the full designed simulation,
greenfield, slice by slice — each slice ending with a running, inspectable sim.

**Approach:** Ground-up rebuild. The prototype is reference-only.

## Transition rules

1. **Greenfield, no adapters.** The prototype sim (`EpochSim`, `Sim/*` phases,
   per-cell political state, sim serializer sections) and the Unity atlas are
   exploratory PoC: reference-only. New code replaces them outright; superseded
   code is **deleted** as its replacement lands. Git history is the reference
   archive; the design docs already carry the lessons. No compatibility
   adapters, no interim shims, no old-golden preservation.
2. **The hex tier is product, not prototype.** Phase-1 system/body generation,
   hex geometry, `StableHash`/`RollChannel`/`ValueNoise` are the design's Tier 3
   and shared substrate — their suite stays green throughout. (Tier-1 density and
   the seeding passes survive until Slice F replaces them causally.)
3. **A fresh test suite grows with each slice**: determinism byte-identity,
   invariants, shape-acceptance bands, and new goldens per slice — testing the
   new sim, never the old one.
4. **Every slice ships its REPL surface** (the map-legibility gate, P1): each
   slice ends with a sim you can run, inspect, and eyeball in the inspector.
5. **Unity atlas rebuild is a separate late batch** (post-Slice D at the
   earliest, ideally post-H) — one rebuild against the settled data model
   instead of parity churn per slice.
6. **Artifact format is new** (design §P6 layer sections, versioned per layer);
   the v5 serializer is superseded in Slice B. Determinism discipline unchanged:
   stateless hash rolls, fixed iteration order, artifact-stamped config.

## The slices

| # | Slice | Contents | Depends on |
|---|---|---|---|
| **A** | **Foundations** | World-year rates + `GalaxyConfig` reshape (design knobs) · event grammar v2 (world-year, clock stratum, visibility, type families) · seven-phase `EpochSim` skeleton (phases run, mostly empty) · controller interface types (policies/acts records per `frame/controller-contract.md`) | — |
| **B** | **Two-plane state** | Sparse registries (ports, facilities; fleet/segment records as structs) over slimmed natural-raster `RegionCell` · expansion = port establishment (colonization chain) · territory/domains derived from the port registry · lanes · prototype sim code deleted · new artifact format | A |
| **C** | **Substrate catalogs** | 17 goods + Grade + recipes/variants · demand profiles · legality schema · 14-type infrastructure catalog + siting + production formula · potentials from raster fields — pure Core data & functions, unit-tested standalone | A (parallel with B) |
| **D** | **Segments & markets** | Population segments (two identity layers, demographics, migration basics) · market-per-port state · price engine incl. re-export demand · freight: arbitrage / contracts / internal logistics (fleet-capacity stub) · household income · stockpiles · simple credit | B + C |
| **E** | **Fleets** | Design sheets/lineages · aggregation vectors · yard production chains · six postures (posted capacity replaces the stub) · movement/supply/endurance floors · wreckage residue · traffic-derived news-speed data | D |
| **F** | **Deep genesis** | Cosmic sim (0a: potential prior, field stack, features) · life & precursors (0b: biosphere loop, emergence schedule, precursor arc sim, archaeology) · derived lean/metallicity/biospheres replace seeding passes 2–4 · staggered polity entry | A (separable; slot after E to build against the settled state model) |
| **G** | **Interior & corporations** | Factions/legitimacy/government forms/graduation · characters/roles/succession/dynasties/notables · tech domains + diffusion · temperament composition · corporations + outlaw institutions (niches from D, charters from graduation) | D (+E for commanders) |
| **H** | **Relations & war** | Contact · warmth/tension · treaty ladder/federation/vassalage/dynastic instruments · casus belli + spark mechanism · theater/objective war on fleet vectors · sieges on reserves · settlements · native policy & emergence crises | E + F + G |
| **I** | **Narrative** | Compressed-belief perception (replaces perfect-info stubs) · news pulses/stances/reputation · chronicle views + era detection · incremental POI compiler + salvage niches · plagues | H |
| **J** | **Handoff & certification** | World-state handoff layer · resumability tests (fine-tick stepping of the same machine) · delta boundary · full-design acceptance pass · docs/diagram final sync | I |
| **K** | **Unity atlas rebuild** | New atlas against the settled model: domain/lane/price/war/faction layers, panels, drill-down | H+ (batched) |

## Sequencing rationale

- **C parallel to B**: catalogs are pure data/functions with no sim contact.
- **F mid-sequence, not first**: the epoch sim consumes genesis only through the
  board-interface; the existing seeding output is the designed degenerate
  version, so nothing blocks on genesis — and building it after B avoids
  implementing against a data model that's about to invert. (User-confirmed
  order 2026-07-09.)
- **Perception stays perfect-info until I**: decisions see fresh truth until
  then — honest, and keeps L4 out of every earlier slice.
- **Between B and D the sim is expansion-only** (a settlement-history sim) —
  a legitimate intermediate product, mirroring how slice 1 of the prototype era
  was expansion-only.

## Slice gates (every slice)

- `dotnet test` green: hex-tier suite + all new-sim suites to date.
- Determinism: byte-identical artifact for same config; load-vs-rebuild
  equivalence.
- New goldens frozen at slice end (red-window within the slice).
- REPL eyeball acceptance recorded (what to look at, what "looks right" means —
  defined in each slice's plan).
- Design-doc conformance: the slice's mechanics match its `docs/design/`
  section; deviations require a design-doc amendment in the same branch.

## Process

Per slice: task-level plan (writing-plans) → subagent-driven execution →
final whole-branch review + fix wave → finishing-a-development-branch → merge.
The established dispatch conventions (verbatim test-summary lines, gate
language, file lists complete per task) carry over unchanged.
