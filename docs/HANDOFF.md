# Session Handoff — 2026-07-10 (Slice G: Interior & corporations — merged)

State: `main`, merged locally, **not yet pushed** (push on user say-so).
Tests 419/419 green — hex-tier suite untouched at 100%. ProjectSettings
churn remains uncommitted as always.

## What this session did: Slice G of the epoch-sim rebuild, merged

A polity is no longer a monolith with a species-flavored controller —
it's a government with factions at its back, a ruler with a name, real
tech domains, and corporations it doesn't fully control. All new code in
`src/Core/Epoch/Interior/` (namespace stays flat). Ledger (tasks,
decisions, surprises): `docs/superpowers/plans/2026-07-10-slice-g-ledger.md`.

- **Government + interior** (`GovernmentForm`, `PolityInterior`,
  `InteriorOps`): eight-form catalog seated in ideology space × species
  (hive/machine/lithic embodiment-gated); per-polity official ideology
  (drifts toward popular at form inertia), legitimacy (SoL trend ×
  alignment × ruler prestige × accommodation; war term stubbed for H),
  cohesion (strain-discounted, hive-floored), enforcement (garrison
  hulls). Homeworld segments seed from a species ideology tilt.
- **Characters** (`Character`, `CharacterOps`): sparse-by-construction
  roster (courts, fleet commanders filling E's `CommanderId`, faction
  leaders, executives, capped notables); deterministic on-demand minting
  (culture syllable names; ideology position + boldness/zeal/competence/
  ambition); species-real mortality (lithic centuries, hive continuity,
  machine deprecation) + ruler assassination hazard; succession per form
  with heirless crises founding new houses; dynasties accrue prestige
  into legitimacy. Events 700–703; `EventLog.ForCharacter` = the P8
  biography index (`bio` in the REPL renders a life from the log).
- **Factions** (`Faction`, `FactionOps`): six bases form from real state;
  strength/agenda/militancy; budget pressure bends Allocation's weights
  (bounded by form tolerance); the appeasement line pays factions up to
  strength × share of the allocatable base (conserved treasury→faction
  flow); grievance compounds on the unmet fraction. Events 304/305.
- **Graduation** (`GraduationOps`): strength × grievance vs legitimacy ×
  enforcement × grip. Schisms secede real polities (ports, people,
  fleets + hull ledgers, pop-shares of every treasury, culture split —
  the Culture registry's long-awaited mechanic; leader crowned over a
  reseated form); coups lurch ideology and reseat forms (contested →
  civil-war stub for H); revolts martyr leaders and compound grievance.
  Events 302/303/306/307. Seed-42 r12: 10 polities become ~13–15 over
  the millennium.
- **Tech domains** (`TechState`, `TechOps`): per-polity Industrial/
  Military/Astrogation/Life tier ladders (geometric thresholds);
  research = Allocation executing the standing `ResearchSplit`,
  consuming Refined Exotics × Compute from own markets (wages recycle;
  a research demand pull sites the labs); TechAdvanced (207); trade
  diffusion across sovereignty borders (capped one tier below source) +
  salvage diffusion from out-graded wreckage; entry tiers from the
  emergence schedule (the entry-design grade hack deleted);
  **`Economy.TechTierStub` retired** — recipes, grade ceilings, design
  sheets, lane reach, siting radius, and demographics read real tiers.
  `Tech.Ceiling/Region(polity, domain)` is the provided interface.
- **Temperament composition** (`Temperament.Compose`): species × official
  ideology × ruler × faction pressure, weighted by form; rides
  `PerceptionView.SelfTemperament`; GenesisController's laws, reserves,
  and yard priorities read it — fixed species reads retired from Intent.
- **Corporations** (`Corporation`, `CorporationOps`,
  `CorporateController`): the niche watcher raises merchant factions
  where profit persists unclaimed (freight niches run the arbitrage's
  own profitability math); charter graduation incorporates at
  persistence + CharterOpenness + capital floor (cartels and pirate
  bands charter nowhere); corporations ARE actors with conserved books
  (`ICreditLedger`/`LedgerOf` unify all owner payouts); operations:
  facility investment by founding character, freight hulls sourced from
  any host-polity market and posted on gradient lanes (haulers now EARN
  the freight fee — `MarketEngine.PayHaulers`), cartel black-book skim,
  fleet supply with real attrition; dividends + lobby feed host
  corporate factions; nationalization act end-to-end; deaths leave
  residue (books settle whole — debt lands on the sovereign, never
  wiped). Events 600–604.
- **Artifact**: 17 layers (actors v4 carries ResearchSplit; interior v5 =
  INTR/CHAR/DYNA/TECH/FACT; corporations v1 = CORP); byte round-trips,
  LoadThenContinue green; golden frozen at the final format.
- **Knobs**: six new epoch families — `Interior` (12), `Character` (11),
  `Faction` (18), `Tech` (12), `Corporate` (18) + TUNING tables and
  structural notes (form catalog, basis agendas, temperament maps,
  lifespans, niche suffixes). Events next free: economic 208, political
  308, military 403, diplomatic 5xx (H opens), corporate 605, character
  704. `RollChannel` next free: **66** (60–65 used; 35/36 are the dead
  prototype's — never reuse).
- **REPL**: `polity [id]` (form, bars, reign, factions, charters) ·
  `characters [polityId]` · `bio <charId>` · `tech` · `corps` ·
  `emap tech` (the Astrogation reach gap) · chronicle prose for all 15
  new event types · `watch` untouched.
- Fresh-eyes review: 7 confirmed findings, all fixed (houseless coup
  thrones, revenue-less freight lines, stale executives, corpse
  crownings, HashSet iteration, band lane/port id conflation, phantom
  TechAdvance past the diffusion cap).
- Eyeball-accepted 2026-07-10 after one user-flagged wave: **corporate
  stillbirths were a founding supply race** (the slice-E lesson,
  unapplied — no demand pull, mirage niches, penniless charters). Fixed
  with `AddCorporateDemand`, arbitrage-real niche detection,
  host-polity hull sourcing, `CharterCapitalFloor`, and
  `FoundingGraceEpochs`; a regression test locks the grace in. The
  conservation gate also caught a dangling-else minting ~35k credits in
  corporate dissolution — brace every else.

## Next up

1. **Slice H (Relations & war)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-10-slice-h-kickoff-prompt.md`
   (complete: reading list, contact surfaces G left, scope, boundary).
2. **Push main** when ready (slices F and G are both local-only).
3. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · REPL eyeball · merge decision;
kickoff-prompt chaining); hex-tier suite never breaks; ProjectSettings stays
uncommitted; bash printf for REPL piping; parallel slices never share a
checkout — take a `git worktree` each; every calibration constant goes in
a knob registry + TUNING.md; every new `src/Core` file gets a two-line
`.meta` with a fresh guid. New this slice: **any new goods consumer needs
a demand pull at Markets or it starves** (three strikes now: E yards, G
research, G corporations); step-transient fields must be provably zero at
epoch boundaries. Older carried minors: `git show a1f5843~40:docs/HANDOFF.md`.
