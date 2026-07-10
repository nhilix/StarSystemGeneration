# Slice E Ledger — Fleets (slice-e-fleets)

Ordered task checklist per the kickoff prompt
(`2026-07-09-slice-e-kickoff-prompt.md`). Updated and committed as tasks
complete — the resumability record. E gives the economy its hulls: design
sheets/lineages, aggregation vectors, yard production, the six postures
(posted capacity replaces D's `LaneMath.Capacity` freight stub at
`MarketEngine.cs` Arbitrage/AddReExportDemand), movement/supply/endurance,
wreckage, traffic data. Design sources: `fleets/ships-and-fleets.md`,
`frame/space-and-travel.md`, `economy/markets.md` §4.

Architecture decisions (made at kickoff, flag deviations):

- **Chassis catalog is data-as-code** (like Goods/Infrastructure): the
  role × size grid with per-cell stat bases lives in a static table
  (`ShipCatalog`), structural constants per the TUNING.md appendix
  convention. Calibration *dials* (hull costs, throughput, upkeep rates,
  readiness/attrition, posted-capacity factors) go in a new `FleetKnobs`
  family + KnobRegistry + TUNING.md.
- **Design records store derivation inputs, sheets compute on demand**:
  `ShipDesign(id, owner, role, size, mark, name, componentGrade, techTier,
  designedYear)`; the ~15-stat sheet is a pure function
  (`DesignMath.Sheet`) of those + species embodiment/temperament. Vectors
  (`FleetMath.Vectors`) likewise on demand, never stored (design doc layer 2).
- **Lineage drift**: a polity re-registers a design (mark+1, inherited
  name) when its capital market's ShipComponents mean grade exceeds the
  design's by `Fleet.MarkGradeStep`. Appended registry records — visible
  cultural history.
- **Fleet object deepens in place**: composition = hull groups
  (designId → count + mean grade, design-id order), posture enum (6),
  route/target context (laneId / portId / hex by posture), HomePortId,
  Readiness. CommanderId = −1 until G.
- **Yards build in Allocation**: military budget share accrues to a new
  `MilitaryPoints` treasury; polities with an active Shipyard convert
  market ShipComponents (+ Armaments for warships) into hulls per
  `ShipbuildingPriorities` (design-id keyed; genesis controller writes
  them from doctrine/militancy; PerceptionView gains own-design briefs).
  Purchases are conserved transfers into the market pool. Throughput =
  yard tier × `Fleet.YardHullsPerTierPerYear`.
- **Hull conservation counters** on PolityRecord (Built/Wrecked/Scrapped),
  serialized as fleets-layer `HULLS` records (avoids an actors-layer bump).
  Gate: built == active + wrecked + scrapped, per polity, always.
- **Posted capacity replaces the stub**: per-lane capacity = Σ posted
  fleets' (cargo × count × trips/year); trips/year =
  `Fleet.FreightTripsPerYearBase` × LaneMath.TransitSpeed / distance.
  Re-export pull scales by the same posted capacity (no hulls → no pull —
  design-conformant). Fleet management (Allocation) redistributes freight
  hulls across owned lanes by lane value, deterministic id order.
- **Bootstrap furniture**: homeworld entry seeds a starter fleet (freight
  hulls + 1 colony hull + escorts by militancy) beside the starter
  industry — same genesis-furniture convention, no events.
- **Colony convoys make founding physical**: FoundColonyAct requires a
  colony hull in reserve at a port within reach; Resolution dispatches a
  convoy fleet (Expedition posture, event 402), moves it (fuel drawn,
  endurance floor gates), founds the port, scraps the colony hull
  (conserved into the Scrapped counter), re-posts survivors as the
  colony's reserve.
- **Supply**: fleet upkeep (MilitaryUpkeep profile: armaments+fuel,
  machinery for automation-heavy) draws from the home-port market in
  Allocation, paid from polity credits; met fraction drives Readiness
  (condition-style drift); Readiness below the attrition floor wrecks
  hulls into `WreckageRecord`s at the fleet hex (event 401). Reserve
  posture draws `Fleet.ReserveUpkeepFactor`. Arbitrage freight fuel
  becomes a physical inventory draw (the documented D deferral).
- **Blockade posture** severs freight/parity/re-export over the target
  port's lanes (same mechanism as SeveredLanes, derived from fleet state);
  `lanecut` debug hook stays until H. REPL gains a debug posture command
  for the eyeball.
- **Traffic data**: `TrafficMath.Frequency(state, lane)` — derived from
  posted fleets, never stored; `emap traffic` renders it; Perception
  consumes it in I.
- **Events**: military block opens — 400 ShipClassLaunched,
  401 FleetAttrition, 402 ConvoyDispatched (payloads + serializer +
  SimTraceView). Economic 207 stays free. Rolls: economy stays roll-free;
  if naming needs one, channel 41 appends.
- **Artifact**: fleets layer 1→2 — DESIGN, FLEET (with hull map field),
  WRECK, HULLS records; id == index validation; golden regenerated
  deliberately per history-changing task (same-commit), frozen at slice
  end.

## Tasks

- [x] 0. **Branch + ledger** — branch `slice-e-fleets` from main; this file.
- [x] 1. **Catalog + design math + knobs** — `ShipCatalog` (chassis grid,
      stat bases), `DesignMath.Sheet` (embodiment/doctrine/tech/grade
      derivation), `FleetMath.Vectors`, `FleetKnobs` + registry + TUNING
      rows. Gate: unit tests over the pure functions; KnobRegistryTests.
- [x] 2. **Design registry + lineages** — `ShipDesign` records on SimState,
      genesis designs per polity at entry, mark drift, PerceptionView
      design briefs, event 400, artifact DESIGN records (fleets layer → 2).
      Gate: unit tests; artifact round-trip; golden regen.
- [x] 3. **Yard production + starter fleets** — MilitaryPoints accrual,
      yard hull building from market components (+armaments), fleet
      founding/joining, hull counters, homeworld starter fleet, NAVY/FLEET
      serialization. Gate: conservation test (credits + hulls); golden regen.
- [x] 4. **Postures + posted capacity** — posture/route state, Allocation
      fleet management (post freight to lanes, patrols by militancy,
      reserve default), `PostedCapacity` replaces `LaneMath.Capacity` in
      Arbitrage + AddReExportDemand, blockade severance, TrafficMath.
      Gate: no-hulls-no-freight test; blockade-spike parity with lanecut;
      shape still alive over 40 epochs.
- [x] 5. **Movement + supply + convoys** — fleet upkeep demand + draws,
      readiness, attrition → wreckage (event 401), physical freight fuel,
      endurance floors, colony convoy resolution (event 402) gating
      founding on hulls. Gate: unsupplied-fleet decay test; convoy
      founding test; hull conservation incl. wrecks/scraps.
- [x] 6. **Artifact v2 complete + load gates** (landed incrementally with
      tasks 2–5: DESIGN/FLEET/WRECK/NAVY, id==index, round-trips,
      LoadThenContinue green over full fleet state) — WRECK records, full
      fleets-layer v2, id==index validation, version refusal,
      LoadThenContinue byte-identity with fleet state. Gate: all artifact
      tests; golden regen.
- [x] 7. **REPL surface** — `fleet [id]` dump (composition, posture,
      vectors, supply), `designs [actor]`, `emap traffic`, debug posture
      command; help text. Gate: piped-stdin smoke via bash printf.
- [x] 8. **Shape acceptance + calibration** — 40-epoch runs across seeds:
      fleet counts bounded, freight capacity sane (posted routes carry it),
      colonies still found, credits conserve to the mint, hulls conserve;
      the parked machinery-upkeep calibration question addressed here
      (fleet upkeep lands on the same markets). Full `dotnet test` green,
      hex-tier untouched.
- [ ] 9. **Fresh-eyes whole-branch review** subagent + one fix wave.
- [ ] 10. **USER: REPL eyeball** — posted routes visibly carrying freight
      (shipment counts rise where fleets post; a lane without hulls moves
      nothing) and a colony convoy founding a port. Tune knobs as directed.
- [ ] 11. **Golden freeze + wrap-up** — golden frozen at final format ·
      USER merge decision · HANDOFF · **write Slice F kickoff prompt**
      (deep genesis — read the roadmap's sequencing rationale) · flip the
      kickoff checkbox · push only on user say-so.

## Notes / surprises

- **Naval bootstrap wall (task 3)**: with the military-construction pull
  alone, the first shipyard sited at y975 (epoch 39/40, radius 12) —
  components price signal, machinery banking, and 4 construction years
  stack too slowly. Resolution mirrors D's starter-industry precedent: the
  homeworld starter set gains a **tier-1 Shipyard** (a species that
  arrived at spaceflight arrived by ship). Hull chain complete from epoch
  one; 33–67 hulls per polity per history. **Flag at eyeball.**
- **AddMilitaryDemand** (new market sub-step): a funded polity registers
  Ship Components demand at its yard port (capital until a yard exists) —
  the MilitaryConstruction use-case D left unused. Without it the
  components price floors and yards never pencil out
  (`Fleet.MilitaryPullComponents`).
- **BuildFacilities behavior change (D code, flag at eyeball)**: the
  affordability check moved *into* candidate scoring — an unaffordable
  high scorer (the shipyard's 16 machinery) used to block its port from
  building anything at all; now the best *buildable* candidate wins.
  Radius-12 golden went from 6 facility builds per history to ~24, chains
  visibly more diverse. Latent D issue exposed by the components signal.
- **NAVY record** (fleets layer): MilitaryPoints + hull ledger live
  fleet-side so the actors layer stays v2.
- Military treasuries still accumulate faster than yards spend (60k by
  epoch 40) — task 5's fleet upkeep drains against it; recheck at task 8
  calibration.
- Mark drift hasn't fired in golden runs yet: recipe component grades
  hover below design grade + 0.15. Revisit at task 8 (or accept: lineages
  drift when tech/grade actually moves, which is honest).
- **Freight was structurally dead on main** (task 4 discovery): 0
  arbitrage shipments over entire 40-epoch histories at radius 12 AND 21 —
  pre-E. Two stacked causes: (1) every polity runs deficit-financed
  (credits −11k…−77k), and arbitrage clamped shipments to exporter
  treasury → always 0; (2) freight cost/hex over 8–29-hex lanes swamps
  raw-goods glut prices, and the parity cap sits just above break-even so
  the drift takes epochs to reach viability. Fix: **merchants trade on
  working capital** — same convention D gave producers in RunRecipe (the
  ledger dips within the step, payout lands at distribution, insolvency is
  Allocation's problem). After: shipments 0 → 130–160/epoch by late
  history; famine events 808 → 622 (seed 42, radius 12). D's economy
  "worked" through the parity price cap alone — a promise of imports that
  never physically flowed; E's hulls made the lie visible. **Flag at
  eyeball.**
- Import parity now requires a *served* lane (posted capacity > 0, not
  severed): an unserved market spikes exactly like a blockaded one —
  naval shortage is visible on the price map.
- Posture manager churn note: posted fleets pool + redeal freight hulls
  every epoch (grades blend per design); empty posted fleets linger as
  registry records (id==index — no deletion). Acceptable growth; disband
  mechanics can come with H salvage if it bothers.
- **Task-5 shape** (seed 42, radius 12, 40 epochs): 21 foundings, every
  one convoyed (event 402); famine events 189 (was 808 pre-E — slower
  hull-gated colonization founds fewer doomed ports, and freight feeds
  the rest); attrition wrecked 9–39 of 15–52 hulls built per polity —
  mostly escorts starving on armaments at ports with no arsenal, which is
  honest texture but worth a task-8 look. Fleet upkeep is bought from
  MilitaryPoints at market prices and recycles as home-port wages.
- Convoy staging: lane hops are free at this clock, so the convoy stages
  from the own port *nearest the target*; only the off-lane leg gates on
  the colony design's endurance (`Fleet.EnduranceHexesPerPoint` × stat ≈
  27 hexes ≥ the 24-hex colonization reach). Escorts don't ride along yet
  (piracy lands with H); the arrived convoy docks as the colony's first
  (empty) reserve fleet.
- Military treasuries still over-accumulate (28–63k by epoch 40) despite
  upkeep purchases — the main task-8 calibration target (hull costs,
  upkeep rate, or a militancy-scaled Budget.Military).
- **Task-8 calibration findings** (all A/B'd on seed 42, radius 12):
  - **Supply met is need-weighted, not min**: under min-met an armaments
    (or machinery) drought erased whole navies within epochs; weighted-met
    hollows them to degraded readiness instead — militia rot, not
    evaporation. Shipment *counts* fell 5× with more surviving capacity
    while famines stayed flat: one full hold replaces five drip runs, so
    the Markets note now reports **units moved** beside the count.
  - **Civilian hull upkeep draws Ship Components (spares), not
    Machinery** — coupling the merchant marine to the economy's dominant
    sink starved freight; components also finally get an ongoing sink and
    freight out to colony ports where fleets home.
  - **The parked D machinery-upkeep question is answered: catalog
    machinery-upkeep coefficients halved** (Infrastructure.cs). At the old
    rates no machinery remained for Ship Components production, which
    gated hulls, which gated expansion once founding needed convoys —
    foundings collapsed 96 → 21 per history. After halving: 43 foundings,
    famines 737 (main) → ~197, freight ~180 units/epoch late-game.
    Machinery is still the dominant sink at half rate. **Flag at eyeball.**
  - A militancy-scaled Budget.Military was tried and dropped (famines
    worsened ~15%; budget weights stay the controller's taste for G).
  - **Expansion is now industrially gated** (43 vs D's 96 foundings/
    history): a real navy bottleneck, not a bug — pace is the user's
    taste call at the eyeball. Military treasuries still accumulate
    (~20-90k); acceptable until controllers get smarter (G).
- FleetShapeTests: hull ledger conserves across seeds (built == active +
  wrecked + scrapped, wreck records match), fleet registry bounded,
  posted capacity nonzero late-game. TUNING.md gains the Fleet family
  table + chassis-catalog structural note.
