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
- [ ] 2. **Design registry + lineages** — `ShipDesign` records on SimState,
      genesis designs per polity at entry, mark drift, PerceptionView
      design briefs, event 400, artifact DESIGN records (fleets layer → 2).
      Gate: unit tests; artifact round-trip; golden regen.
- [ ] 3. **Yard production + starter fleets** — MilitaryPoints accrual,
      yard hull building from market components (+armaments), fleet
      founding/joining, hull counters, homeworld starter fleet, FLEET/HULLS
      serialization. Gate: conservation test (credits + hulls); golden regen.
- [ ] 4. **Postures + posted capacity** — posture/route state, Allocation
      fleet management (post freight to lanes, patrols by militancy,
      reserve default), `PostedCapacity` replaces `LaneMath.Capacity` in
      Arbitrage + AddReExportDemand, blockade severance, TrafficMath.
      Gate: no-hulls-no-freight test; blockade-spike parity with lanecut;
      shape still alive over 40 epochs.
- [ ] 5. **Movement + supply + convoys** — fleet upkeep demand + draws,
      readiness, attrition → wreckage (event 401), physical freight fuel,
      endurance floors, colony convoy resolution (event 402) gating
      founding on hulls. Gate: unsupplied-fleet decay test; convoy
      founding test; hull conservation incl. wrecks/scraps.
- [ ] 6. **Artifact v2 complete + load gates** — WRECK records, full
      fleets-layer v2, id==index validation, version refusal,
      LoadThenContinue byte-identity with fleet state. Gate: all artifact
      tests; golden regen.
- [ ] 7. **REPL surface** — `fleet [id]` dump (composition, posture,
      vectors, supply), `designs [actor]`, `emap traffic`, debug posture
      command; help text. Gate: piped-stdin smoke via bash printf.
- [ ] 8. **Shape acceptance + calibration** — 40-epoch runs across seeds:
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

(running log — appended as they happen)
