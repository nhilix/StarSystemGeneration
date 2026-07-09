# Inter-Polity Dynamics (Design Pass 5)

Status: **draft — awaiting user review**
Date: 2026-07-09
Parent: `2026-07-09-epoch-sim-master-frame-design.md` (pass 5). Builds on passes
1–4 and the space-and-travel amendment. Product docs:
`docs/design/interpolity/relations.md`, `war.md`.

## 1. Overview

Contact and stances, per-pair warmth/tension with a treaty ladder (trade pact →
non-aggression → defense alliance → federation/vassalage), dynastic instruments,
and the war redesign: casus belli across four cause categories with a
tension-driven spark mechanism, theater/objective conduct consuming the fleet
vectors, sieges with reserve-drain duration, politics-driven weariness and
termination, negotiated settlements from per-objective outcomes, and aftermath as
first-class residue. Resolves the inherited tickets: non-deficit war causes and
war-goal variety.

## 2. Decisions

- **Theater/objective war model** (over abstract war-score and hex-level
  operational): wars decompose into objectives (ports, lanes, domains, fleets);
  fleets post to objectives; per-objective engagement resolution from fleet
  vectors × fortification × supply × commander × seeded rolls. Spatially real,
  battle-at-hex POI-ready, uses everything passes 1–3 built.
- **Two continuous relations quantities + discrete treaties**: warmth
  (interdependence — trade volume, dynastic ties, honored treaties) and tension
  (friction — overlap zones, claims, strain, agitation, ideology gap × zeal);
  treaties gate on warmth with mutual consent; breaking a rung is a reputation
  event.
- **Federation as new-polity merge** (gates: sustained alliance, warmth, ideology
  compatibility, openness, cohesion); treaty federations start legitimacy-high vs
  conquest empires.
- **Vassalage with a protection market**: imposed by settlement or chosen under
  threat; tribute, defense obligation, policy lock; absorption and secession
  exits.
- **Dynastic instruments**: marriages/wardships buy warmth and create succession
  claims (future tension); rare personal unions as federation fast-path with
  built-in crisis.
- **Casus belli categories** (economic / ideological / political / spark) with
  the spark mechanism: incidents roll in high-friction space, escalating only
  when tension is loaded and a casus belli exists. Non-deficit war causes land:
  crusades, liberations, successions, faction discharges.
- **Weariness is the interior responding**: losses and SoL decline shift
  ideology, feed peace factions, erode legitimacy — a polity breaks when its
  politics break. Replaces the counter-based weariness model.
- **Negotiated settlements** priced from per-objective holdings: cessions,
  reparations, vassalization, imposed legality, white peace; acceptance on
  *perceived* continuation cost (P3 — stale perception lets wars overrun their
  rational end).
- **Aftermath as residue**: standing claims, veteran factions, war heroes,
  wreckage/ruin POIs, debt overhang, conduct reputation feeding pass 6.

## 3. Testing Strategy

- **Invariants**: treaty state machine legal (no alliance without contact, no
  federation without alliance); tension/warmth bounded and source-traceable;
  every war has a casus belli and objective set; objective outcomes conserve
  hulls/facilities/segments; settlements only transfer held or conceded
  objectives; extinct belligerents fight no fronts; every war terminates or is
  live at final epoch.
- **Unit tests**: spark escalation odds vs tension; each casus belli category
  constructible; engagement vector math (swarm/screen/tracking cases);
  siege duration vs reserves/fortress; settlement acceptance under perception
  gaps; vassal absorption and secession paths; federation composition math.
- **Acceptance bands** (reference config): wars per 40 epochs by cause category
  (non-economic wars occur); treaty distribution; federation and vassalage
  counts; white-peace vs decisive ratios; mean war length.
- **Goldens**: reference relations/war summary snapshot.

## 4. Frame-Consistency Check (master frame §9)

Additions only. Diplomacy matching sits in Resolution as the frame specified;
war declaration/settlement demands are Intent acts; weariness routes through the
pass-4 interior rather than adding state; conduct consumes pass-3 vectors and
pass-2 reserves/strain unchanged; conquest composition uses pass-4 segments. No
phase, taxonomy, or interface reshape.

## 5. Deferred / Follow-Up (owners)

- News/stance repricing of conduct reputation, atrocity magnitudes, era naming of
  great wars (pass 6).
- Ground/troop invasion depth beyond siege-capture (future: only if the war model
  proves to need it; berths exist on the sheets).
- Espionage/intelligence operations (deferred with pass-4 intrigue).
- Peacekeeping/multilateral wars (coalitions beyond defensive alliances) — the
  treaty machinery supports extension; not designed now.

## 6. Amendments to Prior Docs

- Prototype war model (goal cells, front contests, counter weariness,
  aBroke/dBroke) superseded on implementation.
- Flow diagram: pass 5 pill → specced; stamp bump.
