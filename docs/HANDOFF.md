# Session Handoff — 2026-07-15 (Slice L, Locality — MERGED, both phases)

State: `slice-l-locality` merged to `main` locally at `4b0a8a6` (not pushed —
push on say-so). 942/942 `dotnet test` post-merge · determinism byte-identity
(golden regenerated twice: once at Phase 1 end, once more after Phase 2's
extraction rewrite) · two fresh-eyes whole-branch reviews (Phase 1, Phase 2),
each with one fix wave · REPL/Unity eyeball surfaced the finding that reopened
Phase 2 (see below) · merge accepted 2026-07-15. `slice-cu-currency` is a
**separate, still-open** branch/session — not part of this merge, see below.
Prior handoff (Slice ME, monetary equilibrium) folded in below, unchanged.

## Slice L — locality, two phases (closed)

**Phase 1** — design `docs/superpowers/specs/2026-07-14-locality-mega-slice-design.md`;
plan `docs/superpowers/plans/2026-07-14-locality-bodies-addressable-plan.md`
(9 tasks). Built the addressing foundation: `BodyRef(StarIndex, SlotIndex)`;
`SettledSystems` registry (epoch-tier, memoizes a hex's generated system the
first time anything touches it — hex tier still never persisted, only the
settled-hex *set* is); claim-aware facility body-assignment at groundbreaking
(fixes "two mines collapse onto the same belt"); `OrbitDistance` primitive;
atlas reads decided placement instead of guessing; `Settlement.SettledHexes`
metric.

**Phase 1's REPL/Unity eyeball surfaced something bigger than a bug**: the
user found a hex with a port and facilities in a system with **zero bodies**
in any orbit slot. Root cause (pre-existing, not introduced by Slice L):
`Siting.Score` ranks candidate hexes from regional raster fields entirely
decoupled from `BodyGenerator`'s independent per-slot body-kind roll (which
can legitimately null out every slot). Slice L's atlas work just made this
visible for the first time, by rendering the real committed system instead of
a fresh per-render guess that silently degraded to the same fallback.
Digging into *why this mattered* revealed Phase 1's own stated "throughline" —
extraction reading real body-level richness — was never actually built: it
was a bounded `[0.5,1.5]` multiplier (`RichnessModifier`) bolted onto
*unchanged* hex-aggregate `CellFields` math, going fully inert (neutral) for
any body-less or type-mismatched facility. The user, in their own words: "this
issue is literally the slice completely failing to address its original and
fundamental goal... A mine needs to extract resources from a planet or an
asteroid belt, those entities need to have a richness value derived from the
stellar genesis and turned into real mechanical resource values... think: A
rock has 1000 iron ore, a mining platform can take 100 ore out of it a year."
Design reopened, brainstormed fresh, Phase 2 built from scratch.

**Phase 2** — design `docs/superpowers/specs/2026-07-15-body-resource-stock-design.md`;
plan `docs/superpowers/plans/2026-07-15-body-resource-stock-plan.md` (7 tasks).
Replaced the multiplier-on-unchanged-math approach entirely:
- **Mine/ExcavationSite**: a real, finite, depletable per-body resource stock
  (`SimState.BodyResources`, `Dictionary<(HexCoordinate, BodyRef), Stock>`,
  reusing the existing `Stock(good, quantity, grade)` struct). Rolled once,
  lazily, at groundbreaking (regional richness sets the expected mean, a
  deterministic per-body hash gives real variance) — genuinely serialized
  (real mutable state, unlike the never-persisted hex tier), genuinely
  depleted over time, capped so a facility can never produce more than the
  body has left.
- **Skimmer/AgriComplex**: a renewable yield computed directly from the
  claimed body's own real generated attributes (gas-giant `Size`,
  `Biosphere`/`Hydrographics`) — real per-body variance, no depletion (a gas
  giant's mass and a living biosphere don't run dry at any facility's scale).
- **Groundbreaking now rejects** any extraction-type facility that resolves
  no eligible body at all — no Facility, no Project created — instead of
  silently building a permanently non-functional one. Applied uniformly
  across every facility-creation path: normal groundbreaking, colony founding
  (`CompleteExpedition`), and every new polity's entry starter industry (a
  gap the controller found mid-plan-review and folded in).
- `RichnessModifier`/`ExtractionPotential` retired entirely — zero remaining
  callers.

**Two whole-branch reviews, two fix waves**: Phase 1's review found the
richness formula didn't deliver real variance for belts/wreckage/gas-giants
(generator Size ranges didn't match the formula's assumptions) plus two
smaller gaps (genesis-path facilities rendering at deep-space instead of
falling back to the port body; richness leaking onto non-extraction
facilities) — all fixed same-session, which is what surfaced the deeper
architectural problem above. Phase 2's review found one Important item (a
stale doc comment) — fixed — and confirmed independently that the throughline
is real this time: `RichnessModifier`/`ExtractionPotential` have zero callers,
every facility-creation path shares one body-assignment/stock-roll helper,
determinism/conservation/tick-invariance all hold under direct trace.

**Along the way, Phase 2 also fixed a real, unrelated, pre-existing bug**: the
`BodyResources` registry (added in an early Phase-2 task) was never actually
wired into `ArtifactSerializer` — its own doc comment claimed it was
serialized, but nothing wrote it. This was silent and harmless until
extraction started actually depleting the registry, at which point
save→reload determinism broke. Found and fixed in the same task that first
exposed it (byte-identity tests can't be re-baselined — they compare two live
paths that must match exactly).

**Nine-plus emergent-history tests needed re-tuning** across both phases
(war/treaty/relations/fine-tick snapshots for the fixed seed-42 reference
history) as real, legitimate downstream consequences of facilities actually
producing real ore/yield for the first time (previously many rode `Body =
None`, producing nothing). Every single re-tune rests on an independently
verified real mechanism, not a threshold loosened until green — including one
case where the implementer's own first diagnosis was wrong and had to be
corrected by a dedicated investigation before the fix landed (disclosed, not
hidden, in the ledger).

**Filed as follow-ups, NOT resolved this slice** (all in the ledger,
`docs/superpowers/plans/2026-07-15-slice-l-ledger.md`, in full detail):
1. **Adjacent-hex spillover** when a hex's eligible bodies are all
   claimed/depleted — no facility can currently expand past a body-poor hex.
   Raised directly by the user mid-slice; deferred because it changes
   `Facility.Hex` semantics and touches the separate `Siting.cs` hex-ranking
   module — needs its own brainstorm/design pass, not a quick patch.
2. **Colony founding can still create a bodiless extraction dud**:
   `CompleteExpedition` doesn't reject on a `None` body the way groundbreaking
   does (justified for *starter industry* as mandatory civilization
   furniture, never argued for expeditions, where real resources are spent
   shipping equipment to a hex that may hold nothing). Same body-blind-siting
   root cause as #1.
3. `BodyResourceOps.Commit` assumes Mine/ExcavationSite are single-good
   (`Produces[0]`) — true today, but a second product on either catalog entry
   would silently double-drain the stock. Needs a guard/comment.
4. `FineTickTests`' provisions tolerance is now 0.85 (widened 4× across this
   slice) — nearly toothless; only fails past a ~6.7× coarse/fine divergence.
   Split into its own guard next time it's touched.
5. A Mine/ExcavationSite at a genuinely zero-richness hex still builds
   (rejection is body-*presence*-based, not stock-*value*-based) — rolls a
   0-quantity stock. Design-consistent, just noting it's not a covered case.
6. Unity `SystemStage.cs`'s `OrbitRef` alias (added Phase 1 Task 1, when the
   type moved to the Epoch layer) still isn't compiler-verified in this
   environment (no Unity compiler available) — outstanding since Phase 1,
   worth a real Unity-editor compile pass whenever the atlas is next opened.
7. `Siting.Score` itself stays body-blind (regional raster fields only,
   decoupled from what the hex-tier generator actually produces) — a
   deliberate cost tradeoff from the original design, not reopened here.

**Next kickoff, ready but not started**: the deferred "population/off-lane"
half of the original locality mega-slice design —
`docs/superpowers/plans/2026-07-15-slice-l2-population-offlane-kickoff-prompt.md`
(population segment body-refs, distance-weighted staffing, Patrol coverage
falloff, off-lane routing + detection roll) — now consumes `BodyRef`/
`OrbitGeometry`/`SettledSystems`/`BodySiting` exactly as they landed, plus the
new `BodyResources`/depletion mechanics from Phase 2. Item #1 above (adjacent-
hex spillover) is flagged prominently in that kickoff prompt as a likely-
related design question worth deciding together, not separately.

## Slice CU — currency & FX (in flight, SEPARATE session, not part of this merge)

`slice-cu-currency` branch: 23 commits, a full 12-task implementation
(currency data model, per-currency ledgers, FX rate recompute, corp wallet
integration, cross-currency conservation fixes, serialization, REPL surface,
a `markets.md` doc rewrite). Design docs (`2026-07-14-slice-cu-research.md`,
the v1/v2 currency & FX design) are on `main` already; the implementation
lives entirely on the unmerged branch. **As of this handoff, 2 tests fail at
the branch's tip** (`LaneBuilderTests.DefaultHistory_BuildsTreesAndHubs_NotAllPairsWebs`
among them) and it has not been through a whole-branch review. Being handled
in its own session — expect it to rebase onto this merge and manage its own
conflicts (likely touches `MarketEngine.cs`/`ArtifactSerializer.cs`, both
touched by Slice L too) before its own merge decision.

## Slice ME — monetary equilibrium (closed, prior handoff)

Design `docs/superpowers/specs/2026-07-13-monetary-equilibrium-design.md`;
ledger `docs/superpowers/plans/2026-07-13-slice-me-ledger.md` (7 tasks, all
reviewed clean). Fixed the treasury-spiral pathology SH diagnosed — see prior
handoff content for full detail (allocation base decoupled from stock,
bounded sovereign issuance, steady issuance, broadened borrowing,
tick-honest loan servicing, debt-to-income gate, capitalization ceiling).

**Filed as follow-up, NOT resolved this slice**:
1. `Segment.MeanSoL` still runs below SIMHEALTH's healthy floor economy-wide.
2. `FederationOps.MergeInto` reissues a loan without carrying
   `OriginalPrincipal` — same class of gap as a save/load bug fixed in ME,
   different trigger.
3. **The foundational one, which spawned Slice CU**: `Credits` was one
   universal currency with every polity minting unilaterally — no exchange
   rates, no separation. Slice CU (above) is the in-flight fix.

## Prior handoffs (K5, SH) — unchanged, folded below

## Slice K5 — the system stage (closed)

Kickoff `2026-07-12-slice-k5-kickoff-prompt.md`; ledger
`2026-07-12-slice-k5-ledger.md` (decisions, the T8 review verdict, two
eyeball waves, the re-learned batch trap). Living diagram republished
(§3 zoom caption, §7 SystemStage row as built, §9 System + Facility
panel rows).

- **Core** (`src/Core/Atlas`): `SystemQuery.At` — the orbit-view read
  model: the hex-tier system (stars, a ring row for EVERY slot, occupied
  orbit rows) computed on demand, never persisted, plus epoch overlays
  ATTACHED to orbits by deterministic type affinity (mine→belt/rock,
  skimmer→gas giant, agri→best biosphere, excavation→wreckage,
  everything else→the port body; port→most-settled body) — **NOTE: this
  guess-based attachment is what Slice L's atlas work (above) replaced with
  real decided placement.** Uncommissioned facilities fold into their
  construction sites (one thing, one mark). Layout angles are a pure fnv
  hash — no RollChannel. `FacilityPanel.Card` — type/family/tier/condition/
  active (≡ MarketEngine.IsActive, zero drift), owner with the corp
  REGISTRY id for panel links (id spaces differ from actor ids — review
  finding).
- **The fifth LOD band**: `LodBand.System` keys on ABSOLUTE distance
  (5.0, guarded for toy galaxies); `MapFade`/`StageFade` crossfade
  curves fold into every map lens (lanes/glyphs/lattice via the shared
  curves; ports/news/domain/nature/price via new OnZoom hooks;
  `AtlasBillboard` gained `_Tint`, `DomainField` gained `_MapFade`).
  Starfield deliberately never fades.
- **SystemStage** (`unity/Assets/Atlas/SystemStage.cs`): EVERY visible
  system hex renders while the crossfade is live (world-space meshes,
  rebuild keyed on the visible-hex set) — zooming magnifies one until
  it fills the view, no pop-in. Option-A orbit grammar (the
  `236896d9…` artifact): thin #262C3F rings per slot, dashed belts, a
  subtle habitable annulus, star core+halo, moons at the body's rim,
  settled worlds ringed #FFBF4F, layouts scaled to fit inside their hex.
  Vertex colors LINEARIZED (the washed-palette bug). Stage is coplanar
  with the lattice; draw order rides renderQueue. No text on the stage.
  Gained an `OrbitRef` alias to `Epoch.BodyRef` in Slice L Task 1 (not
  yet compiler-verified — see follow-up #6 above).
- **Same selection, same panels**: stage publishes typed pickables
  (port>facility/site>body priority on ties); star/body →
  **System panel** (NEW — the hex's system info: stars, every orbit,
  overlay links), facility → **Facility card** (NEW), site → Project,
  port → Market+Polity. Tooltip retitles to the hovered orbit thing.
  Selection ring is a screen-constant ~3px stroke.
- **Closeout sweeps**: PoC `unity/Assets/Scripts` remnant deleted;
  every runtime Mesh/Material/Texture2D in Assets/Atlas carries
  HideAndDontSave (the flag carried since K2 — closed).

## Slice SH — the sim-health harness (closed, parallel session)

Merged to main at 2926928, folded into K5 before its merge-out. Spec
`docs/superpowers/specs/2026-07-12-sim-health-harness-design.md`; ledger
`2026-07-12-slice-sh-ledger.md`; doc surface **`docs/SIMHEALTH.md`**.
The probe (`src/Core/Epoch/Health/`): MetricRegistry + MetricsOps,
always-on MoneyRow per phase + MetricRow/polity rows per epoch into
`SimState.Health` (in-memory ONLY, never serialized). Conservation:
entry endowment is the sim's only mint; residual ≈ 1e-8 across 32
histories, frozen by ConservationTests. Sweep runner (`sweep
<experiment.json>`, byte-identical CSVs; `runs/` disposable). REPL
`ehealth`. Gained `Extraction.BodyStockRemaining` and
`Settlement.SettledHexes` metrics in Slice L (both families).

## Carried / flagged

1. **Slice L follow-ups** (see Slice L's section above): adjacent-hex
   spillover, colony-founding bodiless dud, single-good stock assumption,
   FineTick band looseness, zero-richness dud construction, Unity compile
   verification, body-blind siting.
2. **Slice CU** is in flight in a separate session (see above) — expect a
   rebase/merge from that session; re-check `git log main` before assuming
   this handoff's state is still current.
3. **ME follow-ups**: SoL still below the healthy floor economy-wide;
   `FederationOps.MergeInto`'s loan-reissue doesn't carry `OriginalPrincipal`.
4. **SH deferrals**: expedition purses valued at CURRENT ColonyCost;
   O(events²) snapshot scan (trivial at 40 epochs).
5. **CE carried debt** (CE ledger C17): relay bids until the multi-hop
   trader slice; courier allocation fee-blind; stalled InTransit
   couriers can lock fee+cargo; capital-goods chains anemic.
6. Timeline branch switch-back UI · unbounded keyframe memory (K4).
7. Per-lens readability deep-dives + orbit-view polish (labels stayed
   OFF the stage — a deliberate divergence from the option-A mock;
   revisit if the System panel isn't enough) — backlog.
8. Menu F1–F4 stubs; NEW GALAXY → atlas seed handoff (post-roadmap).
9. SystemQuery runs per visible hex per rebuild (~25–50 at crossfade) —
   fine today; cache per (hex, epoch) if panning ever janks.
10. The roadmap's designated successor queue:
    `docs/superpowers/specs/2026-07-11-design-acceptance.md` (13 filed
    gaps: player verbs, perceived-price trading, sanctions, plague/war/
    fleet depth…).
11. Multi-hop actor runs over perceived books (retires relay bids; the
    P3 trader edge) — unscheduled; measurable with the sweep harness.

## Worktree / environment traps (verified through L — see the L ledger's
list, carried from K4/K5/SH)

Gitignored `unity/Packages/manifest.json` / `packages-lock.json` /
`src/Core/csc.rsp` must be copied into fresh worktrees before Unity
batch runs; **batchmode dies in ~2s (exit 1, ~1KB log) while the editor
holds the project — and a trailing `echo exit: $?` masks the failure;
verify log size + output mtimes**; MCP bridge approval is per-project;
goldens are CRLF on disk; PowerShell mangles piped stdin (bash
`printf`); vertex colors need explicit `.linear` in the linear
pipeline; `runs/` is disposable (never keep the only copy of anything
there); the health series is in-memory (step before `ehealth`);
**parallel sessions can move `main` mid-slice — re-check `git log main`
before any merge-out and fold main back in first.** **NEW (Slice L):**
Windows worktree removal can fail with "Filename too long" on deeply
nested Unity `Library/PackageCache` paths — use a `\\?\`-prefixed
`cmd /c rd /s /q` (or robocopy-mirror-empty) fallback, not plain
`rm -rf`/`Remove-Item`.

## Next up

1. **User atlas review** — the user's own stated next step: open the Unity
   atlas, review it thoroughly with both mega-slices (Locality, and
   eventually Currency) landed, and decide what fixes/next work to prioritize
   from there. Not scripted further here — follow their lead.
2. **Slice L2 (Population/off-lane)** — ready, not started:
   `docs/superpowers/plans/2026-07-15-slice-l2-population-offlane-kickoff-prompt.md`.
   The natural next locality increment once the user is ready for it.
3. **Slice CU (Currency & FX)** — in flight in a separate session (see
   above). Will need its own merge decision once that session lands it.
4. **Slice K6 (The economy surfaces)** — parallel-safe with L2/CU
   (worktrees): `docs/superpowers/plans/2026-07-12-slice-k6-kickoff-prompt.md`
   — TRADE lens on the rail, order-book + contracts panels, freight
   purposes on the map, war-supply readout; zero sim behavior. Its own
   kickoff already flags a sequencing note: some panel work may need
   touch-ups now that Facility/Project carry real body refs and real stock.
5. **The gap-list backlog** — the roadmap's designated successor queue
   (item 10 above).
6. User read-through of the design specs — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings
stays uncommitted; parallel slices take worktrees (never a shared
checkout); every new `src/Core` file gets a two-line `.meta` with a
fresh guid; the design is the spec — deviations amend `docs/design/`
in-branch, flagged. Unity gates: EditMode suite + AtlasSmoke batch twin
(editor 6000.5.2f1). Tuning conclusions clear the ensemble bar
(SIMHEALTH.md) before landing in TUNING.md. **NEW (Slice L):** when a
whole-branch review finds a root-cause problem bigger than a fix wave can
address (Phase 1's richness-formula failure), reopen the design properly —
brainstorm → new spec → new plan → subagent-driven-development — rather than
patching around it; the design-is-the-spec rule cuts both ways.
