# Session Handoff — 2026-07-09 (Slice E: Fleets — merged)

State: `main`, pushed (carries slices A+C+B+D+E). Tests 316/316 green —
hex-tier suite untouched at 100%. ProjectSettings churn remains uncommitted
as always.

## What this session did: Slice E of the epoch-sim rebuild, merged

Everything abstract became physical: trade flows need hulls, founding takes
a convoy. Ledger (the full task/decision/surprise record):
`docs/superpowers/plans/2026-07-09-slice-e-ledger.md`.

- **Designs & lineages** (`ShipCatalog.cs` data-as-code chassis grid,
  `DesignMath.cs`, `ShipDesign.cs`): per-polity designs on the role × size
  grid; ~18-stat sheets derive on demand from embodiment/doctrine/tech/
  grade (never stored); marks inherit names, chronicle as
  ShipClassLaunched (400 — the military event block opened), and only
  advance when the yard can afford a hull of them.
- **Fleet object** (`FleetRecord.cs`): composition (design-id-sorted hull
  groups with blended grades), six postures, home port, readiness, vacant
  commander slot (G fills it). Vectors (`FleetMath.cs`) compute on demand:
  additive mass, formation minima for stealth/endurance.
- **Yards** (`FleetOps.BuildFleets`, Allocation): Budget.Military accrues
  to a real `MilitaryPoints` treasury; yards convert market Ship
  Components (+Armaments for warships) into hulls by D'Hondt over the
  standing ShipbuildingPriorities (lineage-keyed — survives mark
  advances); homeworlds start with a tier-1 shipyard + starter fleet
  (genesis furniture: a spacefaring species arrived by ship).
- **Postures** (`FleetOps.ManagePostures`): freight hulls rebalance across
  owned lanes as Posted fleets; escorts patrol capitals; blockade posture
  severs its port's lanes (lanecut debug hook remains). **Posted capacity
  replaced the LaneMath.Capacity stub** in Arbitrage + re-export: a lane
  without hulls moves nothing; import parity requires a *served* lane, so
  naval shortage spikes the price map like a blockade. Traffic frequency
  (`TrafficPerYear`) is the slice-I news-speed data; `emap traffic` shows it.
- **Supply** (`FleetOps.SupplyFleets`): fleets draw fuel + armaments
  (warships) / ship-components spares (civilian — deliberately not
  machinery) from home market **then polity stockpile** (the design's
  quartermaster fallback; genesis controller banks parts + fuel per
  port); need-weighted met drives readiness; below the floor, hulls wreck
  into `WreckageRecord`s at real hexes (401). Hull ledger conserves:
  built == active + wrecked + scrapped, across seeds.
- **Colony convoys**: founding requires a reserve colony hull; it stages
  from the nearest own port, the off-lane leg gates on endurance
  (`Fleet.EnduranceHexesPerPoint`), chronicles ConvoyDispatched (402), and
  the hull scraps into the colony. Expansion is now industrially gated
  (~half D's founding pace — accepted at eyeball as the design's point).
- **Freight actually flows now**: it was structurally dead on main (0
  arbitrage shipments per whole history — exporter clamped to
  deficit-financed treasuries). Merchants trade on working capital (D's
  own RunRecipe convention). Radius-12 famine events 737 → ~160.
- **Calibration** (the parked D question, answered): catalog
  machinery-upkeep coefficients **halved** (old rates starved Ship
  Components production → hulls → expansion); fleet upkeep magnitude
  0.025/point-year after eyeball wave 1. All dials: `Fleet` family
  (17 knobs) + 2 Controller knobs in KnobRegistry + TUNING.md.
- **Artifact**: fleets layer 1→2 (NAVY/DESIGN/FLEET/WRECK records,
  id==index + hull-map validation); golden frozen at slice end;
  load-then-continue byte-identity green over full fleet state.
- **Events**: military 400–402 live; next military free: **403**; economic
  next free: **207**. `RollChannel` next free: **41** (fleets stayed
  roll-free).
- **REPL**: `fleet [id]` (composition/vectors/supply) · `designs [actor]` ·
  `emap traffic` · `fleetpost` (debug posture override) · Markets trace
  note reports shipment volume ("N shipments (M units)") — counts alone
  mislead once capacity is real.
- Eyeball-accepted 2026-07-09 ("okay for now") after one tuning wave;
  remaining balance items (0.4-readiness merchant cohorts where
  components production lags, attrition chronicle chatter, military
  treasury accumulation) are explicitly knob territory for later slices.

## Next up

1. **Slice F (Deep genesis)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-09-slice-f-kickoff-prompt.md`
   (complete: the transition surfaces in SkeletonBuilder, artifact layer
   plan, next-free values, and E's hard-won lessons are baked in).
2. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · REPL eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings stays
uncommitted; bash printf for REPL piping; parallel slices never share a
checkout — take a `git worktree` each; **every calibration constant goes in
KnobRegistry + TUNING.md** (never a bare const); every new `src/Core` file
gets a two-line `.meta` with a fresh guid. Older carried minors: see
`git show a1f5843~40:docs/HANDOFF.md`.
