# Slice AC — the atlas catch-up — design (2026-07-21)

Status: approved (brainstormed with the user 2026-07-21). **Renamed from
"K6 broadened" at user direction** — this SUPERSEDES the original K6 kickoff
(`2026-07-12-slice-k6-kickoff-prompt.md`, "economy surfaces" only), which
should not be run; its scope is Phase 2 of this slice. One mega-slice, four independently-mergeable phase gates,
each with its own short editor eyeball (user decision — atlas work is
inherently visual; a grammar misstep in phase 1 must not survive into phase 4).

## Motivation

The atlas was completed through Slice K5 (2026-07-12) and last touched by
Slice L's placement fix. Since then four bodies of sim state landed that the
atlas has never surfaced: the **contract economy** (CE: order books, courier
contracts, freight purposes, war supply), the **currency chain** (CU-1..4 +
BF: a `Currency` per polity, FX rates, banks with reserves and backing
ratios, monetary credibility feeding federation), **domain expansion** (DX:
satellite workings, outposts, frontier graduation — state DX explicitly
produced "so the atlas pass is pure rendering"), and **off-lane locality**
(L2: off-lane routing with detection, patrol coverage falloff, population
body/hex addresses). The sim the atlas shows is four mega-slices behind the
sim that runs. This slice closes the whole gap in one coherent pass over the
shared chrome (lens rail, legend, dock, panels), rather than four separate
touches.

Scope decisions fixed by the user:

- All four surface groups in one slice; **one mega-slice, four phase gates**
  (the L/DX pattern), ordered so each eyeball builds on a stable base.
- **Per-phase eyeballs** — four short editor looks, not one consolidated gate.
- Scope = new-state surfaces **plus free-standing cheap debt** (the `OrbitRef`
  compile verification open since Slice L; event-surface readthrough of new
  event types). Readability deep-dives, labels-on-stage, timeline branch UI
  stay deferred.

## Invariants (the K-slice contract, absolute)

- **Zero sim behavior.** Read-only queries only. The golden is byte-untouched
  — asserted mechanically in every phase gate, not assumed.
- **REPL parity via shared queries** (the K3 rule): every atlas surface reads
  the same `src/Core/Atlas` query its REPL twin reads — one derivation, zero
  drift. Where the Inspector holds a private derivation today (see Phase 2's
  `TradeCells`), the derivation moves INTO Core.Atlas and the Inspector calls
  it.
- Legend entries derive from lens constants via `LegendQuery`;
  `LegendDriftTests` extend to every new lens/mode.
- `unity/ProjectSettings` churn stays uncommitted. Every new `src/Core` file
  gets a two-line `.meta` with a fresh guid.

## §1 Phase 1 — Domain interior (DX surfaces)

The map grammar change comes first; every later phase draws over it.

- **New Core.Atlas query — `DomainInteriorQuery`** (name indicative): for a
  polity/domain scope, the worked hexes (derived from `Facility.Hex` across
  the domain, the satellite workings DX scattered), the outposts
  (`SimState.Outposts` — today zero reads anywhere in `src/Core/Atlas`), each
  with name, parent port, resident presence, founding year, and candidacy
  status (interior vs. frontier per DX's `G` gate), plus graduated flag.
  `domain <port>` in the REPL is the parity surface.
- **`DomainFieldLayer` gains interior structure**: worked hexes and outpost
  hexes read as *filled/inhabited* against the plain domain glow — the domain
  stops being a uniform gradient and shows its skeleton. Rendering stays in
  the atlas visual grammar (space, glows, billboards — never a painted hex
  board).
- **Outpost marks**: named marks in the port-mark family, visually subordinate
  (smaller, no service ring, no market affordance). Tooltip carries name +
  candidacy status; selection routes to the parent port's panels plus an
  outpost row (no new panel type — an outpost is a registry record, not an
  actor, and the panel treatment matches).
- **Events**: `OutpostFounded = 314` (payload exists) and graduation (rides
  the port-established path) read correctly in the news lens / chronicle
  surfaces.
- **SystemStage needs nothing**: `SystemQuery.At` already filters facilities
  per hex (`SystemQuery.cs:114`), so satellite hexes' works already render on
  their own stage. Verify, don't rebuild.

**Eyeball 1**: load seed 42 late-epoch, find a DX-expanded domain — worked
hexes and a named outpost visibly structure the domain; a graduated outpost
reads as a port with history.

## §2 Phase 2 — Economy/trade (the original K6 scope)

- **TRADE lens on the rail**: the Inspector's `EpochMapView.TradeCells`
  derivation — per live lane, the steepest ACTIONABLE reference gradient
  (asks at the cheap end; the saturation filter is load-bearing, keep it) —
  ports into a Core.Atlas `TradeLens`; the Inspector switches to calling the
  query. Glyph cell in the authored atlas grammar; `LegendQuery` entries from
  the lens constants; `emap trade` is the parity surface.
- **Order-book panel** (`ebook` parity): port → the book — resting asks/bids
  per good with owners, qty, grade, limit vs. reference (`BookOps` reads,
  extending `MarketPanel`'s existing ask depth/grade — extend, don't fork).
  Reachable from the Market panel in the same dock (K3 `DockKit`).
- **Contracts panel** (`econtracts` parity): the courier job board —
  open/in-transit contracts with route, cargo, fee, priority (WAR called
  out), fulfiller; drawer vs. dock panel decided at the eyeball (the K3
  THREADS/STATS precedent).
- **Freight purposes on the map**: traffic rendering distinguishes courier /
  **war convoy** / spread run / state haul (tint or glyph, grammar-consistent);
  `ShipmentPanel` gains the purpose + rider-contract link
  (`CourierOps.OfShipment`).
  - **AMENDED (AC2.F2, user decision 2026-07-22):** in-flight marks only
    ever show boundary-surviving off-lane crawls — lane-borne shipments
    launch and arrive inside one 25-year step, so `state.Shipments` is
    empty at most keyframes. The moving-economy read is carried by
    **recent-flow trails** instead: courier/war-convoy launches captured
    at step time by a passive observer (`SimState.ShipmentObserver`,
    threaded per step by `EpochEngine`), held in-memory beside each
    TimeMachine keyframe (never serialized), drawn on the works lens as
    attenuated origin→dest strokes subordinate to live marks and lane
    strokes. REPL twin: `eflows`.
- **War-supply readout**: War/Fleet panel names a deployed fleet's forward
  depot (`FleetOps.NearestOwnedPortId`); contested-lane shading on the war
  lens ONLY if the interdiction presence read exposes as a cheap read-only
  query — never duplicating `ShipmentOps`' rules.

**Eyeball 2**: click a hub port, read its book against `ebook`; open the job
board; TRADE lens against `emap trade`; find a war convoy.

## §3 Phase 3 — Currency & banking (CU/BF surfaces)

- **Currency zones as a mode of the existing polity/domain rendering** — tint
  by currency id, not a bespoke new subsystem. After CU-3 consolidations,
  federations/unions visibly share a color; a `Retired` currency's zone
  disappears. Legend entries drift-tested like any lens.
- **PolityPanel monetary block**: currency name, numeraire/FX rate with
  recent drift, bank reserve, backing ratio (`Reserve ÷ ClaimOnState`) — the
  REPL's polity currency line (BF's claim surface) is the parity target.
- **MarketPanel prices state their currency** — a reader should never wonder
  which unit a price is in, now that units genuinely differ.
- **Federation context**: RelationsPanel names monetary credibility where
  CU-4's term participates (BackedShare-derived, read-only, same measures —
  no new derivation).

**Eyeball 3**: currency mode shows zones; a union shares one tint; a polity's
panel reads reserve/backing/rate coherently against the REPL line.

## §4 Phase 4 — Off-lane, events, debt sweep (L2 + cheap debt)

- **Off-lane crawls render distinctly**: an off-lane shipment (no lane path)
  draws as a direct hex-path crawl, dashed/attenuated against lane traffic;
  `ShipmentPanel` shows off-lane status + detection risk context.
- **Patrol coverage readout** on FleetPanel: coverage falloff from dock
  (L2's `OrbitGeometry`-based falloff), read-only.
- **Event readthrough**: `CargoSeized = 409` off-lane variants, settle/
  outpost/graduation events — all read well in news/chronicle surfaces; fix
  copy/payload rendering gaps found, nothing more.
- **Cheap debt**: the `OrbitRef` alias (`SystemStage.cs`, added Slice L Task
  1) finally compile-verified in a real editor session; `AtlasSmoke` extended
  to render every lens including TRADE and the currency mode.

**Eyeball 4**: blockade a lane (existing sim behavior), watch freight elect a
visible off-lane crawl; check the event feed reads the new world cleanly.

## Gates (every phase, all mechanical, all mandatory)

`dotnet test` green with the golden asserted byte-untouched · determinism
byte-identity · EditMode suite green (`LegendDriftTests` + new panel tests) ·
`AtlasSmoke` renders every lens · REPL surfaces still match their Core
queries · the phase's editor eyeball. Standard protocol otherwise: scope nod ·
per-phase eyeballs · merge decision; one fable whole-branch review + one fix
wave before merge; living diagram republished at wrap-up
(`docs/diagrams/unity-atlas-design.html` §8/§9 rows).

## Boundary — NOT this slice

- No sim behavior of any kind (the invariant, restated as scope).
- No play-clock trading UI; no perceived-books work (economy slices, not
  atlas slices).
- Per-lens readability deep-dives, labels-on-stage, timeline branch
  switch-back UI, keyframe memory — all stay filed.
- The flat/sparse-economy pass (DX's standout sim follow-up) is untouched:
  this slice renders the world as it is, including its flatness. If the
  domain-interior surfaces make the flatness *visible*, that is evidence for
  that pass, not scope for this one.

## Forward roadmap

- The flat/sparse-economy pass will change what these surfaces show (richer
  domains, real freight) — the surfaces are built against queries, so they
  ride along free.
- Localized goods → real freight (DX follow-up) would give the freight
  purposes real hauling stories; nothing here blocks it.
- A later readability/polish pass owns the deferred K5 items.

## Provided interface

- `DomainInteriorQuery` (worked hexes, outposts + candidacy, graduated flags)
  — consumed by `DomainFieldLayer`, outpost marks, tooltip, `domain` REPL.
- `TradeLens` in Core.Atlas — consumed by the rail and `emap trade`.
- Order-book and contracts panel queries (extending `MarketPanel`/`BookOps`
  reads) — consumed by dock panels and `ebook`/`econtracts`.
- Currency-zone mode + PolityPanel monetary block reads (currency, rate,
  reserve, backing ratio) — consumed by the map mode, panels, REPL parity
  lines.
- Off-lane/patrol read surfaces on Shipment/Fleet panels.
- Extended `LegendQuery`/`LegendDriftTests`/`AtlasSmoke` coverage for every
  new lens/mode.
