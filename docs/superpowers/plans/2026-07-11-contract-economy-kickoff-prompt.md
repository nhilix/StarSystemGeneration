# Contract Economy — Session Prompt

You are starting **the contract-economy slice**, under the lighter protocol in
`/CLAUDE.md` (read it first). The Time & Logistics pass is complete: Stage 1
(the project ledger — every duration is a `Project` consuming a per-year basket
over world-years) and Stage 2 (located logistics — stock lives per port in
`Port.StockQty/StockGrade`, remote sourcing is `Shipment` records over
`LaneNetwork` with priced leg years, blockade stalls, piracy on channel 75, and
a requisition channel in Allocation). What the economy still is NOT: fulfilled
by actors. Freight is routed by the ENGINE (`MarketEngine.Arbitrage` decides
what moves where), and requisitions are engine logistics too. This slice makes
logistics something actors DO: **standing buy/sell contracts, fulfilled by
freight-line corporations chasing the price gradient** (spec "Future passes",
P5 — profit walks the goods).

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules.
2. **The spec seed**: `docs/superpowers/specs/2026-07-11-time-and-logistics-design.md`
   §"Future passes" (the contract-economy paragraph is the mandate) and §4b
   (the substrate this rides on). NOTE: this slice needs its own **design
   pass first** — a brainstorm + spec like the time-and-logistics one; the
   paragraph is a direction, not a design. Budget a spec session before code.
3. `docs/design/economy/markets.md` — §"Freight moves" already NAMES the
   target model (arbitrage on perceived prices, **procurement contracts
   escrowed and propagating over the news graph**, internal logistics);
   `docs/design/frame/controller-contract.md` — "post procurement contract"
   is already a polity act, and the corporation contract already lists
   route bids + a standing investment plan.
4. `docs/HANDOFF.md` — current state.
5. The stage-2 ledger for the carried seams:
   `docs/superpowers/plans/2026-07-11-slice-t2-ledger.md`.

## What stage 2 left ready (build on this, don't reinvent)

- **`ShipmentOps`** (`src/Core/Epoch/ShipmentOps.cs`): `Dispatch`/`DispatchVia`
  create priced shipments; the private `Sail` is THE one movement rule
  (closed-leg stalls, piracy channel 75, delivery); `PlanRoute` prices any
  port pair (live-lane Dijkstra or off-lane crawl). Contracts should FULFILL
  into dispatches, not reinvent movement.
- **`MarketEngine.Arbitrage`** is the engine-routed channel contracts replace
  (or demote to a residual): it settles costs at departure and pays the
  exporter as destination supplier via `SupplyRecord` at arrival — the
  escrow/payout pattern a contract object needs already exists there.
- **`ShipmentOps.RaiseRequisitions`** is the state-logistics channel that
  becomes polity-posted contracts; its flagged approximations are this
  slice's real work: sourcing is port-id-ordered (should be bid/nearest),
  per-order capacity is `min lane capacity × window` (should be actors'
  hulls competing), and it runs free (should cost freight rates paid to the
  hauler — `PayHaulers` in MarketEngine shows the fee flow).
- **Freight-line corps** (`CorporationOps`, Niche.Freight) invest in hulls
  and post them on gradient lanes; `FleetMath`/`FleetOps.PostedCapacity` is
  the capacity they'd bid with. Corp planning is deliberately minimal (they
  pack ONE build at a time against income — `InvestFacilities`); this slice
  is where corps get their real `StandingPlan` if the design wants it.
- **News graph** (`NewsOps`): contracts propagating "at news speed" have a
  working carrier — pulses with per-polity delivery years.
- **RollChannel next free: 76.** 73 is retired, never reuse. 75 is shipment
  piracy.

## Carried seams this slice should close (from the stage-2 ledger)

- Requisition sourcing nearest-first / bid-based; real shared capacity
  competition between the market and requisition channels.
- **Front supply lines** (war-side §4b deepening): interdictable convoys to
  the front — sequenced WITH this pass per the spec.
- Corp standing plans (portfolio scheduling) if the design calls for it.
- Piracy loot without a Markets scratch deposits plain (no supplier credit)
  — unify when contracts restructure deposits.
- Project cancellation still stages no chronicle event (no fitting
  WorldEventType) — consider adding one if this slice touches event types.

## Boundary sketch (confirm at the scope nod after the design pass)

Contracts as records (posted, escrowed, expiring, chronicled) · fulfillment
by freight actors against posted capacity · requisitions and the stockpile
procurement become posted contracts · Arbitrage demoted/retired · front
supply lines if the design includes them · hex-tier suite untouched ·
determinism discipline (contract matching = deterministic ordered math; any
new roll takes channel 76+).

## Session shape (per /CLAUDE.md)

Design pass (brainstorm → spec, user-approved section by section) → scope
nod → branch `slice-<x>-contract-economy` → TDD → one whole-branch review +
one fix wave → golden re-freeze once → REPL surface (`econtracts`?) → three
user gates only.

- [ ] Contract economy complete
