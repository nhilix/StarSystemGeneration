# Slice E Kickoff — Session Prompt

You are starting **Slice E (Fleets)** of the epoch-sim implementation
roadmap, under the lighter protocol in `/CLAUDE.md` (read it first). E gives
the economy its hulls and the galaxy its navies: design sheets and lineages,
aggregation vectors, yard production chains, the six postures (posted
capacity replaces slice D's freight stub), movement/supply/endurance floors,
wreckage residue, and traffic-derived news-speed data. Everything abstract
becomes physical: trade flows need hulls (frame/actors.md).

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules.
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — meta-plan
   (row E; F follows, G consumes commanders).
3. **The design doc E implements**: `docs/design/fleets/ships-and-fleets.md`
   — the chassis grid, two-layer stat model (sheet + epoch aggregation
   vectors), production, the fleet object and six postures, movement/supply,
   attrition/wreckage, information carriage, commander role slots. Also
   skim `frame/space-and-travel.md` (the three leg types fleets compose)
   and `economy/markets.md` §4 (the freight generators fleets will carry).
4. **What Slice D landed** (ledger
   `docs/superpowers/plans/2026-07-09-slice-d-ledger.md` — read its notes;
   the macro-economics lessons there will save you days):
   - **The market engine** (`src/Core/Epoch/MarketEngine.cs`): the market
     step in fixed order — SupplyLands → AssembleDemand (+ industrial +
     construction-pull + re-export demand) → **MoveFreight** → AdjustPrices
     (with import parity over lanes) → Clear → DistributePools. Freight is
     three generators (reserve release, lane arbitrage, procurement) budgeted
     by `MarketStepScratch.LaneCapacityUsed` against
     `LaneMath.Capacity(a,b) × years` — **that expression is the
     fleet-capacity stub you are replacing with posted-posture capacity**.
     Arbitrage gates on realized net (post-tax owner share); everything is
     conserved ledger moves (shape gate: credits conserve exactly to the
     entry mint — keep it that way; every E flow must be a transfer).
   - **What's waiting for you in the economy**: Shipyards exist and bank
     Ship Components nobody consumes yet (~150 units by epoch 40);
     militancy-flavored polities hold Armaments reserves
     (`ControllerKnobs.ArmamentsPerPortPerMilitancy`);
     `UseCase.MilitaryConstruction/MilitaryUpkeep` demand profiles are wired
     but unused; `Economy.FuelPerUnitPerHex` monetizes freight fuel — E's
     hulls should draw it physically (a documented D deferral).
   - **State model**: `SimState.Fleets` holds `FleetRecord(Id, OwnerActorId,
     Hex)` — a slice-B stub you deepen into the design's fleet object
     (composition per design, posture, supply state; commander slot stays
     empty until G). Registries are id-ordered, iteration order fixed (P6),
     the economy is roll-free — fleets may need rolls (`RollChannel` next
     free: **41**; append, never renumber).
   - **Knob discipline (new, hard)**: every calibration constant goes in a
     config knob family + `KnobRegistry` (`src/Core/Epoch/KnobRegistry.cs`,
     name-sorted, one-line doc) + a consequences row in `docs/TUNING.md`.
     KnobRegistryTests enforce it. The artifact serializes knobs as
     `KNOB|Family.Name|value` lines — adding knobs never reshapes the
     format. Add a `Fleet` knob family.
   - **Artifact** (`ArtifactSerializer`): 12 layers, config v3 (KNOB lines),
     actors v2 (policies + credits), segments v2 (identity layers), markets
     layer (MARKET/CULTURE/RESERVE/LOAN). **E bumps the fleets layer 1→2**
     (composition/posture/supply) and appends design-sheet records (new
     layer or within fleets — your call, appended to the Layers list).
     Load validates id == index; the strongest gate is
     `LoadThenContinue_EqualsTheStraightRun` (byte-identity) — anything
     read across epochs is state and must serialize (D learned this with
     `LastSubsistence`).
   - **Events**: economic 200s next free **207**; military block is
     400–499 (empty — first entries are E's: ship classes launched, fleets
     lost to attrition?). Payload records need ArtifactSerializer +
     SimTraceView cases (serializer refuses unknown payloads loudly).
   - **REPL**: `epoch <seed> [epochs] [radius]` · `estep [n]` ·
     `emap [domains|lanes|price [good]]` · `market <portId>` ·
     `lanecut <a> <b>` (debug blockade) · `chronicle` · `esave/eload` ·
     `knobs [filter]` · `goods` · `infra`. E adds at minimum a `fleet`
     dump and a traffic/route map layer.
   - Surprises worth knowing (details in the ledger): wages flow from
     *realized* revenue; producers idle at negative margin and throttle
     into gluts (`Economy.MinUtilization`); treasury spending pays
     construction wages (money is never destroyed); polities budget epoch
     *receipts* (deficit-financed development); **upkeep is the economy's
     dominant sink** — fleet upkeep draws will land on already-tight
     machinery/armaments markets, so expect a calibration pass (the
     catalog-upkeep question is parked in the ledger); every new `src/Core`
     file needs a `.meta` (two lines, fresh guid).

## Scope (roadmap row E)

- **Design sheets & lineages**: per-polity designs on the role × size
  chassis grid, stat sheets derived from embodiment/doctrine/tech/grade;
  lineage drift over epochs (improved marks, inherited names).
- **Aggregation vectors**: fleet composition → combat/logistics vectors
  (strike, sustained, screening, tracking, detection, stealth, capacity,
  endurance floor, upkeep) computed on demand, never stored.
- **Yard production chains**: shipyards convert Ship Components
  (+ Armaments, + Compute for advanced) into hulls per Allocation queue
  policies; hulls conserved end-to-end (P4).
- **Six postures**: posted (freight capacity per route — **replaces
  `LaneMath.Capacity` in MoveFreight**), escort (risk vectors), patrol
  (legality enforcement data), blockade (stationed interdiction — can
  replace the `lanecut` debug hook's role), expedition/convoy (the moving
  posture; colony convoys make founding physical), reserve (docked,
  readiness decay).
- **Movement & supply**: the three leg types, endurance floors on off-lane
  legs, fuel/upkeep drawn from home-port markets (physically now), readiness
  then hull loss when unsupplied.
- **Wreckage residue**: losses conserve into wreckage records at real hexes
  (salvage sites; the narrative layer compiles them in I).
- **Traffic-derived news-speed data**: per-lane traffic frequency from
  posted routes — the *data* only; Perception consumes it in slice I.

**Boundary**: no combat resolution or war (H — vectors are produced, not
consumed); commanders are role *slots* only (G fills them with characters);
piracy risk needs lawlessness inputs H/G provide — post the interface, stub
the value; perception stays perfect-info (I). New `RollChannel`s appended
from 41; new economic events from 207, military from 400.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-e-fleets` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-e-ledger.md`; TDD; frequent
   commits. Don't share a checkout with another live session — take a
   `git worktree` if one exists.
3. Gates: `dotnet test` green (hex-tier untouched) · determinism
   byte-identity incl. fleet state · load-vs-rebuild + load-then-continue
   equivalence · credits conserve to the mint (fleets buy hulls and fuel
   with real credits) · hulls conserve (built = active + wrecked +
   scrapped) · shape bands over 40 epochs (fleet counts bounded, freight
   capacity sane).
4. REPL surface: `fleet <id>`/fleet listing dump (composition, posture,
   vectors, supply) · a traffic or route map layer beside domains/lanes/
   price · the eyeball gate is **posted routes visibly carrying the
   freight** (shipment counts rise where fleets post; a lane without hulls
   moves nothing) and **a colony convoy founding a port**.
5. User gates: scope nod · REPL eyeball · merge decision.
6. Wrap-up: merge · HANDOFF · **write the Slice F kickoff prompt** (deep
   genesis — read the roadmap's sequencing rationale first) · flip the box
   below · push only on user say-so.

- [x] Slice E complete
