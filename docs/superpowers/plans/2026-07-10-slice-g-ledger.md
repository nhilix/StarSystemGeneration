# Slice G Ledger — Interior & Corporations

Branch `slice-g-interior` off main (216cee8). Scope nod: 2026-07-10.
Kickoff: `2026-07-10-slice-g-kickoff-prompt.md`. This file is the
resumability record: ordered tasks, gates, decisions, surprises — updated
as work lands.

## Standing gates (every task)

- `dotnet test` green; hex-tier (Phase-1) suite never breaks.
- Determinism byte-identity for same config (SimTraceView render).
- Artifact save/load stays green; any new cross-step state serializes in
  the same task; golden regenerated same-commit when history changes;
  `LoadThenContinue_EqualsTheStraightRun` is the strongest gate.
- Credits conserve to the mint (appeasement/research/dividends are flows,
  never sinks or mints).
- Every new calibration constant → `KnobRegistry` + TUNING.md row; catalog
  data is data-as-code with a TUNING structural note.
- Every new `src/Core` file gets a two-line `.meta` with a fresh guid.
- New event types in stable blocks: political 3xx (next free 302),
  corporate 6xx (opens here), character 7xx (opens here); evolutionary
  next 106, economic next 207, military next 403. `RollChannel` appends
  from **60**.

## Tasks

- [x] **G0 — branch + ledger** (this commit).
- [x] **G1 — Government forms + polity interior state** *(done: catalog +
  seating in `Interior/GovernmentForm.cs`, state in `PolityInterior.cs`,
  recompute in `InteriorOps.cs`; homeworld segments + official line seed
  from the species ideology tilt; colony segments carry the official
  line; interior layer v1 (INTR); knobs `Interior.*` ×12 + TUNING rows;
  golden regen — 382/382 green)*: eight-form
  catalog (data-as-code: seat in ideology space × species, succession
  rule, policy inertia, faction tolerance, legitimacy source); polity
  interior state (government form, official ideology 4-vector,
  legitimacy, cohesion, enforcement) seated at entry from species +
  popular ideology; Interior-phase recompute each epoch (legitimacy =
  f(SoL trend, official-vs-popular ideology gap, war-outcome stub,
  ruler prestige [enters with G2], cultural accommodation); cohesion =
  segment-aggregated legitimacy discounted by structural strain: size,
  culture count, capital distance). Serialize (interior layer v1),
  golden regen, knobs `Interior.*`.
- [x] **G2 — Characters** *(done: `Interior/Character.cs` (+Dynasty),
  `CharacterOps.cs`; events 700–703 RulerAscended/CharacterDied/
  SuccessionCrisis/NotableEmerged with `ICharacterPayload` biography
  index (`EventLog.ForCharacter`); channels 60–62; courts seated at
  entry (ruler+heir(dynastic)+marshal), per-epoch death checks with
  species-real age curve + ruler assassination hazard, succession per
  form (heir → crisis → fresh house; committee forms mint; machine
  forks silently), commanders fill warship/expedition fleets, founder
  notables on colony foundings (capped), dynastic prestige feeds the
  ruler legitimacy term; interior layer v2 (CHAR/DYNA); knobs
  `Character.*` ×11 + TUNING; golden regen — 390/390 green. DECISION:
  characters are registry-level with payload-referenced events, NOT
  Actors entries — the actor-substrate/controller slot is play-scope
  (P2), out of G's boundary)*: registry + deterministic on-demand generation
  (culture syllable names, ideology position + boldness/zeal/competence/
  ambition, species-real lifespans incl. hive continuity + machine
  fork/deprecate); role slots (ruler/heir/marshal per form; fleet
  commanders fill `FleetRecord.CommanderId`; faction leaders arrive in
  G3, corp execs in G7); aging + per-epoch death checks (age curve +
  hazards: war for commanders, assassination ∝ faction militancy × own
  ambition × unpopularity); succession per form + succession-crisis
  events; dynasties + prestige (feeds legitimacy); notables capped per
  polity (founder from colony foundings now; war hero/pirate lord/
  explorer when their triggers exist); renown from event participation;
  biography derivable from the log (P8 test). Events open 7xx block.
  Channels append from 60. Serialize (characters/dynasties), golden
  regen, knobs `Character.*`.
- [x] **G3 — Factions** *(done: `Interior/Faction.cs`, `FactionOps.cs`;
  events 304 FactionFormed / 305 FactionDissolved; channel 63
  FactionSeed; one active faction per (polity, basis); leaders minted
  as characters (sacral leaders are Prophet notables); budget pressure
  via `PressedBudget` in Allocation (bounded by form tolerance,
  redirects never mints); appeasement pays each faction up to
  strength × `AppeasementDemandShare` × the allocatable base, rationed
  pro-rata — treasury→faction-wealth flow in the conservation test;
  grievance accrues on the unmet fraction, decays appeased;
  dissolution returns wealth to segments; interior layer v3 (FACT);
  knobs `Faction.*` ×13 + TUNING; golden regen — 396/396 green.
  SURPRISE: with demand priced off receipts and decay at 0.04/yr the
  system self-appeased to zero grievance galaxy-wide — demand now
  prices off the same allocatable base as the pool (share 0.2) and
  decay dropped to 0.008/yr; seed-42 grievances now range 0–5.2 and
  differentiate polities)*: registry; six-basis formation from real state
  (ideological cluster distance, culture minority, frontier distance,
  corporate dividends [wired live in G7], veteran/commander networks,
  sacral surge); strength (pop share + wealth + patron renown), agenda
  (policy deltas), militancy; Interior-phase pressure (bounded
  budget/policy drift toward strong factions' agendas); appeasement
  spending (`Budget.Appeasement` treasury flow → faction wealth,
  conserved); grievance accrual for unappeased strong factions; faction
  leaders minted as characters. Events in 3xx. Serialize, golden regen,
  knobs `Faction.*`.
- [x] **G4 — Temperament composition** *(done: `Interior/Temperament.cs`
  — `Compose` blends species/ideology/ruler/faction terms by the form's
  CompositionWeights; ideology→trait map + per-basis faction pulls are
  structural catalog (TUNING note next sweep); PerceptionView gains
  `SelfTemperament` (computed in Perception, P3-clean, species-only
  fallback for shape skeletons); GenesisController law code, armaments
  reserves, and yard priorities read the composition — fixed species
  reads retired from Intent; no new knobs (mappings are structural);
  golden regen — 400/400 green)*: species disposition × official
  ideology × ruler personality × faction pressure, weighted by
  government form; composition computed at Perception (P3-clean, rides
  the view); `GenesisController` and other Intent paths consume it;
  fixed `SpeciesProfile` temperament reads retire from Intent. Knobs
  `Temperament.*`, golden regen.
- [ ] **G5 — Graduation**: threshold `strength × grievance >
  legitimacy × enforcement`; basis-routed: **schism** (regional/
  cultural: domains secede — new polity actor + record, culture split
  via the Culture registry, ports/segments/facilities/fleets
  reassigned, name from its own culture; credits split conserved),
  **coup** (throne-seeking: ruler replaced by faction leader, ideology
  lurch, possible form change; contested → civil-war *event stub* for
  H), **revolt** (failed graduation: unrest damage, martyrs,
  compounding grievance), **charter** stub (economic basis: waits for
  G7 corporations; grievance holds meanwhile). Form changes are
  chronicle landmarks. Events in 3xx. Serialize, golden regen.
- [ ] **G6 — Tech domains**: per-polity 4-domain tier state
  (Industrial/Military/Astrogation/Life) + progress; research as
  Allocation execution (Research budget split, consumes Refined
  Exotics × Compute effectiveness from own markets, spend recycles as
  wages — conserved); geometric tier thresholds; `TechAdvance` event;
  `Tech.Ceiling(polity, domain)` / `Tech.Region(polity, domain)`;
  consumers rewired: recipe gating + grade ceilings (MarketEngine),
  design sheets (ShipDesign), port service/inter-port radii
  (Astrogation), pop growth (Life), doctrine hooks reserved for H
  (Military); trade-contact diffusion (∝ volume × openness, capped one
  tier below source) + salvage diffusion (wreckage grade above own
  ceiling; precursor digging is I); espionage slot reserved; starting
  tiers from `EntryGradeBonus` (Astrogation/Industrial per design) and
  the entry-design grade hack deleted; **retire `Economy.TechTierStub`**.
  Serialize (tech layer), golden regen, knobs `Tech.*`.
- [ ] **G7 — Corporations**: persistent-niche watcher (price gradients
  across lanes, unserved profitable routes, unexploited deposits,
  prohibited margins; persistence counters over consecutive epochs);
  charter graduation founds (host `CharterOpenness` gate; the merchant
  faction incorporates; founding niche stamps character: extraction /
  freight / fabricator / **cartel**; pirate bands registry-level until
  H); corporate actor (ActorKind.Corporation) + `CorporateController`
  (policies: investment, route bids, dividend rate, lobby targets,
  risk appetite; acts per contract); portfolio: facilities
  (OwnerActorId), freighters via the fleet interface, internal
  logistics at cost (own-network transfers skip market transactions,
  net buys/sells at endpoints); dividends → host-polity faction wealth
  (conserved); lobbying strengthens aligned factions / nudges policies
  within bounds; `NationalizeAct` resolution (assets to state, scandal,
  corp flight); deaths: bankruptcy cascade, nationalization, niche
  death — residue. Events open 6xx block. Serialize (corporations
  layer), golden regen, knobs `Corporate.*`.
- [ ] **G8 — REPL surfaces + golden freeze**: `polity <id>` panel (form,
  legitimacy, cohesion, ruler + reign + dynasty, factions with
  strength/grievance bars, tech tiers); `characters [polityId]` +
  `bio <charId>` (biography from the log); `tech` panel + `map` tech
  layer; `corps` registry dump (niche, portfolio, dividends, host);
  chronicle prose for every new event type in `SimTraceView.Describe`;
  `watch` stays intact (byte-neutral observation). Golden frozen once
  at slice end.
- [ ] **G9 — fresh-eyes whole-branch review** (one subagent) + one fix
  wave; TUNING.md sweep (all new knob rows + catalog structural
  notes); shape-band tests: factions form but polities survive
  (graduations low single digits per polity over 40 epochs),
  characters ≈ a dozen per polity, tech tiers advance without runaway.
- [ ] **G10 — eyeball gate**: a polity readable as a story (form, reign,
  succession, a faction rising on real grievance and graduating — a
  schism visible on the domain map, a corporation founded from a
  visible niche, a tech gap visible on the map). User runs REPL.
- [ ] **G11 — merge decision + wrap-up**: merge to main · HANDOFF ·
  write Slice H kickoff prompt (relations & war) · flip kickoff
  checkbox · push only on user say-so.

## Decisions

- New code lives in `src/Core/Epoch/Interior/` (namespace
  `StarGen.Core.Epoch` stays flat — the folder is organization only),
  so the interior systems don't drown the phase files.
- Knob families planned: `Interior`, `Character`, `Faction`,
  `Temperament`, `Tech`, `Corporate` on EpochSimConfig.
- `TechAdvance` sits in the **economic 2xx block (207)** — research is
  an Allocation/economy mechanic per the design doc; the political
  block keeps graduation events.
- Factions/characters/dynasties get their own id spaces (not actor
  ids); corporations ARE actors (ActorKind.Corporation) once chartered.
- `EntryGradeBonus` maps to starting tech tiers (its design intent);
  whether the field itself survives is decided in G6.

## Surprises

- (running list)
