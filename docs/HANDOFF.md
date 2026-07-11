# Session Handoff — 2026-07-11 (Slice H: Relations & war — merged)

State: `main`, merged locally, **not yet pushed** (push on user say-so;
slices F, G, and H are all local-only). Tests 487/487 green — hex-tier
suite untouched at 100%. ProjectSettings churn remains uncommitted as
always.

## What this session did: Slice H of the epoch-sim rebuild, merged

The galaxy's polities are no longer strangers expanding past each other —
they are neighbors with loaded borders, treaties with teeth, and wars
that end when the loser's politics break. All new code in
`src/Core/Epoch/Interpolity/`. Ledger (tasks, decisions, surprises, the
review wave, the eyeball wave):
`docs/superpowers/plans/2026-07-10-slice-h-ledger.md`.

- **Contact + relations** (`PolityRelation`, `RelationsOps`): polities
  meet when port reach overlaps; per-pair warmth/tension drift toward
  targets recomputed from live legible sources (trade, treaties,
  dynastic ties / overlap, standing claims, interdiction, ideology ×
  zeal, agitation, militancy); tension decays only when sources
  resolve. Claims (cultural-kin, lost-territory, succession,
  liberation) raise, feed tension and the casus-belli menu, and
  release when their causes do.
- **The treaty ladder** (`ResolveTreaty`, `FederationOps`): trade pact
  (tariff cut + cross-border lanes), non-aggression (tension damping +
  spark de-escalation), defense alliance (coalitions answer, attackers
  price them); **federation** fuses a NEW polity (pop-weighted
  composition, fresh name, founding legitimacy high) — entangled
  friendly borders discount the gate and warmth damps their overlap
  tension (the interleaved core federates or fights, never simmers);
  **vassalage** binds tribute + foreign-policy lock + delivered
  protection, exits by absorption or secession. **Actors retire**
  (`Actor.Retired`) — merged away, annexed, submitted; they never
  re-enter.
- **Dynastic instruments**: marriages/wardships buy warmth now and
  lapse into succession claims two reigns later, held by the prouder
  house, dying with its line.
- **War** (`War`, `WarOps`, `WarConduct`, `WarResolution`): the
  casus-belli menu computes from real state (price shocks, chokepoints,
  blockades, crusades, liberation/succession/lost-territory claims,
  military-faction discharge, vassal secession, **expulsion** — the
  port that settled into an older sphere, by founding dates); border
  incidents roll in surveyed overlap space and mostly fizzle;
  declaration carries grounded objectives + a demand — bounded
  (cede/reparations/vassalize/independence) or **Annihilation** when
  hatred is saturated with stacked claims (no surrender accepted; the
  loser is annexed whole). Conduct: doctrine + marshal nerve mobilize
  warships into war fleets per objective; blockade postures sever real
  lanes (the debug lane-cut hook is deleted); engagements resolve on
  fleet vectors × fortification (Fortress joins BuildableTypes at
  Military ≥ 2) × supply distance × commander competence; losses
  conserve to wreckage; sieges grind on port larders + fortress tiers,
  break on relief; captures transfer domains with segments intact.
  Termination: exhaustion (WarWearinessPerYear finally live + loss
  share), legitimacy collapse (the interior war term is live too),
  fleet exhaustion, capital loss, extinction, or concession;
  settlements read per-objective outcomes and leave residue (claims,
  veteran militancy, war heroes, legitimacy swings, tension relief).
- **War is expensive** (eyeball wave): belligerents shift budget to the
  military, yards pivot to warships, quartermasters corner armaments/
  parts/fuel at `War.MobilizationFactor` (fabricators boom), and
  warships EAT — `War.RationsPerHullPerYear` competes with household
  subsistence (~1.5 realm SoL points per 6-epoch war), and unfed
  fleets lose readiness (sieges starve navies).
- **Contiguous borders** (eyeball wave): colony scores drop
  `Expansion.EncroachmentPenalty` per foreign sphere entangled, the
  controller refuses net-negative sites (boxed-in realms consolidate),
  and founding into a sphere costs instant tension. Frontier domains
  are solid blobs; the packed core resolves by federation or war.
- **Civil wars** (`CivilWarOps`): contested coups split the realm
  through the extracted `GraduationOps.FoundSplinter` (shared with
  schisms), loyalists rally to the deposed ruler, and Submission
  settlements merge the loser back whole.
- **Natives** (`NativeOps`): pre-spaceflight origins fire on the native
  window — free births (species minted at date via the skeleton's own
  derivation), uplift client vassals (Life ≥ 2), integrated cultural
  minorities, or suppressed emergences arming every rival with a
  standing liberation casus belli.
- **Artifact**: 19 layers (relations v4 REL/CLM, wars v1 WAR/OBJ,
  origins v2 ResolvedEpoch, actors v5 Retired); LoadThenContinue green;
  golden frozen at the final format (last regen: the rationing wave).
- **Registries**: events next free — economic 208, political **310**,
  military **408**, diplomatic **511**, corporate 605, character 704.
  `RollChannel` next free: **70** (66 FederationSeed, 67 WarSpark,
  68 Battle, 69 CommanderFate). Knobs: `Relations.*` (26), `War.*`
  (28), `Genesis.Native*` (4), plus Controller.BaseTariffRate and
  Expansion.EncroachmentPenalty — all in KnobRegistry + TUNING.
- **REPL**: `relations [id]` (gauges WITH live source terms, bonds,
  claims) · `wars` / `war <id>` (fronts, siege clocks, commanders, the
  war's own chronicle) · `emap war` / `emap tension` · chronicle prose
  for all 17 new event types · `watch` untouched · `lanecut`
  superseded by real interdiction (`fleetpost <id> blockade <portId>`).
- **Design docs amended** (same branch, per hard rules): war.md gained
  the Spatial cause row, war-aims-scale-with-hatred, and total
  mobilization; relations.md gained expansion-prices-the-neighbors and
  entanglement-pushes-fusion (pair-mean openness).
- Fresh-eyes review: 7 confirmed findings, all fixed (stranded war
  fleets self-blockading a reunified capital; vassal bonds surviving
  retirement; monotonic lost-territory grudges; cross-war
  demobilization; undelivered vassal protection; nested overlord
  chains; ghost-war victories).
- Eyeball accepted 2026-07-11 after one user-flagged wave (map soup →
  contiguity + federation pull + war aims + war cost, all above).

## Next up

1. **Slice I (Narrative)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-11-slice-i-kickoff-prompt.md`
   (complete: reading list, the perfect-info stubs H left, scope,
   boundary, H's lessons).
2. **Push main** when ready (slices F, G, H are all local-only).
3. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · REPL eyeball · merge
decision; kickoff-prompt chaining); hex-tier suite never breaks;
ProjectSettings stays uncommitted; bash printf for REPL piping; parallel
slices never share a checkout — take a `git worktree` each; every
calibration constant goes in a knob registry + TUNING.md; every new
`src/Core` file gets a two-line `.meta` with a fresh guid; any new goods
consumer needs a demand pull at Markets or it starves; grace periods
precede death clocks; step-transients provably zero at epoch boundaries.
New this slice: **histories churn — actors retire** (tests pick live
subjects via `EpochTestKit.FirstLiveRelation`, never `Relations[0]`);
golden regen one-liner: `printf 'epoch 42 40 12\nesave
tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt\nquit\n' | dotnet
run --project src/Inspector`. Older carried minors:
`git show a1f5843~40:docs/HANDOFF.md`.
