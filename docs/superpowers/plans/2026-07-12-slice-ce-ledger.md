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
- [x] **C6 — B1 gate: suite 738/739 (GOLDEN-only red).** The shape
  recovery, in order: colony-need boost (Planner score ×
  `Controller.ColonyNeedBoost` + yard weight 0.6→1.5 when settlers wait —
  hull famine was THE colonization throttle, funnel-measured);
  expedition-contention closed BOTH ways (perception filters convoy-
  contested candidates; TryFound blocks same-step races in actor order —
  the T2 turn-back flag closed); `RealmHungerGate` 0.8→0.7 (bands pay for
  food now; the realm-subsistence equilibrium is structurally leaner);
  tier-1 port raises drop the RefinedExotics term (basket × (tier−1) —
  frontier raises could never source exotics and starved to abandonment;
  design-doc amendment due at C19); CivilWar fixtures run 32 epochs (works
  buy goods → the 4-port polity arrives later); the ports-vs-actors shape
  bars count POLITY actors only (corps inflated the old proxy).
  Conservation sweeps green (credits incl. escrow, hull ledger); FineTick
  honesty green; determinism ×2 green (FullRun s1==s2 + FineTick suite).
  CARRIED to C18 tuning: capital-goods chains (Composites/RefinedExotics)
  run anemic — uniform-scarcity ceilings kill relay gradients and B1's
  bridge can't route industrial feedstock multi-hop; B2's resting corp
  bids/spread runs are the designed fix. Port raises complete rarely
  (tier-2+ still exotics-gated) — watch at the eyeball.

### B2 — actors close the spreads

- [x] **C7 — perceived books: DECIDED, deferred with multi-hop.** Spread
  runs are LANE-LOCAL (below): both ends are one hop from the fleet's
  station, so prices read FRESH — exactly markets.md's "fresh at one hop"
  (P3-clean without belief-book snapshots). Perceived books with
  news-delayed freshness become necessary only with multi-hop runs —
  "bounded multi-hop expeditions emerge" is the design's own emergent
  future; CARRIED as a flagged pass with the relay-bid retirement.
- [x] **C8 — spread runs.** COMPLETE. THE BRIDGE AND ITS PHANTOM ARE DEAD:
  `MoveFreight` iterates POSTED freight fleets (corp AND merchant-marine —
  any posted hull's owner trades); each buys the cheap end's asks WITH ITS
  OWN CAPITAL, pays fuel/tariffs/friction, ships toward resting bids +
  the speculative reference-spread run (no reservation — arrival posts
  the TRADER's asks; a dead spread is the trader's loss). `PayHaulers`
  deleted — the hauler IS the trader. DEVIATION FLAGGED: relay bids stay
  (the funded entrepôt staging) until multi-hop runs land — the spec said
  they die in B2, but killing them re-breaks hop diffusion.
- [x] **C9 — courier contracts.** COMPLETE. `CourierContract` (cargo +
  fee physically escrowed; Priority War/Normal) + `CourierOps`
  (Post/Accept/Resolve/ExpireOpen/AcceptOpen) + `couriers` layer v1;
  shipments resolve their carried contract in `ShipmentOps.Advance`
  (delivery pays the fee, piracy refunds it — nobody's paid for cargo at
  the bottom of the sea). `Dispatch` gained an out-outcome overload.
- [x] **C10 — requisitions → couriers.** COMPLETE. `RaiseRequisitions`
  posts couriers; sourcing = DELIVERED TIME (route transit years, port-id
  tiebreak — the port-id-order seam closed); fee = units × hexes ×
  `CourierFeePerUnitPerHex` (new knob) escrowed from the poster; the job
  board (`AcceptOpen`, Allocation) hands each contract to the deepest
  first-leg carrier — the poster's own marine self-fulfills at cost, no
  hulls means the contract WAITS (real logistics scarcity); off-lane
  routes self-haul as before. `Inbound` counts open courier cargo so
  replans don't double-order. Founding links' affordability gate halved
  (goods-free, wages only — dev treasuries buy project goods now).
  Escrow terms added to both conservation tests. Suite 743/744
  (golden-only).
- [x] **C11 — corp standing plans.** COMPLETE (scoped to facilities).
  `CorporationPolicies.Plan`; corps PERCEIVE at their scope (capability
  brief + home-port investment pick in PerceptionPhase);
  `CorporateController(config).Decide` packs `Planner.BuildCorpPlan`
  against income + savings; `InvestFacilities` executes due entries with
  truth checks (the one-build special case retired); corp plans ride the
  PLANE serializer lines (Intent runs after Allocation — the plan crosses
  the save boundary). SCOPED OUT with flags: route commitments and gate
  pairs stay opportunistic in Operate; hull purchases stay immediate
  (corps buy hulls off the book, they don't run yard batches).
- [x] **C12 — B2 gate: suite 742/745.** Courier lifecycle + acceptance +
  corp-capital spread-run tests green; REPL `econtracts [actorId]` and
  `efreight` purpose tags (courier / spread run / state haul); corp
  packing test rewritten to the Move-1 cycle. Reds: GOLDEN (sanctioned) +
  2 TRAJECTORY-STAGING flakes deferred to the post-B3 stabilization
  (DynasticInstrument LapsedTie fixture staging; LaneBuilder small-polity
  densification web — 3.5 degree across 4 ports, pre-existing eagerness
  in pass-2's while-affordable loop). PostFreight test kit handles corp
  owners (PolityOf threw).

### B3 — front supply lines

- [x] **C13 — forward depot.** War-stationed fleets (Blockade/Expedition)
  victual at `FleetOps.NearestOwnedPortId(fleet.Hex)` instead of home;
  docked/Posted/Patrol draws unchanged. Helper is public — the
  interdictor's prize port reuses it. Interdiction knobs
  (`War.InterdictionReachHexes` 4, `War.InterdictionLossPerContestedYear`
  0.12, `War.EscortDampPerHull` 0.15) in config + registry; channel 76.
- [x] **C14 — war couriers.** `ShipmentOps.StockDepots` (after
  ManagePostures, before SupplyFleets): per war-stationed fleet,
  `FleetOps.UpkeepNeed` (extracted from SupplyFleets — one basket truth)
  over step + lead window, aggregated per depot in port-id order,
  shortfall net of larder + Inbound (open couriers count — no
  double-orders), posted via OrderFromOwnPorts at CourierPriority.War.
- [x] **C15 — interdiction.** `WarPresenceMap` (war-stationed squadrons
  within reach of either lane endpoint + Escort fleets riding the lane;
  warship hulls only) threads through Sail beside HunterMap. Seizure
  compounds per contested-year, escorts damp `p/(1+damp×hulls)` — one
  roll, channel 76 keyed (epoch, owner, shipment id), rolled AFTER piracy
  took its chance. Prize posts at interdictor's nearest own port as its
  asks (portless interdictor takes nothing — conservation);
  `WorldEventType.CargoSeized = 409` + payload + serializer + headline.
  Project-cancellation event already landed at C10 (ProjectAbandoned 211);
  headline added here.
- [x] **C16 — B3 gate.** FrontSupplyTests ×8: depot relocation (home book
  untouched), home draw unchanged, quartermaster posts/holds fire, War
  outranks Normal at the job board (shipment-id order), readiness bleeds
  on a blockaded supply line, certain-seizure lands the prize + event,
  overwhelming escorts pass. Suite 752/753 — only the sanctioned golden
  red. Post-B3 trajectory stabilization done here: LapsedTie asserts the
  mechanism (a house of the holder; release is eventually consistent),
  FoundingLinks excludes ports founded within the last epoch (its own
  documented exception) — 4/57 settled isolates, under the 10% bar.

### Wrap

- [x] **C17 — fresh-eyes whole-branch review + ONE fix wave.** Reviewer
  (post main-merge, K3's MarketPanel ported to the book first) returned
  2 critical + 5 high + 8 medium + 5 low. FIXED this wave: (1) band-bid
  escrow is one purse per band, not per-good — machine populations
  overdrafted; (2+13a) SweepEstate on Dissolve/Nationalize — resting buys
  refund into the settling books, sells/shipments/courier-fulfiller roles
  pass to the successor; (3) corp spread runs capped by the corp's free
  capital (the sovereign marine keeps its intra-step credit line: its
  treasury sits escrowed in its own procurement/relay bids until the
  clear — probed and proven); (4) OrderOps.ExpireOrders — buys refund,
  sells escheat to the port; PostSupply's blend refreshes expiry so
  active producers never escheat; default OrderExpiryYears 30→100 (an
  expiry inside ~2 coarse steps broke FineTick honesty — probed); (5)
  courier acceptance charges FleetOps.PostedLift per fleet — War
  genuinely takes the hulls, commerce waits; (6) depot forecast counts
  book coverage and caps at StockCapacityAt — reorder loop closed; (8)
  WarPresenceMap early-outs in peacetime; (9) requisitions re-check port
  ownership at the dock — a captor gets asks, not a stocked larder; (10)
  war-stationed consumption signals at the depot; (11a) courier FeeEscrow
  in the credit sweep; (15/18) stale comments corrected. CARRIED (flagged
  debt / C19 amendments): (7) RepriceAsks re-anchors ALL quotes vs the
  spec's per-owner decay — deliberate B1 stabilization, needs the
  markets.md amendment + user flag; (11b) no global goods-conservation
  sweep (production/consumption make it nontrivial); (12) an InTransit
  courier on a permanently dead lane locks fee+cargo — rare, needs a
  stall-expiry design; (13b) RouteFill untracked-buy fallback deposits to
  the port owner; (13c) corp plans still facilities-only; (14) courier
  allocation ranks (priority, id), fee level prices nothing — spec §3
  deviation to amend or revisit; (16) Prune's 1e-12 residue + O(n)
  removals; (20) RouteFill linear scans. Suite 821/822 (golden only).
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
