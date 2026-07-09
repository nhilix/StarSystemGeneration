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
   procurement, fuel for movement.
3. **Price adjusts locally** — each (market, good) price is persistent state that
   drifts toward clearing: excess demand pushes up, glut pushes down, rate-limited.
   Elasticity derives from the band mix. Markets never perfectly clear —
   persistent gradients *are* the trade opportunities.
4. **Freight chases gradients** — shipments plan by expected profit
   (price gap × quantity − fuel − tariffs − risk), execute within lane capacity
   (the fleet-capacity interface). Cross-border shipments require legality at both
   ends and non-sanctioned relations. Freight is what drags connected markets
   together; interdiction is what splits price zones.
5. **Clearing & consequences** — consumption satisfies band priority; unmet
   subsistence → famine; unmet standard-of-living → SoL decline (growth,
   legitimacy, migration pressure); unmet industry inputs → facilities
   underproduce.

At the play clock the same state drifts continuously between clearings; an
agent-order-matching layer can later trade against posted prices without changing
this machinery.

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

Depots and polity strategic reserves hold stocks against policy targets.
**Provisions reserves buffer sieges and famines**: a besieged domain draws down
reserves before starving, giving sieges a duration structure — rich prepared
polities endure, poor ones break fast. Perishability: provisions decay in storage,
medicine slowly, durables negligibly — reserves are a real cost, not free
insurance.

## Interdiction strain

Blockade and sanction strain = per-lane **realized-versus-potential trade value**:
the profitable shipments interdiction prevented, minus smuggling leakage. Measured
at the lane where it happens (no global recomputation, no multi-counting), it feeds
war weariness and relations.

## Sanctions and tariffs

Both live in relations space as Intent-phase outputs. A **tariff schedule** prices
foreign freight per polity/good at your ports. A **sanction** closes your lanes and
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

- **Prices** (per market per good, with mean grade) — the universal value signal
  (frame cross-cutting interface 2): expansion attractiveness, war-goal value,
  migration pull, and investment siting all read price-derived valuations.
- Polity income streams and true-wealth readout.
- Loan/default state and events.
- Per-lane strain measurements.
- Reserve levels (siege endurance, famine buffering) for war and interior layers.
