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
- [ ] **C2 — matching.** Book views in scratch; per-(port, good) cross
  while best bid ≥ best ask at maker price; ties (price, order id);
  transaction tax per fill to port sovereign; labor share of facility
  owners' realized sales to local segments. Tests: determinism ×2, maker
  price, partial fills, tax/labor conservation.
- [ ] **C3 — post & reprice migration.** Facility output → owner sell
  orders (quote rule = old drift repointed: sold out ⇒ raise, glut ⇒ cut,
  rate-limited); port posts band bid tranches (subsistence/SoL/luxury,
  escrowed from segment wealth, refund on reprice); upkeep + project
  baskets + procurement post buys; `Market.Inventory` deleted, every
  Deposit/Draw caller migrated (piracy loot → hunter sell orders at haven —
  the plain-deposit unification); reference price derivation. Gate: suite
  compiles green except sanctioned reds.
- [ ] **C4 — bridge freight.** Arbitrage loop re-read against books (lift
  best ask vs distant best bid net of freight+fuel+tariff, posted capacity,
  ships via DispatchVia, no reservation). Marked throwaway (dies in C8).
- [ ] **C5 — consumption from fills.** Bands consume filled bids (famine /
  SoL from fill fractions, same thresholds); facility inputs underproduce
  on unfilled bids; `ProjectOps.Feed` draws site larder only. Tests:
  starved port still starves; fed port doesn't.
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
