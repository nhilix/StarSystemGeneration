# Freight Flows Amendment — Multi-Hop Trade, Contracts & Internal Logistics

Status: **draft — awaiting user review**
Date: 2026-07-09
Parent: `2026-07-09-markets-wealth-corporations-design.md` (amends its freight
model). Product docs touched: `docs/design/economy/markets.md`,
`corporations.md`, `frame/controller-contract.md`.

## 1. The problem (user review finding)

Pure adjacent-gradient freight cannot serve concentrated distant demand: a
shipyard needing ore across five markets waits ~five market-steps for the price
signal to diffuse hop-by-hop, and goods refuse to transit markets with no local
consumption (nothing bids them in). Multi-stage chains (ore → alloys →
components → yard) had no explicit flow mechanism.

## 2. Decisions

- **Re-export demand**: market demand assembly includes bids from arbitrageurs
  seeing outbound gradients — transit hubs are bid up without local consumption;
  entrepôts emerge. (Bug fix to the clearing model.)
- **Bounded multi-hop expeditions**: arbitrage freight plans against *perceived*
  prices (fresh at one hop, freshness-discounted with distance): end-to-end gap ×
  confidence − Σ per-leg costs. The reach horizon is emergent from perception
  decay and leg costs, not a rule. This replaces the earlier "freight uses true
  lane-endpoint prices" note — freight now honestly runs on perception like every
  other behavior (P3 strengthened, not patched).
- **Procurement contracts**: concentrated demanders post escrowed
  deliver-G-to-Z-at-premium-P orders (a controller act for polities and
  corporations); contracts propagate over the news graph as public events —
  directed demand arrives at news speed rather than diffusing per-hop. Freight
  lines' business model; the play-clock job board.
- **Internal logistics**: corporations and polities move goods within their own
  networks at cost, with no market transaction at intermediate ports (markets see
  net endpoint activity). Vertical integration and military supply lines are the
  same mechanism.

Chains therefore form from three generators — ambient arbitrage (with entrepôt
pricing), directed contract pulls, and owned internal chains — while facility
siting on price signals migrates processing stages toward advantaged nodes over
epochs.

## 3. Consistency

P3: both new mechanisms run on the existing perception model (freshness-discounted
prices; contracts as news events). P4: contracts escrow; internal transfers are
conserved moves. P1: contract boards and expedition routes are inspectable; the
job board is play-scope content. Costs bounded: contract matching is a small pass;
expedition planning is horizon-limited.

## 4. Testing (added to the pass-2 suite)

Entrepôt convergence on a constructed A–B–C chain (B has no local demand);
contract fulfillment beats diffusion latency on a constructed distant-yard
scenario; internal-logistics transfers leave no intermediate market trace;
expedition horizon shrinks with staleness and grows with gradient size.
