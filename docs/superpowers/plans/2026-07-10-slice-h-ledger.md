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
- [ ] **H5 — Casus belli + spark + declaration**: four-category menu
  computed from real state (economic: resource seizure/chokepoint/
  punitive; ideological: crusade/liberation/containment; political:
  succession claims/military-grievance discharge/vassal enforcement or
  secession; spark: border incidents in contested-overlap space,
  escalation ∝ tension, fizzles are events too); DeclareWarAct carries
  objectives + settlement demand; War registry (belligerents via
  defense alliances, war leaders, objectives). Events 403+ military.
- [ ] **H6 — War conduct**: theater/objective model each epoch —
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
- [ ] **H7 — War economy, weariness, termination, settlement**:
  armaments/fuel drain through existing upkeep + mobilization demand;
  war loans; weariness through the interior (losses + SoL decline shift
  ideology → peace/military factions → legitimacy war term armed,
  `Economy.WarWearinessPerYear` live or retired deliberately); break
  conditions (political collapse, exhaustion, capital loss); settlement
  negotiated from per-objective outcomes (cessions, reparations,
  tribute, vassalization, white peace); aftermath residue: standing
  claims, veterans → military factions, war heroes, debt overhang.
- [ ] **H8 — Native policy + emergence crises**: PreSpaceflight origins
  get projected emergence dates; inside claimed space the host's
  NativePolicy resolves (uplift → client vassal; integrate → member +
  cultural faction; exploit/protectorate-cage → suppressed emergence =
  standing liberation casus belli + graduation path); free-space
  emergences found polities as slice F does.
- [ ] **H9 — Civil wars**: G's contested coups fight through the war
  machinery against a provisional polity (reuse graduation founding
  flows, no forks).
- [ ] **H10 — REPL surfaces**: `relations [id]` panel (per-pair warmth/
  tension/treaty/claims with sources), `wars` / `war <id>` panel
  (objectives, fronts, sieges, commanders), `emap` war/tension layers,
  chronicle prose for every new event, `watch` intact.
- [ ] **H11 — fresh-eyes whole-branch review + one fix wave**; TUNING
  sweep; shape bands at seeds 42/7/1234 (wars start AND end; most pairs
  at peace most of the time; no permanent galaxy-wide war; federations/
  vassalizations rare but present).
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
