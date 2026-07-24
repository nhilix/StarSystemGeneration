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

## Phase 2 — Economy/trade (the original K6 scope)  [spec §2]  ✅ CLOSED — Eyeball 2 ACCEPTED

**Eyeball 2 accepted (2026-07-22)** after the fix wave: tables accepted
(tuning deferred to a future full-panel design pass); recent-flow trails
accepted once per-leg routing landed (`38de1e2` — "looking much better").
Alpha 70/130 stands for now; final polish pass may revisit.

- [x] **AC2.1** — `TradeLens` in Core.Atlas: port `TradeCells` verbatim
      (saturation filter kept). **Opus** (the one derivation-move with drift
      risk). TDD.
- [x] **AC2.2** — `EpochMapView.Render` `trade` layer calls `TradeLens`
      (`emap trade` parity); rail key `"trade"` threaded through `LegendQuery`
      + `LensRail` + `LegendDriftTests.RailKeys`. *(Threading landed with
      AC2.3 — the drift test asserts all three places atomically, so the key
      couldn't split across tasks; AC2.2 was the REPL re-point alone.)*
- [x] **AC2.3** — TRADE lens Unity layer + rail chip + `AtlasSmoke` shot.
- [x] **AC2.4** — Order-book panel (`ebook` parity): resting asks/bids per good
      (owner, qty, grade, limit vs reference) via `BookOps` reads, extending
      `MarketPanel`'s ask depth/grade rows (extend, don't fork). Reachable from
      the Market panel in the same dock.
- [x] **AC2.5** — Contracts panel (`econtracts` parity): courier job board
      (open/in-transit: route, cargo, fee, priority WAR called out, fulfiller).
      Drawer vs dock decided at Eyeball 2.
- [x] **AC2.6** — Freight purposes on the map + `ShipmentPanel` purpose field
      (derived per choice #2) + rider-contract link (`CourierOps.OfShipment`).
- [x] **AC2.7** — War-supply readout: War/Fleet panel names the deployed
      fleet's forward depot (`FleetOps.NearestOwnedPortId`); contested-lane
      shading ONLY if a cheap read-only presence query exists (else skip).
- [x] **AC2.G** — Phase gate ✅ (2026-07-22: dotnet **1256/1256** · golden
      byte-untouched · determinism green (`DeterminismTests`, 4 facts) ·
      Unity compile clean (0 error CS) · EditMode **16/16** · AtlasSmoke
      **17/17** shots incl. trade) + **Eyeball 2 ACCEPTED** after the fix
      wave (AC2.F1 tables `7aff333`, AC2.F2 recent flows `6376180`, per-leg
      trails `38de1e2`; final gate dotnet **1276/1276**).

## Phase 3 — Currency & banking (CU/BF surfaces)  [spec §3]  ✅ CLOSED — Eyeball 3 ACCEPTED

**Eyeball 3 accepted (2026-07-22).** FX-model question raised and answered
(single numeraire rate; cross-rates implied by ratio). **Polish-pass
candidate queued:** Relations panel could show the implied CROSS-rate
between the two compared polities (`A.Rate/B.Rate`) — the number a human
actually wants; panel wording `rate X numeraire` inherited from the REPL.

- [x] **AC3.1** — Currency-zone tint mode on the existing polity/domain render
      (`DomainAccent` gains a `Currency` member; tint by currency id, unions
      share a color, `Retired` zone disappears). Legend drift-tested.
- [x] **AC3.2** — `PolityCard` monetary block: currency name, numeraire/FX rate
      + recent drift, bank reserve, backing ratio (`Reserve/ClaimOnState`) —
      mirror `InteriorView.cs:47-77`. `PolityPanel` query + `PanelViews` arm.
      *(Drift NOT derivable read-only — no stored rate history anywhere in
      state; rate shown alone, gap filed below.)*
- [x] **AC3.3** — `MarketCard` prices state their currency
      (`LocalCurrencyOf(portId)` → currency name) — no reader guesses the unit.
- [x] **AC3.4** — `RelationsPanel` names monetary credibility where CU-4's term
      participates (BackedShare-derived, read-only, no new derivation; the
      panel had no natural row — minimal federation-context row added).
- [x] **AC3.G** — Phase gate ✅ (2026-07-22: dotnet **1289/1289** · golden
      byte-untouched (last golden touch `93a4ea1`, pre-Phase-2) · determinism
      green · Unity compile clean · EditMode **16/16** · AtlasSmoke **18/18**
      incl. `atlas-smoke-currency.png` with real zones) + **Eyeball 3**
      PENDING (currency mode zones; union shares a tint; polity panel
      reserve/backing/rate vs REPL line — panels are live-editor only).

### Phase 3 log
- **AC3.1 DONE** (`5bf598f`, Sonnet). `CurrencyLens` (new) — deterministic
  currency-id→color slots, retired zones drop out; fifth accent
  `DomainAccent.Currency` on `DomainFieldLayer`; `LensRail` + `LegendQuery`
  + `LegendDriftTests` in sync; smoke gained the currency shot. 1281 (+5).
- **AC3.2 DONE** (`a4a7de3`, Sonnet). `PolityCard` monetary block
  (currency/bank/claims incl. backing guard mirrored from the REPL);
  table-kit rows; REPL untouched, parity by shared-source-field test.
  1285 (+4). **Gap filed: numeraire-rate drift needs stored rate history —
  sim-side, route to roadmap at wrap-up.**
- **AC3.3+3.4 DONE** (`8bf9ee0`, Sonnet, combined — shared `PanelViews`
  edits). Market panel states its currency at header level (sentinel for
  currencyless); relations gained a minimal BackedShare credibility row
  (absent pre-genesis). 1289 (+4).
- **Phase 3 gate (AC3.G) GREEN** — evidence `.superpowers/sdd/ac3.G-gate.md`.
  → Eyeball 3.

## Phase 4 — Off-lane, events, debt sweep (L2 + cheap debt)  [spec §4]

- [x] **AC4.1** — Off-lane crawls render distinctly (direct hex-path,
      dashed/attenuated vs lane traffic); `ShipmentCard` gains off-lane status +
      detection-risk context (`PatrolCoverage.At`, read-only, choice #4).
- [x] **AC4.2** — Patrol-coverage readout on `FleetPanel` (falloff from dock,
      `OrbitGeometry`-based, read-only).
- [x] **AC4.3** — Event readthrough: `CargoSeized=409` off-lane variants +
      settle/outpost/graduation events read well in news/chronicle; fix copy/
      payload gaps found, nothing more.
- [x] **AC4.4** — Cheap debt: `OrbitRef` alias (`SystemStage.cs:9`) compile-
      verified in a real editor session; `AtlasSmoke` extended to render every
      lens including TRADE + currency mode.
- [x] **AC4.G** — Phase gate ✅ (2026-07-23: dotnet **1301/1301** after the
      Eyeball-4 overlap fix `df0f992` · golden byte-identical · determinism
      green · Unity compile clean · EditMode **16/16** · AtlasSmoke **18/18**
      trails visible) + **Eyeball 4 ACCEPTED** (2026-07-23, "lgtm") after the
      trail/crawl de-overlap; Obs-C (all-or-nothing off-lane routing) filed to
      sim roadmap.

### Phase 4 log
- **AC4.1 DONE** (`1d4123b`, Sonnet). Off-lane crawl paths: dashed direct
  hex-path strokes (all four purpose tints — any purpose can go off-lane) vs
  AC2.F2's solid two-purpose memory trails; `ShipmentCard` gains off-lane
  flag (`RouteLaneIds.Count==0`, choice #4) + "crossing patrolled space"
  context (`PatrolCoverage.At` read-only along the path). 1293 (+4). Taste
  carry: `WorksLens.CrawlDashMin/Max/OnFraction/CrawlPathAlpha` first-pass.
- **AC4.2 DONE** (`2df8b38`, Sonnet). FleetPanel patrol-coverage summary via
  `PatrolCoverage.At` max-over-candidate-victims (magnitude is victim-
  independent; zero when at war with nobody — true reading, posture gates
  absence). 1298 (+5). Judgment flagged for Eyeball 4: narrower
  "actual current enemies" read is a one-loop change if preferred.
- **AC4.3 DONE** (`175bcfc`, Sonnet). CargoSeized copy no longer implies a
  lane on off-lane seizures; fractional prizes legible. **Two sim-side
  payload gaps FILED (not fixed): `CargoSeizedPayload` can't distinguish
  on-lane vs off-lane seizure; graduation riding `PortEstablished` is
  indistinguishable from a fresh port.**
- **AC4.4 DONE** (`7721cda`, Sonnet). Sweep: smoke steps once (trails render
  headless now); 11 stray Epoch `.cs.meta` tracked; `OrbitRef` alias
  compile-verified (log evidence, no change); 18-shot coverage confirmed;
  `TradeLens.FlatAlpha` widened public, `LaneLayer` reads it (alpha-45
  duplication resolved).

## Slice-end wrap-up (per CLAUDE.md)

- [x] One fable whole-branch review (`model: fable`) + one fix wave.
      **Review (2026-07-23): FIX-THEN-MERGE, 0 Critical / 1 Important / 4
      Minor** (`.superpowers/sdd/ac-fable-review.md`) — independently
      re-verified 1301/1301, golden untouched, `ShipmentObserver` null path
      side-effect-free + never serialized, K3 parity + rail-key three-place
      contract + derived-not-stored + `WarPresenceMap` visibility-only
      widening all hold. **Fix wave (`1b25981` fix + `3a38b3f` scene
      rebuild):** Important — Atlas.unity regenerated with all Phase 1-4
      layers via `AtlasViewSceneSetup.RunFromCli` + defensive null guards on
      `AtlasRoot`/`LensRail` for domainInterior/outpost; Minors —
      `RegistryLegendTests` now guards `"currency"`, FleetPanel caption
      reworded to "hostile patrol exposure" (not own reach), contracts
      header open/in-transit count fixed. Re-gate: dotnet **1301/1301** ·
      golden untouched · compile clean · EditMode 16/16 · AtlasSmoke 18/18
      against the rebuilt scene.
- [x] Golden re-frozen once at slice end IF any intended change → **NOT
      re-frozen: asserted byte-untouched** (zero sim behavior; last golden
      touch `93a4ea1`, pre-Phase-2).
- [x] Merge to main locally → push → HANDOFF → diagram → kickoff → Trello.
      **DONE 2026-07-23:** merged `--no-ff` to main `701d01f`, **pushed**
      (1301/1301 on the merged tip) · `docs/HANDOFF.md` prepended with the
      AC-merged section + Slice UP next-up · living diagram §8/§9 updated
      (`973a1ba`) and **republished** to the stable artifact URL
      (`b8ce4102`, verified the live version was a strict subset first) ·
      next kickoff already existed (Slice UP, `2026-07-22-slice-up-kickoff-
      prompt.md`, gated post-AC — no new one needed) · Trello: AC card →
      **Merged** with full status, Slice UP unblocked, two Backlog cards
      filed ("Sim gaps surfaced by Slice AC" + "Atlas panel/visual polish
      pass"). **SLICE AC COMPLETE.**

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

- **Obs-C — off-lane routing is all-or-nothing and unpriced (Eyeball 4,
  2026-07-23).** User observed 30-40+ hex off-lane courier crawls spanning
  distances that would be 3-4 gate spans on the network. Verified cause:
  `ShipmentOps.PlanRoute` (`ShipmentOps.cs:136-153`) elects off-lane ONLY
  when `LaneNetwork.ShortestPath` finds no live path — and then the ENTIRE
  journey is one direct crawl. **No mixed routing exists** (lane spans + a
  short crawl across a gap): one missing link anywhere converts a mostly-
  laned journey into a full multi-decade crawl. Compounding: contract
  posting/acceptance nowhere weighs transit years (couriers with 50y+ ETAs
  get posted and fulfilled), and `PlanBestRoute`'s comment records that the
  value/urgency-weighted election was explicitly deferred ("design
  boundary"). Fix themes for the sim review: (a) mixed lane+off-lane path
  planning; (b) transit-time discount in courier posting/acceptance;
  (c) the deferred weighted election. Connects to the market-locality
  research's known off-lane locality gap.

Route at wrap-up to the flat/sparse-economy pass roadmap + `docs/HANDOFF.md`;
offer Trello cards. (Not fixed here — zero sim behavior.)

- **Eyeball 4 fix (atlas-side, FIXED `df0f992`):** live off-lane crawls
  double-drew — dashed crawl path + their own launch-step violet memory
  trail on the same line. `RecentFlowQuery.Trails` now suppresses a trail
  whose shipment is still in flight at the keyframe (live = crawl's job;
  trail = completed movement); `eflows` keeps every row, tagging
  `(in transit)`. 1301/1301 · compile clean · EditMode 16/16 · smoke 18/18.

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
- **AC2.1 DONE** (commit `78e3755`, Opus). `TradeLens` in Core.Atlas: `Segments
  (model,eye)` (live lanes only, `TradeSegment{LaneId,A,B,Spread,Band,Weight,
  Color}`), `Cells(model)` (the `emap trade` parity target, loop-identical),
  `BandOf` (emap glyph thresholds .05/.25/.50/1), `MarginGold` public for the
  legend, ONE private `SpreadOf` (saturation filter verbatim). 1234/1234 (+7).
- **AC2.2 DONE** (commit `fb361ca`, Sonnet). `emap trade` re-pointed at
  `TradeLens.Cells`; private `TradeCells`+threshold chain deleted (−36 net).
  Output byte-identical (stash before/after capture vs seed-42 artifact).
- **AC2.3 DONE** (commit `045524f`, Sonnet). TRADE on the rail: `LaneMode.Trade`
  stroke mode in `LaneLayer` (match by `TradeSegment.LaneId`, never index),
  three-way radio chip (lanes/traffic/trade), `LegendQuery` `case "trade"` from
  lens constants, `LegendDriftTests.RailKeys` += "trade" — the three-place
  contract landed atomically here. `atlas-smoke-trade.png`. **Trap logged:
  AtlasSmoke regenerates `Atlas.unity` — `git checkout` it before staging.**
  Minor for final review: `IdleTradeAlpha=45` duplicated (TradeLens.FlatAlpha /
  TrafficLens.IdleAlpha are private 45s too — widening was out of scope).
- **AC2.4 DONE** (commit `8d974f2`, Sonnet). Order-book panel: Core book query
  (asks+bids: owner/qty/grade/limit-vs-reference), `ebook` re-pointed
  (byte-identical), Market panel gains the book section (extend-don't-fork).
  1238/1238 (+4). Eyeball notes: no foldout yet (may run long at busy epochs);
  bid rendering TDD-covered but seed-42/y1750 sample showed asks only.
- **AC2.5 DONE** (commit `927664a`, Sonnet). Contracts panel: `ContractsPanel`
  Core query (open/in-transit, route/cargo/fee/WAR-flag/fulfiller),
  `econtracts` re-pointed (byte-identical, 130-row transcript), Unity dock
  panel (K3 THREADS/STATS pattern; drawer-vs-dock seam kept for Eyeball 2).
  1243/1243 (+5). Eyeball note: WAR tag reuses `ssg-tag--bad` (same as
  STALLED) — confirm the dual meaning reads.
- **AC2.6 DONE** (commit `18e615a`, Sonnet). Freight purposes:
  `FreightPurposeQuery` in Core.Atlas (the ONE derivation — decided choice #2),
  `efreight` re-pointed (byte-identical incl. a live war-convoy row),
  `WorksLens.FreightMark` purpose tints (+war convoys sized biggest; STALLED
  red still overrides), works legend +4 purpose entries (no rail-key change),
  `ShipmentCard` gains `Purpose`+`Rider` + panel link to the contracts board.
  1251/1251 (+8). Eyeball notes: rider link opens the whole board (no per-row
  deep-link); stalled war convoy distinguishable by size only; only WarConvoy
  gets a chip tag.
- **AC2.7 DONE** (commit `b812a47`, Sonnet). War-supply readout: forward depot
  (`FleetOps.NearestOwnedPortId`) on Fleet+War panels and both REPL surfaces;
  contested-lane shading via `WarLens.ContestedLanes` calling
  `ShipmentOps.WarPresenceMap` **widened private→internal (visibility+doc
  only — verified zero behavior)**, never re-deriving reach/posture rules.
  1256/1256 (+5).
- **Phase 2 gate (AC2.G) GREEN (2026-07-22): dotnet 1256/1256 · golden
  byte-untouched · determinism green in-suite · Unity compile clean (320KB
  log, 0 error CS) · EditMode 16/16 · AtlasSmoke 17/17 lenses.** Evidence:
  `.superpowers/sdd/ac2.G-gate.md` (worktree scratch). → Eyeball 2.
- **Eyeball 2 findings (user, 2026-07-22):** (1) *No freight visible anywhere.*
  Diagnosed to root cause — NOT a render bug: epoch-boundary keyframes (the
  only displayable moments) never hold lane-borne shipments (transit < the
  25y step; seed 42: zero at epochs 40–50, 16 by epoch 80 — all off-lane
  crawls, the only boundary survivors; the golden holds zero SHIP rows).
  Dead data sources verified: delivered couriers pruned (registry live-only),
  no shipment/courier event kinds, pulses carry event ids only. **User
  decision: recent flows for couriers + war convoys** — snapshot-in-time
  over discrete-step feel. (2) *Market/order-book panels too dense* —
  restructure as tables.
- **AC2.F1 DONE** (commit `7aff333`, Sonnet). Market + order-book + contracts
  panels as structured tables (DockKit table kit + AtlasChrome.uss; REPL
  formatting untouched, zero Core changes). dotnet 1256/1256. **User
  accepted; tuning deferred to a future full-panel design pass** (file at
  wrap-up). Unity compile for this commit rides the next batch run.
- **AC2.F2 DONE** (commit `6376180`, Opus). Recent flows: passive
  `ShipmentObserver` tap (transient `SimState` delegate, threaded per step
  by `EpochEngine`, reset in finally; null ⇒ bit-identical — asserted over
  12 stepped epochs), `TimeMachine` keeps per-keyframe flows in-memory
  (base frame honestly empty), `RecentFlowQuery` courier/war-convoy filter +
  corridor-aggregated `FlowTrailMarks`, Unity `FlowTrailLayer` under the
  lane strokes on the works chip (no new rail key), REPL twin `eflows`
  (~90 courier flows per estep on seed 42 while `efreight` shows zero).
  Design amendment: catch-up spec §2 freight bullet flagged. dotnet
  **1270/1270** (+14), golden byte-identical. **Unity batch gates for
  `7aff333`+`6376180` PENDING (editor was open) — run at next closed-editor
  window, then re-eyeball trails (alpha floor 70/cap 130 = first guess).**
