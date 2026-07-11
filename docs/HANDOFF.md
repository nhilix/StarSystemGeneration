# Session Handoff — 2026-07-11 (Slice I: Narrative — merged)

State: `main`, merged and **pushed** (user pushed 2026-07-11 — slices
F through I are all on origin now). Tests 532/532 green — hex-tier
suite untouched at 100%. ProjectSettings churn remains uncommitted as
always.

## What this session did: Slice I of the epoch-sim rebuild, merged

The galaxy stopped being omniscient: decisions now run on what an actor
*believes*, word travels the lanes at traffic speed, reputation travels
with it, history compiles into anchored places, and plague rides the same
arcs the news does. All new code in `src/Core/Epoch/Narrative/`. Ledger
(tasks, decisions, the review wave, eyeball exhibits):
`docs/superpowers/plans/2026-07-11-slice-i-ledger.md`.

- **The news graph** (`NewsOps`): news speed per lane = f(posted traffic)
  (base 4 hex/yr + up to 12 at saturation; wilds crawl at 0.5); delay
  fields by Dijkstra over the port graph, derived from live fleet state
  every step, never persisted.
- **Compressed belief** (`Belief`, `BeliefOps`; PerceptionPhase rewired):
  self-facts read fresh; other-side facts (strengths, coalitions,
  casus-belli menus, war-objective candidates, war front reports,
  corporate books) read through per-(observer, subject) snapshots that
  refresh when elapsed years cover the news delay and FREEZE between
  refreshes. Wars run past their rational end — the concession decision
  reads believed exhaustion (seed 42: Mial fights the Alloys War on
  50-year-old reports). Serialized as the `belief` layer (PBEL/WBEL/
  CBEL/STANCE); LoadThenContinue never re-surveys.
- **News pulses** (`NewsPulse`): Chronicle emits for Public events over
  `News.PulseMagnitudeFloor`; Perception delivers when age ≥ delay;
  arrival (not emission) refreshes beliefs and reprices stances; the
  journey (per-polity arrival years) rides the pulse record — NOT the
  log, which would multiply by audience count (perception-and-news.md
  P1 amended accordingly). Pulses expire at `PulseMaxYears`.
- **Stances & reputation** (`ReputationOps`): per-audience stance table
  on `Actor.Beliefs`, moved at news arrival through the observer's
  temperament — open traders sanction treaty-breakers, militants respect
  bold conquest, dogmatic distance amplifies condemnation (structural
  constants; dials are `News.StanceDecayPerYear` +
  `Relations.ReputationWarmthWeight`). The pair-mean stance is warmth
  term [5] — one wire reprices first contact, treaty gates, and the war
  appetite. Regional events spread by contact (stateless log-tail scan).
  Nationalization's foreign fallout finally travels (the H stub).
- **Chronicle views + eras** (`EraDetector`): epochs cluster by weighted
  event signature (war 3 / upheaval 2 / treaty 2 / expansion 1, floor 3),
  merge, and name from participants ("The Marno Expansion", "The Concord
  of Orve and Selthasel"); ≥4 quiet epochs = "The Long Peace"; repeats
  take numerals. Recomputable annotation, never state. `chronicle` is
  era-annotated; `chronicle place <q> <r>` is the archaeology view.
- **The POI compiler** (`PoiRecord`, `PoiCompiler`, runs inside
  Chronicle): battlefields (wreckage ≥ floor — ONE record per hex ever,
  reopened by fresh wrecks, never resurrected from the same immutable
  WreckageRecords), ruins (ports empty past `RuinsDeadEpochs` measured
  from `Port.LastPopulatedYear` — ports layer v2), fallen capitals
  (Annexed settlements), memorials (deep famines, suppressions),
  precursor sites charted when expansion reaches `SurveyReachHexes`.
  One live anchor per hex by magnitude; salvaged-out fields fade unless
  monumental. Events 208/310–312/408 with prose.
- **Salvage & expeditions**: `CorporateNiche.Salvage` ("Salvors") via
  the ordinary niche/charter rule; supply lands in the Markets phase
  through `scratch.Supplies` (buyers pay at distribution — zero credit
  mint; the hull ledger invariant holds, depletion counted on the POI);
  stripped fields stop teaching (`TechOps` lesson × remaining fraction);
  precursor digs yield exotics + Astrogation lessons and deplete the
  site; every salvage charter mints an Explorer notable.
- **Plagues** (`Plague`, `PlagueOps`, in Interior before demographics):
  outbreaks where people crowd (RollChannels 70/71), spread gated by
  `TrafficPerYear` (quarantined/blockaded lanes carry nothing —
  quarantined lanes also read zero traffic, so news drops to the base
  crawl), machine embodiment immune, deaths shrink Size and never touch
  wealth (inheritance), burnout → immunity window. `QuarantineAct`
  resolved at last: the controller closes lanes from its healthy ports
  to visibly infected neighbors (doorstep truth, no news gating);
  `Lane.QuarantinedUntil` (lanes v2) joins SeveredLaneIds — freight,
  migration, and contagion stop together (war blockade progress reads
  postures, not this flag).
- **Artifact**: 22 layers — new `belief`, `pulses`, `pois`, `plagues`;
  `ports` v2 (LastPopulatedYear), `lanes` v2 (QuarantinedUntil). Golden
  frozen at the final format. LoadThenContinue + byte-identity green.
- **Registries**: events next free — economic **211**, political
  **314**, military **409**, diplomatic 511, corporate 605, character
  704. `RollChannel` next free: **72** (70 PlagueOutbreak,
  71 PlagueSpread). Knobs: `News.*` (7), `Poi.*` (13), `Plague.*` (8),
  `Relations.ReputationWarmthWeight` — all in KnobRegistry + TUNING.
- **REPL**: `belief <x> [y]` (belief BESIDE truth with staleness years)
  · `news [id]` (pulses in transit / one journey) · `stances [id]`
  (reputation per audience) · `eras` · era-annotated `chronicle` +
  `chronicle place` · `poi [id]` · `emap plague` · prose for all 8 new
  event types · `watch` verified byte-identical.
- Fresh-eyes review: 2 confirmed findings + 3 notes, all fixed in one
  wave (battlefield resurrection mint; ruins grace on age not
  empty-duration; phantom traffic on closed lanes; portless salvor
  charter crash; ArtifactTests ports-version literal).
- Eyeball accepted 2026-07-11, no fix wave needed.

## Deliberately deferred (flagged, not silent)

- Ruins-POI **lawlessness/piracy** wiring and **memorial stance
  anchors** — listed as live effects in chronicle-and-poi.md's table;
  the kickoff's named effects (salvage niches, expeditions) are wired.
  Good slice-J candidates if wanted.
- News crosses quarantined lanes at the base crawl (word outruns sealed
  borders) — deliberate.
- Same-epoch multi-hop plague spread is lane-id-order dependent —
  deterministic, accepted.

## Next up

1. **Slice J (Handoff & certification)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-11-slice-j-kickoff-prompt.md`
   (complete: reading list, what I left ready, scope, boundary).
2. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · REPL eyeball · merge
decision; kickoff-prompt chaining); hex-tier suite never breaks;
ProjectSettings stays uncommitted; bash printf for REPL piping; parallel
slices never share a checkout — take a `git worktree` each; every
calibration constant goes in a knob registry + TUNING.md; every new
`src/Core` file gets a two-line `.meta` with a fresh guid; any new goods
consumer needs a demand pull at Markets or it starves; grace periods
precede death clocks; step-transients provably zero at epoch boundaries;
histories churn — actors retire (tests pick live subjects via
`EpochTestKit.FirstLiveRelation`). New this slice: **belief state must
serialize or LoadThenContinue diverges** (the `belief` layer is the
pattern); **immutable residue registries must anchor once** (wreckage →
one battlefield per hex ever; the precursor `Charted()` rule); scan the
log tail by `WorldYear == state.WorldYear − years` for last epoch's
events (stateless, order-safe); golden regen one-liner: `printf 'epoch
42 40 12\nesave tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt\nquit\n'
| dotnet run --project src/Inspector`. Older carried minors:
`git show a1f5843~40:docs/HANDOFF.md`.
