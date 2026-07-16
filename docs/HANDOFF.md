# Session Handoff — 2026-07-15 (Slice CU-1, Currency & FX — MERGED)

State: `slice-cu-currency` merged to `main` locally (via a merge of `main`
into the branch to fold in Slice L first, then a `--no-ff` merge out —
not pushed, push on say-so). 1014/1014 `dotnet test` post-merge · hex-tier
suite intact · determinism byte-identity (golden regenerated twice: once
mid-slice after the fleet-upkeep fix, once more folding in Slice L) · one
whole-branch fresh-eyes review (fable) plus one fix wave · the real 32-run
committed acceptance sweep re-run three times across the session as fixes
landed, the last time against the actual post-merge tip · merge accepted
2026-07-15.

## Slice CU-1 — currency & FX foundation (closed)

Research (`docs/superpowers/specs/2026-07-14-cu-monetary-theory-research.md`,
`-cu-game-precedent-research.md`, `-cu-genre-precedent-research.md`,
`-cu-mechanism-options.md`); design
`docs/superpowers/specs/2026-07-14-cu-currency-fx-design.md` (v2 — v1 was
itself caught short by a fable review before implementation started); ledger
`docs/superpowers/plans/2026-07-14-slice-cu-ledger.md` — 12 originally
planned tasks grew to 15 sequential tasks plus a whole-branch-review fix
wave and a main-merge task, every insertion driven by a real finding, not
scope creep for its own sake.

**What shipped**: replaces the sim's one universal `Credits` currency with
a `Currency` per living polity (`Supply`, a numeraire `NumeraireRate`,
per-currency mint/conversion counters), a deterministic per-epoch FX-rate
pass (a quantity-theory money-per-output density formula reading the
*prior* epoch's ending state, `Economy.FxSensitivity`/`FxReceiptsFloor`
knobs), one shared `ConvertCurrency` primitive fired at every
currency-crossing site (order-book fills/cancellations/refunds, freight/
tariffs, bilateral transfers — tribute/reparations/couriers/graduation
splits, federation/war absorption + loan reissue, migration, construction
wages, every port-ownership-change wealth transfer), corporations holding
real multi-currency wallets (`Corporation.Holdings`, `Deposit`/`Withdraw`
with a deterministic matched-bucket-then-ascending-id draw-down, no
overdraft — a deliberate asymmetry with polities, which can go negative
since they alone mint), and loans genuinely denominated in the **lender's**
currency (built as real mechanism, not a comparison-only patch — FX risk
sits with the borrower, converting at issuance and at every servicing
epoch at the *current* rate).

**The review chain earned its keep, repeatedly, across the whole session**
— each finding independently verified against source before being accepted,
several disproving an implementer's own first diagnosis:
- Task 1's `Corporation.Credits` bridge (a transitional `_legacyCredits`
  field while write-sites migrated) was correctly reasoned, verified by
  hand-tracing the getter/setter algebra, and fully removed by Task 7 once
  every caller migrated.
- A fable review of the v1 design (before implementation started) found the
  "convert only at order entry" model was structurally wrong — order-book
  fills/refunds/cancellations move money across currencies with zero
  conversion in the actual code. Caught before a single line of
  implementation landed.
- Task 6b found and fixed a real corp-wallet conservation leak (debit sites
  discarding `Withdraw`'s capped return, overcrediting counterparties).
- Task 7b found and fixed a project-bid-refund leak (`RefundTreasury`
  feeding a negative amount through a no-overdraft guard that silently
  swallowed it) — the *prior* task's implementer had misdiagnosed this
  exact bug as "pre-existing, out of scope"; a reviewer directly bisected
  commit-by-commit and proved it was a genuine in-slice regression first
  appearing at currency activation.
- Task 8 widened from a narrow two-site fix into a full cross-currency
  movement audit (per user decision, after the fourth "one more omitted
  site" surfaced) and found **7** total omitted conversion sites — this is
  the pass that got per-currency conservation genuinely holding for the
  first time, independently verified.
- Task 9 found `Currency.Supply` had zero write sites through the whole
  slice — every FX rate had been pinned at exactly 1.0 the entire time,
  every conversion bit-exact identity, the FX-risk mechanism never fired
  through any real gameplay path. Fixed with a genuine Supply-write pass;
  confirmed rates now visibly diverge in a real full-history run.
- **The most serious**: after Task 9's seed-42-only unit tests all passed,
  running the actual 32-run committed acceptance sweep for the first time
  found ~15/32 runs with conservation residuals 5-9 orders of magnitude
  over ME's tolerance, correlated with loan principal blowing past ME's
  validated bound. Root-caused (Task 14) to `FleetOps.DrawUpkeep` charging
  a foreign market's local-currency cost 1:1 against a polity's
  own-currency `MilitaryPoints` pool (fires when a fleet's home port is
  captured by another polity) — bisected to the Allocation phase
  specifically, with `MergeInto`/war absorption explicitly ruled out. Fixed
  and independently re-verified by re-running the full 32-run sweep from
  source: worst residual 1.123e-07 across all 32 runs (order-of-magnitude
  consistent with ME's tolerance, not the prior blowout).
- The final whole-branch review found three more real, precisely-located
  bugs (a stale-comparison-rate bug in `Borrow`'s debt ceiling; `MergeInto`'s
  loan reissue not converting a corp-lender loan's principal when the
  *borrower* changed — corp loans are borrower-denominated, not
  lender-denominated; the reissue dropping `OriginalPrincipal`, resetting
  the capitalization ceiling's runway at every absorption) — all three
  fixed as part of Task 14. **The third one turns out to be the exact
  gap ME filed as its own follow-up #2** (below) — now genuinely closed,
  not just for the new cross-currency case but for the plain same-currency
  case too (`newOriginal` is now always derived from `loan.OriginalPrincipal`,
  never silently reset to the current principal).

**Merging Slice L in**: `main` absorbed Slice L (locality) while this slice
was in flight, 55 commits ahead of this branch's merge-base. `main` was
merged into `slice-cu-currency` before the merge-out (not the other
direction) so this branch could resolve its own conflicts. Four conflicts,
all reconciled correctly (independently reviewed): `ArtifactSerializer.cs`'s
version tuple (no layer bumped by both slices — clean union); `docs/
TUNING.md` (purely additive); the golden (regenerated fresh, not
hand-merged); and — the one requiring real judgment —
`Health/MetricsOps.cs`, where this slice's per-currency conservation
residual rework had to survive fully intact underneath Slice L's new
`SettledHexes`/`BodyStockRemaining` fields, which it did (verified by
reading the merged file against both parents directly, not trusting the
auto-merge).

**Acceptance**: the real 32-run committed sweep re-run three times as fixes
landed (Task 14's fix, then again post-merge against the actual tip):
`Polity.NegativeTreasuries` breathes 32/32; `cheap-credit` diverges from
`baseline` on every seed (delta range -51.5% to +158.1% final supply);
worst per-currency conservation residual across the whole sweep 1.6e-7
(post-merge re-run — same order-of-magnitude class as the clean post-fix
number, not a regression); max loan principal in the sweep is a large
nominal figure in a `lean-labor`-variant run's weak currency, proven NOT a
leak by the clean residual at that same run/epoch (numeraire-converted
values stay small — this is exactly the kind of number the MoneyRow
docstring fix below exists to stop from reading as alarming).

**Filed as follow-ups, NOT resolved this slice**:
1. **Corp bankruptcy is now near-unreachable through normal play** — every
   corp debit caps at wallet holdings since Tasks 1/6b, so the
   `Dissolve(...Bankrupt)` path (`CorporationOps.cs`) can no longer fire
   through ordinary flow. A genuine regime change from pre-slice behavior
   (over-extended corps used to go bankrupt) — undecided whether this is
   intended (bankruptcy replaced by `NicheDied` as the only real exit) or a
   lifecycle gap worth a design look.
2. A handful of sub-`1e-12` dust sinks (`Corporation.Withdraw`'s drained-
   bucket remainders, `OrderOps.Prune`'s escrow floor, `ServiceLoans`'s
   force-zero after a partial-payment round trip) — bounded, currently
   absorbed by the conservation tolerance, not fixed, just noted.
3. The conservation tolerance quietly became relative (`≤1.3e-9 ×
   max(1,|Supply|)`) rather than ME's literal absolute bound — defensible
   (FP error scales with magnitude) but should be stated explicitly in the
   design doc rather than left implicit.
4. Three known, accepted scope-boundary gaps need consolidating into one
   documented list (currently scattered): `ProjectOps`' gate-pair project
   bids draw from `DevelopmentPoints`/`MilitaryPoints` pools that never
   convert when posting at a foreign-currency remote port (narrow — gate-
   pair projects only); the colony-purse 1:1 nominal re-denomination at
   absorption (explicitly the CU-1 absorption-stub boundary, CU-3's job
   per the design doc); an untriggered edge where migration to a genuinely
   unowned (`CurrencyId == -1`) port would drop wealth from all supplies
   (proven untriggered by the sweep passing, but not impossible).
5. **ME follow-up #1 (`Segment.MeanSoL` below the healthy floor
   economy-wide) is UNCHANGED** — out of scope for this slice, still open.
6. **ME follow-up #2 (`FederationOps.MergeInto`'s loan reissue not carrying
   `OriginalPrincipal`) is now RESOLVED** — confirmed fixed as part of this
   slice's Task 14 (see above); verified directly in code
   (`FederationOps.cs:485-496`), not just assumed from the fix's stated
   scope.

## Slice L — locality, two phases (closed, prior handoff)

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

## Slice ME — monetary equilibrium (closed, prior handoff)

Design `docs/superpowers/specs/2026-07-13-monetary-equilibrium-design.md`;
ledger `docs/superpowers/plans/2026-07-13-slice-me-ledger.md` (7 tasks, all
reviewed clean). Fixed the treasury-spiral pathology SH diagnosed — see prior
handoff content for full detail (allocation base decoupled from stock,
bounded sovereign issuance, steady issuance, broadened borrowing,
tick-honest loan servicing, debt-to-income gate, capitalization ceiling).

**Filed as follow-up, NOT resolved this slice**:
1. `Segment.MeanSoL` still runs below SIMHEALTH's healthy floor
   economy-wide. **Still open** — see Slice CU's follow-up #5 above.
2. `FederationOps.MergeInto` reissues a loan without carrying
   `OriginalPrincipal`. **RESOLVED in Slice CU** — see Slice CU's
   follow-up #6 above.
3. The foundational one, which spawned Slice CU: `Credits` was one
   universal currency with every polity minting unilaterally — no exchange
   rates, no separation. **Resolved by Slice CU** (above).

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
  guess-based attachment is what Slice L's atlas work replaced with
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
  yet compiler-verified — see Slice L follow-up #6 above).
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
histories, frozen by ConservationTests — **NOTE: Slice CU reworked this
into a per-currency residual** (see Slice CU's section above); the old
single-lump measure is gone. Sweep runner (`sweep <experiment.json>`,
byte-identical CSVs; `runs/` disposable). REPL `ehealth`. Gained
`Extraction.BodyStockRemaining` and `Settlement.SettledHexes` metrics in
Slice L (both families, both survived the Slice CU merge intact).

## Carried / flagged

1. **Slice CU follow-ups** (see Slice CU's section above): corp bankruptcy
   near-unreachable (regime-change question), sub-1e-12 dust sinks,
   conservation tolerance now relative not absolute, three known
   scope-boundary gaps needing consolidated documentation.
2. **Slice L follow-ups** (see Slice L's section above): adjacent-hex
   spillover, colony-founding bodiless dud, single-good stock assumption,
   FineTick band looseness, zero-richness dud construction, Unity compile
   verification, body-blind siting.
3. **ME follow-ups**: SoL still below the healthy floor economy-wide
   (still open); `FederationOps.MergeInto`'s loan-reissue `OriginalPrincipal`
   gap (RESOLVED in Slice CU).
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

## Worktree / environment traps (verified through CU/L — see the CU/L
ledgers' lists, carried from K4/K5/SH)

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
before any merge-out and fold main back in first** (Slice CU did exactly
this: merged `main`'s Slice L into the branch, resolved 4 conflicts,
re-ran the full sweep, THEN merged out). Windows worktree removal can
fail with "Filename too long" on deeply nested Unity `Library/
PackageCache` paths — use a `\\?\`-prefixed `cmd /c rd /s /q` (or
robocopy-mirror-empty) fallback, not plain `rm -rf`/`Remove-Item`.
**NEW (Slice CU):** a slice's own single-seed/unit-test-scale conservation
checks passing is NOT sufficient evidence conservation holds — the real
acceptance instrument is the full committed multi-seed sweep; run it
before declaring a conservation-sensitive milestone settled, not just at
the very end.

## Next up

1. **User atlas/REPL review** — the user's own call on when to look at both
   mega-slices (Locality, Currency) landed together and decide what to
   prioritize next. Not scripted further here — follow their lead.
2. **Slice CU-2 (Bank actor)** — the first of Slice CU's own forward
   roadmap (design doc's "Forward roadmap: CU-2 through CU-4" section):
   `docs/superpowers/plans/2026-07-15-slice-cu2-bank-actor-kickoff-prompt.md`.
   A new first-class actor type attached to a `Currency`, taking over the
   `ConvertCurrency` primitive's exchange-management role CU-1 left as a
   bare function call with a bookkeeping tally.
3. **Slice L2 (Population/off-lane)** — ready, not started:
   `docs/superpowers/plans/2026-07-15-slice-l2-population-offlane-kickoff-prompt.md`.
   Parallel-safe with CU-2 (worktrees).
4. **Slice K6 (The economy surfaces)** — parallel-safe with L2/CU-2
   (worktrees): `docs/superpowers/plans/2026-07-12-slice-k6-kickoff-prompt.md`
   — TRADE lens on the rail, order-book + contracts panels, freight
   purposes on the map, war-supply readout; zero sim behavior. Will now
   also want to surface per-currency prices/rates somewhere, given CU-1
   landed after this kickoff was written — flag to whoever picks it up.
5. **The gap-list backlog** — the roadmap's designated successor queue
   (item 10 in Carried/flagged above).
6. User read-through of the design specs — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings
stays uncommitted; parallel slices take worktrees (never a shared
checkout); every new `src/Core` file gets a two-line `.meta` with a
fresh guid; the design is the spec — deviations amend `docs/design/`
in-branch, flagged. Unity gates: EditMode suite + AtlasSmoke batch twin
(editor 6000.5.2f1). Tuning conclusions clear the ensemble bar
(SIMHEALTH.md) before landing in TUNING.md. When a whole-branch review
finds a root-cause problem bigger than a fix wave can address (Slice L's
Phase 1 richness-formula failure), reopen the design properly — brainstorm
→ new spec → new plan → subagent-driven-development — rather than patching
around it. **NEW (Slice CU):** for anything conservation/invariant-sensitive,
the real acceptance sweep is the instrument, not a single seed's unit tests
— budget time to run it, more than once, before the slice is declared done.
