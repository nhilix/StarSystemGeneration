# Markets, Prices, Wealth & Credit

The L1 price machinery: how supply meets demand, how value emerges, and how wealth
moves. Runs in the Markets phase at every port market
([../substrate/market-geography.md](../substrate/market-geography.md)), on the goods
and Grade system of [../substrate/commodities.md](../substrate/commodities.md).

## The market step

Per port market, fixed deterministic order:

1. **Supply lands** — facilities sell output into their domain market (stocks carry
   grade).
2. **Demand assembles** — population bands (subsistence near-inelastic,
   standard-of-living moderate, luxury/narcotics elastic), industry inputs, polity
   procurement, fuel for movement — plus **construction pull**: every in-flight
   project sited on the domain adds its per-year basket as standing local demand,
   so a build boom bids up alloys and machinery for the duration of the works and
   that price signal sites the next mine (P5) — plus **re-export demand**: bids
   from arbitrageurs who see outbound gradients, so a transit hub's price is bid up
   even with zero local consumption. Without this term, goods refuse to enter
   markets that don't personally want them; with it, entrepôts emerge.
3. **Freight moves** — three flow generators, all within lane capacity (the
   fleet-capacity interface), all requiring legality at both ends and
   non-sanctioned relations. Freight plans against the prices the previous
   drift posted; the drift then reads what freight actually delivered — an
   import-fed port prices its arrivals, a blockaded one their absence:
   - **Arbitrage freight**: shipments plan by expected profit against
     **perceived prices** (P3 — fresh at one hop, freshness-discounted with
     distance): end-to-end gap × freshness confidence − Σ(fuel + tariffs + risk
     per leg). Single-hop trades dominate, but **bounded multi-hop expeditions**
     emerge wherever a large gradient clears the per-leg costs despite stale
     information — the reach horizon is emergent, not a rule.
   - **Contracts**: concentrated demanders (a shipyard project, a mobilizing
     polity, a besieged reserve) post **procurement contracts** — deliver good G
     to port Z at premium P, escrowed (P4). Contracts propagate over the news
     graph as public events, so directed demand arrives at news speed instead of
     diffusing one market-step per hop. Freight lines live on fulfilling them; at
     play scope they are literally the job board.
   - **Internal logistics**: corporations and polities move goods within their
     own networks at cost — no market transaction at intermediate ports; markets
     see only net buys and sells at the endpoints. Vertical integration and
     military supply lines are this mechanism under different flags. The polity
     half is the **requisition channel**: every Allocation the quartermaster
     compares each project site's coverage (larder + shelf + inbound cargo)
     against its basket over the step plus a lead window, and raises shipping
     orders from the polity's own port stockpiles toward the shortfall —
     bypassing price, never time, route, or capacity. Consumption stores
     (provisions, fuel, ship parts, armaments) keep their target share at the
     source; construction materials were banked to be shipped.
   **Routed goods take transit time.** A haul is a `Shipment` record: origin,
   destination, cargo, and a route over the lane network whose leg years are
   priced at departure (lane speed = a freight base × the gate-tier transit
   multiplier; off-lane legs at slow crawl). Dispatch and each Markets step
   sail the same rule: a closed leg — blockade, quarantine, a dead gate —
   stalls the freight where it floats (the fortress starves at the pace of its
   last delivery), a leg hunted by a raiding band rolls piracy for the years
   sailed under its guns (the loot lands at the band's haven), and arrival
   puts the cargo on the destination shelf — or, for a requisition, in the
   destination larder. A transit that fits inside the current step on an open,
   unhunted-or-lucky route is sub-step blur and delivers within the step.
   In-transit cargo is conserved, visible state (P1).
   Freight is what drags connected markets together; interdiction is what splits
   price zones.
4. **Price adjusts locally** — each (market, good) price is persistent state that
   drifts toward clearing over the step's realized supply: excess demand pushes
   up, glut pushes down, rate-limited. Elasticity derives from the band mix.
   Markets never perfectly clear — persistent gradients *are* the trade
   opportunities.
5. **Clearing & consequences** — consumption satisfies band priority; unmet
   subsistence → famine; unmet standard-of-living → SoL decline (growth,
   legitimacy, migration pressure); unmet industry inputs → facilities
   underproduce.

At the play clock the same state drifts continuously between clearings; an
agent-order-matching layer can later trade against posted prices without changing
this machinery.

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

**Stock has an address.** Every unit of every good is on a market shelf, in a
port's located stockpile, or in transit on a shipment — there is no polity-wide
pool. Stockpiles live per port, per good, banked by procurement toward the
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

- **Contracts** (open procurement orders per port per good) — consumed by freight
  planning, corporate route bids, and the play-clock job board.
- **Prices** (per market per good, with mean grade) — the universal value signal
  (frame cross-cutting interface 2): expansion attractiveness, war-goal value,
  migration pull, and investment siting all read price-derived valuations.
- Polity income streams and true-wealth readout.
- Loan/default state and events.
- Per-lane strain measurements.
- Reserve levels (siege endurance, famine buffering) for war and interior layers.
