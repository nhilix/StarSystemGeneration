# Narrative & Handoff (Design Pass 6)

Status: **draft — awaiting user review**
Date: 2026-07-09
Parent: `2026-07-09-epoch-sim-master-frame-design.md` (pass 6 — the final pass).
Builds on all prior passes. Product docs:
`docs/design/narrative/perception-and-news.md`, `chronicle-and-poi.md`,
`handoff.md`.

## 1. Overview

The L4 layer: the formalized event grammar with visibility semantics, news pulses
at traffic-derived speed, compressed per-actor perception, observer-filtered
stances with derived (never global) reputation, chronicle views with recomputable
era detection, the **incremental** event→POI compiler whose POIs are live sim
objects, and the world-state handoff with its resumability and controller-handover
contracts. Completes the eight-pass design phase.

## 2. Decisions

- **Compressed beliefs** (over full belief mirrors and global-truth-with-delays):
  per actor — stance table, per-region freshness map (traffic-refreshed), cached
  headline facts. Intent reads truth through the freshness filter; stale regions
  serve old values. Cheap, deterministic, honestly renderable at play clock.
- **Event grammar with visibility**: one schema across all four clock strata
  (id, world-year, stratum, type, actors, location, magnitude, valence,
  visibility, payload); eight type families; public/regional/secret visibility
  (secret = no pulse — smuggling runs, quiet deals, future espionage substrate).
  Indexes (place/actor/war/character) are views; the character index is the
  biography.
- **Reputation is derived, never stored globally**: per-observer stances updated
  on news arrival through the observer's temperament composition; per-audience
  aggregates consumed as gates. The same actor is monster and hero to different
  audiences.
- **Era detection as annotation**: epochs clustered by dominant event signature,
  named from participant cultures; recomputable layer, never sim state.
- **Incremental POI compilation with live effects** (user refinement, adopted —
  supersedes finalization-batch compilation): the compiler runs in the Chronicle
  phase every epoch; POIs exist from the moment of creation and influence the
  ongoing sim with effects mirroring their play-clock meaning — wreckage fields
  are salvage niches (salvage corporations found via the ordinary charter rule;
  fields deplete as recycled), ruins modulate lawlessness, lost capitals are
  cultural claim anchors feeding irredentist tension, memorial sites anchor
  stances and culture. Finalization compiles nothing — it builds indexes and the
  handoff; the map is always current at every epoch.
- **Handoff contract**: complete registries + deliberately open threads (untidied
  final epoch); resumability by construction (world-year rates everywhere — no
  genesis-only mechanics); controller handover as slot occupancy (P2's final
  test); **the log never closes** (the live game appends to the same stream;
  player deeds propagate as news and can mint the player as a notable); delta
  boundary = artifact + deltas + log continuation.

## 3. Testing Strategy

- **Invariants**: news never arrives before it happens; freshness monotone in
  traffic; secret events emit no pulses; every POI's source events exist and its
  effects cease when depleted; era layer recomputes identically; biography
  queries reference only real events; handoff round-trips (serialize → load →
  identical step results).
- **Unit tests**: pulse propagation/attenuation on constructed graphs; stance
  updates per temperament exemplars; audience-aggregate reputation gates; era
  clustering on constructed logs; salvage-niche founding on a constructed
  battlefield; claim-anchor tension feed.
- **Acceptance bands** (reference config): news lag core-vs-rim; stance
  divergence across audiences; POI counts by type; era count and lengths over 40
  epochs.
- **Goldens**: reference chronicle summary, era list, POI registry.

## 4. Frame-Consistency Check (master frame §9)

Additions only; this pass largely *cashes* frame promises: Perception phase
content (compressed beliefs), Chronicle phase content (grammar + incremental
compiler), traffic-derived news (pass 3's mechanism consumed), P2/P7 final tests
stated as contracts. One frame-doc touch-up on merge: the frame's POI-compiler
mentions ("at artifact finalization") update to incremental compilation.

## 5. Deferred / Follow-Up (owners)

- Espionage on the secret-visibility substrate; misinformation/distortion
  modeling (future depth, noted since the parent spec).
- Era/biography prose rendering quality (inspector/atlas surface work).
- Player-as-notable minting thresholds (game layer).

## 6. Amendments to Prior Docs

- `docs/design/chronicle-and-poi` supersedes the frame docs' and 0a/0b specs'
  "at artifact finalization" compiler phrasing: compilation is incremental;
  finalization builds indexes + handoff only. (Deep-genesis strata already
  compiled within their own clocks — unchanged.)
- Flow diagram: pass 6 pill → specced; stamp → design phase complete.
- **This completes the eight-pass roadmap**: implementation re-planning across
  the whole design is the next phase (master frame §7 sequencing note).
