# Ships & Fleets (Design Pass 3)

Status: **draft — awaiting user review**
Date: 2026-07-09
Parent: `2026-07-09-epoch-sim-master-frame-design.md` (pass 3). Builds on passes 1–2
and the space-and-travel amendment. Product doc:
`docs/design/fleets/ships-and-fleets.md`.

## 1. Overview

The fleet model — the shared component the economy (freight, piracy risk) and war
(combat vectors) both consume. Ship designs on a role × size chassis grid with
per-polity species/culture/tech-flavored instantiation and lineage drift; a
two-layer stat model (rich design sheets, cheap epoch aggregation vectors); yard
production conserving hulls end-to-end; fleet objects with a six-posture vocabulary
including posted-capacity (cashing pass 2's freight stub); supply-ranged movement;
wreckage residue; traffic-emergent news speed (cashing the P3 promise); and
commander roles.

## 2. Decisions

- **Fleets + posted capacity** (over every-voyage simulation and military-only
  objects): all hulls live in conserved fleet objects, but only maneuvering fleets
  (war, convoys, expeditions) move per epoch step; routine shipping is a fleet
  *posted* to a route providing capacity. Play clock resolves posted fleets into
  actual voyages. Concrete hulls, cheap epochs.
- **Two-layer stat model** (user pushback incorporated — six stats too simple for
  role/quality distinctness): ~15-stat design sheets in four blocks
  (Combat: strike/sustained/tracking/armor/screens/PD; Mobility:
  speed/maneuver/endurance/efficiency; Capacity: cargo/hangar/berths; Operations:
  sensors/signature/crew/automation/upkeep), with grade and tech acting per-stat
  and precursor hulls above ceilings. Epoch resolution consumes aggregated fleet
  vectors only — rock-paper-scissors combat texture (swarm/capital/screen,
  scout-enabled ambush) at aggregate cost. One source of truth, two samplings (P7).
- **Chassis grid + per-polity design lineages**: universal role × size grid;
  designs instantiated from embodiment, culture/doctrine, tech, grade; named marks
  drifting over epochs. Refit variants as sub-designs (Q-ships, smuggler builds) —
  doubling as the play-clock outfitting system.
- **Six-posture vocabulary**: Posted / Escort / Patrol / Blockade /
  Expedition-Convoy / Reserve — the complete answer to "where are the ships" for
  every mechanic that needs one (freight, piracy, customs enforcement, blockade
  strain, colonization, mobilization).
- **Supply-ranged operations**: fleets draw fuel/upkeep from home ports; unsupplied
  fleets decay; supply convoys are ordinary fleets and raiding them ordinary
  posture play.
- **Wreckage conservation**: losses become salvage/battlefield residue at the hex
  of death (P4 → P1).
- **Traffic-emergent news speed**: per-lane news velocity from posted-traffic
  frequency; couriers/scouts as deliberate info assets; the news-speed knob
  retired as promised by the frame.
- **Commander roles defined here**, drama consumed by passes 4–5.

## 3. Testing Strategy

- **Invariants**: hull conservation (built = active + wrecked + scrapped);
  posted capacity = Σ cargo × availability; endurance floor respected on off-lane
  legs; unsupplied decay monotone; wreckage sites exist where losses occurred;
  vectors recompute deterministically from composition.
- **Unit tests**: sheet derivation per embodiment/tech/grade exemplars; per-stat
  grade emphasis; vector aggregation; posture transitions; piracy-risk pricing
  with/without escort; news-speed derivation from traffic.
- **Acceptance bands** (reference config): fleet counts and hull populations in
  range; freight capacity roughly matching trade volume; wreckage accumulation
  plausible; distinct design lineages per species verifiable in output.
- **Goldens**: reference design-sheet set + fleet census snapshot.

## 4. Frame-Consistency Check (master frame §9)

Additions only. The fleet model fills the shared-component slot the system map
reserved (owned by neither L1 nor L3); the pass-2 freight-capacity stub is
satisfied with the declared shape; Perception's traffic-derived speed lands where
the frame promised; commander roles use the existing role bridge. No phase,
taxonomy, or interface reshape.

## 5. Deferred / Follow-Up (owners)

- Battle resolution consuming the vectors; doctrine; mutiny/defection (pass 5;
  character drama pass 4).
- Ground/troop actions via berths (pass 5 decides scope).
- Play-clock outfitting UI and per-voyage resolution of posted fleets (game layer).
- Carrier strike-craft cycling detail; boarding; fighter design depth — only if
  pass 5's battle model demands it.

## 6. Amendments to Prior Docs

- Prototype `MilitaryStockpile` scalar formally superseded by fleet composition on
  implementation.
- Flow diagram: pass 3 pill → specced; stamp bump.
