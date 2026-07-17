# Slice DX kickoff — domain hex expansion (a starport's region comes alive)

You are opening **Slice DX**: a starport's domain stops being a glow drawn
around a point and becomes a living region — corporations and population
settling its hexes on their own books, while the polity's treasury still
decides the large moves. This is the **adjacent-hex spillover** thread (Slice L
follow-up #1), deferred three times under a too-narrow "let a facility overflow
to the next hex" framing, now generalized into domain aliveness. The design is
approved and committed; this slice implements it **directly from the spec** per
the lighter protocol — **no separate writing-plans document.** The committed
task ledger (`docs/superpowers/plans/2026-07-16-slice-dx-ledger.md`, which you
create) is the resumability record.

Branch `slice-dx-domain-expansion`. Worktree `.worktrees/slice-dx-domain-expansion/`.

## READY, but deliberately NOT auto-started — confirm the hold at the scope nod

The user is holding new parallel dev until the in-flight structural slice
(**CU-3** currency consolidation, and whatever else in the live
MC→BF→CU-3→CU-4 chain is on a worker right now) **stabilizes**, to avoid two
sessions fighting over `main`. **Part of your scope nod is confirming with the
user that the hold is lifted** and DX is cleared to start. Do not branch before
that confirmation — surface it as the opening line of the nod, alongside the
scope summary below.

## Read first, in order

1. **`docs/superpowers/specs/2026-07-16-domain-hex-expansion-design.md`** — the
   authoritative design and the spec for this slice. Read it whole. The three
   phase gates, the two-scales-stay-separated principle, the anti-clustering
   guarantee, the determinism/conservation invariants (§5), the boundary (§8),
   and the three "open implementation choices" you decide at plan time (the
   settle-election machinery, `G`'s exact parameterization, the hauling-cost
   proxy) all live here. This slice implements it directly — there is no
   intermediate plan doc.
2. `docs/superpowers/specs/2026-07-14-locality-mega-slice-design.md` — the
   machinery DX consumes: `SettledSystems`, `BodyRef`, `OrbitGeometry`,
   `BodySiting.PortBody`, distance-weighted staffing. DX builds on top of
   these; it does not reopen them.
3. `docs/superpowers/specs/2026-07-15-body-resource-stock-design.md` (incl. the
   Slice-L2 amendment at the tail) — `BodyResources` depletable stock,
   `BodyResourceOps.Commit`, `BodySiting.CompetesForBody` (per-resource-class
   claims), and the groundbreaking-rejects-on-no-eligible-body rule. Stage 1's
   opportunity score reads these; Stage 1 consumes the rejection semantics
   unchanged.
4. `docs/superpowers/plans/2026-07-15-slice-l-ledger.md` **and**
   `docs/superpowers/plans/2026-07-15-slice-l2-ledger.md` — **how the locality
   interfaces actually landed** (real commits, real fix waves, what got
   re-tuned). More reliable than the design docs for "what exists now."
   Especially: L2 confirms `RollChannel.ShipmentDetection = 78` is the last
   live channel (Phase 2 took 77 for `BodyResourceStock`), so **79+ is free**;
   L2's staffing rewire (`StaffingOps.WeightedWorkforce`, commit `2a0f300`) is
   the exact seam Stage 2's `segment-hex` rewire extends; L2's FineTick saga is
   the cautionary tale about un-gated per-epoch groundbreak cadence — read it
   before you add any per-step trigger.
5. `docs/HANDOFF.md` — current main state and the live chain (MC → BF → CU-3 →
   CU-4, WT queued). Confirms what may land on `main` while you work.
6. `CLAUDE.md` — slice-session workflow, hard rules, subagent-driven-
   development requirement, model usage.

Plus the code seams (re-verify every file:line against the current tree before
writing a task — this branch has a strong track record of plans going stale
within a session):

- `src/Core/Epoch/CapabilityOps.cs` — `ConstructionCandidatesFor` (~75–140,
  the cell→hex scan rework) and `PickHex` (~237–248, demotes from selector to
  a score bonus).
- `src/Core/Epoch/Port.cs` — `PortDomains.ServiceRadius` (~77–79), the basis
  for `G`; `PortDomains.Services`.
- `src/Core/Epoch/StaffingOps.cs` — `ProximityWeight` (~16–31): the
  `port-hex → facility-hex` hop that becomes `segment-hex → facility-hex`.
- `src/Core/Epoch/PopulationSiting.cs` — `Assign` resolves within the port's
  system today; Stage 2 extends it to an arbitrary domain hex's committed
  system.
- `src/Core/Epoch/BodySiting.cs` — `IsExtraction`, `CompetesForBody`,
  `RenewableYield`, `PortBody` (consumed as-is).
- `src/Core/Epoch/ProjectOps.cs` — `SpawnFacilityConstruction` (~38–74) +
  `PlaceFacilityBody` (~84–102, the commit + claim-aware body + stock roll,
  consumed unchanged); `CompleteExpedition` (~629–736, the founding body Stage
  3's graduation mirrors — port + market + segment + encroachment-tension bump).
- `src/Core/Epoch/Phases.cs` ~153–169 — the corp perception path that
  synthesizes a single home-port candidate from `CorporationOps.PlannedFacility`
  today. Stage 1 routes corps through the domain scan instead. **This is the
  owner-filter trap — see Traps.**
- `src/Core/Epoch/ColonyValuation.cs` — `CandidatesFor` (expedition target
  scoring) and `EncroachedPolities`; Stage 3 adds graduation as a candidate
  kind in the same expansion scoring, and tightens the encroachment spacing
  into a hard frontier gate.
- `src/Core/Epoch/ArtifactSerializer.cs` — the `Layers` table (~26–36): the
  `segments` layer bump (v3→v4 for `PopulationSegment.Hex`) and a new appended
  `outposts` layer after `banks`.

## What this slice builds — three phase gates, the Slice L pattern

One mega-slice, **three phase gates, each independently mergeable and each
sweep-verified before the gate is declared passed:**

- **Stage 1 — Satellite workings (spec §2).** `ConstructionCandidatesFor` drops
  from cell to **hex** granularity across a port's service radius, with a
  body-aware opportunity score (extraction scores on eligible-unclaimed body
  richness/stock, discounted by distance + a hauling proxy; support/processing
  keeps port-body affinity). Unsettled hexes use an on-demand `Generator.Generate`
  **preview** (no `SystemRegistry.Commit`, nothing persisted, roll-free);
  settled hexes read `SettledSystems`/`BodyResources`. Anchors demote to a
  score bonus. Corps run the same scan scoped to their home-port domain.
  Groundbreaking rejection and per-class claims consumed unchanged. **No new
  persisted state.**
- **Stage 2 — Outposts (spec §3).** `PopulationSegment` gains a serialized
  `Hex`. Sustained unmet weighted-labor demand at a worked hex — measured in
  **world-time**, never step counts — triggers a segment to elect to settle
  (spends real `Wealth` as habitat construction wages), moving its `(Hex, Body)`
  to the satellite hex; that settlement founds a lightweight **outpost** record
  (`SimState.Outposts`; not an actor, no market — trades through the parent
  port). `StaffingOps.ProximityWeight` rewires to `segment-hex`; satellite
  wages redirect to resident segments.
- **Stage 3 — Frontier graduation (spec §4).** A mature outpost at distance
  **≥ G from every existing port** (`G` derived from service radii — spacing
  scales with config, so adjacent ports are structurally impossible; interior
  outposts never graduate) enters the same polity expansion scoring as an
  expedition. Cost = `ColonyCost` discounted by existing facilities/population,
  charged from `ExpansionPoints`, run as an administrative promotion project
  (**no convoy**) that completes into a tier-1 `Port` + `Market`, re-attaches
  resident segments, re-resolves facility market attachment, and fires
  `CompleteExpedition`'s encroachment-tension bump.

## Scope, mechanical acceptance, and the eyeball gate

Implement the spec stage by stage, TDD, frequent commits, committed task ledger
updated as you go. **Each stage is its own mergeable gate and is sweep-verified
before you declare it passed** — do not carry a stage's conservation risk into
the next.

**Mechanical acceptance (every gate, all mandatory):**
- `dotnet test StarSystemGeneration.sln` green — the hex-tier (Phase-1
  generation) suite **never** breaks.
- Determinism byte-identity for same config (two runs; save→load→save).
- Goldens re-frozen **once**, at slice end (siting output legitimately changes
  — do not re-freeze per stage).
- The **32-run committed sweep** is the conservation instrument for each of the
  three conservation-sensitive flows (settle payment, wage redirect, graduation
  cost) — see Traps. Not seed-42 units.
- New coverage per spec §7: Stage 1 siting determinism + a richer neighbor hex
  out-competing a depleted/fully-claimed port hex + the corp-scan owner-filter
  fix; Stage 2 staffing rewire + wage-redirect conservation + settle-election
  world-time behavior; Stage 3 frontier gate (interior outpost never candidates)
  + cost discount + promotion integrity.

**Eyeball gate (the taste gate — user runs the REPL, spec §6):** a new
`domain <port>` view showing satellite hexes with their facilities/output,
outposts with resident segments and candidacy status (interior vs. frontier),
and settle/graduation events in history. The user should *see* a domain
blooming over epochs, an outpost forming where work concentrated, a frontier
outpost graduating while interior ones stay subordinate, and **no two ports
adjacent.**

## Boundary — NOT this slice (spec §8)

- **Atlas rendering** of blooming domains, outposts, satellite workings, and
  graduation — deferred to the future atlas pass. DX produces the *state* that
  pass will read; the REPL is this slice's only surface.
- **Ongoing intra-domain population churn** beyond the single settle election —
  no continuous re-sorting of segments across a domain's hexes.
- **Corp expansion beyond the home domain** — `CorporationOps.InvestGateLanes`
  and the gate-pair / cross-border machinery are untouched; corps scan their
  home domain only.
- **Passenger-ship migration** — population relocates as an accounting event,
  not as cargo on a hull.
- **Outposts as war objectives** beyond what falls out of their facilities
  being ordinary contestable `Facility` rows — no new siege/objective type.
- **Off-lane / patrol behavior** beyond consuming L2's existing
  `StaffingOps`/`OrbitGeometry` machinery.

## Worktree setup

Use the `using-git-worktrees` skill; project convention is `.worktrees/` at
repo root (gitignored for exactly this). Expect
`.worktrees/slice-dx-domain-expansion/`. Copy the gitignored files a fresh
worktree needs before any build/batch run: `src/Core/csc.rsp`,
`unity/Packages/manifest.json`, `unity/Packages/packages-lock.json` — check the
Slice L / L2 ledgers for anything added to this list since. Windows worktree
removal can fail with **"Filename too long"** on Unity's `Library/PackageCache`
— use a `\\?\`-prefixed `cmd /c rd /s /q` fallback if `git worktree remove`
fails, **not** plain `rm -rf`.

## Traps to carry (state each one concretely)

- **The 32-run committed sweep is the conservation instrument, not seed-42
  units** (the standing convention since CU-1). Three flows in spec §5 are
  conservation-sensitive — **settle payment** (segment `Wealth` → habitat
  wages), **wage redirect** (satellite wages → resident segments instead of
  port households), **graduation cost** (`ExpansionPoints` spent, discounted).
  Run the sweep and check the worst `Money.ConservationResidual` before
  declaring each stage done; a flow that moves *where* value lands must never
  change *how much* exists. `ConservationTests` stays green throughout.
- **The corp owner-filter latent bug (spec §2).** `ConstructionCandidatesFor`
  filters ports by `port.OwnerActorId == actorId`. Ports belong to polities; a
  corp's actor id never matches (its home port belongs to `HostPolityId`), so
  naively pointing a corp at this method scans **zero ports** and returns
  empty. Scope the corp scan by its **home-port domain (`corp.HomePortId`)**,
  not owner identity. Regression-test that a corp's home-domain scan returns
  real candidates. Verify the current corp path (`Phases.cs` ~153–169) before
  rewiring.
- **RollChannel: 79+ is free, but verify in-branch.** `ShipmentDetection = 78`
  is the last live channel (L2); Phase 2 holds 77 (`BodyResourceStock`). New
  channels (outpost naming, settle tiebreaks) append after 78 — **read
  `src/Core/Rng/RollChannel.cs` yourself** before assigning, don't trust this
  number blind. **Stage 1 stays roll-free** (previews are pure).
- **World-time, never step counts** (`[[time-not-ticks]]`, a hard rule). The
  settle-election trigger is *sustained unmet labor over world-years*. L2's
  FineTick saga is the cautionary tale: an un-gated per-epoch groundbreak
  cadence made project completions scale with clock resolution and cost a
  multi-task fix. Any DX trigger or duration must be world-time state, not a
  per-step artifact — and confirm the existing facility groundbreak cadence
  gate (`Infrastructure.FacilityGroundbreakCadenceYears`, landed in L2) still
  covers the new hex-granular candidates.
- **KnobRegistry registration is mandatory.** `G`'s inputs (which radii, which
  margin) and any new knob (habitat cost, settle threshold, hauling-proxy
  weight) must be registered in `KnobRegistry.cs` (name-sorted) + documented in
  `docs/TUNING.md`. An **unregistered knob silently reverts on reload**,
  breaking determinism and the tuning sweep.
- **Goldens re-frozen once, at slice end** — not per stage.
- **`git log main` before every merge-out.** A structural slice from the live
  chain (**CU-3** currency consolidation is the one the user flagged in
  flight; MC / BF / WT may also move main) **will likely land while you work**
  — fold `main` in first and resolve conflicts, the CU-1 precedent. Watch for
  touches to `MarketEngine.cs`, `ProjectOps.cs`, `ArtifactSerializer.cs`,
  `Phases.cs` — DX's own targets — as fresh staleness points.
- **The hex-tier (Phase-1 generation) suite never breaks.** DX only ever runs
  the generator as a discarded preview (`Generator.Generate` without commit) or
  reads already-committed systems; it never persists a hex the sim didn't
  already commit through groundbreaking.

## Model usage (per CLAUDE.md)

Route every task through **subagent-driven-development** — Sonnet default,
Opus escalation per task, decided at dispatch time. Strong **Opus-escalation
candidates**, named explicitly:

- **Stage 1's hex-granular scan rework** — spans siting / perception /
  determinism (the cell→hex spiral, the body-aware score, the corp owner-filter
  fix, the preview path), and the determinism byte-identity gate rides on it.
- **Stage 2's settle payment + wage redirect** and **Stage 3's graduation
  cost** — the three conservation-sensitive flows; each escalates for the
  money-conservation invariant, the same discipline L2/CU-1 used for their own
  conserved-transfer tasks.
- **The serializer layer changes** — `segments` v3→v4 and the new `outposts`
  layer touch save/load round-trip determinism (the `BodyResources` lesson: a
  layer that lags the state breaks reload).

One **fresh-eyes whole-branch review** (pinned to `model: fable`) before merge,
followed by one fix wave — standard protocol. Given this branch's track record,
budget for the review surfacing something larger than a fix wave; if it does,
stop and brainstorm properly rather than patching around it.

## Wrap-up, in order (per CLAUDE.md)

Merge to main locally → update `docs/HANDOFF.md` → write the next slice's
kickoff prompt (or, if the gap-list backlog is the more natural next step by
then, say so instead of forcing one) → sync Trello (`StarSystemGeneration`
board — move DX's card to Merged; create one if it doesn't exist yet; file
anything new surfaced mid-slice) if reachable → push only when the user says to.
