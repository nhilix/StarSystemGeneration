# Slice K6 Kickoff — Session Prompt

You are starting **Slice K6 (The economy surfaces)** — a post-roadmap
atlas extension under the lighter protocol in `/CLAUDE.md` (read it
first). The contract economy merged to main (2026-07-12): markets are
order books with named parties, internal logistics are courier
contracts, freight is spread runs / couriers / war convoys, and war
fronts run supply lines that interdiction can cut. The Inspector REPL
grew the eyeball surfaces (`ebook`, `econtracts`, `emap trade`,
`efreight` purpose tags); **K6 brings those same surfaces into the Unity
atlas** — a new trade lens on the rail and the book/contract story in
the panels — so the economy the sim now runs is the economy the atlas
shows.

**Sequencing**: K6 assumes **K5 (system stage & closeout) is merged**.
If K5 is still in flight, take a worktree and branch from main AFTER its
merge — the lens rail, legend, and dock are shared chrome.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules (`unity/ProjectSettings` churn
   stays uncommitted, always).
2. **The contract-economy spec** —
   `docs/superpowers/specs/2026-07-12-contract-economy-design.md`
   (the model you are surfacing; its "Implementation amendments" block
   lists what shipped vs what deviated) and the rewritten
   `docs/design/economy/markets.md` (the order-book step, couriers) +
   `docs/design/interpolity/war.md` §Front supply lines.
3. **The CE ledger** —
   `docs/superpowers/plans/2026-07-12-slice-ce-ledger.md` (C17's carried
   debt list — several items are atlas-relevant caveats, e.g. relay bids
   kept, fee-blind courier ranking).
4. The K5 ledger (`2026-07-12-slice-k5-ledger.md`, once it exists) and
   the K4 ledger `2026-07-12-slice-k4-ledger.md` — the SimHost two-event
   contract (`Loaded` vs `TimeChanged`) every new surface must ride, and
   the worktree traps.
5. The K3 ledger `2026-07-12-slice-k3-ledger.md` — SelectionModel /
   InspectorDock / PanelViews / LegendQuery architecture you EXTEND, and
   the drift-proof legend pattern (LegendDriftTests).
6. `docs/HANDOFF.md` — current state; living diagram
   `docs/diagrams/unity-atlas-design.html` (§8 lens rail, §9 panels —
   republish to its stable URL with the new rows).

## What the repo already gives you (build on this, don't reinvent)

- **Sim state**: `SimState.Orders` (`MarketOrder`: side, owner, port,
  good, limit, qty, grade, escrow), `SimState.Couriers`
  (`CourierContract`: poster, route, cargo basket, fee escrow,
  priority War/Normal, status, fulfiller, shipment id), `Shipment`
  (channel Freight/Requisition; a courier's rider found via
  `CourierOps.OfShipment` — see how `Repl.cs` RenderBook/RenderContracts
  and the efreight purpose tag derive everything).
- **Book reads**: `BookOps.AskQty/AskGrade/BestAsk/BidDepthAbove`;
  `Market.Price` is the reference readout. `MarketPanel` (Core.Atlas)
  already reads ask depth/grade since the CE merge — extend, don't fork.
- **The trade lens's math**: `src/Inspector/EpochMapView.cs`
  `TradeCells` — per live lane, the steepest ACTIONABLE reference
  gradient (asks at the cheap end; unfiltered it saturates — keep the
  filter). Port the derivation into a `src/Core/Atlas` lens query in the
  K2 pattern; the Inspector keeps its copy or calls the query (prefer
  the query — one derivation, zero drift, the K3 rule).
- **War-front reads**: `FleetOps.NearestOwnedPortId` (forward depot),
  war-stationed postures (Blockade/Expedition), `WorldEventType.
  CargoSeized = 409` in the chronicle, `War.InterdictionReachHexes`.
  The interdiction presence map is private to `ShipmentOps` — expose a
  read-only query if the war lens shades contested lanes; do NOT
  duplicate its rules.

## Scope — what K6 delivers

1. **TRADE lens on the rail**: per-lane spread intensity (the actionable
   gradient), glyph cell in the authored atlas, LegendQuery entries from
   the lens constants (drift-proof), LegendDriftTests extended.
2. **Order-book panel** (`ebook` parity): port click → the book —
   resting asks/bids per good with owners, qty, grade, limit vs
   reference; reachable from the Market panel (same dock, K3 DockKit).
3. **Contracts panel** (`econtracts` parity): the courier job board —
   open/in-transit contracts with route, cargo, fee, priority (WAR
   called out), fulfiller; a TopBar drawer or dock panel (match K3's
   THREADS/STATS pattern; your call at the eyeball).
4. **Freight purposes on the map**: the works/traffic story
   distinguishes courier / **war convoy** / spread run / state haul
   (tint or glyph — the atlas visual grammar, not the PoC board), and
   ShipmentPanel gains the purpose + rider-contract link.
5. **War-supply readout**: WarPanel (or FleetPanel) names a deployed
   fleet's forward depot; contested-lane shading on the war lens if the
   presence query lands cheaply. CargoSeized events already ride the
   chronicle — check they read well in the event surfaces.
6. **Diagram + HANDOFF + next kickoff** at wrap-up, as ever.

## Boundary — what K6 does NOT do

- **Zero sim behavior**: read-only queries only; the golden must not
  move (K-slice invariant — assert it in the gate).
- No play-clock trading UI (the player-facing order flow is the live
  game's pass).
- No perceived-books work (multi-hop actor runs over stale books are the
  next ECONOMY slice, not an atlas slice).
- The credit-loop equilibrium flag (all entered polities deep-negative,
  worst ≈ −400k at seed 42 — CE did NOT close it) is sim-side: carry it,
  don't chase it here.

## Gates (all mechanical, all mandatory)

`dotnet test` green with the golden untouched · determinism byte-identity
· EditMode suite green (LegendDrift + new panel tests) · AtlasSmoke
renders every lens including TRADE · the REPL surfaces still match their
Core queries (parity is the point). Three user checkpoints only: scope
nod · eyeball (load seed 42, click a hub port, read its book; open the
job board; TRADE lens against `emap trade`; find a war convoy) · merge.

## Worktree / environment traps (verified through K4 — reread the K4
ledger's list)

`unity/Packages/manifest.json`, `packages-lock.json`, `src/Core/csc.rsp`
are gitignored — copy them into a fresh worktree BEFORE Unity batch
runs; batchmode can't run while an editor holds the project; the editor
MCP bridge starts revoked per-project; goldens are CRLF on disk.
