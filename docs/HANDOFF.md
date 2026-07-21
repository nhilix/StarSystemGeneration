# Session Handoff — 2026-07-20 (Slice DX MERGED + PUSHED · a starport's domain comes alive)

**Slice DX — domain hex expansion — MERGED & PUSHED** to `main` at `6ab5f0f`
(`--no-ff`, pushed). **1218/1218** `dotnet test` on the merged tip · 32-run
conservation sweep worst relative residual **6.3e-15** (all three DX money flows +
the graduation cross-currency fix) · determinism byte-identity · graduation fires
**22/32** sweep runs (40 ports) · one fable whole-branch review (**MERGE**, 0
Critical/0 Important/0 Minor) · golden re-frozen fresh at slice end. Design
`docs/superpowers/specs/2026-07-16-domain-hex-expansion-design.md`; ledger
`docs/superpowers/plans/2026-07-16-slice-dx-ledger.md`.

**What DX shipped — a starport's service radius stops being a glow and becomes a
living region.** Three phase gates (the Slice L pattern):
- **Stage 1 — Satellite workings.** `CapabilityOps.ConstructionCandidatesFor` scans
  per **hex** across a port's domain (was per cell), body-aware: extraction scores on
  the best eligible-unclaimed body (settled → real `SettledSystems`/`BodyResources`;
  unsettled → a **roll-free `Generator.Generate` preview**, discarded), discounted by
  labor commute **and a fuel-grounded hauling term** (`FuelPerUnitPerHex × (hexDist +
  orbitalSteps) × fuelPrice`, discounting by the value-fraction fuel eats) **and a
  dispersion term** (fans same-class extraction off the port body). Support/processing
  keeps port affinity. Corps run the same scan scoped to `HomePortId` (the owner-filter
  latent bug, fixed). No new persisted state.
- **Stage 2 — Outposts.** `PopulationSegment.Hex` (serialized, segments **v3→v4**) +
  `SimState.Outposts` (new `outposts` layer). A world-time **settle election**
  (`Interior/SettleOps.cs`, `RollChannel.OutpostName = 79`) relocates the **smallest**
  eligible household (keeps the port core peopled — user call) to the **most-under-served**
  worked hex, spending its own `Wealth` on habitat wages (conserved), founding an
  `Outpost`. Staffing rewired to `seg.Hex`; production wages redirect to residents
  (`MarketEngine.PayProductionWages`, staffing-weighted, conserved).
- **Stage 3 — Frontier graduation.** A mature outpost **≥ G from every port core**
  (`G = 1 + GraduationMarginHexes`, default **2** — literal anti-adjacency: "never
  found a port adjacent to another"; parent counted; NOT domain-scale) enters the same
  polity expansion scoring (`GraduateOutpostAct`, `ProjectKind.OutpostGraduation`), cost
  = `ColonyCost` discounted by facilities+pop from `ExpansionPoints` recycled as
  promotion wages (conserved), completing **in-place** into a tier-1 `Port`+`Market`,
  re-attaching residents (cross-currency-safe), firing the encroachment bump.

**The Stage-3 saga (why this slice was long) — three corrections, all user-adjudicated:**
1. **Gate geometry** was twice conflated with the *expedition reach-leap*
   (`EncroachedPolities` sum, then `ServiceRadius(1)+margin`) — both demanded an outpost
   sit *outside* its parent's domain, impossible since outposts form *inside* it.
   Corrected to **densification**: `G=2` literal anti-adjacency (§4 amended twice).
2. **Hauling** was an arbitrary decay — **regrounded** to real freight (§2 amended),
   user-brainstormed.
3. **The domain barely spreads** because the sim's **economic map is flat & sparse**
   (uniform body value at every distance, ~2.6 industry facilities/domain) — DX
   *exposed* a pre-existing, sim-wide gap. Decision: **ship DX honest** (dispersion term
   + G=2 make graduation fire, rarely, on the flat map), file the root fix as a pass.
   *A conservation leak that only surfaced once graduation fired (a bare `PortId`
   re-attach re-denominating resident wealth 1:1 across a currency boundary) was
   root-caused and fixed (convert + `RecordConversion`, the `ConvertPortHoldings`
   pattern).*

**Follow-ups DX filed (in the design's Forward roadmap) — the natural successors:**
- **Flat & sparse economic geography (THE domain-bloom lever).** Heavy-tail body value
  (few rich, many poor → a real reason to reach for a frontier body) + higher build
  density. Genesis/economy pass, sim-wide, the same "richness needs variance" theme
  Slice L hit. **This is the standout next step — it makes DX's machinery actually
  bloom.**
- **Localize goods → real freight.** Output posts directly to the port market with no
  address; until goods live at a hex/body and must be *hauled*, freight is an estimate.
  Prerequisite for hauling as a real runtime force (DX's deferred "Option B"). Pairs
  with the flat-economy pass.
- **Outpost abandonment as a first-class event** (residents-to-zero / workings deplete →
  a real triggered event with news/REPL/metric).

**NEXT UP:** no forced chain (the CU chain is closed; DX is done). Recommend the user
pick from: **(a) the flat/sparse-economy pass** (DX's standout follow-up — a
brainstorm→spec→slice, makes domains genuinely come alive), (b) **WT** (war termination,
kickoff ready, parallel-safe), (c) **K6** (economy surfaces / TRADE lens), (d) **PL**
(nominal price level), or (e) the gap-list backlog. Write the chosen kickoff once they
decide (the flat-economy one starts with a brainstorm).

**Standing preference update (2026-07-20):** **push on merge by default** — the user
clarified "not pushing isn't really a convention; push whenever you merge unless I say
otherwise." (Older HANDOFF/CLAUDE text saying "push only on say-so" is superseded.)

---

# Session Handoff — 2026-07-18 (Slice CU-4 MERGED + PUSHED · the CU chain is CLOSED)

**Slice CU-4 — bank/currency-union strength → federation generation — MERGED &
PUSHED** to `main` at `9947532` with `--no-ff`. 1145/1145 `dotnet test` on the
merged tip · 32-run conservation sweep **1.223e-15 relative** (= CU-3) · clock
instrument telescopes (`live_polities` byte-identical across 25/5/1y) · no runaway
(Polity.Live 59.6→59.4) · one fable whole-branch review (**MERGE**, 0
Critical/Important-blocking; one fix wave cleared 2 Important test/doc + 1 Minor) ·
golden re-frozen (diff is *exactly* the two knob-stamp lines — seed-42 is a 0→0
no-op; the effect fires on other seeds). Design
`docs/superpowers/specs/2026-07-18-cu4-monetary-federation-design.md`; ledger
`docs/superpowers/plans/2026-07-18-slice-cu4-ledger.md`.

**What CU-4 shipped — the CU chain's closing link (economy ←→ politics).** Monetary
credibility now feeds the federation *decision*, so the economy the chain built
shapes the political map:
- **`Bank.BackedShare`** = `Reserve/(Reserve+ClaimOnState)` (0/0→0) — one bounded
  [0,1] signal (1 = saver, 0 = debtor), the sibling of BF's FX `unbacked`. Shared
  guard `SimState.BackedShareOf(currencyId)`.
- **Peer fusion** — `FederationCredibilityDiscount × min(cred_A, cred_B)` discounts
  the federation warmth gate, **mirrored across BOTH** the true gate
  (`FederationOps.FederationGateHolds`) and the perceived offer gate
  (`ControllerContract.EffectiveGate`) — a true-gate-only discount is inert (offers
  filtered upstream). Plumbed live via `RelationBrief.OtherCredibility` +
  `PerceptionView.OwnCredibility` at the `Phases.cs` snapshot build (the
  `OverlapShare` precedent — **no serialization change**).
- **Vassal absorption** — `VassalAbsorptionCredibilityDiscount × max(0, cred_overlord
  − cred_vassal)` eases only the warmth bar in `VassalExits` (the world-year duration
  gate is untouched → clock-invariant by construction; `max(0,…)` = a more-credible
  vassal earns no discount but never a penalty).
- Two knobs, both registered in `KnobRegistry`, **activated at 0.20** (validated on
  the committed instruments via an inert-at-0 checkpoint, BF discipline).

**The emergence, now a mechanism** (was CU-3's accidental gift): a monetarily weak
vassal (cred 0) under a credible overlord (cred 1) is peacefully absorbed where the
plain warmth bar would leave it bound forever — eyeball seed 9091 (Lusshaka absorbs
Misha) and seed 31337 (Nyduzen absorbs Thano).

**The whole CU chain is now CLOSED: CU-1 ✅ → CU-2 ✅ → BF ✅ → MC ✅ → CU-3 ✅ →
CU-4 ✅.** No CU-5. Follow-ups noted, not chained (see the CU-4 "carried" block below).

**Also live, not next:** WT (war termination, L2-chained, parallel-safe) and DX
(domain hex expansion — HELD lifted 2026-07-17, a `dx-worker` was spawned parallel to
CU-4; check its status). Neither depends on CU-4.

## Carried from CU-4 (follow-ups, none blocking)

1. **Effect concentrates on the absorption seam at 0.20.** Peer fusion needs a rarer
   both-credible near-threshold pair (the `min` rule), so no *new* fusion appeared in
   the sampled seeds — the fusions that occur fire regardless of the knob. Clean
   headroom to **0.30 / asymmetric knobs** (fusion higher than absorption) if a more
   legible fusion effect is ever wanted — validated-safe on conservation/clock.
2. **Credibility is read LIVE, not belief-mediated.** `OtherCredibility` is computed
   at snapshot from true state (the `OverlapShare` precedent). A future slice could
   route it through `BeliefOps` so foreign monetary standing is *discoverable* (stale
   at distance, like `OtherStrength`). Deliberately not done — structural-input
   treatment.
3. **Binding gate + seek-protector trigger stay military-driven.** CU-4 eases only
   absorption *completion*, not vassalage *entry*. Making monetary collapse itself
   initiate a bid for protection is the perceived-view trigger change CU-4 declined.
4. **War/conquest decisions untouched** — debtor-conquest stays emergent (Slice WT's
   seam).

## Slice MC — P7-clean polity entry + clock-invariance instrument (MERGED)

**The defect (proven):** the sim's economy was clock-dependent (differed between
25y/epoch and 1y/epoch) — a P7 violation. The polity entry gate mis-multiplied
units: `EpochGenesis` wrote `EntryEpoch = entryYear / YearsPerEpoch`, while
`Phases`/`NativeOps` read `EntryEpoch × GenerationYears` — agreeing **only** at the
default `YearsPerEpoch == GenerationYears == 25`. Invisible at the default clock;
at 1y it stretched the whole emergence schedule 25×, admitting ~25× fewer
polities.

**The fix:** replaced `Actor.EntryEpoch` with `Actor.EntryYear` (an unambiguous
world-year). Genesis is now **clock-invariant by construction** (that division was
its only clock read). Killed the **collapse regime** entirely (5 seeds where the
fine clock's *real* economy died) and made the ports ratio ≥1.0 on 20/20 seeds.
Deliberately moved the default 25y product (polities no longer enter up to 24
years early via truncation) — **user-approved**; golden re-frozen.

**The instrument (the durable deliverable):** the project's first **committed
clock-invariance sweep** — `src/Core/Epoch/ClockPlan.cs` + a `SweepRunner` clock
mode + `docs/superpowers/plans/2026-07-17-clock-invariance-experiment.json` (20
seeds × {25y,5y,1y}, ~5s). It holds world-time constant by construction
(`epochs × YearsPerEpoch = span`, refusing the epochs+worldYears mix, non-divisible
spans, duplicate clocks) and reports **nominal vs real divergence separately** —
the analytical frame that **five prior throwaway-harness diagnoses all blurred**.
On its first use it settled a 5× disagreement between two prior harnesses. No P7
claim should ever again be made with a throwaway harness — extend this instead.

**The lesson of this slice (write it on the wall):** **five successive diagnoses
of the clock-dependence were each refuted** — the issuance cap (never binds at
25y), mint feedback (zeroing both mints leaves 16×), price drift (~11%; a demoted
symptom), the band-bid budget cap (neutralizing it: 61.5×→61.5×), and one whole
investigation invalidated by its own harness applying the clock after genesis.
Every one was plausible; instrumentation killed each. The root cause only fell to
a **removal test** (a no-op at 25y; `live_polities` → exactly 1.000× on 20/20) that
no prior candidate survived. This is `[[market-clock-dependence]]`'s real ending.

**Handed on as its own slice (characterized, not chased):** the surviving
"diverge regime" is a **nominal price-level divergence** — the *same goods* change
hands ~1.5× more (real, ≈ clock-invariant) at ~10.7× the value per unit (nominal).
The churn hypothesis (receipts double-counting) was measured dead (churn multiple
1.02× against a clock ratio of 25). The sharp lead for that slice: **re-measure
demoted diagnosis #3 (price drift) with a volume-weighted realized clearing
price** — it was demoted on an *unweighted reference* price, a different statistic,
measured pre-instrument. New instrument metrics `Economy.GoodsTransacted` (real)
and `GoodsValueCleared` (nominal) are in place for it.

## Slice BF — the bank as monetary authority (MERGED at 0bdb009)

Branch `slice-bf-bank-flow` (worktree `.worktrees/slice-cu3` — a stale directory
name from the mid-session pivot). Merged to main `0bdb009`. All 13 ledger tasks
done: tasks 1–5b before the MC park, then 6 (residual retirement term) · 7 (FX
backing) · 8 (REPL claim surface) · 9 (sweep + backing characterization) · 10
(eyeball accepted) · 11 (fable review MERGE + fix wave) · 12 (golden freeze) · 13
(this merge). Design `docs/superpowers/specs/2026-07-16-bf-bank-flow-design.md`;
ledger `docs/superpowers/plans/2026-07-16-slice-bf-ledger.md`. The narrative below
is the slice's history (it opened as CU-3, pivoted, parked behind MC, resumed).

**How this session got here:** it opened as **CU-3** (currency consolidation),
whose kickoff demanded a sequencing decision on CU-2 follow-up #1
(`[[bank-reserve-flow-gap]]`). The orchestrator recommended CU-3-first; **the user
chose to fix the prerequisite first** → the session pivoted to BF. BF then hit its
*own* prerequisite and the user made the same call again → Slice MC. CU-3's
kickoff (`2026-07-16-slice-cu3-kickoff-prompt.md`) stays on disk, unstarted, still
valid.

**What BF is:** closes CU-2 follow-up #1 — the bank's reserve had one inflow (the
FX spread, ~0.1% of deficit funding), so it could never intermediate. Rather than
levying every receipt site, BF makes the sim's existing dominant chokepoint a bank
operation: `IssueSovereignCredit` → `Bank.LendToState`, moving the bank to ~100%
of deficit funding **without touching a receipt site**. The bank gains an asset
side (`ClaimOnState` — a claim, not a holder, per `MetricsOps.cs:24`'s
LoanPrincipal precedent); servicing is **surplus-only** and interest **never
capitalizes** (no compounding term exists anywhere — that is the entire
spiral-proofness argument, and why it needs no `LoanCapitalizationCeiling`
analogue); principal repayment **destroys money** — the sim's first monetary sink.
Backing ratio feeds the FX rate; **no reserve gate**, so ME's lender-of-last-resort
floor stays absolute.

**Landed (tasks 1–5b):** data model `aaefa5f` · serialization `b2dea02` (banks
v1→v2, markets v5→v6) · knobs `feb8abe` · `LendToState` `82cc074` · servicing +
money sink `65cdf4a` · design amendment §4a `3983ec2` · year-scaled servicing
`6b5bb49` · park `aa5ce62` · record correction `d8bc4ad`.
**NOT started: tasks 6–13** (residual term, FX backing term, REPL, sweep + backing
activation, eyeball, whole-branch fable review, golden freeze, merge).

**Known-red on that branch, all expected** (detail in the ledger header): the
standing mid-slice golden; **6 negative** conservation residuals (the task-6 gap —
the residual does not yet subtract `CumulativeFiatRetired`; task 6 closes them);
and `FineTick_ProjectCompletions`, which **only Slice MC can green**.

**On resume:** fold main in first, then **re-baseline BF's knob defaults (§9)
before task 9's sweep** — MC will move every price and receipt.

### The two design findings BF earned (both from evidence, neither from review)

1. **§4a — "time, not ticks" (amended in-branch, `3983ec2`).** The original §4
   charged a fraction of a treasury *stock* once *per epoch*, so servicing
   intensity scaled as 1/`YearsPerEpoch` (treasury diverged 4× between clocks).
   Now the share compounds per world-year (`1 − (1 − s)^years`, `DecayIdlePools`'
   precedent) while **interest stays LINEAR in years** — because rule 2 forbids
   compounding, the claim cannot grow between world-years, so each accrues on the
   same principal. Knobs renamed to the repo's `PerYear` convention. Both hard
   rules still hold **structurally, not by a clamp**.
2. **§3's freeze of ME's issuance cap is CORRECT and stands.** Two plausible
   diagnoses were **refuted by experiment**: the cap **never binds at 25y, not
   once**; and zeroing both mints still leaves receipts diverging 16×. Do not
   amend §3. This is the session's clearest lesson — the first plausible story was
   wrong twice, and only instrumentation settled it.

## Slice MC — P7-clean nominal price formation (NEXT; kickoff written)

**The defect** (`MarketEngine.DriftReferencePrices`, `MarketEngine.cs:1132–1169`,
**re-verified present on main**): demand is normalized per generation
(`/StepFraction`) but supply is the **raw resting-ask stock**, whose size scales
with step length. A 25y step dumps 25 years of production into one clearing →
permanent glut → prices pinned at 1.000. A 1y step trickles → stock-outs
(`supply = 0 → demand/eps ≈ 1e9 →` clamped every step) → prices at the 59–100
ceiling. **The two clocks sit in opposite saturated price regimes.** Nominal
receipts/yr diverged 68× while the real economy diverged only 2–3× — the tell that
this is nominal, not real. Structural across seeds; a dead world diverges 1.0× as
a clean control.

**⚠ The investigation's magnitudes are pre-L2** (measured at `768a8e4`). L2's own
two time-not-ticks fixes (facility groundbreak cadence; hull-batch build duration)
touch **neither** `DriftReferencePrices` — so the *diagnosis* stands and was
re-verified against main's source — but every *number* must be re-measured on main
before being quoted. Both the kickoff and the investigation carry this caveat.

**The obvious fix is already tested and REFUTED** (Finding 6): drop `/StepFraction`
and compound `factor^years` — the formula correct on paper — improves 16×→5× but
**does not converge and inverts**. `unsoldAsks` is inherently step-length-dependent,
so no reformulation *of the drift alone* works. A real fix must address supply
batching (flow-vs-flow, or a normalized ask window) — a design question, hence a
brainstorm, hence a slice.

**Not in tension with the LOLR floor** (Finding 7): the cause is upstream and
monetary-policy-agnostic. The **converse** risk is the one to watch — a fix raising
coarse receipts ~16× moves ME's monetary operating point, and *that* needs ensemble
validation per the SH bar.

**Also MC's job — correct the record:** `FineTickTests`' docstring asserts
*"confirmed: no per-tick-vs-per-year formula defect ... legitimate
economy-trajectory drift"*, and its band has been widened **three times
(0.6→0.85)** absorbing this exact defect under that false explanation. The test has
been the sim's early-warning system for a structural bug and was progressively
silenced. Correct it; re-tighten the bands to what the fixed engine supports.

## Slice L2 — population & off-lane (closed, MERGED and PUSHED at `dda698b`)

State: `slice-l2-population-offlane` merged to `main` at `dda698b` with
`--no-ff` — **pushed** (user say-so, 2026-07-16). 1081/1081 `dotnet test`
post-merge · hex-tier suite intact · determinism byte-identity · golden
re-frozen (three times across the slice: once at slice end, once after the
review fix wave, once more when main's CU-2 was folded in — regenerated
fresh each time, never hand-merged) · one whole-branch fresh-eyes review
(fable): **MERGE, no Critical**, two Important, both fixed in one wave · the
32-run committed acceptance sweep re-run on the merged tip with CU-2 folded
in — **worst conservation residual ratio 4.126e-15** against a 1.3e-9
tolerance · merge accepted 2026-07-16.
Kickoff `docs/superpowers/plans/2026-07-15-slice-l2-population-offlane-kickoff-prompt.md`;
ledger `docs/superpowers/plans/2026-07-15-slice-l2-ledger.md` (7 planned tasks
grew to 10 + a fix wave — Tasks 8-10 were user-approved root-cause fixes for
invariant breaks the slice UNMASKED, not scope creep).

## Slice L2 — population & off-lane (closed)

**What shipped** (the deferred half of the locality mega-slice design — this
CLOSES the locality arc: Phase 1 addressing + Phase 2 body stocks + L2):
- **Population segments carry a real body address** (`PopulationSiting.Assign`,
  `PopulationSegment.Body`), set at all four creation sites (homeworld,
  migration's `FindOrFoundSegment`, colony founding, native emergence). The
  first time population data exists BELOW the port. Visible in the REPL:
  `market <portId>` now prints each segment's body.
- **Colony founding skips bodiless extraction duds** — mirrors groundbreaking's
  reject (closes Slice L follow-up #2).
- **Body claims became per-resource-class** (user design call): a Mine
  (depleting ore) and an AgriComplex (farming the biosphere) share one rich
  rocky world; only same-class extractors exclude each other, and
  Mine/ExcavationSite still exclude each other because they share the single
  `(hex,body)` stock. `BodySiting.CompetesForBody`.
- **The AgriComplex 0.3 renewable floor is gone** (same user call): agri yield
  derives purely from biosphere + hydrographics, 0 at a fully barren dry body —
  so a barren-hex colony gets a Mine and imports food. Skimmer's mass-based 0.5
  floor deliberately retained (a gas giant always has mass).
- **Facility staffing is distance-weighted** (`StaffingOps`, hex-hop +
  local-hop to the facility's body, `Economy.StaffingDistanceFalloff`). Changes
  production magnitude ONLY — `PayWages`/money flow untouched.
- **Patrol coverage falls off with orbital distance** from the fleet's dock
  (`PatrolCoverage.At`, `War.PatrolCoverageFalloff`).
- **Off-lane travel is a first-class elected route** computed alongside the lane
  path (`ShipmentOps.OffLaneRoute` / `PlanBestRoute`); the courier board elects
  it when its lane is severed instead of stalling.
- **Off-lane detection roll** (`RollChannel.ShipmentDetection = 78` — NOT the
  plan's stale 77, which Phase 2 had taken): a conserved seizure to the
  detecting patrol's nearest owned port, mirroring interdiction exactly. **Gated
  on an ACTIVE WAR** (review finding — a peacetime/allied patrol seizes nothing).

**The slice's real story: two "time, not ticks" invariant breaks it UNMASKED**
(found by a slice-end investigation, both fixed in-slice on the user's call —
"fix it so we don't pile up a backlog"):
1. **Facility groundbreaking had no world-time cadence gate** (`Phases.GroundbreakFacility`)
   — its only ceiling was the facility STOCK cap, so a finer clock accreted
   facilities faster over the same world-years. Pre-existing, but MASKED until
   L2's per-class claims let the surplus groundbreaks actually build. Fixed with
   a per-port gate mirroring `Expansion.FoundingCadenceYears` (new knob
   `Infrastructure.FacilityGroundbreakCadenceYears` = 25 = GenerationYears — the
   equality that makes both clocks telescope).
2. **Hull-batch build duration was size-based / count-independent**, so a coarse
   step's bundled slots cost ~25× per year over the same duration and got
   **DROPPED** by the planner's affordability check while fine slivers fit — the
   coarse (REAL) sim had been silently dropping the navies it planned. Fixed:
   `duration = max(size floor, Count / yardRate)` via a shared
   `DesignMath.HullBatchYears` used by BOTH `Planner.CostOf` and
   `ProjectOps.SpawnHullBatch`, plus (review finding) a **per-year rather than
   full-lump treasury gate** at `GroundbreakHullBatch`. Per-year cost is now
   yard-throughput-bounded and tick-invariant; total cost conserved.
   **Consequence: the real sim now builds ~11× more hulls and the economy grew
   ~2.5× (golden CLOCK 39896 → 100857).** User accepted this at the eyeball.

**Evidence discipline that paid off**: `FineTickTests.cs` was **never touched on
this branch** — the telescoping guard went green from production changes alone,
which is the strongest available proof the fix was honest rather than a loosened
band. The fable reviewer verified this independently.

**Task 10 reworked a serially-fragile test** (`Siege_FallsThePort_SegmentsIntact`,
re-tuned by epoch-nudging 4× across prior slices) into a deterministic
constructed siege — decoupled from `FirstLiveRelation`'s emergent pick, driving
the REAL `WarConduct.FightWars` path, assertions STRENGTHENED, mutation-tested
RED three ways, 18/18 across backdrop epochs 13-30. It no longer drifts.

**Acceptance**: 1081/1081 post-merge; 32-run sweep on the merged tip worst
residual ratio 4.126e-15 (tolerance 1.3e-9); **siege-race amplification measured
and DISPROVED** — vs pre-L2 main on seed 42/40 epochs, wars went 9 → 18 and wars
taking an objective 2 → 6 (22% → 33%), while "no war ever reaches 2/2" is
IDENTICAL before and after. The bigger navies increased war and conquest rather
than suppressing them.

**Filed as follow-ups, NOT resolved this slice**:
1. **THE big one — the siege clock races the war-termination clock** (found by
   Task 10, measured above). `ResolutionPhase` runs `FightWars` AND
   `WarResolution.Terminate` in the same phase; a defender's navy ground down by
   the siege's own battles trips `SideBroke`'s FLEET-STRENGTH condition
   (`WarResolution.cs:108-133`), `Terminate` settles a nominal attacker victory,
   but `Settle` only cedes objectives already `Taken` (`:204-210`) — a still-
   `Contested` siege cedes NOTHING and freezes forever. **Overwhelming naval
   force therefore makes conquest LESS likely** (it breaks the defender's fleet
   before the siege clock lands). Pre-existing and NOT amplified by L2 (measured),
   but it is why no war in seed 42 ever takes all its objectives. This is the
   chained next-up — see Next up #2.
2. **Adjacent-hex spillover** (Slice L follow-up #1) — deferred a THIRD time
   (explicitly, at L2's scope nod). Still needs its own brainstorm/design pass;
   changes `Facility.Hex` semantics, touches `Siting.cs`.
3. **Residual latent**: `Planner`'s `OffLaneRoute` duplicates `PlanRoute`'s
   inline off-lane fallback (deliberate — the task forbade touching PlanRoute;
   dedupe by having PlanRoute's fallback call OffLaneRoute on a later touch).
4. `SpawnHullBatch`'s `yardTiers = 0` param sits BEFORE `startedYear` — all 8
   callers verified safe, but a future POSITIONAL `startedYear` would silently
   bind to `yardTiers`. Reorder or make named-only on a later touch.
5. `Math.Pow` negative-base NaN at insane knob values in the detection block —
   fails safe (no seizure), pre-existing identically in interdiction; clamp both
   together someday.
6. Detection's dual coverage computation (accumulator via `PatrolCoverage.At`,
   owner attribution via an inline rescan) is provably consistent ONLY because
   detection passes `BodyRef.None` (localHop always 0). Unify if detection ever
   goes body-aware.
7. `RichestBiosphere` never sites an Agri on a `Barren` body even at
   Hydrographics=100, though the yield formula would pay a wet dead world —
   siting is stricter than yield. Cosmetic; one doc sentence if ocean-world
   farming ever matters.
8. The facility cadence gate keys on (owner, port), so a freshly CAPTURED port's
   new owner can groundbreak immediately (no project record under its id).
   Epoch-boundary noise at worst.

## Slice CU-2 — the Bank actor (closed, prior handoff)

State: `slice-cu2-bank-actor` merged to `main` locally with `--no-ff`
(main had NOT moved from the branch base 81c03c6 — L2 still in flight in its
own worktree, no fold-in needed) — not pushed, push on say-so. 1047/1047
`dotnet test` · hex-tier suite intact · determinism byte-identity · one
whole-branch fresh-eyes review (fable, no Critical) + one fix wave · the
32-run committed acceptance sweep run twice (before and after the fix wave),
worst per-currency residual 9.0e-16 relative (FP epsilon, unchanged by the
fix wave) · golden re-frozen once at slice end · merge accepted 2026-07-16.
Design spec `docs/superpowers/specs/2026-07-15-cu2-bank-actor-design.md`;
ledger `docs/superpowers/plans/2026-07-15-slice-cu2-ledger.md` (8 tasks grew
to 14 real sub-tasks — the exchange-site audit split into 4a-4f, several
inserted by real findings).


**What shipped**: a first-class `Bank` per `Currency` (`src/Core/Epoch/Bank.cs`,
keyed by currency id, minted 1:1 in `SimState.FoundCurrency`, serialized in a
new `banks` layer, `SimState.BankOf`). A conversion spread
(`Economy.ConversionSpread` = 0.005) sequestered into `Bank.Reserve` OUT of
circulating `Currency.Supply` — so reserve accumulation strengthens a currency's
FX rate. The reserve funds polity deficits FIRST (a `Reserve → Credits` transfer,
`FundDeficit` in `Phases.cs`) with the bounded fiat mint (`IssueSovereignCredit`)
as lender-of-last-resort backstop. A REPL bank surface on the currency line.

**Spread incidence is direction-specific** (spec §3, driven by a real
conservation fork the audit surfaced): repatriation (money arriving into a
holder's own currency) NETS the recipient via `SettleConversion`; payment
(converting own money to pay foreign) GROSSES UP the payer via `SkimToReserve`
+ full `RecordConversion` (payee whole — this is what let the market's
pay-recipients-gross-then-debit ordering stay correct without reordering).
Absorption/graduation re-denomination is EXEMPT (`PolityRecord.DepositExempt`),
as are port-ownership re-denomination and order-post refunds.

**Conservation is reserve-aware**: `SupplyOps.WalkNative` walks circulating
balances only (so FX tightens); the per-currency residual (`MetricsOps`) adds the
live `Bank.Reserve` to the balance side. Reserves are provably non-negative (sign
guards + a full-history guard test). The whole-branch review verified the
exchange-site inventory is complete and every real cross-currency movement skims
exactly once.

**The review earned its keep**: it caught a real spec violation mid-audit (the
absorbed treasury was being clipped — fixed to exempt, Task 4f) and, at the
whole-branch pass, a latent conservation leak the gross-up had *widened* (corp
`TreasuryAvailable` returned the raw wallet total while `Corporation.Withdraw`
yields only `value/(1+spread)` — a fragmented-corp funder could under-provide and
mint the difference; closed in the fix wave `a534b49`) plus two unregistered
knobs (a config-artifact-stamping hard-rule violation that also blocked the
tuning pass — registered in the same wave).

**Acceptance**: FX rates diverge dramatically (0.002–0.473 across currencies in
one seed-42 history); trade-hub currencies build real reserves while frontier
ones stay thin (the intended "you earn your monetary backstop" emergence).
`NegativeTreasuries` breathes; variants diverge sensibly (baseline 854k →
flush-start 2.8M → lean-labor 29M final supply).

**Filed as follow-ups, NOT resolved this slice**:
1. **THE big one (user-raised at the eyeball, architectural — see
   `[[bank-reserve-flow-gap]]` memory)**: the bank's reserve has ONE inflow (the
   FX spread), but a polity's dominant flows (receipts/taxes/wages/upkeep) go
   DIRECTLY into `PolityRecord.Credits`, bypassing the bank entirely. So the bank
   sees only cross-currency activity (~0.1% of deficit funding; even 10× spread →
   ~1%) and can never be a meaningful monetary intermediary. **No spread value
   fixes this — it needs a design pass** to route the polity's money flow THROUGH
   the bank (bank takes deposits, polity draws on the bank). Likely a prerequisite
   for CU-4's "bank strength → federation" to have teeth. Sequence it before or
   alongside CU-4. The merged mechanism is correct and conservation-clean — this
   is about making bank agency *effectual*, not a bug.
2. **Spread/ratio tuning deferred** (user decision): accepted the 0.005/0.5
   defaults for CU-2; both are now registered, sweep-tunable knobs. Tuning is best
   done in a dedicated economic-balance pass informed by CU-3/CU-4 — and is moot
   until follow-up #1 lands (it can't overcome the scale mismatch).
3. **Order-cancel-skims vs project-bid-refund-exempt asymmetry** (documented in
   the spec's settlement section) — a deliberate call, revisit if it feels wrong.
4. **Observability** (review finding 8, Minor): whole-sim `MoneyRow.Supply`
   excludes `Bank.Reserve`, so galaxy "total money" slowly under-counts as banks
   capitalize. NOT a defect (the invariant is per-currency and reserve-aware);
   consider a `MoneyRow.Reserves` field for SIMHEALTH dashboards.
5. Two conservation tests (`ServiceLoans_CrossCurrency`, `Dividend`) assert via a
   MEASURED skim rather than a derived `×(1±spread)` — sibling tests pin the
   magnitude/direction, so redundant coverage exists; acceptable.

## Slice CU-1 — currency & FX foundation (closed, prior handoff)

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
2. **Slice L2 follow-ups** (see Slice L2's section above): **the siege clock vs
   war-termination clock race (the big one — chained as Next up #2)**;
   adjacent-hex spillover (deferred a 3rd time); OffLaneRoute/PlanRoute
   duplication; SpawnHullBatch param order; Math.Pow NaN clamp; detection's dual
   coverage compute; Agri siting stricter than yield; cadence gate on a captured
   port.
3. **Slice L follow-ups** (see Slice L's section above): adjacent-hex
   spillover (**still open, deferred again at L2's scope nod**),
   colony-founding bodiless dud (**RESOLVED in L2 — Task 2**), single-good
   stock assumption, FineTick band looseness (**RESOLVED in L2 — the band was
   never the problem; two real invariant breaks were, both fixed; the guard now
   passes on production changes alone**), zero-richness dud construction, Unity
   compile verification, body-blind siting.
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
**NEW (Slice L2):**
- **Regenerating the golden: anchor the write on the `.csproj`, not on a
  `Goldens/` directory probe.** `bin/Debug/netX/Goldens` exists too (the csproj
  copies it), so a walk-up looking for `Goldens` writes into `bin` and the source
  golden silently never changes (an empty `git diff` that looks like "no drift").
- **Staging polity pools in a test that runs a phase is a false-green trap**:
  `AllocationPhase` credits `MilitaryPoints` (`Phases.cs:435`) BEFORE
  `Groundbreak` (`:468`), so real income swamps a staged treasury and a
  treasury-gate test passes pre-fix. Zero `pr.Receipts` in the fixture.
- **A test coupled to emergent history will be re-tuned forever.**
  `Siege_FallsThePort_SegmentsIntact` was epoch-nudged 4× across slices because
  it fished its fixture out of seed 42 via `FirstLiveRelation`. When a test needs
  re-tuning a 2nd time, decouple it (construct the scenario) instead of nudging —
  L2 did, and it now holds across backdrop epochs 13-30.
- **When a tuned band goes red, suspect the mechanism before the band.** L2's
  FineTick redness looked like band drift (it had been widened 4× before) and was
  in fact TWO real invariant breaks stacked. Instrument the per-kind breakdown
  before touching a threshold — and note the fix is proven precisely by NOT
  having touched the test file.

## Next up

1. **User atlas/REPL review** — the user's own call on when to look at both
   mega-slices (Locality, Currency) landed together and decide what to
   prioritize next. Not scripted further here — follow their lead.
2. **Slice WT (war termination — the siege vs settlement clock race)** — chained
   by L2, kickoff ready:
   `docs/superpowers/plans/2026-07-16-slice-wt-war-termination-kickoff-prompt.md`.
   **A design slice** (brainstorm → spec → implement), not a patch: L2 measured
   that ~2/3 of wars end taking nothing and NO war in a 1000-year history ever
   achieves all its objectives, because a war settled by `SideBroke`'s
   fleet-strength condition cedes only `Taken` objectives — so a 75%-complete
   siege evaporates, and overwhelming force is *counterproductive* (it breaks the
   defender's fleet before the siege lands). Pre-existing and NOT caused by L2
   (measured pre/post). The kickoff carries the evidence, the four candidate
   models, and the pre/post table as the acceptance instrument. Parallel-safe
   with the CU lineage (different subsystems) — worktrees.
3. **Slice CU-3 (Federation-triggered currency consolidation)** — **NOT started;
   back in Kickoff Ready.** The 2026-07-16 spawn opened as CU-3 but its
   sequencing decision (CU-2 follow-up #1) resolved to "fix the prerequisite
   first" → it pivoted to **BF**, which then chained **MC**. CU-3 now sits behind
   BF in the chain (MC ✅ → BF → CU-3 → CU-4). Kickoff still valid:
   `docs/superpowers/plans/2026-07-16-slice-cu3-kickoff-prompt.md`. When it runs,
   it merges banks that have gained an asset side and a money sink (BF), so it
   consolidates **reserves AND claim books** — richer than its original scope
   (see BF design §8).
4. **Slice DX (Domain hex expansion)** — spec approved + committed
   (`docs/superpowers/specs/2026-07-16-domain-hex-expansion-design.md`),
   kickoff chained
   (`docs/superpowers/plans/2026-07-16-slice-dx-kickoff-prompt.md`), Trello
   card in Kickoff Ready. Closes the adjacent-hex-spillover thread (Slice L
   follow-up #1) generalized: satellite workings → outposts → frontier
   graduation, three phase gates, sim-only. **DELIBERATELY HELD** (user
   decision 2026-07-16): do not spawn until Slice CU-3's hiccups are fully
   resolved — no new parallel dev lines until then; confirming the hold is
   lifted is part of the scope nod.
5. **Slice K6 (The economy surfaces)** — parallel-safe (worktrees):
   `docs/superpowers/plans/2026-07-12-slice-k6-kickoff-prompt.md`
   — TRADE lens on the rail, order-book + contracts panels, freight
   purposes on the map, war-supply readout; zero sim behavior. Will now
   also want to surface per-currency prices/rates somewhere, given CU-1
   landed after this kickoff was written — flag to whoever picks it up.
6. **The gap-list backlog** — the roadmap's designated successor queue
   (item 10 in Carried/flagged above).
7. User read-through of the design specs — still outstanding.

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
**NEW (Slice L2):** a slice is responsible for what it UNMASKS, not just what
it breaks. L2's own work was clean, but it removed a mask hiding two pre-existing
"time, not ticks" breaks; the user's call was to fix them in-slice ("so we don't
pile up a backlog") rather than file them, and each fix revealed the next layer —
budget for that, and re-confirm scope with the user at each layer rather than
chasing indefinitely. Corollary: **prefer the evidence that cannot be faked** —
"the test file was never touched and the guard went green" beats any amount of
narration that a fix is honest. And **a plan can contradict itself**: L2's source
plan said "hostile" in prose and "any non-owner" in its interface spec, and the
implementation faithfully followed the wrong one — when a review finds code
disagreeing with a doc, check which is wrong before assuming it's the code.
