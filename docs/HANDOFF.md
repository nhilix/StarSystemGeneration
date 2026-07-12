# Session Handoff — 2026-07-11 (Time & logistics Stage 2 on `slice-t2-located-logistics`)

State: branch `slice-t2-located-logistics` complete and green (**689/689,
twice** — determinism ×2; hex-tier untouched; golden re-frozen once at slice
end after the review fix wave — the red window closed), **awaiting the user's
REPL eyeball + merge decision**.
`main` at `0702f86` (the Stage-1 merge). ProjectSettings churn uncommitted.

## Time & logistics Stage 2 — located logistics (spec §4b, closed)

Spec `docs/superpowers/specs/2026-07-11-time-and-logistics-design.md` §4b;
ledger `docs/superpowers/plans/2026-07-11-slice-t2-ledger.md` (per-task, with
the review findings and carried flags). **Stock has an address; remote
sourcing is shipments over the lane network that take transit time.**

- **Located stockpiles** (`Port.StockQty/StockGrade`, `DepositStock`/`DrawStock`):
  the global `PolityRecord.ReserveQty/Grade` pool is DELETED. Serialized as
  STOCK lines (markets layer v2, RESERVE lines dead). Ownership is the port's
  owner — conquest, federation, and schism move stock by moving the port (the
  merge/split blocks are gone). Capacity = port tier × `StockCapPerPortTier`
  + active Depot tiers × `StockCapPerDepotTier`; decay compounds per
  world-year, each Depot tier multiplying the rate by `DepotDecayFactor`.
- **The reserve treasury**: `Budget.Reserves` (0.10) had NEVER been spent —
  the stage-1 golden held zero RESERVE lines; procurement always lost to the
  drained credit balance. Now `PolityRecord.ReservePoints` (POLITY tail,
  actors v7) accrues in the budget split, funds `Procure` (each own port buys
  toward its target share from its own market), splits at schism, merges at
  federation, and is counted by the mint-conservation gates.
- **Shipments** (`Shipment`, `ShipmentOps`, `SimState.Shipments` +
  `NextShipmentId`, trailing shipments layer v1): origin/dest ports, cargo
  with grades, route lane ids + leg years priced at departure
  (`FreightHexesPerYearBase` × gate-tier `TransitSpeed`; off-lane at
  `OffLaneFreightHexesPerYear` crawl). ONE sailing rule (`Sail`) shared by
  dispatch and the per-Markets-step `Advance` (arrivals land BEFORE supply
  and draws): closed legs (blockade/quarantine/dead gate) stall the freight —
  dispatch is NOT exempt, so state logistics cannot resupply through a
  blockade even at coarse tick; hunted legs roll piracy (channel 75, keyed
  step/owner/shipment) for the years sailed — loot lands at the band's haven
  with the band as supplier. Sub-span open-route transits deliver in-step
  (sub-step blur). In-flight only; arrivals/losses leave the registry.
- **`MoveFreight` transit**: Arbitrage routes through `DispatchVia` on its
  own lane — costs settle at departure, the sale lands with arrival.
- **The requisition channel** (`ShipmentOps.RaiseRequisitions`, called per
  polity in Allocation): covers in-flight project sites AND pre-positions
  due-soon plan entries (`RequisitionLeadYears` window, `GroundBroken`
  guard); gate pairs provision both ends; consumption stores (provisions,
  fuel, ship parts, armaments) keep their target share at the source;
  never ships to ports the funder doesn't own; orders capped at the route's
  weakest-lane capacity over the window.
- **Pass-1 draws are local-only**: site market + the site port's larder when
  the funder owns it (`ProjectOps.Feed`); gate pairs draw per end, the
  scarcer end pacing the pair (`FeedGatePair`); `AddConstructionPull`
  registers half the pair's demand at EACH end and **tapers every pull to
  the remaining years**.
- **Located capability brief**: `PortBrief.Stock` (Perception clones the
  larder); the planner leans toward supplied sites
  (`Controller.PlanSupplyWeight`, score × 1−w+w·coverage);
  `Planner.EntryBasketPerYear` shared with the quartermaster.
- **Fine-tick gaps closed**: yard slots accrue on a STATELESS world-time
  clock (floor(rate·year) telescopes exactly — RollChannel 73 retired);
  founding cadence via `Expansion.FoundingCadenceYears` (25) in `TryFound`
  (coarse unchanged, fine founds at the same world pace); FineTick hulls
  band tightened 0.6→0.5; the completions test counts UNITS (hulls per
  batch).
- **Residue closed**: founding kit tier-scaled (`RequiredGateTier` at
  dispatch) and riding the expedition as cargo (`PerYearBasket` doubles as
  the hold for travel kinds); a turned-back convoy banks the kit at the
  staging larder; completion STATE stamps interpolated
  (facility/gate `CommissionedYear`, expedition `FoundedYear`); corps pack
  builds against income (one at a time until income carries more).
- **REPL**: `efreight` (route, cargo, sailed/total, live ETA, STALLED) ·
  `emap works` (#=sites, >=freight/convoys) · `eprojects`/`eplan` intact.
- **Knobs added** (all registered + TUNING.md): DepotDecayFactor,
  StockCapPerPortTier/PerDepotTier, FreightHexesPerYearBase,
  OffLaneFreightHexesPerYear, RequisitionLeadYears, ShipmentLossPerHuntedYear,
  PlanSupplyWeight, FoundingCadenceYears.
- **Design tree amended**: `economy/markets.md` (freight transit + located
  stockpiles + requisition channel), `economy/assets-and-investment.md`
  (local-only draws, per-end gates), `frame/controller-contract.md`
  (stockpile-targets mechanism), `substrate/infrastructure.md` (Depot).
- Seed-42 eyeball numbers: 9 wars declared / 5 burning, 175 live lanes,
  ports founding steadily; requisitions visibly crawling off-lane to
  frontier sites in `efreight`.

## Deliberately deferred / flagged

- **Contract economy is next** — kickoff:
  `docs/superpowers/plans/2026-07-11-contract-economy-kickoff-prompt.md`
  (needs its own design pass first). Carried into it: nearest-first /
  bid-based requisition sourcing, real shared capacity competition, front
  supply lines, corp standing plans, scratch-less piracy loot attribution.
- **Dotted domains**: the levers are now `Controller.PlanSupplyWeight`
  (raise = consolidation lean) and `Controller.PortRaisePlanScore`; judged
  by the emap eyeball — no tuning applied this slice (war shape and
  colonization bars all green untouched).
- Staged chronicle events still stamp Chronicle's step year (only STATE
  stamps interpolate); project cancellation still stages no event.
- Turn-back kit deposits at neutral 0.5 grade (the draw's blend isn't
  stored).

## Next up

0. **REPL eyeball + merge decision for THIS branch** (the taste gate):
   `epoch 42` · `efreight` (requisitions in transit with ETAs) ·
   `emap works` · `eprojects all` · the throttle test: `elanes` to pick a
   lane feeding a construction site, `equarantine <laneId>`, `estep`, then
   `efreight` (STALLED) and `eprojects` (the ETA slides — starvation at the
   pace of the last delivery). Merge locally on the nod; push on say-so.
1. **Contract economy** — fresh session, point it at the kickoff prompt
   (design pass first).
2. **Slice K2 (Lens catalog)** — fresh session,
   `docs/superpowers/plans/2026-07-11-slice-k2-kickoff-prompt.md`; then K3,
   K4, K5 per `docs/superpowers/plans/2026-07-11-slice-k-roadmap.md`.
3. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings stays
uncommitted; bash printf for REPL piping; parallel slices take worktrees;
every new `src/Core` file gets a two-line `.meta` with a fresh guid; every
calibration constant in a knob registry + TUNING.md. The design is the spec —
a deviation amends the affected `docs/design/` doc in the same branch (this
slice amended four). Golden regen: a temporary xunit fact writing
`ArtifactSerializer.ToText` of the seed-42/radius-12 default run to
`tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt`, deleted after.
