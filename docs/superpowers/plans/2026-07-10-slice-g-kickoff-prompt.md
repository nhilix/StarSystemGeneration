# Slice G Kickoff — Session Prompt

You are starting **Slice G (Interior & corporations)** of the epoch-sim
implementation roadmap, under the lighter protocol in `/CLAUDE.md` (read it
first). G gives polities an inside: factions that pressure and graduate,
government forms, characters in roles (E's commander slots finally fill),
real tech domains (retiring `Economy.TechTierStub`), the temperament
composition (retiring the fixed species temperament as the Intent
personality), and corporations founded from D's persistent niches. After
G, a polity is no longer a monolith with a species-flavored controller —
it's a government with factions at its back, a ruler with a name, and
corporations it doesn't fully control.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules.
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — row G
   and the sequencing rationale (G consumes D's niches and E's commander
   slots; H needs G's temperament composition + F's natives).
3. **The design docs G implements**:
   - `docs/design/polity/factions-and-government.md` — six faction bases,
     pressure/appeasement/grievance, the graduation table (schism / coup /
     charter / revolt), legitimacy & cohesion, the eight government forms,
     and **the temperament composition** (species × ideology × ruler ×
     factions, weighted by form) that replaces any fixed species vector.
   - `docs/design/polity/characters.md` — sparsity (roles + notables
     only, ~a dozen per polity), on-demand deterministic generation,
     species-real lifespans, succession (+ crises), dynasties,
     notables, renown, derivable biographies (P8).
   - `docs/design/economy/technology.md` — four domains (Industrial /
     Military / Astrogation / Life), tier ladders gating **ceilings and
     regions**, research as Allocation execution (Refined Exotics ×
     Compute), the three diffusion channels (trade / salvage / espionage-
     reserved). Provided interface: `Ceiling(polity, domain)`,
     `Region(polity, domain)`.
   - `docs/design/economy/corporations.md` — persistent-niche founding
     through the charter graduation, founding-niche character (incl.
     cartels + pirate bands as outlaw cousins), the corporate controller,
     cross-border portfolios + internal logistics, dividends → faction
     wealth, lobby/nationalization, death + residue.
   - Skim `frame/actors.md` (graduation mechanism, actor kinds) and
     `frame/controller-contract.md` (corporations are controllers too).
4. **What G replaces / fills**:
   - `Economy.TechTierStub` (EpochSimConfig) — recipe gating and design
     TechTier read it today (`ShipDesign.cs`, production); real domains
     retire it (flag: `EntryGradeBonus` from F should feed starting
     Astrogation/Industrial tiers — that was its design intent).
   - `FleetRecord.CommanderId = -1` — E left the slot; commanders enter
     through character roles.
   - `SpeciesProfile` temperament axes as the *direct* Intent personality
     — the composition replaces the direct read (species disposition
     remains one term). `GenesisController` reads composition weights by
     government form.
   - `PolityPolicies.CharterOpenness` (exists, unused) — the charter
     gate. `Budget.Appeasement` (exists, unused) — faction appeasement.
     Budget weights generally stop being "the controller's taste" and
     start feeling faction pressure.
   - `Culture` registry (one per species since B) — schism graduation is
     the split mechanic the comment has been waiting for.
5. **What Slice F landed** (ledger
   `docs/superpowers/plans/2026-07-09-slice-f-ledger.md` — the notes
   carry the hard-won lessons):
   - Genesis is causal end-to-end: `SapientOrigin` (emergence schedule),
     `PrecursorWave` registry (typed sites incl. dormant remnants),
     `GalacticFeature` registry, RegionCell v2 residue fields. Machine
     species descend from precursor capitals (`DescendantOfWaveId`).
     G contact surface: **salvage-channel tech diffusion** can read
     wreckage (E) and — bounded — precursor site *presence* for flavor,
     but archaeology/salvage consumption is slice I; do not dig.
   - `PolityRecord.EntryGradeBonus` — maturation quality + late-emerger
     contact bonus; currently lifts entry design grades only. G should
     convert it into starting tech tiers (Astrogation/Industrial per the
     design) and delete the grade hack.
   - Deep-time chronicle seeds the log floor; `WorldEvent.WorldYear` is
     long; `SimTraceView.YearLabel` renders any zoom. Event blocks: next
     evolutionary free 106, economic 207, political 3xx (PolityEmerged
     300, PortEstablished 301 used), military 403, diplomatic 5xx,
     corporate 6xx (G opens it), character 7xx (G opens it).
     `RollChannel` next free: **60** (append-only; 30 + 40 retired).
   - Knob discipline now has two registries: epoch-side `KnobRegistry`
     (EpochSimConfig families) and galaxy-side `GalaxyKnobRegistry`
     (GalaxyConfig). G's dials are epoch-side (new families: `Interior`?
     `Tech`? `Corporate`? — name them, sort them, TUNING rows, tests
     enforce). Catalog data (government forms, faction bases, notable
     types, domain definitions) is data-as-code with a TUNING structural
     note (E's chassis catalog is the pattern).
   - Artifact: 15 layers now (…, features, origins, precursors). G
     appends its layers (factions, characters, tech, corporations —
     probably one `interior` + one `corporations` layer; decide); actors
     layer is at v3. Golden regenerated per history-changing task
     (same-commit), frozen once at slice end; the strongest gate is
     `LoadThenContinue_EqualsTheStraightRun`.
   - **F lessons that will bite G**: rates that are slow lotteries
     destroy causality (make triggers threshold-crossings of real
     pressures, per the design); sentinel values near int.MinValue
     overflow on subtraction; a cached lookup over a rebuildable registry
     needs invalidation; report volumes, not counts; `.meta` files for
     every new `src/Core` file; bash `printf` for REPL piping; Read the
     file before fixing Grep's phantom `\ ` comments.
   - **REPL / watch**: `watch <seed> [radius] [epochs] [frameMs]` runs
     the whole pipeline as an in-place animation (`FrameAnimator` — clip
     lines to terminal width, never wrap). G's surfaces should include a
     polity panel (form, legitimacy, factions with strength bars, ruler +
     reign), a tech panel/map layer, corporation dumps, and chronicle
     prose for every new event type (`SimTraceView.Describe`).

## Scope (roadmap row G)

- **Factions**: six-basis formation from real state (segment ideology
  distance, culture minorities, frontier distance, corporate dividends,
  veteran/commander networks, sacral surges); strength/agenda/militancy;
  Interior-phase pressure on policies; appeasement spending (the budget
  line exists); grievance; **graduation**: schism (new polity from
  domains + culture split), coup (leadership + ideology lurch; contested
  → stub the civil war as an event until H), charter (→ corporation),
  revolt (crushed: unrest, martyrs, compounding grievance).
- **Legitimacy & cohesion** driving graduation thresholds.
- **Government forms**: the eight-form catalog seated in ideology space ×
  species; succession rules, policy inertia, faction tolerance,
  legitimacy sources; form changes through graduation events.
- **Characters**: role slots (ruler/heir/marshal per form, faction
  leaders, corporate executives, fleet commanders — fill
  `FleetRecord.CommanderId`), on-demand deterministic generation
  (personality: ideology position + boldness/zeal/competence/ambition),
  species-real aging + death checks, succession (+ crisis events),
  dynasties + prestige, notables (war hero, founder, prophet, pirate
  lord, magnate, explorer — capped), renown from event participation.
- **Tech domains**: four per-polity tiers; research in Allocation from a
  standing budget split (Refined Exotics × Compute — both goods exist);
  `TechAdvance` events; ceilings/regions consumed by grades, recipes,
  design sheets, port axes; trade-contact + salvage diffusion (espionage
  slot reserved); starting tiers from F's maturation quality.
- **Temperament composition**: species × official ideology × ruler ×
  faction pressure, weighted by form — the `GenesisController` reads the
  composition; fixed species reads retire from Intent.
- **Corporations**: persistent-niche watcher (price gradients, unserved
  lanes, deposits, prohibited margins); charter graduation founds them
  (character stamped by niche; cartels + pirate bands from lawless
  niches — pirate *bands* may stay registry-level until H gives raiding
  teeth); corporate controller (policies/acts per the contract);
  portfolios (facilities, freight via the fleet interface, internal
  logistics at cost); dividends → faction wealth; lobbying;
  nationalization act; deaths with residue.

**Boundary**: war machinery (civil war resolution, succession wars) is H —
record the events, stub resolutions; espionage diffusion reserved, not
built; plagues are I-adjacent (polity/plagues.md — not G); archaeology/
salvage *consumption* including precursor-site digging is I (salvage
diffusion may read wreckage grades only); no perception changes (perfect
info until I); corporate/character play-scope controllers are P2-shaped
but AI-only.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-g-interior` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-g-ledger.md`; TDD; frequent
   commits. Don't share a checkout with another live session — take a
   `git worktree` if one exists.
3. Gates: `dotnet test` green (hex tier untouched) · determinism
   byte-identity · load-vs-rebuild + LoadThenContinue over the new
   layers · conservation (appeasement/research/dividends are treasury
   flows — credits still conserve to the mint) · shape bands (factions
   form but polities don't dissolve every epoch; graduations across a
   40-epoch history in the low single digits per polity; characters ~a
   dozen per polity; tech tiers advance without runaway).
4. REPL surface: polity panel (form, legitimacy, factions, ruler) ·
   character/dynasty dump + biography view (P8 test: reconstruct a life
   from the log) · tech panel + map layer · corporation registry dump ·
   chronicle prose for every new event · watch stays intact.
5. User gates: scope nod · REPL eyeball · merge decision. Eyeball gate
   suggestion: **a polity you can read like a story** — its form, its
   ruler's reign and succession, a faction rising on real grievance and
   graduating (a schism you can point at on the domain map), a
   corporation founded from a visible niche, and a tech gap you can see
   on the map (whose ports reach farther).
6. Wrap-up: merge · HANDOFF · **write the Slice H kickoff prompt**
   (relations & war — consumes E's fleets, F's natives/schedule, G's
   temperament composition; read the roadmap row before writing) · flip
   the box below · push only on user say-so.

- [ ] Slice G complete
