# Session Handoff — 2026-07-14 (Slice ME, Monetary equilibrium — MERGED)

State: `slice-me-monetary` merged to `main` locally (not pushed — push on
say-so). Gates at the ME merge: **900/900 dotnet** post-merge · determinism
byte-identity (reference artifact regenerated twice more, final refreeze
at merge) · full 8-seed × 4-variant committed ensemble sweep re-run twice
more post-merge-prep (see below) · fresh-eyes whole-branch review plus one
fix wave, then two further discovery-driven fix/review/sweep cycles inside
the same session · user eyeball accepted via a published dashboard
artifact (🪙) · merge accepted 2026-07-14. ProjectSettings churn stays
uncommitted. Prior handoff (Slice K5, System stage & closeout) below,
unchanged.

## Slice ME — monetary equilibrium (closed)

Design `docs/superpowers/specs/2026-07-13-monetary-equilibrium-design.md`;
ledger `docs/superpowers/plans/2026-07-13-slice-me-ledger.md` (7 tasks, all
reviewed clean). Fixes the treasury-spiral pathology SH diagnosed: every
polity's treasury crossed zero within a few epochs and never recovered,
mechanically, by construction (`AllocationPhase` swept `max(Credits,
Receipts)` into pools every epoch with the six budget shares summing to
exactly 1.0 — zero margin for upkeep/loan-service/tribute).

**What landed** (`src/Core/Epoch/Phases.cs`, `Policies.cs`,
`EpochSimConfig.cs`/`KnobRegistry.cs`, `Health/MetricsOps.cs`,
`ArtifactSerializer.cs`, `Loan.cs`):
- Allocation base decoupled from the stock (`Receipts` only, plus
  same-epoch borrowed/steady-minted amounts — see below); a declared
  `BudgetWeights.Operations` margin (never subtracted from `Credits`);
  idle-pool decay recycling Expansion/Development/Military points back to
  `Credits`; a household wealth levy (`Economy.WealthTaxRatePerYear`/
  `WealthTaxFloorPerPop`) recirculating excess segment wealth.
- Bounded sovereign issuance (`IssueSovereignCredit`) — a second declared
  mint beside the one-time entry endowment, capped at
  `SovereignIssuanceRate × Receipts`, runs AFTER `Borrow` each epoch (a
  same-session fix — issuance-before-borrow was crowding out the loan
  market entirely on the first pass, 97% fiat share, zero loans).
- A THIRD declared mint, steady growth-linked issuance
  (`SteadyIssuanceRate × Receipts`, every polity, every epoch, flows
  through the same budget split) — the user's "minting should be
  continuous, not a two-time event" correction.
- `Phases.Borrow` broadened to scan corporations as lenders too; borrowed
  principal now flows through the SAME epoch's budget split
  (`PolityRecord.BorrowedThisEpoch`) rather than sitting inert in
  `Credits` — a loan can now actually fund the fix for the deficit that
  caused it (the user's insight: "how is the polity going to pay back the
  loan" if borrowed money can't reach investment pools).
- `ServiceLoans`'s interest/amortization made tick-honest (compounds per
  world-year, matching `DecayIdlePools`'s shape — the old linear formula
  demanded ~100% of principal back every 25-year epoch, unpayable by
  construction). `Economy.LoanTermYears` raised 50→125.
- A debt-to-income borrowing gate (`Economy.MaxDebtToIncomeRatio`, a
  lender-side "credit score" check — no NEW loan to a borrower already
  over-leveraged) and a loan capitalization ceiling
  (`Economy.LoanCapitalizationCeiling`, forces default once a loan's
  principal doubles from its `OriginalPrincipal` — closes the residual
  runaway-debt tail the debt-to-income gate alone couldn't reach, since it
  only gates NEW borrowing, not an already-open loan's ongoing
  capitalization).
- `Money.CumulativeFiatIssued` / `Money.CumulativeSteadyIssuance` metrics;
  the conservation residual formula nets out all three declared mints now.
  `Loan.OriginalPrincipal` persists across save/load (markets v4).

**Acceptance** (the committed sweep,
`2026-07-12-debt-diagnosis-experiment.json`, re-run 4 times across the
session as fixes landed): `Polity.NegativeTreasuries` breathes 32/32 ·
live bounded loan market (max principal anywhere in the final ensemble
~37k, down from millions on two seeds before the capitalization ceiling)
· `cheap-credit` vs `baseline` diverge on every seed (dead knob alive) ·
conservation residual ≤1.3e-9 across all 32 histories. Dashboard artifact
(🪙, published, not committed — regenerate from the sweep CSVs if needed)
walks the before/after story.

**Filed as follow-up, NOT resolved this slice**:
1. `Segment.MeanSoL` still runs well below SIMHEALTH's healthy floor
   (~0.10–0.17 average final vs. SH's own pre-slice 0.32–0.45) and
   population contracts 3–10% from peak by end-of-history across variants
   — real, but outside this slice's acceptance criteria (spiral fixed ≠
   economy thriving).
2. `FederationOps.MergeInto` reissues a loan on polity absorption/
   federation without carrying over `OriginalPrincipal` — same class of
   ceiling-reset gap as the save/load bug fixed this slice, reached via a
   different trigger (pre-existing, not introduced here).
3. **The foundational one, driving the next kickoff**: `Credits` is one
   universal currency shared by every polity, corporation, and market —
   no exchange rates, no per-polity currency separation anywhere in the
   codebase. This slice gave EACH polity independent, unilateral minting
   authority over that shared currency (`IssueSovereignCredit` +
   `SteadyIssuanceRate`, decided per-polity, every epoch). Real-world and
   in-genre precedent (the Eurozone forbids member states from
   unilaterally printing euros for exactly this reason; EVE Online's ISK —
   this project's own repeatedly-cited precedent — is a single universal
   currency whose faucets are tuned centrally by CCP, never by individual
   players or factions) strongly suggests this is structurally unsound: on
   the sweep, 83–97% of the final money supply across variants is
   fiat-issued, and every polity's mint dilutes every OTHER polity's
   credits with no coordinating mechanism at all. Caught by the user
   reviewing the acceptance dashboard, not resolved in-session — the
   mechanism ships as-is (it fixed the spiral, validated across the
   ensemble) but is explicitly flagged as needing redesign. Next kickoff:
   `2026-07-14-slice-cu-currency-kickoff-prompt.md`.

## Prior handoff (Slice K5) — unchanged below

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
  everything else→the port body; port→most-settled body).
  Uncommissioned facilities fold into their construction sites (one
  thing, one mark). Layout angles are a pure fnv hash — no RollChannel.
  `FacilityPanel.Card` — type/family/tier/condition/active
  (≡ MarketEngine.IsActive, zero drift), owner with the corp REGISTRY id
  for panel links (id spaces differ from actor ids — review finding).
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
`ehealth`. **The treasury-spiral diagnosis** (its product):
`2026-07-12-debt-diagnosis.md` + dashboard artifact — structural,
universal, conserved; Allocation drains max(Credits, Receipts); the
2×-lender gate kills the credit market at epoch 1–4 (LoanRatePerYear is
currently a dead knob). Fix levers + acceptance criteria in the report.

## Carried / flagged

1. **Currency/minting model — the ME follow-on, kickoff ready** (ME
   fixed the spiral but gave every polity unilateral minting authority
   over ONE shared galaxy-wide currency — see Slice ME's section above,
   item 3): `docs/superpowers/plans/2026-07-14-slice-cu-currency-kickoff-prompt.md`.
2. **ME follow-ups** (not blocking, see Slice ME's section): SoL still
   below the healthy floor economy-wide; `FederationOps.MergeInto`'s
   loan-reissue doesn't carry `OriginalPrincipal`.
3. **SH deferrals**: expedition purses valued at CURRENT ColonyCost;
   O(events²) snapshot scan (trivial at 40 epochs).
4. **CE carried debt** (CE ledger C17): relay bids until the multi-hop
   trader slice; courier allocation fee-blind; stalled InTransit
   couriers can lock fee+cargo; capital-goods chains anemic.
5. Timeline branch switch-back UI · unbounded keyframe memory (K4).
6. Per-lens readability deep-dives + orbit-view polish (labels stayed
   OFF the stage — a deliberate divergence from the option-A mock;
   revisit if the System panel isn't enough) — backlog.
7. Menu F1–F4 stubs; NEW GALAXY → atlas seed handoff (post-roadmap).
8. SystemQuery runs per visible hex per rebuild (~25–50 at crossfade) —
   fine today; cache per (hex, epoch) if panning ever janks.

## Worktree / environment traps (verified through K5/SH — see the
K4/K5 ledgers' lists)

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
before any merge-out and fold main back in first.**

## Next up

1. **Slice CU (Currency & minting model) — research phase first**: the
   ME follow-on. `docs/superpowers/plans/2026-07-14-slice-cu-currency-kickoff-prompt.md`.
   The session opens with a deep research pass (real monetary theory on
   currency unions/shared-currency systems, how other games with a single
   universal currency handle faucets — EVE ISK is this project's own
   repeatedly-cited precedent) BEFORE any brainstorm/design/implementation
   commitment — do not skip to implementation.
2. **Slice K6 (The economy surfaces)** — parallel-safe with CU
   (worktrees): `docs/superpowers/plans/2026-07-12-slice-k6-kickoff-prompt.md`
   — TRADE lens on the rail, order-book + contracts panels, freight
   purposes on the map, war-supply readout; zero sim behavior.
3. **Slice L (Locality — bodies become addressable)** — a parallel
   session already produced its design + implementation plans + kickoff
   while ME was in flight (merged to main independently, not yet
   started): `docs/superpowers/plans/2026-07-14-slice-l-locality-kickoff-prompt.md`,
   design `docs/superpowers/specs/2026-07-14-locality-mega-slice-design.md`.
   Also parallel-safe with CU/K6 (worktrees) — this is the
   "population/fleet locality + genesis-vs-simulation body disconnect +
   off-lane gap" work the ME kickoff explicitly deferred.
4. **The gap-list backlog** — the roadmap's designated successor queue:
   `docs/superpowers/specs/2026-07-11-design-acceptance.md` (13 filed
   gaps: player verbs, perceived-price trading, sanctions, plague/war/
   fleet depth…), plus the carried flags above.
5. **Multi-hop actor runs** over perceived books (retires relay bids;
   the P3 trader edge) — unscheduled; measurable with the sweep harness.
6. User read-through of the design specs — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings
stays uncommitted; parallel slices take worktrees (never a shared
checkout); every new `src/Core` file gets a two-line `.meta` with a
fresh guid; the design is the spec — deviations amend `docs/design/`
in-branch, flagged. Unity gates: EditMode suite + AtlasSmoke batch twin
(editor 6000.5.2f1). Tuning conclusions clear the ensemble bar
(SIMHEALTH.md) before landing in TUNING.md.
