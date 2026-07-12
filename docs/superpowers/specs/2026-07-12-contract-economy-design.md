# Contract Economy — the order-book market, actor-fulfilled logistics

**Date**: 2026-07-12 · **Status**: approved by user, section by section
**Mandate**: `docs/superpowers/plans/2026-07-11-contract-economy-kickoff-prompt.md`
(spec seed: time-and-logistics §"Future passes" — "evolve the market step from
pooled per-market clearing into standing buy/sell contracts integrated with
the logistics system"; markets.md §"Freight moves" contracts paragraph).

## The problem

Stage 1/2 of Time & Logistics made goods located and freight timed, but the
economy is still **routed by the engine, not fulfilled by actors** (P5 gap):

- `Market.Inventory` is an anonymous pooled shelf. When a corp mine and a
  polity mine both land ore at one port, "who is selling" has no answer —
  suppliers are credited statistically at distribution, and the market is
  effectively the only seller in town.
- `MarketEngine.Arbitrage` decides what moves where, settles on TRUE prices
  (the design demands perceived, P3), and charges phantom "exporting polity
  merchants" who never chose anything.
- `ShipmentOps.RaiseRequisitions` is engine logistics: sourcing is
  port-id-ordered (should be delivered-cost choice), per-order capacity is a
  lane-math approximation (should be actors' hulls competing), and hauling is
  free (should pay freight rates to a hauler).
- Freight-line corps invest one build at a time (`InvestFacilities` packing,
  T9's deliberate stub) — they own hulls but hold no plan and fulfill nothing.
- War fronts have no supply line: fleet upkeep quietly draws the HOME port,
  so a deep strike never starves and interdiction can't win a war.

## Decisions taken in brainstorm

| Question | Decision |
|---|---|
| Scope | **All three conditional pieces in**: contracts core, front supply lines (spec: "sequenced WITH this pass"), corp standing plans (T9's carried flag). |
| Market model | **The EVE model — the market IS the order book.** A market is the set of open buy/sell orders at a port. Sellers are named actors; goods on sale stay owned by the seller until lifted. Pooled clearing dies. The gradient a hauler chases is best bid at the sink minus delivered cost from the source's best ask (base + fuel + freight + tariffs), evaluated across range — nearest is not always cheapest delivered. |
| Arbitrage fate | **Corp speculation.** Engine `Arbitrage` is deleted; freight corps speculate against *perceived* books with their own capital and hulls. One actor machinery serves contract jobs (guaranteed fee) and spec runs (risky margin). |
| Population demand | **The port posts aggregate buy orders** on behalf of its bands (uniform machinery — everything is an order; freight corps see pop demand in the book like any bid). |
| Internal logistics | **Courier contracts** — a third record kind (EVE courier): move my goods A→B for a posted fee. State requisitions, corp internal moves, and front convoys are all this record. Not dissolved into buy orders (the state re-buying its own goods is circular and unreservable). |

## Approaches considered

- **A — big-bang**: book + actor fulfillment in one gate. One giant
  unfalsifiable red window; rejected (T1/T2 taught that shape drift needs a
  green world to diagnose against).
- **B — three staged sub-slices under one spec** (chosen): B1 order-book
  substrate with a mechanical freight bridge; B2 actor fulfillment (bridge
  deleted); B3 front supply lines. Each stage ends suite-green with the
  world alive.
- **C — contracts bolted onto the pooled market** (the kickoff's minimal
  reading): rejected — the anonymous shelf keeps selling and the ownership
  question stays unanswerable.

---

## 1. The two records

### `MarketOrder`

One record type, two sides. Flat `state.Orders` list (id = creation order,
P6); matching builds per-(port, good) book views in scratch.

- **Identity**: `Id`, `Side` (Buy/Sell), `OwnerActorId`, `PortId`, `Good`.
- **Terms**: `QtyRemaining`, `LimitPrice` (ask for sells, bid for buys),
  `PostedYear`, `ExpiryYear`.
- **Escrow is physical.** A *sell* order **holds the goods**: posting moves
  qty + grade out of the seller's stock into the order (as a `Shipment`
  holds cargo). `Market.Inventory` dies — "the shelf" is the union of live
  sell orders, and conservation reads: every unit is **on an order, in a
  located stockpile, or in transit**. A *buy* order **holds the credits**
  (`Qty × LimitPrice` drawn at post). A fill moves credits→seller and
  goods→buyer atomically; expiry/cancel refunds the remainder. No fill can
  bounce — escrow was checked at the door.
- **Grade** rides the goods (sells carry it, fills blend it, buyers receive
  it); matching is price-only, as today.
- **Population bids**: the port is the bands' buying agent — up to three bid
  tranches per good (subsistence / standard-of-living / luxury), priced by
  band elasticity (subsistence high and near-inelastic, luxury low and
  elastic), escrowed from segment wealth, unfilled remainder refunded to
  segments on reprice.

### `CourierContract`

`Id`, `PosterActorId`, `OriginPortId`, `DestPortId`, cargo basket (the
poster's own goods, escrowed from origin stock at post), `FeeOffered`
(credits escrowed), `Priority` (Normal / War), `ExpiryYear`, status
(Open → Accepted → riding a `Shipment` → Delivered / Expired). Delivery
lands cargo in the poster's destination stockpile and pays the fee to the
fulfiller; expiry returns cargo to origin stock and refunds the fee.

## 2. B1 — the market step, rewritten

Per port, fixed deterministic order:

1. **Arrivals land** — `ShipmentOps.Advance`, unchanged.
2. **Post & reprice.** Facility output mints into the owner's sell orders.
   Quote pricing is mechanical and per-owner: the old price-*drift* rule
   becomes the ask-*repricing* rule — sold out → raise the ask, sitting on
   unsold stock → cut it, rate-limited exactly like today's drift. The port
   posts its band bid tranches from segment income. Facility upkeep, project
   baskets (the construction pull — now literal bids), and polity
   procurement (reserve treasury) post buys. Expired orders refund.
3. **Match locally** per (port, good): cross the book while best bid ≥ best
   ask, trade at **maker price** (the resting order's limit); ties break
   (price, order id) — id is creation order, so price-time priority is pure
   ordered math, no rolls.
   Transaction tax skims each fill to the port's sovereign; the **labor
   share** of a facility owner's realized sales pays local segments at
   today's rate.
4. **Bridge freight** (B1 only, dies in B2): the existing Arbitrage loop
   re-read against books — per lane, lift the best ask at one end against
   the best bid at the other when the spread clears freight + fuel + tariff,
   within posted capacity, dispatched as shipments. Settlement follows the
   spread-run rule (no reservation): a sub-step transit fills the bid now; a
   longer haul sells into whatever book exists on arrival. Keeps the galaxy
   alive until corps take over.
5. **Consumption & consequences**: bands consume their filled bids — famine
   and SoL decline derive from *fill fractions*, same thresholds; facilities
   underproduce on unfilled input bids; project fills land in site stock,
   and `ProjectOps.Feed` draws the site larder only (the shelf is gone).
6. **Reference price**: `Market.Price[g]` survives as a derived readout —
   last-cleared price, falling back to best ask, else the prior value — so
   every downstream reader (expansion scoring, war-goal value, niche
   detection, migration pull) works unmodified.

Legality and tariffs keep their sites: legality gates posting and lifting at
a market; crossing fees collect at the destination-side gate per shipment,
as today.

## 3. B2 — actors close the spreads

- **Perceived books** (P3): corps and quartermasters read, per port in
  range, best bid / ask / depth per good — fresh at home, news-delayed with
  distance. A stale book is real risk.
- **Capacity stays lane-posted.** Corp hulls remain `Posted` to lanes; a
  haul consumes posted capacity per leg as today. The *shipment* stays the
  visible, interdictable object — no new fleet posture; traffic, news-speed,
  and tension readers are untouched.
- **Spread runs**: each Markets step, each freight corp (id order) evaluates
  its posted network against its perceived books: buy at A's asks with its
  own credits, haul, sell into B's bids on arrival. **No reservation** —
  bids gone on arrival means the cargo posts as sell orders at the
  destination and the corp eats the round trip. Engine bridge and
  `PayHaulers` are deleted; profit and loss are both real.
- **Courier fulfillment**: open contracts rank by fee over distance; corps
  accept where their posted network covers the route and capacity is free —
  deterministic allocation (best margin, then corp id). A polity's own
  posted freight hulls self-fulfill at cost before the fee goes to market.
  Delivery pays the fee; piracy/interdiction loss and expiry settle per the
  escrow (cargo lost is lost; the fee refunds).
- **Corp standing plans** (T9's flag): `CorporationPolicies.Plan` — the
  polity scheduler machinery at corp scope: hull batches, route commitments
  (which lanes to post on, from perceived gradients), gate-pair investments,
  packed against trailing income. Replaces the one-build-at-a-time
  `InvestFacilities` special case.
- **Requisitions rewritten**: `RaiseRequisitions` posts courier contracts;
  sourcing becomes delivered-cost order-book choice (which own port's
  stock, over which route, at what fee) — closing the T2 ledger's
  port-id-order and capacity-approximation seams. Quartermaster stores
  (provisions/fuel/parts/armaments) keep their target share at the source,
  as today.

## 4. B3 — front supply lines

- **The front is a demander.** A mobilized force fighting away from home
  draws upkeep (provisions, fuel, armaments, components) from the **nearest
  owned port to the front** — its forward depot — instead of the home port.
- **The quartermaster stocks the depot**: each Allocation, compare the
  depot's stock against deployed consumption over the step + lead window,
  and post **courier contracts at War priority** from rear stockpiles.
  Convoys are ordinary shipments: map-visible, blockade-stalled,
  pirate-hunted.
- **War interdiction — RollChannel 76**, keyed (step, owner actor, shipment
  id): a shipment sailing a leg contested by an enemy of its owner (enemy
  warships posted on or patrolling the lane, or the leg crosses an active
  war zone) rolls seizure per contested-year, the piracy pattern. Seized
  cargo lands at the interdictor's nearest port; the loss is a chronicle
  event. **Escorts damp it**: friendly warship strength on the leg reduces
  seizure odds as a deterministic modifier, not a second roll.
- **Starvation bites readiness**: an under-stocked depot feeds the front
  fractionally; readiness slides via the existing recovery/decay machinery —
  a cut supply line loses the war slowly and legibly (P4). Sieges gain their
  missing half: the defender's larder was modeled; now the attacker's
  corridor is too.

## 5. Determinism, persistence, tests, REPL, knobs

- **Determinism**: matching, quoting, spread evaluation, courier allocation
  are pure ordered math — orders by (price, posted step, id); actors and
  ports by id. The only new roll is interdiction on **channel 76**; 75 stays
  piracy; 73 stays retired.
- **Serialization**: new versioned `ORDERS` and `COURIER` blocks beside the
  shipments layer; `Market.Inventory/InventoryGrade` leave state (markets
  layer bumps; `Price` persists as the reference readout). Conservation
  sweeps escrows: credits (ledgers + segment wealth + buy escrows + courier
  fees) constant; goods (stockpiles + sell orders + shipments + courier
  cargo) constant.
- **Golden**: re-freezes **once** at slice end; the red window spans all
  three stages. Hex-tier suite never breaks.
- **Tests per stage gate** (suite green at each):
  - **B1**: matching determinism ×2; escrow refund on expiry; famine derives
    from fill fractions (a starved port still starves); reference-price
    continuity for downstream readers; FineTick honesty (1y vs 25y books
    clear the same world-year totals within bands).
  - **B2**: a corp buys low / hauls / sells high and books the margin;
    stale-book loss (bids gone → cargo posts as sells); courier lifecycle
    incl. expiry refund; self-fulfillment at cost; standing-plan packing
    never over-commits trailing income.
  - **B3**: depot draw relocation; War priority outranks Normal; interdiction
    seizure conserves cargo; readiness starves on a cut line.
- **REPL** (the eyeball surface): `ebook <port> [good]` — bids/asks/depth
  with owners; `econtracts [polity]` — open couriers, fees, takers;
  `efreight` gains cargo purpose (spread run / courier / war convoy);
  `emap trade` — spread intensity between books. Taste test: watch a
  fortress rise on couriers, blockade the route, read the readiness bleed.
- **Knobs**: quote step, bid tranche elasticities, order expiry years,
  courier fee rate, interdiction odds, escort damping — all in the knob
  registry + TUNING.md.

## Implementation sequencing

One branch, three internal stages, each ending suite-green with the world
alive (shape check: colonization pace, war liveliness, corp survival):

1. **B1 — order-book substrate** with bridge freight.
2. **B2 — actor fulfillment**: bridge deleted, spread runs, courier
   contracts, requisition rewrite, corp standing plans.
3. **B3 — front supply lines**: forward depots, war couriers, interdiction.

Then ONE fresh-eyes whole-branch review + one fix wave, golden re-freeze
once, REPL eyeball, merge decision. Three user checkpoints only: scope nod
(post-spec), eyeball, merge.

## Risks (flagged honestly)

- The B1 red window is the widest this project has opened — every economic
  test touches the market. Shape drift is *expected*: prices now emerge, so
  famine rates, colonization pace, and corp survival all re-tune.
- The bridge is throwaway by design; it is one re-pointed loop we already
  own, not new machinery.
- Performance is bounded: books are short (dozens of orders per port-good at
  worst), built in scratch, matched linearly.

## Future passes (flagged, not designed here)

- **Standing player-facing job board**: couriers + open bids ARE the job
  board at play scope; the play-clock UI over them is a later pass.
- **Grade-discriminating bids** (min-grade terms on buy orders) if grade
  spread ever matters to the chronicle.
- **Credit-financed trading** (margin loans to corps) — loans exist; wiring
  them to order escrow is deferred.

## Design-tree amendments this slice will make

- `economy/markets.md` — the core rewrite: the market step becomes the
  order-book step; stockpile shelf language becomes sell orders; "Freight
  moves" collapses into actor fulfillment + courier contracts.
- `economy/corporations.md` — freight niche = fulfillment + spread
  speculation; corp standing plan.
- `frame/controller-contract.md` — quote/procurement posture policies;
  "post procurement contract" generalizes to post order / post courier
  contract (polity and corp); corp standing plan entry made real.
- `economy/assets-and-investment.md` — project supply = posted bids +
  couriers.
- `interpolity/war.md` — front supply lines, interdiction, escorts.
- `frame/perception-and-news.md` — perceived books at news speed.
