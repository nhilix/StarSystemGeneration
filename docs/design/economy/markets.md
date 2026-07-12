# Markets, Prices, Wealth & Credit

The L1 price machinery: how supply meets demand, how value emerges, and how wealth
moves. Runs in the Markets phase at every port market
([../substrate/market-geography.md](../substrate/market-geography.md)), on the goods
and Grade system of [../substrate/commodities.md](../substrate/commodities.md).

## The market is the order book

**A market IS the set of open buy and sell orders at its port** (the EVE
model). There is no anonymous shelf: goods for sale live on named sellers'
resting orders, wants live on escrowed bids, and every fill has two named
parties. Escrow is physical (P4): a sell order HOLDS its goods (quantity +
grade), a buy order HOLDS its credits, drawn whole at post — a fill can
never bounce. Orders past their expiry refund: buys return the escrow to
their owner, a sell's abandoned goods escheat to the port's stockpile
(dock storage lapsed). What remains on the `Market` record per good is the
**reference price** — the persistent readout every valuation, elasticity,
and controller keeps reading — plus last-cleared volume and the black book.

## The market step

Per port market, fixed deterministic order:

1. **Expiry sweeps** — lapsed orders refund/escheat; stale open couriers
   return cargo and fee.
2. **In-flight freight sails** — arrivals post on the destination book as
   the owner's asks (freight) or land in the owner's larder (requisitions
   — re-checked at the dock: a port that fell in transit gets asks, never
   a stocked enemy larder).
3. **Quotes re-anchor** — resting sells reprice to the current reference
   (nobody quotes yesterday's market). *Deviation, flagged: the spec's
   per-owner quote decay — sold-out sellers raising, glutted ones cutting —
   collapsed FineTick honesty in implementation; discovery lives entirely
   in the reference drift until a per-owner scheme earns its keep.*
4. **Supply lands** — facilities sell output as their owner's resting
   sells (one rolling order per owner/port/good, restock refreshes its
   expiry); salvage and piracy loot post the same way.
5. **Demand posts as escrowed bids** — everything is an order:
   - **Band tranches**: the port posts aggregate bids on behalf of its
     population bands, escrowed from segment wealth (one purse per band —
     poverty caps the want). Priority is expressed through PRICE, not code
     order: subsistence bids at a premium, comfort at reference, luxury
     under it.
   - **Project bids**: every in-flight project bids at a premium for its
     per-year basket, escrowed from the funder's treasuries; fills land in
     the project's **laydown yard** (delivered materials are the project's,
     not the market's).
   - **Procurement**: the sovereign bids toward its stockpile targets from
     the reserve treasury.
   - **Relay bids**: wherever a live, hulled lane shows a price gradient,
     the cheap end's sovereign bids at its own reference to stage goods for
     re-export — hop-by-hop diffusion; without it goods refuse to cross
     more than one hop and every frontier project starves; with it,
     entrepôts emerge. (Kept past B2, flagged: retires when multi-hop
     actor runs land.)
   - **Consumption signal**: lift-only consumers (recipes, fleet upkeep,
     research, military pull) register their want for the price drift —
     they buy off the book directly, but the scarcity they cause must
     still price.
6. **Spread runs** — every posted freight fleet's owner (corporation or
   the sovereign's merchant marine) trades its lane's price gradient with
   its OWN capital: lift cheap asks, sail, post as its asks at the dear
   end — no reservation; the unsold surplus is what disciplines a cut-off
   price. Absorption reads the dear end's real resting bids above
   delivered break-even (base price + fuel + tolls + tax/labor wedge); a
   speculative term sails when the dear reference clears delivered cost.
   Corps front runs from free capital only; the sovereign marine draws on
   the treasury's intra-step credit line (its cash sits escrowed in its
   own bids until the clear).
7. **Matching** — per (port, good): price-time priority (price, then order
   id), fills at MAKER price (the earlier order's limit). Per-fill
   settlement: transaction tax to the port's sovereign, a labor share of
   the net to the staffing segments, the rest to the seller. Unfilled
   band/project/procurement/relay bids cancel and refund at the step's
   end — standing state bids do not linger.
8. **The reference price drifts** — rate-clamped, on the pre-match
   imbalance snapshot: posted bids + consumption signal (generation-
   normalized, P7) versus resting asks. Markets never perfectly clear —
   persistent gradients *are* the trade opportunities.
9. **Clearing consequences** — band fill fractions drive the old truths:
   unmet subsistence → famine; unmet standard-of-living → SoL decline
   (growth, legitimacy, migration pressure); unmet inputs → facilities
   underproduce.

**Routed goods take transit time.** A haul is a `Shipment` record: origin,
destination, cargo, and a route over the lane network whose leg years are
priced at departure (lane speed = a freight base × the gate-tier transit
multiplier; off-lane legs at slow crawl). Dispatch and each Markets step
sail the same rule: a closed leg — blockade, quarantine, a dead gate —
stalls the freight where it floats (the fortress starves at the pace of its
last delivery); a leg hunted by a raiding band rolls piracy for the years
sailed under its guns (the loot posts as the band's asks at its haven); a
leg contested by an enemy of the owner rolls war interdiction, escorts
damping ([../interpolity/war.md](../interpolity/war.md)). A transit that
fits inside the current step on an open, lucky route is sub-step blur and
delivers within the step. In-transit cargo is conserved, visible state
(P1). Freight is what drags connected markets together; interdiction is
what splits price zones.

## Courier contracts — internal logistics as a market

Moving YOUR OWN goods is a contract too: a **courier** posts (origin,
destination, cargo escrowed from the origin larder, fee escrowed from the
ledger) — move my goods A→B for the fee. The job board clears in
(priority, id) order: **War priority outbids commerce for hulls** —
acceptance charges the carrier's real posted step lift, so an exhausted
board makes commerce wait. The fulfiller is the deepest free carrier on
the route's first lane — the poster itself when its own marine is deepest
(self-fulfillment at cost). Delivery pays the fee to the fulfiller;
piracy/interdiction loss refunds it to the poster; expiry returns both.
State requisitions, corp internal moves, and war-front convoys are all
this one record at different priorities. The **requisition channel**
rides it: every Allocation the quartermaster compares each project site's
coverage (larder + laydown yard + inbound) against its basket over the
step plus a lead window, and posts couriers from the polity's own port
stockpiles — sources ranked by delivered time — toward the shortfall.
Consumption stores keep their target share at the source; construction
materials were banked to be shipped. *Deviation, flagged: the spec ranked
open contracts by fee-over-distance; the implementation ranks (priority,
id) and the fee only prices the poster's cost — revisit when carriers
compete.*

At the play clock the same state drifts continuously between clearings; the
player trades against the same books the actors do.

## Household income — how populations afford anything

Purchasing power is earned, not assumed: facilities pay a **labor share** of
revenue to the local segments that staff them (the share shrinks with automation —
automated industry pays owners more and workers less), and the organic baseline
yields subsistence income. Segment income at local prices is what the demand
bands can actually clear, so **SoL derives from the real economy**: a booming
labor-scarce domain bids up income per worker, which is the "opportunity" term
migration reads; a domain hollowed out by automation is rich on paper and poor in
its streets — a faction seed. This closes the loop between production, wages,
consumption, and migration.

## Wealth and taxation

Every transaction moves credits buyer → seller, conserved (P4). Polity income:

- **Transaction tax** on sales at its ports (rate: an Intent-phase policy),
- **Tariffs** on cross-border freight (schedule: policy),
- income from state-owned facilities.

**True wealth** of a polity is an emergent readout: credit ledger + asset book
(facilities, fleets, reserves) — a consequence of real activity, not a stat.

## Credit

Loan objects: (lender, borrower, principal, rate, term). Wealthy institutions lend
by policy. Archetypal borrowers: **polities at war** (war loans — wars can be
financed, and long wars leave debt overhang) and **leveraging corporations**.
Unpayable obligations trigger a **default event**: reputation damage, relations hit
with the lender, collateral seizure (asset transfer — a lender can end up owning a
foreign mine), possible corporate dissolution. All interest and principal flows are
conserved ledger moves. There are no banks as actors; lenders are whoever holds
surplus.

## Stockpiles

**Stock has an address.** Every unit of every good is held by a resting
sell order, in a port's located stockpile, in a project's laydown yard, or
in transit on a shipment or courier — there is no polity-wide pool. Stockpiles live per port, per good, banked by procurement toward the
standing targets: each own port buys toward its share of the target from its
own market, paid from the **reserve treasury** (the Budget.Reserves share of
the income split — procurement never competes with the deficit-financed credit
balance). Capacity is built, not assumed: the port's tier banks a little,
active **Depot** tiers bank a lot and cut decay.

**Provisions reserves buffer sieges and famines** — locally: a besieged port
draws down ITS OWN larder before starving (a rich pool elsewhere feeds nobody
behind the walls), giving sieges a duration structure — rich prepared ports
endure, poor ones break fast. Perishability compounds per world-year where the
stock sits: provisions decay fast, medicine slowly, durables negligibly —
reserves are a real cost, not free insurance. Ownership is the port's owner:
conquest, federation, and schism move stock by moving the port.

## Interdiction strain

Blockade and sanction strain = per-lane **realized-versus-potential trade value**:
the profitable shipments interdiction prevented, minus smuggling leakage. Measured
at the lane where it happens (no global recomputation, no multi-counting), it feeds
war weariness and relations.

## Sanctions and tariffs

Both live in relations space as Intent-phase outputs. A **tariff schedule** prices
foreign freight per polity/good — collected physically at the **gate the shipment
enters through** (space-and-travel.md §Lanes): the destination-side gate's owner
decides the crossing fee. Your own gate is free; a corp-owned gate takes its toll
instead; a foreign polity's gate charges its customs, once per border crossing.
There is no separate market-boundary tariff site. A **sanction** closes your lanes and
ports to a target's flagged trade entirely — the non-war blockade, expressed in the
same lane-legality machinery war blockades use, differing only in cause. Both are
evadable (smuggling, re-flagging) at margin cost; both generate strain on the
target; and both feed the **trade→relations hook**: trade volume builds relation
warmth, strain and seizures corrode it — the mechanical base of the relations
ladder.

## P1 evidence

- **Legible residue**: the per-good price map (spikes at blockades, gluts at
  cut-off producers) is the most readable economic layer; famines, defaults, and
  tax policy all leave chronicle events; trade-volume shading shows the economy's
  arteries.
- **Inhabitable state**: the player trades against real persistent prices, exploits
  stale-price gaps ahead of the news, runs sanctions for margin, takes loans, and
  feels tariff and legality differences port to port.

## Provided interface

- **Order books** (resting asks/bids per port per good, with owners) — what
  spread runs, upkeep lifts, and the play-clock trading screen consume.
- **Courier contracts** (open/in-transit, with fees and priorities) — the
  job board freight lines live on.
- **Reference prices** (per market per good) — the universal value signal
  (frame cross-cutting interface 2): expansion attractiveness, war-goal value,
  migration pull, and investment siting all read price-derived valuations.
- Polity income streams and true-wealth readout.
- Loan/default state and events.
- Per-lane strain measurements.
- Reserve levels (siege endurance, famine buffering) for war and interior layers.
