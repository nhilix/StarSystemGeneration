# Design Tightening Wave — Post-Pass Whole-Design Review

Status: **draft — awaiting user review**
Date: 2026-07-09
Parent: the eight design passes (specs of 2026-07-09). This spec records the full
top-to-bottom cohesion review run after all passes completed, and the tightening
wave applied from its findings.

## 1. Review verdict

Structurally sound: every major cross-pass loop closes (geography → grade →
manufacturing → fleets → war → ruins → salvage → corporations; dividends →
factions → temperament → war causes; strain → weariness → politics → settlement;
traffic → news → stances → diplomacy). The frame's acceptance criterion held —
eight passes, zero reshapes, all amendments additive. Findings below were gaps of
*ownership* (leaned on by many passes, defined by none), weak definitions, and
two near-free simulation vectors.

## 2. Gaps filled (new documents)

- **Technology** (`docs/design/economy/technology.md`): four domains (Industrial,
  Military, Astrogation, Life) with tier ladders; advancement = Allocation-phase
  research policy consuming Refined Exotics × Compute; **diffusion** via trade
  contact (capped one tier below source), salvage/capture (battlefields and
  precursor digs are tech events), and a reserved espionage channel. Fixes the
  runaway-leader default and makes asymmetric emergence survivable. Interface:
  `Ceiling(polity, domain)` / `Region(polity, domain)`.
- **Controller contract** (`docs/design/frame/controller-contract.md`): the
  canonical policies/acts enumeration per actor kind — the Intent-phase API and
  the player UI surface. Contract rules: extensions allowed, no decision points
  outside Intent, perceived-state-only inputs, AI/player symmetry.
- **Plagues** (`docs/design/polity/plagues.md`): contagion on the lane graph
  (news machinery, darker payload), origins incl. excavation releases, Medicine
  effective-units mitigation, quarantine as self-imposed lane closure with
  smuggler leakage, ideology/faction aftermath, memorial residue. Nearly free on
  existing machinery.

## 3. Weak definitions fixed (edits)

- **Household income** (`markets.md`, `population-and-identity.md`): labor share
  of facility revenue → segment income → purchasing power → SoL; automation
  shifts income owner-ward; migration's opportunity term grounded. Closes the
  production–wages–consumption–migration loop.
- **Segment residency** (`population-and-identity.md`): segments are
  domain-level sim state; hex population is a projection; wilds settlements are
  sparse hex-anchored records.
- **Natives & late emergence** (`relations.md` + `life-and-precursors.md`):
  native policy act (protectorate/integrate/exploit/uplift); emergence in claimed
  space resolves through it (client vassal, autonomous member + faction, or
  suppressed emergence = standing liberation casus belli); starting conditions at
  emergence incl. the late-emerger contact bonus.
- **Outlaw institutions** (`corporations.md`): pirate bands founded by the
  persistent-niche rule where the niche is raiding; based at ruin/haven POIs;
  die with their niche.
- **Allied settlement** (`war.md`): supporters settle through war leaders.
- **Freight vs P3** (`markets.md`): stated rationale — freight is distributed
  local traders on inherently-fresh lane-endpoint information.
- **Colonization stitched** (`space-and-travel.md`): the end-to-end chain in one
  place.

## 4. Consistency fixes

`time.md` markets row (epoch-stepped, never fully clearing); `system-map.md`
price interface states grade-effective units; README indexes the controller
contract. POI-compiler timing sweep confirmed clean (incremental everywhere).

## 5. Recorded and deferred

- **Survey/exploration knowledge** (terrain as perception-gated, tradeable
  data): valuable, touches every valuation read — deferred as future depth.
- **Current-era terraforming**: possible on 0b's biosphere-engineering
  machinery; nothing demands it yet.
- **Espionage**: channel slots reserved (tech diffusion, secret events);
  designed with the intrigue substrate when it comes.
- **Ground/troop depth, multilateral coalitions, monetary depth, order-matching
  at play clock**: carried from pass specs, unchanged.

## 6. Status

With this wave the design tree is implementation-ready. Next: implementation
re-planning across the whole design (master frame §7) — early structural items:
world-year rate conversion, raster/registry state inversion, deep-genesis clocks.
