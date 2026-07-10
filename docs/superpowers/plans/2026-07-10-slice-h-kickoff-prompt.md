# Slice H Kickoff — Session Prompt

You are starting **Slice H (Relations & war)** of the epoch-sim
implementation roadmap, under the lighter protocol in `/CLAUDE.md` (read it
first). H gives polities an outside: contact and stances, warmth/tension
per pair, the treaty ladder up through federation and vassalage, dynastic
instruments, a casus-belli menu with the spark mechanism, theater/objective
war fought on E's fleet vectors, sieges on reserves, negotiated
settlements, and native policy resolving F's late emergences. After H, the
galaxy's polities are no longer strangers expanding past each other —
they are neighbors with loaded borders, treaties with teeth, and wars
that end when the loser's politics break.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules.
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — row H
   (consumes E's fleets, F's natives/schedule, G's temperament
   composition; I needs H's wars for news/perception).
3. **The design docs H implements**:
   - `docs/design/interpolity/relations.md` — contact (first-contact
     stance from temperament compositions × strangeness × reputation),
     native policy + emergence crises (suppressed emergence = standing
     liberation casus belli), warmth/tension per pair, the four treaty
     rungs with teeth, federation (a NEW fused polity), vassalage
     (tribute, absorption/secession exits), dynastic instruments →
     succession claims.
   - `docs/design/interpolity/war.md` — the casus-belli menu (economic /
     ideological / political / spark; escalation ∝ tension), declaration
     as an Intent act with objectives + settlement demand, the
     theater/objective model (assignment per doctrine + commander
     personality, engagement resolution on fleet vectors modified by
     fortification/supply/competence, sieges structured by reserves and
     relief), termination when politics break, settlement negotiated from
     per-objective outcomes, aftermath residue (claims, veterans, war
     heroes, wreckage POIs, conduct reputation — reputation *travel* is
     slice I).
   - Skim `fleets/ships-and-fleets.md` (vectors, postures, supply),
     `polity/factions-and-government.md` §war couplings (weariness is the
     interior responding), `frame/controller-contract.md` (war/treaty/
     settlement acts are already typed).
4. **What H replaces / fills**:
   - `SimState.SeveredLanes` — the REPL debug blockade hook; real
     interdiction replaces it ("slice H replaces this with real
     interdiction").
   - `Acts.cs` — `DeclareWarAct`, `TreatyAct`, `SanctionAct`,
     `SettlementResponseAct`, `VassalageAct`, `QuarantineAct`,
     `DynasticInstrumentAct` are typed and unresolved; Resolution learns
     them.
   - `PolityPolicies.DiplomaticPostures` (per-polity stance map, unused)
     and `MilitaryDoctrine` (posture + engagement bias, unused by
     Resolution).
   - `InteriorOps.Recompute`'s war term (stub 0.5) +
     `Interior.LegitimacyWarWeight`; `Economy.WarWearinessPerYear`
     (inert since slice A).
   - `CoupStruckPayload.Contested` — the recorded civil-war stubs G left;
     H fights them (provisional polity per the design).
   - `NotableType.WarHero` (trigger unarmed), commander war hazard
     (characters.md: "war for commanders"), military-faction grievance
     discharge (casus belli), `Faction.Militancy`.
   - `PolityRecord.ReserveQty` is polity-aggregate; "wars make it
     spatial (H)" — sieges run on reserves.
   - Pirate bands (`Corporation.TargetId` = the hunted lane) are
     registry-level; H gives them teeth (raiding, escorts, patrols).
   - `OriginEra.Native` origins (pre-spaceflight homeworlds) +
     `PolityPolicies.NativePolicy` — emergence crises inside claimed
     space.
5. **What Slice G landed** (ledger
   `docs/superpowers/plans/2026-07-10-slice-g-ledger.md` — read the
   Surprises section):
   - **The temperament composition** (`Temperament.Compose`, carried on
     `PerceptionView.SelfTemperament`) — H's first-contact stances, war
     appetite, and doctrine read THIS, never `SpeciesProfile` directly
     (fixed species reads are retired from Intent).
   - **Interior state** per polity: `PolityInterior` (form, official
     ideology, legitimacy, cohesion, enforcement) — the break conditions
     ("a polity breaks when its politics break") and grip are live.
     Graduation (`GraduationOps`) already founds polities via schism and
     coups; war-driven collapse should reuse those flows, not fork them.
   - **Characters**: commanders sit on warship/expedition fleets
     (`FleetRecord.CommanderId` → boldness/competence for engagement
     modifiers); rulers/dynasties with prestige; `EventLog.ForCharacter`
     biography index (name battles' commanders and their bios write
     themselves); `CharacterOps.MintNotable` (cap-aware) for war heroes.
   - **Factions**: military factions form on officer renown × militancy
     ("they'll mostly wait for H's wars"); grievance discharge is a
     political casus belli; peace pressure = weariness shifting ideology
     → factions → legitimacy, all already coupled.
   - **Corporations**: `ICreditLedger`/`SimState.LedgerOf` unify money
     flows (war loans, reparations, tribute can pay anyone);
     `MarketEngine.PayHaulers` pays posted fleets; sanction evasion and
     letters of marque have real books to flow through.
   - **Tech**: `Tech.Region(polity, Military)` feeds design sheets;
     salvage diffusion already reads wreckage in reach — battles seed
     tech spread for free. Fortification tiers gate on Military
     (technology.md) — the Fortress facility type exists in the catalog
     but is not in polity BuildableTypes yet.
   - **Artifact**: 17 layers (interior v5, corporations v1), actors v4.
     Golden regenerated per history-changing task (same-commit), frozen
     at slice end; `LoadThenContinue_EqualsTheStraightRun` is the
     strongest gate. Every new `src/Core` file needs a two-line `.meta`.
   - **Registries**: events — military next free **403**, diplomatic
     **5xx opens with H**, political next 308, economic 208, corporate
     605, character 704. `RollChannel` next free: **66** (35/36
     SimWar/SimBattle were the deleted prototype's — never reuse).
     Knobs: epoch-side `KnobRegistry` (name-sorted, tests enforce) +
     TUNING rows; catalog data (casus belli menu, treaty rungs) is
     data-as-code with a TUNING structural note.
   - **G lessons that will bite H**: the *founding supply race* pattern
     has now struck three times (E yards, G research, G corporations) —
     any new consumer of goods needs a demand pull registered at Markets
     or it starves at frontier markets; grace periods before death
     clocks (war exhaustion, siege collapse) prevent same-epoch
     stillbirths; the conservation gate catches real bugs (a dangling
     else minted 35k credits — brace every else); `required` members
     don't compile on netstandard2.1; don't reshape files with
     PowerShell `-replace` (UTF-8 mojibake — use targeted edits);
     `Faction.PaidThisEpoch`-style step-transients must be provably zero
     at epoch boundaries or LoadThenContinue breaks.

## Scope (roadmap row H)

- **Contact + stances**: reach-overlap detection, first-contact events,
  initial stance from temperament compositions; per-pair relations
  registry (warmth, tension, standing claims) — serialized, with sources
  legible.
- **Treaty ladder**: trade pact / non-aggression / defense alliance
  (each with the design's teeth), mutual consent in Resolution; breaking
  = event. **Federation** (fused NEW polity, population-weighted
  composition) and **vassalage** (tribute flows, absorption/secession
  exits) as the top rungs.
- **Dynastic instruments**: marriages/wardships between dynastic forms →
  warmth now, succession claims later (tension sources).
- **Casus belli + spark**: the four-category menu from real state;
  incidents roll in contested-overlap space, escalation ∝ tension;
  declaration = Intent act with objectives + demand.
- **War conduct**: theater/objective model on fleet vectors (assignment
  per doctrine + commander, engagement resolution with fortification/
  supply/competence modifiers, decisive/attrition/stalemate); real
  interdiction replaces SeveredLanes; **sieges** on defender reserves +
  fortress tier + relief; captures transfer ports/domains with segments
  intact; hull losses conserve into wreckage.
- **War economy + weariness**: armaments/fuel drain, war loans,
  weariness through the interior (ideology shift → peace factions →
  legitimacy) — the existing couplings, armed.
- **Termination + settlement**: break conditions on politics/exhaustion/
  capital; per-objective settlement (cessions, reparations, tribute,
  vassalization, white peace); aftermath residue (claims, veterans →
  military factions, war heroes, debt overhang).
- **Native policy + emergence crises**: native-era homeworlds inside
  domains resolve per policy at emergence (client vassal / member +
  cultural faction / suppressed = liberation casus belli); free-space
  emergences unchanged.
- **Civil wars**: G's contested coups and (optionally) crushed-revolt
  escalations fight through the same war machinery against a provisional
  polity.

**Boundary**: perception stays perfect-info — conduct *reputation* and
news travel are slice I (record the events; stances read them locally);
plagues are I-adjacent; POI compilation of battlefields is I; espionage
stays reserved; the Unity atlas is K. Letters of marque / player-facing
contracts are play-scope flavor — registry-level only if cheap.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-h-war` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-h-ledger.md`; TDD; frequent
   commits. Don't share a checkout with another live session — take a
   `git worktree` if one exists.
3. Gates: `dotnet test` green (hex tier untouched) · determinism
   byte-identity · load-vs-rebuild + LoadThenContinue over the new
   layers (relations/wars) · conservation (reparations, tribute, war
   loans, seizures are flows — credits still conserve to the mint; hull
   ledger conserves through battles) · shape bands (wars start AND end;
   most pairs at peace most of the time; no permanent galaxy-wide war;
   federations/vassalizations rare but present across seeds).
4. REPL surface: relations panel (per-pair warmth/tension/treaties/
   claims) · war panel (objectives, fronts, sieges, commanders) ·
   `emap` war/tension layers · chronicle prose for every new event
   (named wars: "the <name> War") · `watch` stays intact and ideally
   shows borders flaring.
5. User gates: scope nod · REPL eyeball · merge decision. Eyeball
   suggestion: **a war you can read like a story** — tension loading on
   a contested border, the spark incident, the declaration with its
   casus belli, named battles with commanders, a siege, the settlement
   that cedes the port, and the grudge it leaves behind.
6. Wrap-up: merge · HANDOFF · **write the Slice I kickoff prompt**
   (narrative: perception/news/chronicle/POI/plagues — read the roadmap
   row first) · flip the box below · push only on user say-so.

- [ ] Slice H complete
