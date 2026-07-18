# Slice DX — domain hex expansion — task ledger

Branch `slice-dx-domain-expansion`; worktree `.worktrees/slice-dx-domain-expansion/`.
Design (the spec): `docs/superpowers/specs/2026-07-16-domain-hex-expansion-design.md`.
Kickoff: `docs/superpowers/plans/2026-07-16-slice-dx-kickoff-prompt.md`.

Three independently-mergeable phase gates (the Slice L pattern). All task work
routed through subagent-driven-development (Sonnet default; Opus escalation
noted per task). One fresh-eyes fable whole-branch review + one fix wave before
merge. Goldens re-frozen **once** at slice end. Hex-tier suite never breaks.

Baseline: **1137/1137** `dotnet test` on branch base `41f1cb8` (== main tip).

## The three "open implementation choices" — DECIDED at plan time (design §"Open implementation choices")

These are fixed here so subagents do not reopen them. Exact knob homes/values
and final form are confirmed by the implementing subagent against the tests, but
the *shape* is settled:

1. **Settle-election machinery (§3): a DEDICATED pass, not an extension of
   `Migrate`.** `Phases.Migrate`/`FindOrFoundSegment` move population *between
   ports* along lanes on an SoL gradient. The settle election is a different
   animal: triggered by sustained unmet weighted-labor at a *worked hex within
   the same domain*, it moves a segment's `(Hex, Body)` while keeping its
   `PortId`. It reuses the `StagedEvent` news mechanism but shares none of the
   gradient logic. A new dedicated step (its own world-time cadence gate),
   sequenced in the same phase family as `Migrate`.
2. **`G`'s parameterization (§4):** an outpost at hex `h` is frontier-eligible
   iff for **every** existing entered port `p`,
   `HexGrid.Distance(p.Hex, h) > ServiceRadius(cfg, 1) + ServiceRadius(cfg, p.Tier) + Expansion.GraduationMarginHexes`.
   This is exactly `ColonyValuation.EncroachedPolities`' overlap geometry
   (newcomer tier-1 radius + incumbent radius), tightened from a scored penalty
   to a hard gate — so a graduated port can never overlap an existing domain,
   structurally, at any config. New knob `Expansion.GraduationMarginHexes`
   (default TBD by the Stage-3 subagent; ≥ 0; a small positive so "outside the
   domain *plus a margin*"). `AstroRadiusBonus` folded in to match
   `EncroachedPolities` exactly.
3. **Hauling-cost proxy (§2):** a multiplicative discount on the extraction
   opportunity score, `1 / (1 + Economy.HaulingProxyPerHex * hexDistToPort)`,
   where `hexDistToPort = HexGrid.Distance(port.Hex, hex)` — separate from the
   `StaffingDistanceFalloff` proximity term (that prices labor commute; this
   prices moving *output* back to the port market). New knob
   `Economy.HaulingProxyPerHex`. Farther hexes are worth less; the port hex and
   near neighbors keep an advantage scarcity must overcome.

All three new knobs (`GraduationMarginHexes`, `HaulingProxyPerHex`, plus Stage-2
habitat/settle knobs) MUST be registered in `KnobRegistry.cs` (name-sorted) and
documented in `docs/TUNING.md` — an unregistered knob silently reverts on reload.

## Seam map (re-verified against the tree this session)

- `CapabilityOps.ConstructionCandidatesFor` (`CapabilityOps.cs:75-140`): cell
  spiral → `PickHex` collapse. Owner-filter at `:84` (`port.OwnerActorId ==
  actorId`). `PickHex` at `:237-248`. `BuildableTypes` at `:60-69`.
- Corp path: `Phases.cs:153-169` synthesizes ONE candidate at the corp's home
  port hex from `CorporationOps.PlannedFacility` (`CorporationOps.cs:604`).
  Polity caller: `Phases.cs:101-102`.
- `PortDomains.ServiceRadius` (`Port.cs:77-79`); `.Services` (`:84-87`).
- `StaffingOps.ProximityWeight` (`StaffingOps.cs:16-31`): `port-hex → f.Hex`
  hexHop at `:21-22`; local-hop via `OrbitGeometry.OrbitDistance` at `:24-28`.
- `PopulationSiting.Assign` (`PopulationSiting.cs:12-17`): commits the port's
  hex system, returns `BodySiting.PortBody`.
- `PopulationSegment` (`PopulationSegment.cs:20-59`): has `PortId` (get-only),
  `Body` (settable, from locality), `Wealth`. NO `Hex` yet.
- `BodySiting` (`BodySiting.cs`): `Assign`, `IsExtraction` (`:48`),
  `CompetesForBody` (`:67`), `RenewableYield` (`:113`), `PortBody` (`:163`).
- `ProjectOps.SpawnFacilityConstruction` (`:38-74`) + `PlaceFacilityBody`
  (`:84-102`) — consumed UNCHANGED (commit + claim-aware body + stock roll +
  extraction reject). `CompleteExpedition` (`:629-736`). `Complete`'s
  `PortRaise` in-place case (`:565-576`). `ProjectKind` enum (`Project.cs:8`):
  FacilityConstruction, PortRaise, GatePair, HullBatch, ColonyExpedition,
  Mobilization.
- `MarketEngine.PayWages` (`:302-319`): pro-rata by segment size within a port;
  unpeopled → owner polity. `AttachedMarketIndex` (`:103`).
- `ColonyValuation.CandidatesFor` (`:27-81`), `EncroachedPolities` (`:87-103`).
- Expansion dispatch: `Phases.TryFound` (`:1558-1675`) executes a
  `FoundColonyAct`; charges `record.ExpansionPoints -= ColonyCost` at `:1619`;
  world-time founding cadence gate at `:1570-1574`
  (`Expansion.FoundingCadenceYears`).
- `ArtifactSerializer.Layers` (`:26-35`): `("segments", 3)` present; `("banks",
  2)` is currently LAST. New `outposts` layer appends after `banks`.
- `SystemQuery.At` unsettled preview branch: `SystemQuery.cs:65-72`
  (`Generator.Generate(context, hex).System` without commit) — the exact
  roll-free preview Stage 1 reuses.
- `RollChannel`: last live `ShipmentDetection = 78`; `SimExpansion = 33`
  reserved/roll-free; **79+ free** (`RollChannel.cs:118-121`).

---

## Stage 1 — Satellite workings (hex-granular opportunity siting)

Self-funded facility siting per hex across the domain. **No new persisted
state. No conservation-sensitive money flow** (wages still flow to port
households — `PayWages` untouched until Stage 2). Stage-1 gate's sweep is a
**determinism / no-regression** check, not a conservation gate. **Stays
roll-free** (previews are pure).

- [x] **T1.1 — Hex-granular scan rework** (Opus: siting × determinism, the core
  cell→hex spiral). Rework `ConstructionCandidatesFor` to scan **per hex** over
  the port's serviced hexes (deterministic hex spiral, the hex-scale analog of
  the cell spiral — P6). Body-aware opportunity score: extraction types
  (`IsExtraction`) score on best eligible-*unclaimed* body richness/stock
  (settled → real `SettledSystems`/`BodyResources`; unsettled → roll-free
  `Generator.Generate` preview, no commit — mirror `SystemQuery.cs:65-72`), a
  hex whose only eligible body is claimed (`CompetesForBody`) scores 0 there;
  discounted by `ProximityWeight`-shape distance **and** the hauling proxy
  (decision #3). Support/processing types keep **port-body affinity** (scored at/
  near the port hex, fast falloff). Anchor demotes to a **score bonus** (like
  `ColonyValuation`'s +0.4), no longer the selector — `PickHex` retired from this
  path. Register `Economy.HaulingProxyPerHex` (KnobRegistry + TUNING.md). Tests:
  §7 Stage-1 — siting determinism byte-identity; a richer *neighbor* hex
  outcompetes a depleted/fully-claimed **port hex** for extraction; support/
  processing still clusters at the port.
- [x] **T1.2 — Corp domain scan + owner-filter fix** (Opus: owner-filter trap +
  determinism). Route corps through the same scan scoped to their
  **home-port domain** (`corp.HomePortId`), replacing the single synthesized
  candidate at `Phases.cs:153-169`. The scan must scope by home-port, NOT owner
  identity (`port.OwnerActorId == corpActorId` matches zero ports — the latent
  bug). Regression test: a corp's home-domain scan returns real candidates, not
  the empty list the naive reuse yields. Confirm `WantsFacility`/`PlannedFacility`
  gating still governs *whether* a corp builds.
- [x] **T1 GATE — PASSED** (2026-07-18). `dotnet test` 1143 passed / 1 failed
  (seed-42 golden only — the deferred slice-end re-freeze red window; hex-tier +
  DeterminismTests + ConservationTests all green). 32-run `debt-diagnosis` sweep:
  **worst relative conservation residual 2.803e-15** (tolerance 1.3e-9) — no
  regression, as expected (Stage 1 moves no money). Commits `6b8ccdb` (T1.1),
  `e711735` (T1.2). **Independently mergeable.**
  - **⚠ EYEBALL FLAG (balance, not invariant):** hex-granular siting +
    `Economy.HaulingProxyPerHex`=0.25 relocates extraction to the rich frontier
    with a hauling discount → economic magnitude dropped ~33% vs. old
    port-clustered siting (seed-42 CLOCK golden ~137k→~91k). Intended in
    direction (distant works really do cost to haul); the *magnitude* is a
    single tunable knob. Surface at SC.3 eyeball.
  - **Carried to whole-branch review:** `CorpPackingTests` updated to the "same
    scan" behavior (corp funds its whole affordable slate per cycle, not 1/cycle
    — the old stagger was a single-candidate artifact; real invariants kept);
    `CorporationOps.PlannedFacility` is now runtime-dead (delete or re-wire at
    slice end).

## Stage 2 — Outposts (pop follows work)

Three conservation-adjacent rewires. **Conservation-sensitive flows #1 (settle
payment) and #2 (wage redirect)** — sweep-verify the worst
`Money.ConservationResidual` before declaring the gate passed. New RollChannel(s)
79+ for settle tiebreaks/outpost naming.

- [x] **T2.1 — Serializer: `PopulationSegment.Hex` (segments v3→v4) +
  `SimState.Outposts` registry + new `outposts` layer** — DONE (`02bc3e0`).
  `Hex` added (defaults to port hex, initialized at all 4 creation sites),
  segments v3→v4; `Outpost` record + `SimState.Outposts` + `("outposts", 1)`
  layer appended after banks; save→load→save byte-identical. No behavior change.
- [x] **T2.2 — `PopulationSiting` extension** (folded into T2.3). Added
  `Assign(state, portId, hex)` resolving a body within an arbitrary domain
  hex's committed system; the port-hex `Assign(state, portId)` delegates to it.
  Test: `PopulationSitingTests.Assign_WithHex_SettlesAtAnArbitraryDomainHex_NotThePort`.
- [x] **T2.3 — Settle election (dedicated pass)** — DONE. New `SettleOps.Step`
  (Interior/SettleOps.cs), sequenced after `Migrate` in `InteriorPhase`.
  Trigger: a satellite hex whose worked facilities (attached to the port,
  `Produces>0`) are MATURE (`WorldYear − CommissionedYear ≥ SettleMaturityYears`
  — the world-time "sustained" derivation, no per-hex timer) and combined
  weighted workforce `< LaborRequired × (1 − SettleLaborShortfallFraction)`,
  gated per-domain by `SettleCadenceYears` (checked against the domain's last
  outpost founding year, mirroring `FoundingCadenceYears`). Elects the
  largest/lowest-id port-hex household with `Wealth ≥ SettleHabitatCost` (no
  tiebreak roll needed — total order). Conserved payment: `seg.Wealth −= cost;
  MarketEngine.PayWages(port, cost)`. Relocates `(Hex, Body)` via T2.2, keeps
  `PortId`. Founds `Outpost` + a **new** `WorldEventType.OutpostFounded`
  StagedEvent (NOT PortEstablished — reusing it broke port-scoped consumers;
  see report). Knobs registered (`Expansion.Settle*`), `RollChannel.OutpostName
  = 79`. 32-run sweep worst relative residual **4.166e-15** (tol 1.3e-9).
- [x] **T2.4 — Staffing rewire** (Sonnet) — DONE (`1141056`).
  `StaffingOps.ProximityWeight` hexHop `port-hex → f.Hex` → `seg.Hex → f.Hex`;
  local-hop unchanged. No-op on pre-relocation state (seg.Hex == port hex
  everywhere), so the full suite did not shift. Fixed 3 hand-built test fixtures
  that never set `Hex` (production sites all set it correctly). Test: resident
  out-weighs distant port household.
- [x] **T2.5 — Wage redirect** (Opus: conservation flow #2) — DONE. New
  `MarketEngine.PayProductionWages(state, portId, wage)` splits a sale's labor
  share across the port's segments weighted by each segment's aggregate
  weighted-staffing contribution (`Σ_f seg.Size × StaffingOps.ProximityWeight`)
  to the port's active PRODUCING facilities (those with `AttachedMarketIndex ==
  portId` and `Produces>0`). `OrderOps.SettleSale` now calls this instead of plain
  `PayWages`; construction/habitat/refund `PayWages` untouched (size-pro-rata).
  A satellite resident (`seg.Hex == f.Hex`, full weight) out-earns a distant
  port household; a resident-less working still pays commuting households.
  Conserved by construction (shares sum to `wage`); fallback to `PayWages` when
  the port has no producing facilities or zero staffing weight (keeps
  unpeopled→owner revert). Per-step memo `SimState.ProdWageSplits` (transient,
  nulled at `MarketsPhase.Run` top like `GoodsValueCleared`) avoids a
  segments×facilities rescan per sale; rebuilt each step, never staled across
  steps. Aggregate (not per-facility) redirect — per-facility would need sale→
  facility attribution, out of scope (no order-book threading). No new knob (the
  split is structural). Full suite **1165 passed / 1 failed** (seed-42 golden
  only). 32-run `debt-diagnosis` sweep worst relative residual **3.47e-15** (tol
  1.3e-9).
- [x] **T2.6 — `domain <port>` REPL view (initial)** (Sonnet) — DONE (`9da5492`).
  `DomainView.Render(sim, portId)` — port header, satellite hexes + facilities,
  outposts + resident segments + founding year, a labeled Stage-3 candidacy slot.
  `Settlement.Outposts` metric (counts living outposts) registered + SIMHEALTH.md.
  A 100-epoch smoke run rendered a real outpost founding.
- [x] **T2 GATE — PASSED** (2026-07-18). `dotnet test` 1169 passed / 1 failed
  (seed-42 golden only — deferred re-freeze; hex-tier + ConservationTests +
  DeterminismTests green). 32-run `debt-diagnosis` sweep on tip `9da5492`:
  **worst relative conservation residual 3.471e-15** (tol 1.3e-9) — both Stage-2
  flows (settle payment #1, wage redirect #2) hold at FP epsilon. **Outposts
  emerge in all 32/32 runs** (445 total at final epoch, max 29 in a run — pop
  follows work at ensemble scale). **Independently mergeable.**
  - **Carried to whole-branch review / SC.3 eyeball:**
    1. **Port-emptying taste call (T2.3):** the settle election picks the
       *largest* eligible port-hex household; a young single-segment domain can
       relocate wholesale, leaving the port hex un-resident. Cadence-gated, no
       invariant broken. Switching to *smallest*-eligible (or reserving a port
       resident) would keep the port core intact — user's call at the eyeball.
    2. **`WorldEventType.OutpostFounded` added (T2.3):** a new serialized news
       event + payload (reusing `PortEstablished` broke port-scoped consumers).
       Round-trip green.
    3. **`PayWages` widened internal→public + `PayProductionWages` (T2.5):** pure
       API widening for testability (repo has no InternalsVisibleTo).
    4. **Pre-resident wage redistribution (T2.5):** production wages now split by
       aggregate staffing weight, so per-segment wealth (→ SoL/growth/migration)
       redistributes within peopled ports even before anyone relocates when a
       port's facilities sit at different hexes. Small magnitude, residual
       unchanged, no knob.
    5. **tests→Inspector ProjectReference (T2.6):** new infra precedent so
       `DomainView` gets unit coverage.

## Stage 3 — Frontier graduation (infill)

**Conservation-sensitive flow #3 (graduation cost).** Same-polity, same-currency
— zero CU interplay.

- [x] **T3.1 — Frontier gate `G` + candidacy predicate** — DONE (`55ae9e0`).
  `OutpostOps.IsFrontier(state, outpost)` + `FrontierStatus` companion
  (binding-port distance/G/slack for T3.2 scoring + T3.3 REPL) in a
  clearly-named `Interior/OutpostOps.cs` (apart from `GraduationOps`' FACTION
  graduation). `G = ServiceRadius(1) + ServiceRadius(p.Tier) + AstroRadiusBonus
  + Expansion.GraduationMarginHexes` over **every entered port** (parent
  counts, no special-case); graduated → false; frontier iff `dist > G` for all.
  New knob `Expansion.GraduationMarginHexes` (**int, default 1** — no-overlap at
  0, one-hex dead gap at 1); registered KnobRegistry (name-sorted) + TUNING.md +
  ExpansionKnobs. Pure/read-only, **no promotion** (T3.2). 16 tests incl. the
  multi-config anti-clustering theory (gate scales with tier AND margin, never a
  constant). Full suite **1185 passed / 1 failed** (seed-42 golden only — the
  new KNOB line in the artifact stamp; deferred re-freeze). Hex-tier +
  Determinism + Conservation green.
- [x] **T3.2 — Graduation promotion machinery — DONE + CONSERVATION-CLEAN, but
  the rung never fires** (`c83255c`). Built: candidate model carries kind
  (Expedition|Graduation)+OutpostId+discounted Cost; graduation candidates enter
  the same ranked expansion scoring; `GenesisController` emits a new
  `GraduateOutpostAct` (convoy gate is expedition-only); `TryGraduate` re-verifies
  on truth and charges the discounted cost from `ExpansionPoints`; new
  `ProjectKind.OutpostGraduation` (no convoy/fleet/fuel) whose cost **recycles as
  world-time construction wages** via `PayWages` (flow #3 conserved — sweep worst
  residual **3.47e-15**); completes in-place → tier-1 Port+Market, resident
  segments re-attach (`PortId` made settable — the single sanctioned re-attach),
  facilities re-resolve market, outpost `Graduated`, encroachment bump. 5 new
  `Expansion.*` knobs registered. 12 unit tests green (promotion proven correct
  with a CONSTRUCTED frontier outpost). Full suite 1197/1 (golden).
  - **🚧 BLOCKER (structural, cross-task, surfaced by T3.2's sweep): graduation
    fires in 0/32 real histories.** Outposts form only at WORKED hexes, which are
    within the parent's `ServiceRadius` (Stage-1 sites facilities only within the
    domain). But the frontier gate (decision #2, the SUM) requires an outpost
    ≥ `ServiceRadius(1)+ServiceRadius(parentTier)+margin` from EVERY port incl. the
    parent — i.e. ≥ 5 hexes BEYOND the parent's domain edge. An in-domain outpost
    can never clear that, at any knob values (`0 > ServiceRadius(1)+margin` is
    impossible). The two geometries never meet: **outposts form inside domains;
    graduation requires them outside.** Confirmed empirically (seeds 7/42/555:
    outposts form, frontier=0, slack −14/−15 hexes) and geometrically. The
    promotion code is correct; the ladder's third rung is unreachable. **Needs a
    USER DESIGN CALL — escalated 2026-07-18 (see below).** Do not patch around it.
- [ ] **T3.3 — `domain <port>` REPL candidacy + graduation history** (Sonnet).
  Extend the view: per-outpost candidacy status (interior vs frontier,
  distance-to-nearest-port vs `G`); settle + graduation events in history/news.
  SIMHEALTH graduation metric.
- [ ] **T3 GATE**: `dotnet test` green (hex-tier intact); determinism byte-identity;
  32-run sweep — **graduation-cost conservation** worst residual checked,
  `ConservationTests` green; Stage-3 §7 coverage. Commit. **Mergeable.**

## Slice close

- [ ] **SC.1 — Fold `main` in.** `git log main` first — CU-4 (federation
  decision) and/or WT may have landed. Fold main into the branch, resolve
  conflicts (watch `Phases.cs`, `ProjectOps.cs`, `ArtifactSerializer.cs`,
  `MarketEngine.cs`, `ColonyValuation.cs`), re-run the 32-run sweep on the
  merged tip (CU-1 precedent).
- [ ] **SC.2 — Fresh-eyes whole-branch review** (fable, pinned) + one fix wave.
  If the review surfaces something larger than a fix wave, STOP and brainstorm.
- [ ] **SC.3 — REPL eyeball acceptance** (USER checkpoint 2): `domain <port>` —
  a domain blooming over epochs, an outpost forming where work concentrated, a
  frontier outpost graduating while interior ones stay subordinate, no two ports
  adjacent.
- [ ] **SC.4 — Golden re-freeze (ONCE).** Anchor the write on the `.csproj`, not
  a `Goldens/` dir probe (L2 trap).
- [ ] **SC.5 — Merge decision** (USER checkpoint 3) → merge to main locally
  `--no-ff`. Push only on say-so.
- [ ] **SC.6 — Wrap-up**: update `docs/HANDOFF.md`; write the next slice's
  kickoff prompt (or flag the gap-list backlog); sync Trello (DX card → Merged);
  file follow-ups surfaced mid-slice.

## Progress log

- 2026-07-18 — Worktree created off main `41f1cb8`; gitignored files copied;
  baseline 1137/1137. Ledger authored; three open choices decided (above).
  Scope nod accepted (hold lifted by user).
