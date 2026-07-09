# Markets, Wealth & Corporations (Design Pass 2)

Status: **draft — awaiting user review**
Date: 2026-07-09
Parent: `2026-07-09-epoch-sim-master-frame-design.md` (pass 2). Builds on pass 1
(`2026-07-09-substrate-commodities-design.md`) and the space-and-travel amendment.
Product docs: `docs/design/economy/markets.md`, `corporations.md`,
`assets-and-investment.md`.

## 1. Overview

The L1 economic engine: local price adjustment with freight arbitrage, conserved
wealth ledgers with policy-set taxation, simple credit with default events, policy
stockpiles with perishability, per-lane interdiction strain, sanctions/tariffs as
relations machinery, the corporate lifecycle (niche founding → influence →
bankruptcy/nationalization/niche death), and infrastructure
investment/condition/ownership. Resolves the four inherited tickets: sanction
blockades, blockade-strain tuning, provisions stockpiles, and the trade→relations
hook.

## 2. Decisions

- **Local adjustment + arbitrage price engine** (over global equilibrium solve and
  agent order matching): per-(market, good) persistent price drifting toward local
  clearing, rate-limited; freight chases price gradients within lane capacity.
  Never fully clears — gradients are the trade opportunities; perception-friendly
  (P3); drifts smoothly at play clock. *User note recorded*: an agent
  order-matching layer is a candidate play-clock refinement (economic entities
  driving production orders); the price-state interface is designed so agents can
  trade against posted prices without changing the engine.
- **Simple credit** (over cash-only and full finance): loan objects
  (lender, borrower, principal, rate, term); no banks as actors. War loans and
  corporate leverage; default events with reputation/relations fallout and
  collateral seizure. All flows conserved (P4).
- **Corporate founding by persistent profit niche** via the graduation mechanism;
  founding niche stamps character (conglomerate / freight line / combine /
  **cartel** from prohibited niches — no special cartel machinery). Dividends flow
  to host-polity elites → faction wealth (the pass-4 hook that makes corporate
  influence internal politics).
- **Nationalization as the counter-move** to corporate over-power: an Intent act
  with asset seizure, reputation damage, and corporate flight — every option an
  event with fallout.
- **Stockpiles with perishability**: policy-target reserves; provisions decay,
  medicine slowly, durables negligibly. Resolves the provisions-stockpile ticket —
  sieges gain a duration structure (reserves drain before famine).
- **Strain re-grounded per lane**: realized-versus-potential trade value at the
  interdicted lane, minus smuggling leakage. Resolves under-fire (measured where
  trade actually was) and multi-count (one lane, one measurement).
- **Sanctions/tariffs in lane-legality machinery**: sanction = non-war lane/port
  closure to a target's flagged trade (the old `Economy.Passable` extension ticket,
  landed); tariffs = per-polity/good freight pricing at ports. Both evadable at
  margin cost, both strain-generating, both feeding the trade→relations hook
  (volume warms, strain corrodes) — pass 5's mechanical base for the ladder.
- **Facility lifecycle**: Allocation-phase construction from policies (real goods,
  real time, wealth or credit); condition state (decay/damage/repair, output
  scaling); ownership transfer by sale/seizure/nationalization/conquest — all
  conserved ledger events; ruins as residue.

## 3. Testing Strategy

- **Invariants**: credit conservation across all ledger moves incl. interest,
  default, seizure; price positivity and rate-limit bounds; freight never exceeds
  lane capacity; strain ≥ 0 and zero when no interdiction; reserves never negative;
  condition ∈ [0,1]; every ownership transfer leaves a ledger event.
- **Unit tests**: price drift toward clearing under fixed imbalance; band-priority
  clearing (famine before luxury shortfall); arbitrage convergence between two
  connected markets and divergence when the lane cuts; default cascade with
  collateral; siege duration scales with reserves; charter fires on a constructed
  persistent niche and not on a transient one.
- **Acceptance bands** (reference config): price dispersion within/across polities;
  corporate count and lifespan distribution; loan volume and default rarity; famine
  rarity with reserves active; strain nonzero under constructed blockade scenarios.
- **Goldens**: reference-config market/corporate summary snapshot.

## 4. Frame-Consistency Check (master frame §9)

Additions only. Prices (with grade) fill cross-cutting interface 2 as designed; the
controller interface gains corporate policies/acts within its existing shape; the
event grammar gains economic types (charter, default, nationalization, tariff,
sanction) within schema; graduation consumed as specified (charter). Phase order
unchanged — construction executes in Allocation, trade in Markets, sanction acts in
Intent/Resolution. P1/P4 per product doc.

## 5. Deferred / Follow-Up (owners)

- Agent order matching at play clock (user note; game layer, interface-compatible).
- Freight capacity internals, convoy/escort mechanics, piracy risk pricing
  (pass 3 — the fleet model).
- Faction wealth from dividends, elite politics, labor/SoL/legitimacy loops
  (pass 4).
- Enforcement depth of smuggling interdiction; war-time economic warfare doctrine
  (pass 5).
- Currency/monetary depth (multiple currencies, exchange, inflation): consciously
  out — the single abstract credit stands unless a future pass proves need.

## 6. Amendments to Prior Docs

- Prototype `IncomePhase`/`Economy.cs` (3-good flows, TradeBlocked event, system
  value) superseded on implementation by this engine; `Polity.BlockadeLoss`
  semantics migrate to per-lane strain.
- Flow diagram: pass 2 pill → specced; Markets/Allocation phase nodes gain price
  engine/credit wording at next diagram batch; stamp bump.
