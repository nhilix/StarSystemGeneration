# Slice I Kickoff — Session Prompt

You are starting **Slice I (Narrative)** of the epoch-sim implementation
roadmap, under the lighter protocol in `/CLAUDE.md` (read it first). I is
the layer where the galaxy stops being omniscient and starts being
*heard*: compressed-belief perception replaces the perfect-info stubs,
news pulses ride the traffic graph at lane speed, reputation travels and
reprices every stance, the chronicle grows era detection and views, the
POI compiler turns residue (wreckage fields, ruins, razed facilities,
precursor sites) into anchored discoverable places with salvage niches,
and plagues ride the same lanes the news does. After I, decisions run on
what an actor *believes* — wars can run past their rational end because
one side doesn't yet know it's losing.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules.
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — row I
   (consumes H's wars/relations for news and reputation; J needs I's
   perception layer certified).
3. **The design docs I implements**:
   - `docs/design/narrative/perception-and-news.md` — compressed belief,
     news pulses, staleness, stances/reputation.
   - `docs/design/narrative/chronicle-and-poi.md` — chronicle views, era
     detection, the incremental POI compiler, salvage.
   - Skim `frame/simulation-flow.md` (P3: decide on perception, resolve
     on truth), `fleets/ships-and-fleets.md` §Information carriage
     (news speed = f(posted traffic)), `interpolity/war.md` §Aftermath
     (conduct reputation travels the news graph).
4. **What I replaces / fills** (the perfect-info stubs H left, all
   marked "slice I" in code):
   - `PerceptionPhase` (Phases.cs) rebuilds every `PerceptionView` from
     truth each step — briefs (RelationBrief with true gauges + casus
     belli menus, WarBrief with true exhaustion/strength, strengths
     dict, CorporateBrief, colony candidates priced at the capital)
     must become *belief*: stale by distance, refreshed by traffic.
   - `FleetOps.TrafficPerYear(state, lane)` — the news-speed data,
     built in E, consumed by nobody yet.
   - Reputation stubs: the FirstContact stance's reputation term is 0
     (RelationsOps.WarmthTarget); `TreatyBroken` and `PeaceSettled`
     are "events the galaxy hears" but only the pair's warmth moves;
     `CorporateKnobs.NationalizeLegitimacyHit` is "the reputation
     damage stub until slice I's news"; war conduct reputation
     (starvation sieges, honored surrenders, annihilations) is
     recorded but never travels.
   - POI raw material: `SimState.Wreckage` (battlefields — big fields
     where wars ground), razed facilities (Condition floor 0.05),
     `PrecursorSite` registry (dormant remnants flagged), suppressed
     emergence sites. `NotableType.Explorer` trigger is unarmed
     ("ruin expedition"); `TechKnobs.SalvagePerHullPerYear` notes
     "full consumption including precursor digging is slice I";
     corporations could take salvage niches.
   - `SimTraceView`/`chronicle` render the flat log; era detection and
     views (per-place, per-actor exist on EventLog) are the P8 surface.
   - Plagues: nothing exists; they ride lanes (quarantine acts are
     typed in Acts.cs and unresolved — `QuarantineAct` is I's).
5. **What Slice H landed** (ledger
   `docs/superpowers/plans/2026-07-10-slice-h-ledger.md` — read the
   H11 fix wave and H12a eyeball wave notes):
   - **Relations**: `PolityRelation` per met pair (warmth/tension drift
     toward source-computed targets; claims as persistent tension
     sources; treaty rungs with teeth; vassal bonds; dynastic ties).
     `RelationsOps.Step` runs in Interior. Warmth damps the overlap
     tension term; entanglement discounts the federation gate.
     **Stances are already Intent-side** (DiplomaticPostures written
     from briefs) — I stales the briefs, not the machinery.
   - **Wars**: `War`/`WarObjective` registries; conduct in Resolution
     (WarConduct), termination/settlement (WarResolution), civil wars
     (CivilWarOps), natives (NativeOps). `WarBrief.OwnSideExhaustion`
     and `OwnSideStrengthShare` are what concession decisions read —
     the design wants these STALE so wars overrun their rational end.
   - **Actors can retire** (`Actor.Retired` — federations, annexations,
     absorptions, civil-war submissions). Anything iterating polities
     must respect Entered/Retired; `EpochTestKit.FirstLiveRelation`
     exists because Relations[0] can be a dead pair.
   - **Events**: 19 artifact layers (relations v4, wars v1, origins v2,
     actors v5); next free ids — economic 208, political **310**,
     military **408**, diplomatic **511**, corporate 605, character
     704. `RollChannel` next free: **70** (66–69 used).
   - **REPL**: `relations [id]`, `wars`, `war <id>`, `emap war`,
     `emap tension` joined the G-era panels. Golden regen workflow:
     `printf 'epoch 42 40 12\nesave tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt\nquit\n' | dotnet run --project src/Inspector`.
   - **H lessons that will bite I**: histories churn hard now — tests
     that pick "the first X" must filter for live/at-peace/unbonded
     state (see FirstLiveRelation); anything added to PerceptionView
     must reach the constructor's tail with a default (14 params and
     counting — consider a builder if I adds many); per-epoch
     geometry/strength surveys run in Perception — belief caches must
     serialize or be derivable, or LoadThenContinue breaks (the
     transient LastWarmthTerms/LastTensionTerms pattern is the
     display-only escape hatch); staged events must never be read as
     state (Staged clears at Chronicle); the conservation and hull
     ledgers catch real bugs — keep them wired into every new flow.

## Scope (roadmap row I)

- **Compressed-belief perception**: per-actor believed world replacing
  the perfect-info rebuild — freshness per (actor, subject) derived
  from the traffic graph (busy lanes carry news fast, backwaters
  slowly, wilds barely); decisions on belief, consequences on truth
  (P3). Wars run past their rational end; markets misprice distant
  shocks; the casus-belli menu arms on stale facts.
- **News pulses**: Public events pulse along lanes from their location;
  Regional spread by contact; Secret spread not at all. Arrival, not
  emission, updates belief.
- **Stances + reputation**: conduct reputation (treaty breaking,
  atrocities/starvation sieges, honored surrenders, suppressed
  emergences, nationalizations) travels the news graph and reprices
  stances toward the actor — the FirstContact reputation term goes
  live.
- **Chronicle views + era detection**: the P8 surface — era boundaries
  detected from event density/type shifts ("the Long Peace", "the
  Succession Wars"), per-place and per-actor views rendered.
- **Incremental POI compiler**: battlefields (wreckage concentrations),
  ruins (razed facilities, dead polities' worlds), precursor sites
  surfaced as anchored POIs; salvage niches for corporations; ruin
  expeditions arm `NotableType.Explorer`.
- **Plagues**: outbreak, lane-borne spread gated by traffic, segment
  mortality, `QuarantineAct` resolution (self-imposed lane closure —
  the typed act learns Resolution), burnout/immunity. I-adjacent per
  the H kickoff — in scope here.

**Boundary**: the world-state handoff layer, fine-tick resumability,
and the delta boundary are slice J; the Unity atlas is K. Espionage
stays reserved.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-i-narrative` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-i-ledger.md`; TDD; frequent
   commits. Don't share a checkout with another live session — take a
   `git worktree` if one exists.
3. Gates: `dotnet test` green (hex tier untouched) · determinism
   byte-identity · load-vs-rebuild + LoadThenContinue over the new
   belief/news/POI/plague layers · conservation (plague deaths shrink
   segments, never wealth-mint; salvage draws conserve the hull
   ledger's wreckage) · shape bands (belief staleness varies with
   traffic; eras detected; POIs accumulate where history happened;
   plagues burn out, never sterilize the galaxy).
4. REPL surface: a belief panel (what does polity X believe about Y,
   and how stale) · news view (a pulse's journey) · era-annotated
   chronicle · `poi` registry/panel · plague layer on `emap` ·
   chronicle prose for every new event · `watch` stays intact.
5. User gates: scope nod · REPL eyeball · merge decision. Eyeball
   suggestion: **the fog of war made visible** — a war where the
   distant loser keeps fighting because the news hasn't arrived, a
   reputation shock repricing stances polity by polity as it travels,
   and a battlefield POI a later expedition digs.
6. Wrap-up: merge · HANDOFF · **write the Slice J kickoff prompt**
   (handoff & certification — read the roadmap row first) · flip the
   box below · push only on user say-so.

- [ ] Slice I complete
