# Slice AC — the atlas catch-up — task ledger

Branch `claude/slice-ac-0e02d9` (harness worktree at
`.claude/worktrees/slice-ac-0e02d9/`; the kickoff's nominal
`slice-ac-atlas-catchup` name was superseded by the harness-created worktree —
same isolation, work proceeds here). Design (the spec):
`docs/superpowers/specs/2026-07-21-ac-atlas-catchup-design.md`. Kickoff:
`docs/superpowers/plans/2026-07-21-slice-ac-kickoff-prompt.md`.

**One mega-slice, four independently-mergeable phase gates, each with its own
short editor eyeball** (user decision — atlas work is visual, a grammar misstep
in phase 1 must not survive to phase 4). All task work routed through
subagent-driven-development (Sonnet default; Opus escalation noted per task,
dispatched synchronously and verified with `git log`). One fresh-eyes fable
whole-branch review + one fix wave before merge. **Zero sim behavior** — the
golden is asserted byte-untouched every phase, not assumed.

Baseline: **1218/1218** `dotnet test StarSystemGeneration.sln` on branch base
`854b2b5` (== main tip; matches the DX-merge count). EditMode (Unity NUnit)
suite verified separately in a batch run.

## Env / worktree setup (DONE at slice open)

- Copied gitignored `src/Core/csc.rsp`, created `unity/Packages/` and copied
  `manifest.json` + `packages-lock.json` from the main checkout (the K/L/DX
  trap — none are tracked, all needed before any build/batch run).
- Remaining traps to respect: batchmode dies in ~2s while an editor holds the
  project (verify log size + output mtimes, never trust a trailing `echo
  exit:$?`); goldens are CRLF on disk; vertex colors need explicit `.linear`;
  `unity/ProjectSettings` churn stays uncommitted; every new `src/Core` file
  gets a two-line `.meta` with a fresh guid; PowerShell mangles piped REPL
  stdin (use bash `printf`).

## Architecture facts fixed by the seam map (so subagents don't re-derive)

- **Core.Atlas** (`src/Core/Atlas/`, namespace `StarGen.Core.Atlas`): entry
  `AtlasReadModel(SimState)`. Lenses are per-class statics
  (`DomainLens`/`LaneLens`/`TradeLens`(new)/…), **no central lens enum** — a
  rail key is a bare lowercase string duplicated across THREE places that must
  stay in sync: `LegendQuery.For` (arm), `unity/.../LensRail.cs` (chip+toggle+
  `Apply`+`ActiveLegendKey`), `unity/.../Tests/LegendDriftTests.cs`
  (`RailKeys`). Plus `EpochMapView.Render` for REPL parity.
- **Panels** are Core records (`MarketPanel.Card`→`MarketCard`, etc.); Unity
  `PanelViews.cs` builds a UI-Toolkit view over each. Extending a panel = add
  fields to the Core record + its query + the `PanelViews` builder arm.
- **SimHost event contract (K4)**: `Loaded` = new world (full rebuild);
  `TimeChanged` = same world, new moment (re-query). Every new layer/panel
  subscribes to BOTH, as `LensRail`/`InspectorDock` do.
- **DomainFieldLayer** today is a shader plane shaded by port-radius union
  (`_Ports`/`_SlotColors`/`_RelationTex`), NOT per-hex CPU fills — Phase 1
  interior structure is a genuinely new render path there.
- **`SelectionModel` is Unity-side** (`unity/Assets/Atlas/SelectionModel.cs`,
  a MonoBehaviour) — not a Core read-model.

## Decided implementation choices (fixed here; subagents confirm against tests, don't reopen)

1. **DX graduation gate lives in `src/Core/Epoch/Interior/OutpostOps.cs`**
   (`FrontierStatus`/`IsFrontier`, `FrontierStanding` record, `G = 1 +
   Expansion.GraduationMarginHexes`, default 2), NOT `GraduationOps.cs` (that
   is *faction* graduation — a different concept). `DomainInteriorQuery` reads
   `OutpostOps.FrontierStatus` for candidacy; graduated flag is
   `Outpost.Graduated`.
2. **Freight purpose is DERIVED, never a stored field.** `Shipment` carries
   only `ShipmentChannel {Freight,Requisition}`. The 4-way label (war convoy /
   courier / spread run / state haul) is `CourierOps.OfShipment(state,s.Id)` →
   `War`→"war convoy" else "courier"; else `Channel==Freight`→"spread run"
   else "state haul" — the exact `RenderFreight` rule (`Repl.cs`/`efreight`).
   Port this derivation ONCE into a Core.Atlas helper both the panel and REPL
   read (parity).
3. **`TradeLens` preserves the saturation filter verbatim.** The load-bearing
   bit of `EpochMapView.TradeCells` (`EpochMapView.cs:361-396`): a good's lane
   spread only counts if `BookOps.AskQty(state, cheapPort, good) > 1e-9` — a
   gap over an empty book is scarcity, not margin. Move the whole derivation
   into `TradeLens` (Core.Atlas), Inspector calls it (`emap trade` parity).
4. **Off-lane status is derived from `RouteLaneIds.Count == 0`** (the
   `RenderFreight` idiom); detection-risk *context* reads `PatrolCoverage.At`
   (hostile, active-war only) read-only — NEVER duplicating `ShipmentOps`' sail
   rules.
5. **Outposts get no new panel type** — an `Outpost` is a registry record, not
   an actor. Selection routes to the parent port's panels plus an outpost row.

---

## Phase 1 — Domain interior (DX surfaces)  [spec §1]  ✅ CLOSED — Eyeball 1 ACCEPTED

Map grammar first; every later phase draws over it. **Eyeball 1 accepted
(2026-07-22)**: grammar reads right; mark size/alpha nudges deferred to the
final polish/review pass (user call). Two sim-side gaps surfaced (Obs-A/Obs-B
above) — filed, not fixed (zero sim behavior). **Committed `Atlas.unity` NOT yet
regenerated** with the new layers — deferred to slice-end (regenerate once for
all phases via `AtlasViewSceneSetup.RunFromCli`, editor closed; for a live
eyeball before then run the "StarGen/Setup Atlas Scene" menu).

- [x] **AC1.1** — `DomainInteriorQuery` in Core.Atlas: for a port/domain scope,
      worked hexes (facilities where `MarketEngine.AttachedMarketIndex==portId`
      && hex≠port hex — the `DomainView.Render` derivation, moved to Core),
      outposts (`SimState.Outposts` by `ParentPortId`) each with name, parent
      port, resident presence (`Segments` at `(PortId,Hex)`), founding year,
      candidacy (`OutpostOps.FrontierStatus`), graduated flag. **Opus** (spans
      registry/facility/candidacy reads). TDD: xUnit query tests.
- [x] **AC1.2** — `DomainView.Render` (Inspector `domain <port>`) re-pointed at
      `DomainInteriorQuery` (parity; the Inspector stops holding its own
      derivation). Verify `domain` output byte-stable or intentionally-changed.
- [x] **AC1.3** — `DomainFieldLayer` interior structure: worked/outpost hexes
      read as filled/inhabited against the plain domain glow. Stay in the
      visual grammar (glows/billboards, never a painted hex board). `.linear`
      vertex colors if any mesh colors are set.
- [x] **AC1.4** — Outpost marks: named marks in the port-mark family, visually
      subordinate (smaller, no service ring, no market affordance). Tooltip =
      name + candidacy. New `SelectionKind` + `InspectorDock` routing to parent
      port panels + an outpost row (no new `PanelType`).
- [x] **AC1.5** — Events: `OutpostFounded=314` (payload `OutpostId`) +
      graduation (rides `PortEstablished`) read correctly in news/chronicle
      surfaces. Verify copy/payload rendering.
- [x] **AC1.6** — `SystemStage`/`SystemQuery.At` satellite-hex works: **verify,
      don't rebuild** (`SystemQuery.cs:114` already filters per hex).
- [x] **AC1.G** — Phase gate: `dotnet test` green + golden byte-untouched
      (asserted) · determinism · EditMode green (`LegendDriftTests` if a
      domain-mode key changed) · **Eyeball 1** (seed 42 late-epoch: worked
      hexes + named outpost structure a domain; a graduated outpost reads as a
      port with history).

## Phase 2 — Economy/trade (the original K6 scope)  [spec §2]

- [ ] **AC2.1** — `TradeLens` in Core.Atlas: port `TradeCells` verbatim
      (saturation filter kept). **Opus** (the one derivation-move with drift
      risk). TDD.
- [ ] **AC2.2** — `EpochMapView.Render` `trade` layer calls `TradeLens`
      (`emap trade` parity); rail key `"trade"` threaded through `LegendQuery`
      + `LensRail` + `LegendDriftTests.RailKeys`.
- [ ] **AC2.3** — TRADE lens Unity layer + rail chip + `AtlasSmoke` shot.
- [ ] **AC2.4** — Order-book panel (`ebook` parity): resting asks/bids per good
      (owner, qty, grade, limit vs reference) via `BookOps` reads, extending
      `MarketPanel`'s ask depth/grade rows (extend, don't fork). Reachable from
      the Market panel in the same dock.
- [ ] **AC2.5** — Contracts panel (`econtracts` parity): courier job board
      (open/in-transit: route, cargo, fee, priority WAR called out, fulfiller).
      Drawer vs dock decided at Eyeball 2.
- [ ] **AC2.6** — Freight purposes on the map + `ShipmentPanel` purpose field
      (derived per choice #2) + rider-contract link (`CourierOps.OfShipment`).
- [ ] **AC2.7** — War-supply readout: War/Fleet panel names the deployed
      fleet's forward depot (`FleetOps.NearestOwnedPortId`); contested-lane
      shading ONLY if a cheap read-only presence query exists (else skip).
- [ ] **AC2.G** — Phase gate + **Eyeball 2** (hub port: book vs `ebook`; job
      board; TRADE vs `emap trade`; find a war convoy).

## Phase 3 — Currency & banking (CU/BF surfaces)  [spec §3]

- [ ] **AC3.1** — Currency-zone tint mode on the existing polity/domain render
      (`DomainAccent` gains a `Currency` member; tint by currency id, unions
      share a color, `Retired` zone disappears). Legend drift-tested.
- [ ] **AC3.2** — `PolityCard` monetary block: currency name, numeraire/FX rate
      + recent drift, bank reserve, backing ratio (`Reserve/ClaimOnState`) —
      mirror `InteriorView.cs:47-77`. `PolityPanel` query + `PanelViews` arm.
- [ ] **AC3.3** — `MarketCard` prices state their currency
      (`LocalCurrencyOf(portId)` → currency name) — no reader guesses the unit.
- [ ] **AC3.4** — `RelationsPanel` names monetary credibility where CU-4's term
      participates (BackedShare-derived, read-only, no new derivation).
- [ ] **AC3.G** — Phase gate + **Eyeball 3** (currency mode shows zones; a
      union shares one tint; a polity panel reads reserve/backing/rate coherent
      with the REPL line).

## Phase 4 — Off-lane, events, debt sweep (L2 + cheap debt)  [spec §4]

- [ ] **AC4.1** — Off-lane crawls render distinctly (direct hex-path,
      dashed/attenuated vs lane traffic); `ShipmentCard` gains off-lane status +
      detection-risk context (`PatrolCoverage.At`, read-only, choice #4).
- [ ] **AC4.2** — Patrol-coverage readout on `FleetPanel` (falloff from dock,
      `OrbitGeometry`-based, read-only).
- [ ] **AC4.3** — Event readthrough: `CargoSeized=409` off-lane variants +
      settle/outpost/graduation events read well in news/chronicle; fix copy/
      payload gaps found, nothing more.
- [ ] **AC4.4** — Cheap debt: `OrbitRef` alias (`SystemStage.cs:9`) compile-
      verified in a real editor session; `AtlasSmoke` extended to render every
      lens including TRADE + currency mode.
- [ ] **AC4.G** — Phase gate + **Eyeball 4** (blockade a lane, watch freight
      elect a visible off-lane crawl; event feed reads the new world cleanly).

## Slice-end wrap-up (per CLAUDE.md)

- [ ] One fable whole-branch review (`model: fable`) + one fix wave.
- [ ] Golden re-frozen once at slice end IF any intended change (else assert
      untouched — zero sim behavior means the golden should NOT move).
- [ ] Merge to main locally → push (push-on-merge default) → update
      `docs/HANDOFF.md` → republish living diagram
      (`docs/diagrams/unity-atlas-design.html` §8/§9) → write next kickoff →
      sync Trello (Slice AC → Merged; retire superseded K6 framing).

## Sim-side observations surfaced at Eyeball 1 (NOT AC scope — file to sim roadmap)

The atlas renders **faithfully** in both cases below (verified against the actual
seed-42 artifact + `SystemQuery.At` dumps); these are pre-existing sim/generation
gaps the catch-up *exposed*, exactly as the design's boundary anticipates ("if the
surfaces make the flatness visible, that is evidence for a future pass, not scope
here"). Both are the same theme: **the hex-tier generation layer and the epoch sim
disagree about a hex.**

- **Obs-A — facilities built in systemless "empty reach" hexes.** Arsenal #201 @
  (58,-18) and Fabricator #154 @ (58,-19) exist with `Body=None` on hexes where
  `SystemQuery.At` reports **`HasSystem=False`** (the generator produced no system
  at all). DX's per-hex satellite siting (`CapabilityOps.ConstructionCandidatesFor`)
  gates **only extraction** facilities on a resolvable body (Slice L Phase 2);
  non-extraction types (Heavy/Processing/Support — arsenal, fabricator, gate) get
  no system/body-presence check, so they land in empty space. Sibling of Slice L
  follow-up #1 (adjacent-hex spillover) + `Siting.Score` body-blindness. Fix theme:
  extend a system/body-presence (or real-adjacency) gate to non-extraction satellite
  siting; pairs with the flat/sparse-economy pass.
- **Obs-B — generation `Body.Settlement` decoupled from epoch settlement.**
  `BodyGenerator.cs:78` table-picks `Body.Settlement ∈ {None,Outpost,Colony,
  MajorWorld}` (`BodyTables.SettlementTable`); `SocietyGenerator` uses it only for
  genesis society sizing. The `SystemStage` rings `settled` bodies **amber**
  (`SettledColor 0xFF,0xBF,0x4F`) straight off this attribute. So a genesis-"Colony"
  the epoch sim never populated shows as a live colony — e.g. **Mirin I @ (56,-17)**:
  `settlement=Colony` but `PortId=-1, Facilities=0`, and it's a **Barren** world.
  Fix theme (a design question): reconcile genesis settlement with epoch state, or
  the atlas must distinguish "genesis backstory settlement" from "live epoch
  settlement." **AC-adjacent question for the user:** is the amber ring meaning
  genesis-flavor (leave as sim follow-up) or should AC clarify the ring's semantics
  (small in-scope readability fix)?

Route at wrap-up to the flat/sparse-economy pass roadmap + `docs/HANDOFF.md`;
offer Trello cards. (Not fixed here — zero sim behavior.)

## Log (append as work lands)

- **AC1.1+1.2 DONE** (commit `908c56f`, Opus). `DomainInteriorQuery.Card(model,
  eye, portId)→DomainInteriorCard?` extracted into Core.Atlas; `DomainView.Render`
  is now a pure formatter, output byte-identical (pinned by a new parity test).
  Public shape: `DomainInteriorCard(PortId, Tier, Hex, OwnerActorId, OwnerName,
  FoundedYear, SatelliteHexes[DomainSatelliteHex{Hex, Facilities[DomainFacilityRow{
  Id,TypeName,Tier,Active,Condition,Body}]}], Outposts[DomainOutpostCard{Id, Name,
  Hex, FoundingYear, Graduated, Candidacy(DomainCandidacyKind{Graduated,
  FrontierNoPort, Frontier, Interior}, GraduatedPortId, FrontierStanding),
  Residents[DomainResident{SegmentId, SpeciesId, Size, SoL}]}], Events[WorldEvent])`.
  **1227/1227** (1218 + 9), golden untouched. **Carry:** `DomainResident` gives
  `SpeciesId` not a resolved name — the Unity consumer (AC1.4) must resolve against
  `state.Skeleton.Species` (same as the REPL) or we add `SpeciesName` to the query
  if drift risk appears.
- **AC1.3+1.4 DONE** (commits `7d9c92a`, `6404139`, Opus). Domain interior
  surfaced in Unity: a **subordinate billboard marks layer** (`DomainInteriorMarks`
  derivation Unity-side + `DotMarkLayer` base + `DomainInteriorLayer` worked-dust +
  `OutpostLayer`), riding the existing **domains** lens (NO new rail key, legend
  untouched), `.linear` vertex colors, worked/outpost non-overlap, graduated
  outposts left as ports. `SelectionKind.Outpost` + pure `SelectionModel.Resolve`
  (outpost after Port, before Project/Shipment/Fleet/Poi) + tooltip
  (`name · candidacy`). **Panel-routing UI call (for the eyeball):** outpost click
  opens the parent port's Polity+Market panels and renders a leading "SELECTED
  OUTPOST" section inside the Market panel via a new non-breaking
  `PanelRequest.SubId` (no new PanelType). +2 EditMode tests. **Carries for the
  eyeball:** mark sizes/alpha are first-pass taste values; seed-42 has ONE live
  outpost (Belmi, port #7) so worked-skeleton density reads best live/richer seed.
- **Env cleanup:** reverted a CRLF-only churn on
  `unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` (Unity
  re-save noise). **Latent debt found:** 11 untracked `src/Core/**/*.cs.meta` for
  pre-existing Core files (BodyRef/Currency/ClockPlan/…) — `.gitignore:491`
  (`!src/Core/**/*.meta`) intends them TRACKED but past slices committed the .cs
  without metas; Unity regenerated them here. **Fold into AC4.4 cheap-debt sweep**;
  never `git add -A` meanwhile.
- **Phase 1 gate (AC1.G): dotnet 1227/1227 · golden byte-untouched (git-clean) ·
  Unity compile clean (326KB log) · EditMode 16/16 (was 14).** → Eyeball 1.
