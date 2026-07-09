# Slice B Kickoff — Session Prompt

You are starting **Slice B (Two-plane state)** of the epoch-sim implementation
roadmap, under the lighter protocol in `/CLAUDE.md` — **with the one exception
that applies only to this slice: B is the state-model rewrite and gets a full
written plan (superpowers:writing-plans format) that the user reviews before
execution.** Brainstorm nothing; the design is the spec — the plan translates
it into ordered tasks.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules.
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — the meta-plan
   (slice table, transition rules, gates).
3. **The design docs B implements**: `docs/design/frame/space-and-travel.md`
   (the two-plane model — this is B's heart), `docs/design/frame/actors.md`
   (assets, polity as territorial institution),
   `docs/design/substrate/infrastructure.md` (port/lane/facility vocabulary,
   tiers, siting), `docs/design/narrative/handoff.md` (the new artifact format:
   §P6 layer sections, versioned per layer).
4. **What Slice A landed** (build on it, don't re-derive):
   `docs/superpowers/plans/2026-07-09-slice-a-ledger.md` (notes/surprises
   section) and the code in `src/Core/Epoch/`:
   - `EpochSimConfig.cs` — knob families; add B's knobs here (world-year rates).
   - `WorldEvent.cs` / `EventLog.cs` — event grammar v2; new event types append
     into the stable 100-blocks (political 300s, economic 200s…).
   - `Actor.cs`, `SimState.cs` — actor substrate + the sim-state container B
     extends with the sparse registries.
   - `EpochEngine.cs`, `Phases.cs` — the seven phases; B fills Resolution's
     port-establishment resolver and gives Perception/Markets real state to
     reference (still perfect-info/idle).
   - `ControllerContract.cs`, `Policies.cs`, `Acts.cs` — `FoundColonyAct`
     resolves convoyless in B (convoys arrive with Slice E fleets).
   - `EpochRolls.cs` — stateless rolls keyed (step, actor, channel); append new
     `RollChannel` values (next free: 40), never reuse 37–39 (A's stubs, which
     B retires).
   - `StubGenesis.cs` — **replaced** by seeding polities at the existing
     seeding passes' homeworld anchors (the seeding passes themselves survive
     until Slice F).
   - `SimTraceView.cs` — extend the trace; keep every interpolation
     invariant-culture (see the culture-flip determinism test).
5. Reference-only prototype (informs, never constrains): `RegionCell.cs`,
   `Polity.cs`, `SkeletonSerializer.cs`, `EpochSim.cs`, `Sim/*`.

## Scope (roadmap row B)

- **Sparse registries over the natural raster**: port registry, facility
  registry, fleet/segment records as structs — all hex-addressed, all in
  `SimState`. `RegionCell` slims to the natural raster (density, lean,
  metallicity, anchors); every political/development/war field is deleted with
  the prototype.
- **Expansion = port establishment**: the colonization chain (decision → …
  convoyless journey stub → founding → growth) as the Intent → Resolution path;
  `FoundColonyAct` gets its resolver; port tier growth via Allocation.
- **Territory/domains derived**: polity territory = union of port service
  areas, computed from the port registry on demand, never stored; domain
  overlap = contested-influence zones.
- **Lanes**: paired port infrastructure within inter-port range; built, not
  given; capacity/speed from port tiers.
- **Prototype sim deleted**: `EpochSim.cs`, `Sim/*`, `Polity.cs`, `War.cs`,
  `GalaxyEvent.cs`, the v5 `SkeletonSerializer` sim sections, per-cell
  political state — outright, no adapters. Their tests go with them. The
  hex-tier (Phase-1 generation) suite and Tier-1 density/seeding stay green.
- **New artifact format**: layer-sectioned, versioned per layer, config
  artifact-stamped (`EpochSimConfig` finally persists); determinism gates:
  byte-identical artifact for same config, load-vs-rebuild equivalence.

**Boundary**: no goods/markets (C/D own those — ports exist before they trade);
no fleets beyond record structs (E); perception stays perfect-info (I);
emergence schedule stays stubbed (F). REPL surface: map-style view of port
domains + lanes + a chronicle of foundings — the eyeball gate is "empires as
port-domain glows with organic borders" in ASCII.

## Session shape

1. Read the above. **Write the plan** (`docs/superpowers/plans/YYYY-MM-DD-slice-b-plan.md`),
   present it to the user, and wait for approval — this replaces the scope nod.
2. Branch `slice-b-two-plane-state`; task ledger as usual; TDD; frequent
   commits.
3. Delete the prototype the moment its replacement lands, per plan order.
4. One fresh-eyes whole-branch review subagent + one fix wave before merge.
5. User gates: plan approval · REPL eyeball (domain map + founding chronicle) ·
   merge decision.
6. Wrap-up: merge · HANDOFF · **write the Slice D kickoff prompt** (D needs
   both B and C; check C's status) · flip the box below · push only on user
   say-so.

- [ ] Slice B complete
