# Slice CE — contract economy: task ledger

Branch `slice-ce-contract-economy` off main `9533fea`. Spec:
`docs/superpowers/specs/2026-07-12-contract-economy-design.md` (approved
section by section; spec approval doubled as the scope nod, 2026-07-12).
Kickoff: `docs/superpowers/plans/2026-07-11-contract-economy-kickoff-prompt.md`.

Golden red-window is OPEN inside the slice; goldens re-freeze ONCE after the
final review's fix wave. Hex-tier suite never breaks. Determinism ×2 at
gates. Each stage (B1/B2/B3) ends suite-green with the world alive (shape:
colonization pace, war liveliness, corp survival). New rolls: interdiction
takes RollChannel 76; 75 stays shipment piracy; 73 retired, never reuse.

## Grounding (from the code read, before C1)

- Flat `SimState.Orders` (id = creation order, P6) + per-(port, good) book
  views in `MarketStepScratch`; `SimState.Couriers` likewise in B2.
- Escrow is physical: sell orders hold qty+grade (like `Shipment`); buy
  orders hold credits. `Market.Inventory/InventoryGrade` die; `Market.Price`
  survives as reference readout (last-cleared → best ask → prior).
- Every `market.Deposit`/`Draw` caller migrates: SupplyLands, piracy loot
  (ShipmentOps.Sail — unify the scratch-less plain-deposit seam here, the
  kickoff's carried flag), salvage (CorporationOps.DepositSalvage),
  expedition turn-back bank, ReleaseReserves, upkeep draws, Feed.
- Serialization: `ORDERS` + `COURIER` trailing versioned layers beside
  shipments (`ArtifactSerializer`); markets layer bumps (inventory lines die).
- Bridge (B1 only, dies in B2): `MarketEngine.Arbitrage` re-read against
  best bid/ask per lane end; settlement = spread-run rule (no reservation).

## Tasks

### B1 — order-book substrate

- [x] **C1 — MarketOrder record + escrow primitives.** COMPLETE.
  `MarketOrder` (physical escrow: sells hold qty+grade, buys hold credits;
  bid-limit surplus stays escrowed until cancel so refunds return where the
  escrow came from — segments have no ledger), `OrderOps`
  (PostSell/PostBuy/Fill/CancelSell/CancelBuy; fill at maker price = the
  earlier id's limit; dead orders prune from the registry, `NextOrderId`
  keeps identity), `SimState.Orders`, `orders` serializer layer v1
  (ORDNEXT/ORDER). 5 tests green; suite 731/732 — the one red is the
  GOLDEN byte-comparison, the sanctioned window now OPEN (re-freeze at
  C18). Expiry sweep deliberately deferred to C3 (owner-specific landing:
  polity sells → port stock, corp sells → reprice in place).
- [x] **C2 — matching.** COMPLETE. `OrderOps.MatchPort`: per-(port, good)
  cross while best bid ≥ best ask at maker price, (price, order id)
  priority; each fill settles tax → port sovereign and labor share of the
  seller's net → local segments (UNIFORM rule — in the book world nothing
  is ownerless, so every seller pays wages; DistributePools' three-way
  split reproduced per fill); goods return as `OrderFill`s for the caller
  to route. `Fill` extended to return Paid. DECISION: labor share applies
  to ALL sells uniformly, not just facility output — today every scratch
  Deposit registers a SupplyRecord and gets the same split, so this is
  behavior-preserving, and it needs no output-vs-restock flag on the
  record. Suite 734/735 (golden window only). Galaxy-wide determinism ×2
  rides the C6 gate.
- [x] **C3+C4+C5 — the atomic book switch.** LANDED as one wave (the shelf
  cannot half-die). `Market.Inventory/Deposit/Draw` DELETED; facility
  output → owner sell orders; RunRecipe/upkeep/fleet supply/research/corp
  ops/expedition kit → `LiftAsks` (sellers paid at their asks, tax + labor
  settled per sale); band bid tranches escrowed from segment wealth with
  pro-rata fill apportioning + refunds; PROJECT baskets are real escrowed
  bids (goods COST treasuries now) filling a per-project laydown yard
  (`Project.DeliveredQty/Grade`, projects layer v2) that `Feed` consumes
  (+ funder-owned larder; gate-pair per-end pacing MERGED into one yard —
  flagged approximation); procurement bids from ReservePoints; bridge
  freight lifts asks toward REAL resting bids + a SPECULATIVE term (dear
  reference over delivered cost — the unsold surplus is what disciplines a
  cut-off price); RELAY bids (funded re-export — hop diffusion; B1-only,
  dies in C8); piracy loot/salvage/turn-back → PostSupply (plain-deposit
  seam closed); markets layer v3.
  **Price discovery**: quotes re-anchor to the reference each step
  (`RepriceAsks`); the reference drifts on posted bids + the CONSUMPTION
  SIGNAL (the old demand-assembly formulas as drift-only inputs — without
  them, lift-only consumers were invisible and the industrial loop died)
  against resting asks — the old tick-honest clamp, pre-match snapshot.
  **Planner**: packs against income + savings/`PlanSavingsDrawdownYears`
  (new knob, 5y) — receipts are lean net cash now; income-only packing
  deadlocked on idle treasuries. `CapabilityBrief.SavingsPerYear`.
  **Bugs found**: (1) `pr.Credits -= TechOps.Research(...)` — C# compound
  assignment reads Credits BEFORE the call; the book now pays pr as seller
  mid-call and the store clobbered it (credit leak, −1500/run) — call
  sites now evaluate first; (2) .NET's shortest-round-trip double format
  prints 2.98…312E-08 and its ulp neighbor identically — serializer `R()`
  gained a parse-back G17 guard; (3) dissolved/nationalized corps' resting
  sells now pass to the sovereign (dead corps must not keep earning).
  Suite 731/739; the 8 reds = GOLDEN (sanctioned) + 7 shape/honesty items
  (colonization pace ~24 ports vs main 44, port-raise completions, CivilWar
  4-port fixtures, FineTick completions band, GenesisShape) — the C6 gate's
  tuning work, tracked below.
- [ ] **C6 — B1 gate.** Conservation sweeps (credits incl. escrows; goods
  incl. orders); FineTick honesty (1y vs 25y cleared totals in bands);
  shape check vs main; REPL `ebook <port> [good]`; determinism ×2.

### B2 — actors close the spreads

- [ ] **C7 — perceived books.** Perception brief: best bid/ask/depth per
  good per port in range, news-delayed freshness (P3).
- [ ] **C8 — spread runs.** Freight corps evaluate posted network against
  perceived books, buy with own credits, haul, sell into arrival book (no
  reservation — stale-book loss posts sells); DELETE bridge + `PayHaulers`.
  Tests: margin booked; stale-book loss; engine routes nothing.
- [ ] **C9 — courier contracts.** Record + `COURIER` layer + lifecycle
  (Open→Accepted→Shipment→Delivered/Expired; cargo+fee escrow; refunds).
- [ ] **C10 — requisitions → couriers.** `RaiseRequisitions` posts courier
  contracts; delivered-cost sourcing (closes port-id-order + capacity
  seams); quartermaster stores keep target share; polity posted hulls
  self-fulfill at cost before the fee goes to market.
- [ ] **C11 — corp standing plans.** `CorporationPolicies.Plan` via the
  polity scheduler at corp scope (hull batches, route commitments,
  gate pairs) packed against trailing income; `InvestFacilities`
  one-build special case replaced.
- [ ] **C12 — B2 gate.** Courier lifecycle tests, self-fulfillment, plan
  packing never over-commits; shape check; REPL `econtracts [polity]`,
  `efreight` cargo purpose; determinism ×2.

### B3 — front supply lines

- [ ] **C13 — forward depot.** Deployed forces draw upkeep from nearest
  owned port to the front, not home.
- [ ] **C14 — war couriers.** Quartermaster stocks the depot vs deployed
  consumption over step + lead window at War priority (outranks Normal in
  acceptance and self-fulfillment).
- [ ] **C15 — interdiction.** Channel 76 keyed (step, owner, shipment id):
  contested legs roll seizure per contested-year; seized cargo lands at
  interdictor's nearest port + chronicle event; escort warship strength
  damps deterministically. Consider a WorldEventType for project
  cancellation while touching event types (kickoff carried flag).
- [ ] **C16 — B3 gate.** Depot relocation, priority ranking, seizure
  conservation, readiness starves on a cut line; shape; determinism ×2.

### Wrap

- [ ] **C17 — fresh-eyes whole-branch review + ONE fix wave.**
- [ ] **C18 — tuning + golden re-freeze ONCE.** `emap trade` spread lens;
  knobs registered + TUNING.md swept.
- [ ] **C19 — wrap-up docs.** Design-tree amendments (markets,
  corporations, controller-contract, assets-and-investment, war,
  perception-and-news); HANDOFF; next kickoff prompt; eyeball + merge
  decision (user gates 2 and 3).

## Carried / flagged (running)

- Golden red window open from C3 until C18.
- Piracy-loot plain-deposit unification lands in C3 (kickoff flag).
- Project-cancellation chronicle event: decide at C15 (kickoff flag).
- Quarantine clock edge (`>=` vs `>`, FleetOps vs ShipmentOps) — upstream
  Core cleanup flagged by K2; fix opportunistically if touched here.
