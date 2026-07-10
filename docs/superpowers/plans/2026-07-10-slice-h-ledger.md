# Slice H Ledger — Relations & War

Branch `slice-h-war` off main (076861a). Scope nod: 2026-07-10 ("read the
kickoff and begin"). Kickoff: `2026-07-10-slice-h-kickoff-prompt.md`. This
file is the resumability record: ordered tasks, gates, decisions,
surprises — updated as work lands.

## Standing gates (every task)

- `dotnet test` green; hex-tier (Phase-1) suite never breaks.
- Determinism byte-identity for same config (SimTraceView render).
- Artifact save/load stays green; any new cross-step state serializes in
  the same task; golden regenerated same-commit when history changes;
  `LoadThenContinue_EqualsTheStraightRun` is the strongest gate.
- Credits conserve to the mint (reparations, tribute, war loans, seizures
  are flows, never sinks or mints); the hull ledger conserves through
  battles (losses become wreckage at real hexes).
- Any new goods consumer registers a demand pull at Markets (the
  founding-supply-race lesson, three strikes); grace periods precede
  death clocks; step-transient fields provably zero at epoch boundaries.
- Every new calibration constant → `KnobRegistry` + TUNING.md row;
  catalog data (casus-belli menu, treaty rungs) is data-as-code with a
  TUNING structural note.
- Every new `src/Core` file gets a two-line `.meta` with a fresh guid.
- Event blocks: military appends from **403**, diplomatic **5xx opens
  here**, political next 308, economic 208, corporate 605, character
  704. `RollChannel` appends from **66** (never reuse 35/36).
- Brace every else. No PowerShell `-replace` file reshaping. No
  `required` members (netstandard2.1).

## Tasks

- [x] **H0 — branch + ledger** (this commit).
- [x] **H1 — Contact + relations registry** *(done: `Interpolity/
  PolityRelation.cs` (relation + RelationClaim + TreatyRung), 
  `RelationsOps.cs` (geometry survey, contact, kin claims, drift);
  events 500 FirstContact / 501 ClaimRaised / 502 ClaimReleased;
  relations layer v1 (REL/CLM); knobs `Relations.*` ×18 + TUNING;
  RelationBrief on PerceptionView → GenesisController writes
  DiplomaticPostures (5 buckets on net warmth−tension, structural);
  RelationsOps.Step runs in Interior after InteriorOps.Recompute;
  tension model: drift toward source-computed target, rise fast / relax
  slow — target falls only when sources resolve; warmth: baseline −
  openness-filtered strangeness + trade + treaty + dynastic − ideology
  gap; kin claims raise/release from segment sweeps; seed-42 r12:
  101 relations / 105 pairs over 40 epochs, 5 kin claims; golden regen
  (EVENT ids shift + REL/CLM/KNOB/POLICY lines only — economy
  byte-untouched); 428/428 green)*: `PolityRelation` pair state
  (warmth, tension, treaty rung, standing claims with legible sources),
  reach-overlap contact detection, first-contact event (500) with
  initial stance from temperament compositions × strangeness ×
  reputation stub; per-epoch warmth/tension recompute from real sources
  (trade volume, dynastic ties, honored alliances, shared wars won /
  contested-overlap zones, claims, interdiction strain, faction
  agitation, ideology gap × zeal); tension decays only as sources
  resolve. Relations layer v1. Knobs `Relations.*`. Perception carries a
  relations brief; DiplomaticPostures written by Intent from it.
- [x] **H2 — Treaty ladder rungs 1–3** *(done: `ResolveTreaty` in
  RelationsOps (offer/accept/break; mutual offers consent immediately;
  rungs climb one at a time; offers lapse after 4 epochs — structural);
  events 503 TreatySigned / 504 TreatyBroken (public, named); teeth:
  PactTariffFactor at both MarketEngine tariff sites, NonAggression
  damps the tension target ×(1−0.30), trade-pact partners' ports join
  BuildLanes' candidate pool (cross-border lanes — the trade that feeds
  warmth); GenesisController levies openness-scaled tariffs
  (`Controller.BaseTariffRate`) so the cut bites, and climbs/answers/
  breaks per stance + warmth gate (TreatyGateBase/Step); knobs +5
  Relations, +1 Controller, TUNING rows; seed-42 r12: 41 signings, the
  full ladder climbed by warm pairs, 0 breaks (no wars yet); golden
  regen; 435/435 green)*: offer/accept/break through
  Resolution (standing offers on the relation, mutual consent), teeth:
  trade pact (tariff cuts, lane priority), non-aggression (spark
  de-escalation, tension damping), defense alliance (join defensive
  wars, attackers price allied fleets). Warmth gates ascent; breaking =
  public event + warmth crash. Events 501/502.
- [x] **H3 — Federation + vassalage** *(done: `Interpolity/
  FederationOps.cs` — federation as TreatyRung.Federation (rung 4,
  never held: consent executes `Federate`), gate = sustained alliance
  (RungEpoch clock) + warmth ≥ TreatyGate(4) + ideology gap + openness
  + cohesions + no vassal bonds; fusion births a NEW polity
  (pop-weighted species/culture/ideology, syllable name on channel 66,
  fresh court + entry designs, founding legitimacy 0.75 structural),
  parents retire (`Actor.Retired`, actors layer v5 — never re-enter,
  interior nulled); shared merge plumbing `MergeInto` (ports,
  facilities, fleets + commanders + hull ledgers, treasuries, reserves
  grade-blended, hosted corps, characters; open loans reissue against
  the successor, parent-parent debt cancels); vassalage: chosen via
  VassalageAct (truth-verified weakness ratio + consent warmth
  structural 0.25), tribute = receipts share paid before budgeting
  (conserved), foreign-policy lock in ResolveTreaty + controller,
  exits: absorption (clock + warmth + healthy overlord → MergeInto) and
  negotiated secession (overlord cohesion) leaving a LostTerritory
  claim; events 505–508; REL gains RungEpoch/VassalSinceEpoch
  (relations v2); RelationBrief gains IdeologyGap/EpochsAtRung/
  OtherStrength/VassalPolityId; `FleetOps.WarStrength` +
  PerceptionView.OwnStrength; knobs ×9 + TUNING; seed-42 r12: the
  Belzen Federation (Nozen+Selzenvo) born y475 and coup-shaken y800,
  4 vassalages bound late; 7 older tests amended (actors can retire,
  sovereignty moves by merger, notable caps are mint-time valves,
  mints counted by emergence events); golden regen; 443/443 green)*:
  federation merge gate (sustained
  alliance + high warmth + ideology compat + openness + cohesions) →
  NEW fused polity (population-weighted composition, fresh name, form
  from combined ideology, multi-species membership); vassalage (imposed
  by settlement or chosen under threat; tribute flow in Allocation,
  foreign-policy lock, defensive obligation; exits: absorption /
  secession). Events 503–50x.
- [x] **H4 — Dynastic instruments** *(done: ResolveDynasticInstrument
  (both thrones lineage-formed, unbound, ties < 3 → DynasticTies++ +
  LastTieYear clock; event 509, marriage vs wardship by strength
  ratio); lapse after `DynasticTieLapseYears` (75) converts the tie
  into a Succession claim held by the prouder house (dynasty prestige;
  subject = ruling dynasty), released when the line loses its own
  throne; view: SelfDynastic + brief OtherDynastic/DynasticTies;
  controller: one wedding per epoch among warm cordial lineages;
  relations layer v3 (LastTieYear); seed-42 r12: 19 instruments →
  5 succession claims across the millennium; golden regen; 448/448)*:
  marriage/wardship acts between
  dynastic-form polities → warmth now, succession claims later
  (claims point back as tension); rare personal-union fast path
  deferred unless cheap.
- [x] **H5 — Casus belli + spark + declaration** *(done: `Interpolity/
  War.cs` (War/WarObjective/CasusBelli/WarDemand/WarObjectiveSpec) +
  `WarOps.cs`; menu from real state (price-shock seizure, chokepoint,
  punitive interdiction, crusade = gap × ruler zeal, liberation from
  kin/liberation claims, containment = military-tier lead, succession
  claims, lost-territory + military-faction discharge, vassal
  secession, fresh border incident); spark: incidents roll on channel
  67 in surveyed overlap space (NA rung de-escalates), bump tension,
  chronicle as fizzle vs loaded (404); DeclareWarAct re-typed (ints +
  WarObjectiveSpec list); declaration grounds objectives against truth
  (defender ports/lanes, navy default), defender's defense allies +
  both sides' vassals join as supporting belligerents, declaring on a
  partner breaks the treaty publicly, wars named from their causes
  ("the Alloys War", "the Nozen Liberation"); controller: one war at a
  time, tension × (0.5+militancy) appetite gate, attacker prices the
  defender's coalition (DefensiveStrength on the brief), cause
  priority + per-cause objectives/demand; view: CasusBelli menu +
  ObjectiveCandidates + WarBrief list; wars layer v1 (WAR/OBJ),
  relations v4 (LastIncidentEpoch); channels 67–69 reserved; knobs
  `War.*` ×8 + TUNING; seed-42 r12: 11 declarations across 4 cause
  kinds, dozens of incidents mostly fizzling; 456/456 green, golden
  regen)*: four-category menu
  computed from real state (economic: resource seizure/chokepoint/
  punitive; ideological: crusade/liberation/containment; political:
  succession claims/military-grievance discharge/vassal enforcement or
  secession; spark: border incidents in contested-overlap space,
  escalation ∝ tension, fizzles are events too); DeclareWarAct carries
  objectives + settlement demand; War registry (belligerents via
  defense alliances, war leaders, objectives). Events 403+ military.
- [x] **H6 — War conduct** *(done: `Interpolity/WarConduct.cs` — per
  epoch: Reconcile (targets that changed hands are held) → Mobilize
  (doctrine share 0.4/0.6/0.8 ± marshal boldness pools reserve+patrol
  warships into one war fleet per contested objective; Blockade posture
  at ports/lanes = REAL severed lanes via SeveredLaneIds, Expedition
  for fleet hunts; FleetOps.PoolHulls now exempts war postures) →
  Engage (power = vectors × readiness × supply-distance × commander
  competence + ally support factor; defender = local fleets + mobile
  response + allies, × fortress bonus; roll on channel 68 → decisive/
  attrition/stalemate bands; losses conserve to wreckage at the hex,
  quiet FleetOps.Wreck + warship-only defender wrecks; facilities
  scarred on decisive days; commander death roll channel 69 clears the
  fleet slot, victors gain renown → in-place WarHero promotion under
  the notable cap; both commanders indexed by ForCharacter) → Progress
  (sieges tick under superiority, threshold = base + larder (port
  provisions + pro-rata polity reserve vs local hunger, capped) +
  fortress tiers, relief on decisive defense resets; captures transfer
  port + facilities with segments intact, defender fleets rebase;
  blockades taken after held epochs; fleet objective at strength <
  share of start); exhaustion accrues from years (the slice-A
  WarWearinessPerYear, live at last) + loss share; events 405/406/407;
  Fortress joins BuildableTypes gated Military ≥ 2; knobs `War.*` +16
  + TUNING; SURPRISE: Mobilize pooled hulls before checking for
  contested objectives — 41 hulls vanished off-ledger when a war ran
  out of fronts (the hull-ledger test caught it); seed-42 r12: 120
  battles, named commanders holding lines in the chronicle; wars
  don't end yet (H7); 463/463 green, golden regen)*: theater/objective
  model each epoch —
  assignment per doctrine + commander personality; engagement
  resolution on fleet vectors × fortification (Fortress facility joins
  BuildableTypes, Military-tier-gated) × supply reach × commander
  competence + seeded rolls; outcomes decisive/attrition/stalemate;
  hull losses conserve to wreckage; facility condition damage; captures
  transfer intact; sieges structured by defender reserves + fortress
  tier + relief; fallen ports transfer domains with segments intact;
  real interdiction (blockade fleets sever lanes) replaces
  SimState.SeveredLanes. Named battles with commanders (war-hero mints,
  commander hazard). Wars named ("the <name> War").
- [x] **H7 — Weariness, termination, settlement, aftermath** *(done:
  `Interpolity/WarResolution.cs`; the legitimacy war term is LIVE —
  `WarScore` (0.5 at peace; ± objective progress − exhaustion) replaces
  InteriorOps' stub, and `Economy.WarWearinessPerYear` (inert since
  slice A) drives exhaustion with the loss share on top; break
  conditions on truth: exhaustion ≥ 1, legitimacy <
  LegitimacyCollapseFloor, coalition strength < FleetExhaustionShare ×
  mustered, capital lost, extinction; controllers sue for peace
  (SettlementResponseAct) when exhaustion > 0.7 or strength share <
  0.3; Terminate settles: attacker victory (defender broke or all
  objectives taken, attacker whole) executes the demand — cessions
  hold + LostTerritory claims raise per ceded port, reparations =
  treasury share conserved, Vassalize binds (guarded), Independence
  unbinds; anything else = white peace with captures + facilities
  returned; wind-down: demobilization to reserve, tension ×(1−relief),
  veterans bump military-faction militancy both sides, winner/loser
  legitimacy deltas; event 510 PeaceSettled (diplomatic, the
  settlement record); knobs +7 + TUNING; seed-42 r12: 16 settlements
  (white peaces + cessions), no permanent galaxy-wide war (shape test
  locks it); 471/471 green, golden regen. NOTE: victories often cede
  0 ports — sieges rarely complete at current navy scales; flagged
  for the H11 shape pass)*:
  armaments/fuel drain through existing upkeep + mobilization demand;
  war loans; weariness through the interior (losses + SoL decline shift
  ideology → peace/military factions → legitimacy war term armed,
  `Economy.WarWearinessPerYear` live or retired deliberately); break
  conditions (political collapse, exhaustion, capital loss); settlement
  negotiated from per-objective outcomes (cessions, reparations,
  tribute, vassalization, white peace); aftermath residue: standing
  claims, veterans → military factions, war heroes, debt overhang.
- [x] **H8 — Native policy + emergence crises** *(done: `Interpolity/
  NativeOps.cs`; PreSpaceflight origins rank-project onto
  (EmergenceWindow, NativeWindowYears] — deterministic from config,
  never from EpochCount (LoadThenContinue-safe); species minted at
  fire time via `SkeletonBuilder.DeriveSpecies` (made public; rolls
  keyed to the origin cell); host = nearest covering port's owner;
  free space → actor with EntryEpoch=now, founded by the same entry
  loop (full contact-era EntryGradeBonus — behind, not hopeless);
  uplift (Life ≥ 2 gate) → client polity + FirstContact + vassal Bind
  after entry (uplift without the tech integrates instead — never a
  cage); integrate → new culture + segment under the covering port +
  event 309 (the cultural faction coalesces via FactionOps);
  exploit / protectorate-turned-cage → suppressed: captive segment at
  SoL 0.1, standing Liberation claims for every related rival (subject
  = culture id, released kin-style when freed), event 308; controller
  maps NativePolicy from temperament (structural); origins layer v2
  (ResolvedEpoch); knobs Genesis.* +4 + TUNING; seed-42 r21: members,
  clients, and one throttled emergence in the chronicle; 475/475,
  golden regen)*: PreSpaceflight origins
  get projected emergence dates; inside claimed space the host's
  NativePolicy resolves (uplift → client vassal; integrate → member +
  cultural faction; exploit/protectorate-cage → suppressed emergence =
  standing liberation casus belli + graduation path); free-space
  emergences found polities as slice F does.
- [x] **H9 — Civil wars** *(done: `Interpolity/CivilWarOps.cs`;
  contested coups erupt at the coup site — the realm's outer half
  rallies to the deposed ruler as a provisional polity founded through
  `GraduationOps.FoundSplinter` (the schism mechanics EXTRACTED into a
  shared helper, per the kickoff's reuse-don't-fork note), keeping the
  pre-lurch ideology, pre-coup form, and the old king; a
  CasusBelli.CivilWar war (demand Submission, no allies — brothers'
  wars stay in the family) fights for the capital through the ordinary
  machinery; Submission settlements merge the loser back whole
  (either direction) via the federation plumbing; one-port realms
  can't split (the coup stands); EpochEngineTests polity-count amended
  (+ splinters); seed-42 r12: the Thanymi Loyalists fight and lose the
  Ralili Civil War at y750, merging back retired; 479/479 green,
  golden regen)*: G's contested coups fight through the war
  machinery against a provisional polity (reuse graduation founding
  flows, no forks).
- [x] **H10 — REPL surfaces** *(done: `Inspector/InterpolityView.cs` —
  `relations [polityId]` (warmth/tension bars WITH their live source
  terms, bonds + clocks, standing offers, ties, incidents, live
  claims), `wars` registry, `war <id>` (sides + allies + exhaustion
  bars + strength-of-mustered, fronts with siege counters and fall
  thresholds, war fleets with commanders, the war's own chronicle);
  `emap war` (belligerent domains lettered, peaceful fade to commas,
  war fleets burn as !) and `emap tension` (owner's hottest relation
  as digit — the pressure gauge shaded); help text updated; chronicle
  prose landed with each task; `SimState.SeveredLanes` DELETED — real
  interdiction replaced it: MarketView/Migrate read
  FleetOps.SeveredLaneIds (blockades now stop refugees too — a real
  mechanics gain, golden regen), `lanecut` superseded (points at
  `fleetpost <id> blockade <portId>`), tests blockade via
  `EpochTestKit.BlockadePort`; `watch` untouched; 479/479 green)*:
  `relations [id]` panel (per-pair warmth/
  tension/treaty/claims with sources), `wars` / `war <id>` panel
  (objectives, fronts, sieges, commanders), `emap` war/tension layers,
  chronicle prose for every new event, `watch` intact.
- [x] **H11 — fresh-eyes whole-branch review + one fix wave** *(done:
  one subagent reviewed the full branch diff; 7 confirmed findings,
  all fixed in one wave: (1) war fleets stranded on Blockade stations
  when their owner merged away before demobilization — a reunited
  civil-war realm permanently blockaded its OWN capital; Demobilize
  now runs before the Submission merge and MergeInto stands down
  inherited war stations; (2) vassal bonds survived a party's
  retirement, paralyzing orphaned vassals forever — Retire dissolves
  bonds, OverlordOf/HasVassals require living parties; (3)
  LostTerritory claims never released — now resolve when the holder
  retakes the port (and portless secessions leave no −1-subject
  claims); (4) Demobilize's fleet-hunt arm stood down EVERY attacker
  Expedition fleet across concurrent wars — now keyed to the
  defender's capital station; (5) the protection vassals pay tribute
  for is now DELIVERED: an attacked vassal's overlord joins the
  defense; (6) uplift clients no longer bind under a host that is
  itself a vassal (no nested chains); (7) wars whose leader retires
  mid-war settle white immediately — never a victory over a ghost,
  never a resurrecting white-peace port return. Review explicitly
  cleared serializer field orders (REL/CLM/WAR/OBJ/ACTOR v5/ORIGIN v2
  + 17 payload cases), determinism keying (channels 66–69), no
  EpochCount-derived state, conservation through merges/splits/
  tribute/reparations/wrecks, and Inspector bounds. Shape bands at
  42/7/1234: wars 17/31/18 declared, 7/9/4 burning at end (most pairs
  at peace), ~50 treaty signings each, vassalizations everywhere,
  1 federation, 1 civil war, captures real. TUNING complete (Genesis/
  Relations/War tables + structural notes). Golden regen; 479/479)*.
- [ ] **H12 — eyeball gate**: a war readable as a story (tension loading,
  spark, declaration with casus belli, named battles, a siege, the
  settlement ceding a port, the grudge left behind).
- [ ] **H13 — merge + wrap-up**: merge · HANDOFF · Slice I kickoff prompt
  · flip the kickoff checkbox · golden frozen · push on user say-so.

## Decisions

- New code lives in `src/Core/Epoch/Interpolity/` (namespace stays flat
  `StarGen.Core.Epoch` — folder is organization only), mirroring G's
  `Interior/`.
- Relations/war registries serialize as two appended layers:
  `relations` v1, `wars` v1 (after `corporations`).
- Knob families: `Relations`, `War` (+ additions to existing families
  only where the dial already belongs there).
- Warmth/tension recompute and native emergence run in the Interior
  phase; treaty/act resolution and engagement resolution run in
  Resolution (assignment reads doctrine written at Intent — no decision
  points outside Intent).

## Surprises

- (none yet)
