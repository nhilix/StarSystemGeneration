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

- [ ] **T1.1 — Hex-granular scan rework** (Opus: siting × determinism, the core
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
- [ ] **T1.2 — Corp domain scan + owner-filter fix** (Opus: owner-filter trap +
  determinism). Route corps through the same scan scoped to their
  **home-port domain** (`corp.HomePortId`), replacing the single synthesized
  candidate at `Phases.cs:153-169`. The scan must scope by home-port, NOT owner
  identity (`port.OwnerActorId == corpActorId` matches zero ports — the latent
  bug). Regression test: a corp's home-domain scan returns real candidates, not
  the empty list the naive reuse yields. Confirm `WantsFacility`/`PlannedFacility`
  gating still governs *whether* a corp builds.
- [ ] **T1 GATE**: `dotnet test` green (hex-tier suite intact); determinism
  byte-identity same-config (two runs + save→load→save); 32-run sweep — no
  conservation residual regression (Stage 1 moves no money); Stage-1 §7 coverage
  present. Commit. **Independently mergeable.**

## Stage 2 — Outposts (pop follows work)

Three conservation-adjacent rewires. **Conservation-sensitive flows #1 (settle
payment) and #2 (wage redirect)** — sweep-verify the worst
`Money.ConservationResidual` before declaring the gate passed. New RollChannel(s)
79+ for settle tiebreaks/outpost naming.

- [ ] **T2.1 — Serializer: `PopulationSegment.Hex` (segments v3→v4) +
  `SimState.Outposts` registry + new `outposts` layer** (Opus: save/load
  round-trip determinism — the `BodyResources` lesson). Add `Hex` (HexCoordinate,
  defaults to administering port hex) to `PopulationSegment`, serialized as a
  segments-layer v4 bump. Add the `Outpost` record `(id, name, hex, parentPortId,
  foundingYear, graduated)` + `SimState.Outposts` registry (iterated id order,
  P6) + a new `("outposts", 1)` layer appended after `("banks", 2)`. Round-trip
  byte-identity tests. NOT an actor — no treasury/market/controller.
- [ ] **T2.2 — `PopulationSiting` extension** (Sonnet; determinism-adjacent —
  commits a system). Extend to resolve a body within an *arbitrary domain hex's*
  committed system, not only the port's (`Assign` currently hardcodes the port
  hex). Keep the port-hex default path intact.
- [ ] **T2.3 — Settle election (dedicated pass)** (Opus: conservation flow #1 +
  world-time discipline + new roll). New dedicated step: detect a satellite hex
  with **sustained unmet weighted-labor demand** over a **world-time** duration
  (world-years, never step counts — cf. L2's FineTick saga; gate like
  `FoundingCadenceYears`). An eligible resident-less-hex segment elects to
  relocate: pays a **real habitat cost** (segment `Wealth` → habitat construction
  wages, conserved — money leaves Wealth, lands as wages in the existing
  conserved flow), moves its `(Hex, Body)` to the satellite hex (via T2.2), and
  that founding event creates the `Outpost` + a `PortEstablished`-style
  `StagedEvent`. New knobs (habitat cost, sustained-labor threshold, settle
  cadence years) registered. New RollChannel(s) 79+ for tiebreaks. Tests:
  settle-election world-time behavior (sustained triggers; a brief spike does
  not).
- [ ] **T2.4 — Staffing rewire** (Sonnet; production-magnitude + determinism).
  `StaffingOps.ProximityWeight` hexHop `port-hex → f.Hex` becomes
  `segment-hex (seg.Hex) → f.Hex`; local-hop when same hex, unchanged. A resident
  crews its hex's facilities at full weight; distant port households weakly.
  Test: resident vs port-household weight.
- [ ] **T2.5 — Wage redirect** (Opus: conservation flow #2). `MarketEngine.PayWages`:
  a satellite facility's wages pay the **resident segments at that hex** once
  residents exist; a resident-less working still pays commuting port households
  (Stage-1 behavior). Moves *where* credits land, not how many. Sweep-verify.
- [ ] **T2.6 — `domain <port>` REPL view (initial)** (Sonnet). Satellite hexes
  with facilities/output; outposts with resident segments + founding year.
  (Candidacy status added in T3.3.) Plus a SIMHEALTH outpost metric
  (`Settlement.*` family) per design §6 tail.
- [ ] **T2 GATE**: `dotnet test` green (hex-tier intact); determinism byte-identity;
  32-run sweep — **settle-payment + wage-redirect conservation** worst residual
  checked, `ConservationTests` green; Stage-2 §7 coverage. Commit. **Mergeable.**

## Stage 3 — Frontier graduation (infill)

**Conservation-sensitive flow #3 (graduation cost).** Same-polity, same-currency
— zero CU interplay.

- [ ] **T3.1 — Frontier gate `G` + candidacy predicate** (Opus: anti-clustering
  guarantee, design judgment). Implement decision #2's `G`. Predicate: an outpost
  is candidacy-eligible iff frontier (distance ≥ `G` from every entered port);
  interior outposts **never** graduate. Register `Expansion.GraduationMarginHexes`.
  Test: an interior outpost never becomes a candidate, at any config
  (anti-clustering — the whole anti-goal).
- [ ] **T3.2 — Graduation promotion** (Opus: conservation flow #3 +
  multi-subsystem + design judgment). Frontier outpost enters the **same polity
  expansion scoring** as an expedition target (alongside
  `ColonyValuation.CandidatesFor`). Cost = `Expansion.ColonyCost` **discounted**
  by the outpost's existing facilities + resident population, charged from
  `ExpansionPoints`. A new **administrative promotion `ProjectKind`** (no convoy,
  no `FleetPosture.Expedition`, no fuel) with real world-time duration, completing
  **in-place** (mirror `PortRaise`'s in-place completion + `CompleteExpedition`'s
  founding body): tier-1 `Port` + `Market`; resident segments re-attach (`PortId`
  → new port); facilities re-resolve market via `AttachedMarketIndex`; outpost
  marked **Graduated**; `Relations.EncroachmentTensionBump` loop fires (reuse
  `CompleteExpedition`'s). Tests: cost discount (facility-rich outpost costs less
  than bare); promotion integrity (port+market born, segments re-attached,
  facilities re-resolve, tension fires).
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
