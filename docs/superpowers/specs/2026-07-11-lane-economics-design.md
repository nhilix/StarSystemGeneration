# Lane Economics — Gates, Reach, and the Anti-Web Builder

**Date:** 2026-07-11 · **Status:** approved design, awaiting slice scheduling
**Amends (in the implementing branch):** `docs/design/frame/space-and-travel.md`
(§Lanes), `docs/design/substrate/infrastructure.md` (facility table),
`docs/design/economy/corporations.md` (freight-line acts),
`docs/design/economy/markets.md` (tariff collection point).

## Problem

Observed in epoch runs: (1) clustered domains build a lane between **every**
in-range port pair — a dense all-pairs web where A→B→C→D chains should carry
the traffic and make B and C into hubs; (2) domains build **no** lanes to
foreign ports, so the map fragments into self-isolated islands.

Root causes in the current code:

- `Phases.cs BuildLanes`: greedy nearest-missing-pair loop, **flat**
  `Expansion.LaneCost = 25` regardless of distance, no redundancy check — over
  enough epochs every in-range pair saturates into a complete graph.
- Reach is generous and port-derived: `InterPortRangeBaseHexes = 18` +
  `8/tier`, so a tier-3 port reaches 34 hexes — in a cluster, everything
  pairs with everything.
- Cross-owner lanes require a TradePact treaty rung and only polities build;
  corporations post freighters but never build lanes, so the freight-line
  archetype ("unserved profitable lanes", corporations.md) has no founding act.

What already works and is **not** rebuilt: freight is per-lane arbitrage with
import-parity price chaining (`MarketEngine`), so on a tree-ish network A↔D
trade genuinely flows through B and C, paying their haulers and markets per
hop. Multi-hop emerges; only the builder and the cost model change.

## Design

### 1. The Gate facility

New `Substrate.InfraTypeId.Gate`, family **Support**, sited only at port
systems. Tiered 1–3 like every facility; built from real goods (Alloys,
Machinery; Exotics enters at tier 3) drawn from the port's market; owned by
its builder — polity **or corporation**.

**One gate serves exactly one lane.** Gates draw from a per-port gate-slot
budget separate from the industrial facility cap:
`Infrastructure.GateSlotsPerPortTier` (default **2**) — a tier-1 port hosts 2
gates, tier-3 hosts 6. Lane degree is therefore physically capped by port
investment: hub ports must grow before they fan out, and all-pairs webs are
geometrically impossible.

### 2. A lane is a linked gate pair

`Lane` keeps id, canonical ordered port pair, built year, and quarantine, and
gains `GateAId`/`GateBId` (facility ids). A lane is **live** iff both gates
exist and are functional.

- **Half-built lanes** are a real, visible state: one gate standing, the far
  side unfunded. Atlas `LaneLens` and REPL views render them.
- **War**: raiding a gate severs the lane without touching the port; conquest
  seizes gates with the system — the existing facility seizure/destruction
  machinery applies unchanged.
- **Reach** comes from gates, not ports: max lane length =
  `GateReachHexes[min(tierA, tierB)]`, defaults **8 / 16 / 28** hexes.
  `Infrastructure.InterPortRangeBaseHexes` / `InterPortRangePerTierHexes` and
  `Expansion.LaneCost` are **deleted** (knob registry updated). Long corridors
  need tier-3 gates at both ends; facility tier costs are already superlinear,
  so lane cost rises steeply with length for free. The Astrogation bonus
  (slice G) now adds hexes to the gate reach table's result.
- **Capacity and transit speed** derive from gate tiers, not port tiers:
  `LaneMath.Capacity/TransitSpeed` take the two gates; the weaker gate bounds
  both.

### 3. Builder eligibility (the anti-web core)

A candidate port pair is eligible iff, in order:

1. **Reach** — hex distance ≤ reach of the gate tier the builder is pricing.
2. **Slots** — both ports have a free gate slot.
3. **Detour rule** — no existing live-lane path between the two ports, or the
   shortest network path exceeds `Expansion.DetourFactor` (default **1.8**) ×
   direct hex distance. Shortest path = BFS/Dijkstra over live lanes weighted
   by hex length; deterministic id-order tie-breaks (P6).
4. **Congestion exception** — rule 3 is waived when **every** lane on that
   shortest path has run saturated (`LaneCapacityUsed / LaneFleetCapacity ≥
   Expansion.ExpressSaturationFloor`, default **0.9**) for
   `Expansion.SaturatedEpochsForExpress` (default **3**) consecutive epochs.
   Each lane carries a serialized `SaturatedEpochs` counter, updated in the
   Markets phase. Busy corridors earn an express bypass; quiet ones never do.

Polity builder: replace the nearest-missing-pair greed with **cheapest
eligible pair first** (lowest total gate cost for the tier the distance
demands; tie: nearest, then lowest ids), built while the development treasury
affords the gates. Own-to-own and own-to-pact-partner pairs as today, both
through the same eligibility rules.

Emergence seeding: homeworld ports establish with tier-1 gates for their
initial neighbor links so the early economy is not gate-starved.

### 4. Cross-domain lanes and the fee model

**Corporations get the founding act.** A freight-line corp evaluates port
pairs across domain borders: price-gap profit on tradeables above a
threshold, both host polities non-hostile (not at war; relation above a
floor — **no treaty required**), pair passes builder eligibility. The corp
charters in both polities, pays for and owns **both** gates.

**Fees are a per-gate ownership test at crossing time:**

| Gate owner vs. shipper | Fee |
|---|---|
| Shipper owns the gate (corp hauling through its own gates) | free — vertical integration is the efficiency play |
| Corp-owned gate, any other shipper | **gate toll** (`Economy.GateTollRate`, share of freight value) — new conserved trader→gate-owner flow (P4) |
| Polity-owned gate, same-polity freight (shipper = the exporting merchant's polity) | free — load/unload is already taxed at the endpoint markets; no transit double-tax |
| Polity-owned gate, foreign freight | **customs**: the *existing* per-good `TariffSchedule` × `RelationsOps.TariffFactor` (pact cut applies), collected at the **entering** gate |

Customs is a relocation of the existing tariff flow to a physical collection
point, not a new tax — a shipment crossing one border pays once, at the gate
it enters through; the market-side tariff site is removed so nothing is
charged twice. "You can take your business outside our polity, but it isn't
free."

### 5. Risk scales with length

Piracy target-weighting and blockade/interdiction strain gain a lane-length
multiplier (longer lane = more exposed hexes). A weighting factor in existing
ops; no new state.

### 6. Migration

Greenfield (no adapters): `BuildLanes` rewritten, serializer extended for
gate ids + saturation counters, old goldens die and are re-frozen once at
slice end. Existing dense-web artifacts are not preserved.

### 7. Verification

- Hex-tier (Phase-1) suite untouched and green throughout.
- Determinism: pathfinding in id order; counters serialized; byte-identity
  for same config.
- Unit tests: detour rule, slot caps, reach table, congestion trigger, fee
  table (all four rows), corp route evaluation, half-built liveness.
- Topology test: a clustered many-port domain lands at mean lane degree well
  under all-pairs (assert against a fixed seed).
- REPL surface: lane/port views show gate tiers, owners, half-built state,
  toll/customs flows; atlas `LaneLens` renders half-built gates.

## New / changed knobs

| Knob | Default | Replaces |
|---|---|---|
| `Infrastructure.GateSlotsPerPortTier` | 2 | — |
| `Infrastructure.GateReachTier1/2/3Hexes` (three scalar knobs — KnobRegistry binds scalars) | 8 / 16 / 28 | `InterPortRangeBaseHexes`, `InterPortRangePerTierHexes` |
| `Expansion.DetourFactor` | 1.8 | — |
| `Expansion.ExpressSaturationFloor` | 0.9 | — |
| `Expansion.SaturatedEpochsForExpress` | 3 | — |
| `Economy.GateTollRate` | 0.05 | — |
| *(deleted)* | — | `Expansion.LaneCost` |

Gate build costs use the standard facility real-goods cost tables (new Gate
row, superlinear by tier).
