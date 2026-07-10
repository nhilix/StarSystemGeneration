# Tuning — the calibration knob reference

Every calibration dial in the epoch sim lives in one index:
`KnobRegistry` (`src/Core/Epoch/KnobRegistry.cs`). The registry drives three
surfaces, so they can never drift apart:

- **The artifact**: the config layer (v3) serializes every knob as a
  name-sorted `KNOB|Family.Name|value` line — a run's full calibration is
  stamped into its history, and unknown knobs refuse to load.
- **The REPL**: `knobs [filter]` prints every dial with its live value (of
  the loaded sim) and one-line doc.
- **This document**: the consequence-of-turning prose the one-liners can't
  carry.

To run a differently-tuned galaxy today: set properties on `EpochSimConfig`
before `EpochGenesis.Seed` (tests do exactly this), or `eload` an artifact
whose KNOB lines you edited — the loader applies them. A standalone tuning
file (the moddable-XML experience) can layer on top of the registry when the
Unity atlas returns (slice K); the registry's `Find(name)` + `Set` is
already the loader it would need.

**Discipline**: a tuning constant must never exist outside the registry.
`KnobRegistryTests` enforces naming, ordering, docs, and accessor
round-trips. Structural constants (things that define mechanics rather than
calibrate them) stay in code and are listed at the end of this file.

Defaults below are current as of slice D. "Raise/Lower" describes the
first-order consequence; most dials interact, so move one at a time and
watch `emap price`, `market`, and the famine counts in the phase trace.

---

## Sim & Genesis (structural clock — ESIM line, not KNOB records)

| Knob | Default | Meaning |
|---|---|---|
| `Sim.YearsPerEpoch` | 25 | World-years integrated per generational step. Changing it re-times *everything*; rates are per-year by design (P7) so histories stay comparable, but drift caps and logistic steps compound differently. |
| `Sim.EpochCount` | 40 | History depth (~1,000y). Purely how long the story runs. |
| `Genesis.EmergenceWindowYears` | 500 | Latest entry year for staggered polity emergence. Wider = older empires meet younger neighbors; narrower = a crowded simultaneous dawn. |

## Economy — demand magnitudes

The three per-capita rates are the economy's metabolism; the elasticities
shape how demand bends under price.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Economy.SubsistenceUnitsPerPopPerYear` | 0.6 | Hungrier mouths: more famines, more agri, more provisions trade. **The famine dial.** | Easier frontier, food fades as a driver. |
| `Economy.SoLUnitsPerPopPerYear` | 0.4 | Bigger consumer-goods/medicine market: fabricators pay, SoL swings harder. | Populations content with less; less mid-chain industry. |
| `Economy.LuxuryUnitsPerPopPerYear` | 0.15 | Richer luxury/narcotics niche (G's corporations will thank you). | Prestige goods stay ornamental. |
| `Economy.SubsistenceElasticity` | 0.1 | Hunger bends to price (unnaturally stoic famines). | Hunger utterly price-blind. |
| `Economy.SoLElasticity` | 0.5 | Comfort spending flees inflation faster. | Sticky middle-class baskets. |
| `Economy.LuxuryElasticity` | 1.3 | Luxuries vanish at the first price twitch. | Luxuries behave like staples. |
| `Economy.ElasticFloor` / `ElasticCeiling` | 0.25 / 2.0 | The clamp on all of the above — widen to let price swings move demand more violently. | Narrow toward 1.0 to mute elasticity entirely. |

## Economy — price mechanics

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Economy.BasePriceRaw/Processed/Capital` | 1 / 3 / 8 | The founding price ladder — anchors elasticity, utilization, parity, and the ceiling. Steepen to make chain depth more lucrative. | Flatten to make tiers interchangeable. |
| `Economy.PriceDriftMaxPerYear` | 0.04 | Prices whip toward clearing (spikes bloom in one epoch). | Sluggish prices; shocks smear over centuries. |
| `Economy.PriceDriftExponent` | 0.5 | Sharper response to a given demand/supply ratio. | Gentler, more damped drift. |
| `Economy.PriceFloor` | 0.01 | Gluts stay worth something. | Deeper glut basements. |
| `Economy.MaxPriceMultiple` | 100 | Taller famine/blockade spikes — and more paper wealth minted through wages during them. | Muted crises; less legible price map. |
| `Economy.ReExportWeight` | 0.5 | Hubs bid harder for through-traffic: stronger entrepôts, more speculative freight. | Goods only move to final consumers. |
| `Economy.ParityHeadroom` | 1.15 | Connected prices float higher above import break-even (lazier discipline, fatter trade margins). | Tighter parity: connected markets nearly uniform; blockades stand out more. |
| `Economy.BlackMarketMarkup` | 2.5 | Juicier prohibition margins (smuggling, when H arms it, pays more). | Prohibition barely distorts. |

## Economy — production, income, freight, credit, lifecycle

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Economy.MinUtilization` | 0.15 | Producers keep running into gluts (bigger inventories, deeper price basements). | Idle mines everywhere prices sag; scarcer, twitchier supply. |
| `Economy.LaborShare` | 0.4 | Households capture more of every sale: richer segments, poorer polities, faster SoL. | State-heavy economy; households starve amid activity. |
| `Economy.InitialCreditsPerPolity` | 500 | Bigger monetary base (the only polity mint): everything more liquid. | Tight money: development crawls until trade velocity builds. |
| `Economy.InitialWealthPerPop` | 15 | Homeworld households start richer (first-epoch demand). | Lean start; wages must arrive fast. |
| `Economy.FreightCostPerUnitPerHex` | 0.02 | Distance matters more: regional price zones, stronger geography. | A flatter, more integrated market. |
| `Economy.FuelPerUnitPerHex` | 0.005 | Freight pulls harder on fuel markets; refinery-poor regions get cut off. | Movement approaches free (against the design's grain). |
| `Economy.ExportShare` | 0.5 | Markets drain faster to arbitrage/procurement (oscillation risk). | Sticky local stocks; slower trade response. |
| `Economy.RestrictedFriction` | 0.5 | Restricted goods effectively stop moving. | Restriction becomes a paperwork fee. |
| `Economy.ReserveReleaseTrigger` | 0.9 | Polities open granaries at the first shortfall. | Reserves hoarded for true famines only. |
| `Economy.LoanRatePerYear` | 0.02 | Debt overhangs bite; defaults (and seizures) multiply. | Nearly free credit. |
| `Economy.LoanTermYears` | 50 | Gentler amortization, longer debt tails. | Brutal repayment schedules. |
| `Economy.ConditionDecayPerYear` | 0.01 | Neglect ruins facilities fast (upkeep becomes existential). | Facilities coast through shortages. |
| `Economy.ConditionRecoveryPerYear` | 0.05 | Repairs snap back. | Long scars from every shortage/war. |
| `Economy.StockpileDecayPerYear` | 0.002 | Reserves cost more to hold (provisions ×10, organics ×5, medicine ×3 in code). | Cheap insurance; sieges (H) get longer. |
| `Economy.TechTierStub` | 2 | 3 unlocks advanced recipes everywhere (pre-G). 1 locks capital goods entirely — the economy dies. | — |
| `Economy.WarWearinessPerYear` | 0.003 | (Inert until H.) | — |

## Population — demographics, migration, ideology

`FamineLine`, `RefugeeLine`, and `MigrationRatePerYear` set how tragedy
propagates: how early people die, how early they run, and how fast.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Population.FamineLine` | 0.75 | People die (and famines chronicle) at milder shortfalls — darker frontier. | Only deep starvation kills; scarcity is just prices. |
| `Population.FamineShrinkPerYear` | 0.02 | Famines depopulate in an epoch or two (more ghost towns). | Long slow declines; refugees outrun death. |
| `Population.RefugeeLine` | 0.5 | Populations bail earlier — fluid, footloose species. | People endure to the end (more die in place). |
| `Population.RefugeeMultiplier` | 8 | Exodus empties a starving port in 1–2 epochs. | Refugees trickle. |
| `Population.MigrationRatePerYear` | 0.002 | Everyone drifts toward comfort; domains churn. | Rooted populations; diasporas rare. |
| `Population.MigrationMinGradient` | 0.05 | Only stark differences move people. | Perpetual optimization shuffle. |
| `Population.IdeologyDriftPerYear` | 0.01 | Belief tracks conditions within a generation (volatile politics for G). | Deep cultural inertia. |
| `Population.HungerIdeologyLine` | 0.7 | Hardship radicalizes (Authority/Sacral) at the first pinch. | Only catastrophe moves belief. |
| `Population.ProsperityIdeologyLine` | 0.7 | (Lower = comfort liberalizes sooner: Individual/Open.) | Prosperity must be extreme to matter. |
| `Population.SoLDriftPerYear` | 0.02 | Living standards re-rate quickly with market outcomes. | SoL is a long memory. |

## Infrastructure — port physics and construction

The radii/ranges set the map's granularity (slice B); the construction knobs
set how fast the built world thickens (slice D).

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Infrastructure.ServiceRadiusBaseHexes` / `PerTierHexes` | 4 / 4 | Fatter domains: fewer, larger states; more contested overlap. | An archipelago of city-states. |
| `Infrastructure.InterPortRangeBaseHexes` / `PerTierHexes` | 18 / 8 | Longer lanes: denser networks, harder-to-blockade webs. | Chokepoints dominate; H's fortresses will love it. |
| `Infrastructure.MaxPortTier` | 3 | (With catalog growth) taller hierarchies. | Flat port ranks. |
| `Infrastructure.HomeworldPortTier` | 2 | Homeworlds start as hubs. | Everyone starts as an outpost. |
| `Infrastructure.FacilitiesPerPortTier` | 5 | Denser industrial districts per port. | Development spreads thin or stalls — this was the mid-chain bottleneck once. |
| `Infrastructure.ConstructionDevGate` | 25 | Materials only haul toward genuinely funded projects. | Speculative alloy shipments everywhere. |
| `Infrastructure.ConstructionPullAlloys/Machinery/Composites` | 12/8/6 | Stronger frontier pull on build materials (freight fills colonial depots faster). | Colonies wait for gluts to trickle out. |
| `Infrastructure.ConstructionScoreFloor` | 0.12 | Only prime sites develop. | Junk facilities on marginal rock. |
| `Infrastructure.FoodSecurityPremium` | 1.25 | Colonies farm unless extraction is overwhelming (safe, boring). | Every belt colony mines and gambles on food imports. |

## Expansion — colonization pace and development costs

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Expansion.ColonyCost` | 15 | Fewer, better-funded expeditions (the cost lands in settler pockets). | Colony spam. |
| `Expansion.ColonizationReachHexes` | 24 | Bolder leaps into the dark (further from lane relief). | Tight incremental sprawl. |
| `Expansion.LaneCost` | 25 | Sparser networks; more isolated famine pockets. | Everything connects fast; blockades matter less each. |
| `Expansion.PortUpgradeCostBase` | 40 | Rarer nexuses; flatter hierarchy. | Tier inflation. |
| `Expansion.HomeworldSegmentSize` / `ColonySegmentSize` | 3 / 0.5 | Bigger founding populations (more labor, more mouths). | Thin seeds; slower starts. |
| `Expansion.SegmentGrowthPerYear` | 0.01 | Faster natural increase — caps bind sooner, migration pressure builds. | Population is precious; losses take centuries to heal. |
| `Expansion.SegmentCapPerTier` | 2 | Ports carry more people per tier (development = population). | Tier raises become the only growth path. |

## Controller — the genesis AI's standing-policy magnitudes

These tune the *stock AI*, not the world: smarter controllers (and the
player, P2) replace the AI and bring their own numbers.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Controller.RealmHungerGate` | 0.8 | Cautious expansion: consolidate until well-fed. | Expand while starving (pre-fix behavior — colonial graveyards). |
| `Controller.ProvisionsReservePerPort` | 3 | Deep granaries: famine relief and (H) siege endurance. | Hand-to-mouth realms. |
| `Controller.AlloysReservePerPort` | 3 | Bigger state construction banks — faster industrialization. | Construction starved by its own markets again. |
| `Controller.MachineryReservePerPort` | 1.5 | (as above, machinery) | — |
| `Controller.CompositesReservePerPort` | 1 | (as above, composites) | — |
| `Controller.ArmamentsPerPortPerMilitancy` | 2 | Militant species stockpile arsenals (H inherits armed worlds). | Demilitarized reserves. |
| `Controller.ShipPartsReservePerPort` | 3 | Deeper quartermaster stores: fleet upkeep survives bare frontier markets (readiness holds). | Merchant marines rot at 0.4 readiness wherever components don't reach. |
| `Controller.FuelReservePerPort` | 4 | Navy fuel dumps: posted fleets at refinery-less ports stay flying. | Fuel-dry frontiers ground their fleets (0-readiness attrition). |
| `Controller.MilitancyReserveGate` | 0.2 | Only genuine hawks arm. | Everyone keeps a little powder dry. |
| `Controller.NarcoticsProhibitBelowOpenness` | 0.35 | More theocratic drug bans → bigger black books. | Prohibition rare. |
| `Controller.NarcoticsRestrictBelowOpenness` | 0.55 | Wider restricted band (friction, not bans). | Narcotics broadly legal. |

## Fleet — yards, posted freight, supply, attrition, lineages

Slice E's family: the physical carriers. Freight only moves on Posted
hulls, so these dials gate the whole trade layer; watch `emap traffic`,
the shipment volume in the Markets note, and the `fleet` readiness column.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Fleet.YardHullsPerTierPerYear` | 0.2 | Bigger navies and merchant marines per yard tier (components permitting). | Hulls trickle; expansion and freight both thin. |
| `Fleet.HullComponentsBase` | 3 | Dearer hulls: yards drain components and treasuries faster, fleets stay small. | Cheap hulls; the components market barely notices a navy. |
| `Fleet.HullArmamentsBase` | 1.5 | Warships bid up armaments — arsenals pay (H inherits armed yards). | Guns nearly free at the slip. |
| `Fleet.FreightTripsPerYearBase` | 0.3 | More capacity per posted hull (fewer hulls needed per lane). **The freight-throughput master dial.** | Lanes need big fleets to matter. |
| `Fleet.EnduranceHexesPerPoint` | 3 | Longer off-lane legs: convoys reach past the colonization radius easily. | Below ~2.7, Medium pioneers can't cover the default 24-hex reach — expansion stalls hard. |
| `Fleet.FuelPerHullPerHexMoved` | 0.02 | Expeditions burn real fuel; staging ports feel convoys. | Movement approaches free. |
| `Fleet.UpkeepUnitsPerPointPerYear` | 0.025 | Fleets eat harder into fuel/armaments/components — treasuries drain, navies compete with merchants for fuel. | Upkeep cosmetic; military treasuries pile up. |
| `Fleet.UpkeepFuelShare` | 0.4 | Supply tilts toward fuel (refinery-driven readiness). | Tilts toward armaments/components (industry-driven readiness). |
| `Fleet.ReserveUpkeepFactor` | 0.25 | Mothballs cost real money. | Docked fleets nearly free (reserves become the default posture). |
| `Fleet.ReadinessRecoveryPerYear` / `ReadinessDecayPerYear` | 0.05 / 0.02 | Faster snap toward the met fraction (both directions). | Sluggish supply response; shortages take generations to bite. |
| `Fleet.AttritionReadinessFloor` | 0.3 | Fleets start dying at higher readiness — supply failures brutal. | Only total starvation kills hulls. |
| `Fleet.AttritionHullLossPerYear` | 0.02 | Starved fleets evaporate within an epoch or two. | Slow rot; wrecks accumulate gently. |
| `Fleet.MarkGradeStep` | 0.15 | Rare marks: lineages span eras. | Chatty lineages, a mark every grade wobble. |
| `Fleet.MilitaryPullComponents` | 10 | Stronger yard-port components signal: shipyards site earlier, components chains spin up. | Yards may never pencil out (the pre-E stall). |
| `Fleet.StarterFreightHulls` | 4 | Thicker epoch-one trade. | Early lanes empty until yards ramp. |
| `Fleet.StarterColonyHulls` | 1 | Multiple foundings before the first yard hull. | 0 stalls all expansion until a yard builds a pioneer. |
| `Fleet.StarterEscortPerMilitancy` | 4 | Militant species enter with real screens. | Unescorted dawn. |

---

## Structural constants (code, not knobs — deliberately)

These define mechanics rather than calibrate them; promoting them would
invite breaking the model rather than tuning it. Locations given for the
day one of them *does* need to move.

- **Catalog data** — goods, recipes, grade bases, build costs, upkeep draws,
  base outputs, labor requirements: `src/Core/Substrate/Goods.cs`,
  `Infrastructure.cs`. This is the game's content vocabulary ("data as
  code"), versioned with the design docs. *Slice E halved the catalog
  machinery-upkeep coefficients (D's parked question): the old rates left
  no machinery for Ship Components production, which gated hulls, which
  gated expansion once founding needed convoys. Machinery remains the
  dominant sink at half rate; fleets deliberately draw components, not
  machinery.*
- **Chassis catalog** — the role × size grid, per-role stat baselines,
  size/cost/tech scaling, grade sensitivities: `src/Core/Epoch/ShipCatalog.cs`.
  The two-layer stat model's data half; `FleetKnobs` carries the dials.
- **Grade system shape** — tech ceilings, band edges, use-case
  sensitivities, tier factors: `src/Core/Substrate/Grades.cs`,
  `Production.cs`. The neutral machinery grade 0.5 (`MarketEngine`) is the
  grade system's defined midpoint.
- **Terrain potentials** — the richness formulas: `src/Core/Substrate/Potentials.cs`.
- **Perishability multiples** — provisions ×10, organics ×5, medicine ×3
  over `StockpileDecayPerYear`: `AllocationPhase.DecayReserves`.
- **Budget weights & policy defaults** — `PolityPolicies.Default`
  (`Policies.cs`): the six-way budget split, default tax rate 0.10. These
  are *standing policies* — Intent-phase outputs, the controller's to
  change — not world calibration.
- **Siting score weights** — `src/Core/Substrate/Siting.cs`.
- **Colony founding wealth = `Expansion.ColonyCost`** (recycled, not a
  separate dial) and the **homeworld starter industry** composition
  (`InteriorPhase.StarterIndustry`): agri t2 + mine/skimmer/refinery/foundry t1.
- **Ideology drift shape** — the prosperity comfort ×3 factor and famine
  severity scaling: `InteriorPhase.DriftIdeology`.
- **Numerical guards** — ε = 1e-9 in the drift ratio, the 0.05 condition
  floor, hex-tier constants (`StableHash`, `ValueNoise` lattices).

## Galaxy-side knobs (the natural raster)

`GalaxyConfig` (`src/Core/Galaxy/`) calibrates nature, not history: arm
count/tightness/strength, core radius, disc falloff, density target, anchor
multipliers, traversability threshold. Serialized on the artifact's GCONFIG
line. They stay outside the epoch registry because they configure a
different machine (the skeleton builder). Slice F reads the shape knobs as
**potential parameters** (`GalaxyPotential`): where matter wants to be; the
cosmic sim decides where it ends up. `HomeworldRatePerCell` retired with
slice F — polity count is causal now (`Evolution.SapienceRate` plus the era
horizons are the dials).

Slice F adds genesis calibration under its own registry,
`GalaxyKnobRegistry` (`src/Core/Galaxy/GalaxyKnobRegistry.cs`), serialized
as name-sorted `GKNOB` lines in the config layer (v4) with the same
unknown-name load refusal. Same three-surface discipline as the epoch
registry (artifact, REPL `knobs`, this file).

## Cosmic — the deep-time structure sim

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Cosmic.MergerCount` | 2.0 | More infalling dwarfs: more streams, starburst cohorts, structural variety per seed. | A quiet, textbook spiral. |
| `Cosmic.MergerScale` | 1.0 | Heavier injections: mergers visibly reshape density/metallicity maps. | Mergers become flavor events. |
| `Cosmic.StarFormationEfficiency` | 1.0 | Gas burns early: older, dimmer present-day mix, metals arrive sooner, less remaining gas fraction. | Late-blooming galaxy: gas-rich arms, young leans dominate. |
| `Cosmic.EnrichmentRate` | 1.0 | Faster metallicity floor crossings: life viable earlier and wider (0b reads this directly). | Metal-poor galaxy: emergence compresses toward the late window. |
| `Cosmic.GlobularCount` | 6.0 | More ancient metal-poor cluster cells (exotic terrain, own star table). | Rarer exotic terrain. |
| `Cosmic.AgnActivity` | 1.0 | More/wider core sterilization epochs: the inner disc's life starts late, ancient-core powers vanish. | A quiet nucleus; core-adjacent early risers appear. |

## Evolution — life, sapience, the emergence schedule

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Evolution.AbiogenesisRate` | 0.012 | Life starts promptly where viable: more living cells, earlier abiogenesis dates, more origins overall. | Life is precious and late; whole arms stay barren. |
| `Evolution.MaturationScaleGyr` | 6.0 | Longer road to spaceflight: more origins land past the horizon (fewer polities), precursors thin out. **Must exceed the abio→sapience lag (~4 Gyr) or the clamp erases causal dates.** | Everything reaches flight early: crowded precursor era. |
| `Evolution.CatastropheFrequency` | 0.0015 | More setbacks: later, scarred emergences, richer catastrophe texture, fewer sapients. | Smooth gardens everywhere. |
| `Evolution.SpreadRate` | 0.002 | Panspermia clusters life along habitable corridors. | Isolated abiogenesis islands. |
| `Evolution.SapienceRate` | 0.05 | More origins in every era. **Not a clean polity-count dial**: current-era count also hangs on the era horizons and moves non-monotonically (changing the rate changes *when* cells register, which changes which band they land in). Expect 5–16 polities at radius 12 as seed personality. | Sparse minds; some seeds drop below 2 current polities. |
| `Evolution.DomainBudgetFraction` | 0.5 | More galaxy claimable by precursors: bigger arcs, denser ruins, more scars shadowing the emergence map. | Precursors stay parochial; archaeology thins. |
| `Evolution.GrandChance` / `GrandWaveLimit` | 0.15 / 3 | More elder races: galaxy-scale ruin networks, more inter-wave contact. | Grand arcs vanish; pocket rubble only. |
| `Evolution.BioEngineeringRate` | 0.03 | More engineered gardens: anomalously rich biospheres, uplift-flavored early emergences near old territory. | Biology stays wild. |
| `Evolution.DormantSurvivalRate` | 0.08 | More live remnants (war machines, defense grids) among the ruins — encounter density for later slices. | Dead archaeology only. |
