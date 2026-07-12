# Time & Logistics Stage 2 (Located Logistics) — Session Prompt

You are starting **Stage 2 of the Time & Logistics slice — located logistics**,
under the lighter protocol in `/CLAUDE.md` (read it first). Stage 1 shipped the
**project ledger**: every piece of in-flight work is a `Project` that consumes a
per-year goods basket and a wage stream over world-years and completes on a
world-year (construction, port raises, gate pairs, hull batches, colony
expeditions, mobilization), scheduled by a per-actor **standing plan** against a
perceived **capability brief**, executed by two mechanical Allocation passes
(advance in-flight, break ground on due entries). What Stage 1 did **not** do is
give goods an address: draws still fall back to a global per-polity reserve pool,
and freight and requisitions still teleport. Stage 2 closes §4b of the spec —
**stock has an address; remote sourcing is shipments over the lane network that
take transit time.**

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules (`unity/ProjectSettings` churn stays
   uncommitted; determinism discipline; the design is the spec).
2. **The spec**: `docs/superpowers/specs/2026-07-11-time-and-logistics-design.md`
   — **§4b (Located goods and shipping orders)** is the Stage 2 mandate; §5's
   Freight-delivery row; §6 determinism/persistence/tests; the "Future passes"
   section (contract economy is §4b's successor — Stage 2 lays its substrate,
   does not build it). Note the Implementation-sequencing block naming this the
   second staged sub-slice.
3. **The Stage 1 ledger** — REQUIRED, the map of what exists and why:
   `.superpowers/sdd/progress.md` (per-task, with carried reds and the flagged
   gaps) and the Stage 1 plan
   `docs/superpowers/plans/2026-07-11-time-stage1-project-ledger.md`.
4. `docs/HANDOFF.md` — current state and the deferred/flagged list.
5. The design tree, already amended to the project model (Stage 2 amends the
   §4b mechanics onto it): `economy/markets.md` (freight — Stage 2 adds transit
   time), `economy/assets-and-investment.md` (local-only draws),
   `frame/controller-contract.md` (the "stockpile targets → depots/reserves"
   line gets its located mechanism), `substrate/infrastructure.md` (Depot).

## What Stage 1 left ready (build on this, don't reinvent)

- **The project machinery** all draws goods **locally** already
  (`ProjectOps.AdvanceAll`, priority-ordered starvation). The seam Stage 2
  changes is *what "local" means*: today a Pass-1 draw falls back to the funder's
  global reserve array (`ProjectOps.cs:242, 264–268`); Stage 2 makes it the
  site's port stockpile + arrived shipments.
- **The capability brief** (`CapabilityOps.BriefFor` → `CapabilityBrief`) is
  assembled per-actor in Perception but is **not yet located** — it sums income
  and generation without lead times. Stage 2 adds per-port stockpile levels so
  the planner sees where goods are and how far the site is from them (spec §2
  "Located stock", §4b "Planner consequence").
- **`LaneNetwork`** (deterministic Dijkstra, from the lane-economics branch) is
  the routing substrate for shipment transit; gate tier already sets lane speed.
- **`MoveFreight`** in the market engine routes goods but delivers instantly —
  Stage 2 converts routed goods into `Shipment` records that arrive in a future
  world-year.
- **The `// stage-2:` marker** left in `ProjectOps.SpawnGatePair`
  (`ProjectOps.cs:131`): the gate pair currently draws its whole basket + funder
  reserves at the **A end**; Stage 2 makes each gate draw locally at its own
  market, shipments covering shortfalls (spec §5 Gate-pairs row).
- **RollChannel** next free is **75** (74 is the view-only AtlasNebula). Any
  piracy-vs-shipments roll takes 75, keyed (step, actor, channel).

## Scope (Stage 2)

- **Located stockpiles replace the global reserve pool.**
  `PolityRecord.ReserveQty/ReserveGrade` (defined `PolityRecord.cs:43,45`) dies;
  stockpiles live **per port, per good, banked**. Every read/write site below
  moves to located stock — grep them fresh to confirm before touching, they are
  the full migration surface:
  - `PolityRecord.cs:43,45` — the array fields themselves (delete).
  - `ArtifactSerializer.cs:223–226` (write) + `1075–1076` (load) — the RES
    serialization; replaced by a versioned **located Stockpiles** block (tests
    migrate, no adapters — greenfield rule).
  - `MarketEngine.cs:446–447` (stockpile-target demand), `705–722` (release
    from reserve to cover a market shortfall), `888–909` (accumulate surplus
    into reserve) — the reserve accumulate/release engine, re-homed per port.
  - `Phases.cs:458–471` — reserve **decay** at Allocation close (per-port now;
    Depot facilities cut decay / raise capacity — the controller contract's
    "stockpile targets → depots/reserves" mechanism).
  - `FleetOps.cs:394–398` — fleet resupply draws from the **home port's** stock.
  - `WarConduct.cs:397` — siege endurance reads the **defender port's**
    provisions stock (already conceptually located — confirm the per-port read).
  - `FederationOps.cs:375–384` — federation merge combines reserves (now: the
    absorbed polity's per-port stocks stay put, ownership transfers).
  - `GraduationOps.cs:200–204` — schism splits reserves to the young polity
    (now: the seceding ports carry their own stock).
  - `ProjectOps.cs:242, 264–268` — the Pass-1 project draw's reserve fallback
    (now: local port stock + arrived shipments only).
- **Shipment records + transit time.** A `Shipment` = origin port · destination ·
  basket · departure year · **arrival year** (route over `LaneNetwork` ÷ lane
  speed, off-lane legs at slow crawl). In-transit goods are conserved state,
  visible on the lane (P1), lost to piracy, stopped by blockade/quarantine. A
  versioned Shipments serialization block; fixed iteration order by shipment id.
- **`MoveFreight` transit conversion**: the market channel's routed goods become
  shipment records arriving in a future year, not instant deliveries.
- **The requisition channel** (new): when the plan schedules a project,
  Allocation raises shipping orders from the polity's own located stockpiles
  toward the site — bypassing price (the state moving its own goods), never
  bypassing time, route, or capacity.
- **Located capability brief**: per-port stockpile levels in `CapabilityBrief`
  so the scheduler prefers sites near supply and pre-positions stock before
  remote groundbreaking.
- **Carried Stage-1 gaps to resolve here**:
  - **Two fine-tick invariance gaps** (flagged in
    `tests/Core.Tests/Epoch/FineTickTests.cs`): the controller commits **one
    founding per decision step** (a finer clock founds more often over the same
    world-time); the Planner's **`Max(1, tier·rate·span)` hull-batch slot floor**
    fires a unit batch every step at fine tick. Both want a world-time
    normalization of the decision/commit cadence.
  - **White-peace project-ownership revert gap**: a white-peace settlement
    reverts ports/facilities but not in-flight projects — route the revert
    through `WarConduct.TransferPort` (ColonyExpedition excluded, as capture is).

**Boundary:** no contract economy (buy/sell contracts fulfilled by freight-line
actors is the *next* pass — Stage 2 lays its substrate: located stock, shipments,
transit; it does not convert market pulls or requisitions into contracts) · no
front supply lines (interdictable convoys to the war front — sequenced with the
contract-economy pass) · no program-style plan entries (the schema reserves the
discriminator; instantiation is later) · hex-tier suite untouched · no new
controller decision points outside Intent.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-t2-located-logistics` from main (after Stage 1 merges); ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-t2-ledger.md`. Never share a checkout
   — take a `git worktree`.
3. TDD: starvation-by-lead-time (a remote site starves at the pace of its last
   delivery); requisition transit (goods leave origin at departure, exist only in
   the shipment, land at arrival); shipment routing determinism; conservation
   **extended to in-transit goods**; per-end gate draws; the two cadence
   normalizations; white-peace project revert. Golden red-window inside the slice,
   re-frozen once at the end.
4. Gates: `dotnet test` green · determinism byte-identity ×2 · hex-tier untouched
   · new goldens frozen once at slice end · the REPL surface works (`efreight` —
   shipments in transit with routes and arrival years — joins `eprojects`/`eplan`;
   `emap works` markers for construction sites and freight).
5. User gates: scope nod · **REPL eyeball** (watch a polity pre-position stock,
   sever a supply lane, read the starvation in the ETAs and the stranded freight)
   · merge decision.
6. Wrap-up: merge · rewrite `docs/HANDOFF.md` · amend the design tree for the
   §4b mechanics (freight transit in markets, located draws, Depot stockpile
   mechanism) · **write the contract-economy kickoff prompt** (the flagged next
   pass, informed by what landed) · push only on say-so.

## Carried from the final review (fix-wave residue, Stage 2 to close)

Landed fixes changed history once (golden re-frozen); these are the loose
threads the fix wave deliberately did NOT chase — pick them up in Stage 2:

- **Founding-link kit is tier-1 sized** — the expedition ships a tier-1 gate
  pair's basket regardless of the link's actual gate tier (no `TierCostFactor`
  applied at dispatch). A long founding link that needs a tier-2/3 gate is
  under-provisioned; the deficit is currently absorbed silently.
- **Failed expeditions keep the shipped kit as sunk goods** — a convoy that
  turns back (hex colonized mid-flight) refunds the colony cost but the gate
  kit drawn at the staging market stays spent. Reconcile when located logistics
  gives goods a transit home.
- **Completion events stamp the span-START year** — `Complete` stages events at
  `state.WorldYear`, i.e. the step's start, not the true (mid-span) delivery
  year a project with a staggered `StartedYear` actually finishes. Fine tick
  narrows this; a real fix stamps the interpolated completion year.
- **Corp standing plans are unwired** — `CapabilityOps.BriefFor` supports corps
  but `CorporationOps.Operate` still builds mechanically (no Planner/StandingPlan
  for corporate portfolios). Corps don't stagger or pack against income yet.
- **`AddConstructionPull` registers full-span demand for near-done projects** —
  a project one year from completion still pulls its whole per-year basket as
  market demand; the pull should taper with remaining years.
- **The FineTick ×2 band absorbs the hull-slot-floor inflation** — the
  `Planner.cs` `Max(1, …)` slot floor still over-produces hulls at fine tick;
  the honesty band is loose enough to tolerate it. Tighten the band once Stage 2
  fixes the floor (persistent fractional-throughput accumulator).

- [ ] Time & Logistics Stage 2 complete
