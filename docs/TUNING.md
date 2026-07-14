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

**Measuring a dial's macro effect** (slice SH): `docs/SIMHEALTH.md` — the
metric vocabulary, the `ehealth` readout, and the sweep runner
(`dotnet run --project src/Inspector -- sweep experiment.json`) that runs
baseline-vs-variant knob sets across a seed ensemble. A tuning conclusion
should clear seed personality before it lands in this file.

---

## Sim & Genesis (structural clock — ESIM line, not KNOB records)

| Knob | Default | Meaning |
|---|---|---|
| `Sim.YearsPerEpoch` | 25 | World-years integrated per step — the integration step, nothing more (P7). Fine-tick play lowers this over a loaded artifact; rates are per-year by design so histories stay comparable, but drift caps and logistic steps compound differently. |
| `Sim.EpochCount` | 40 | History depth (~1,000y). Purely how long the story runs. |
| `Sim.GenerationYears` | 25 | The calendar length of one generation — the unit every `*Epochs` knob counts (siege floors, charter persistence, federation clocks…). Fixed at genesis scale; fine-tick stepping never changes it. If you re-time genesis with `YearsPerEpoch`, move this with it. |
| `Genesis.EmergenceWindowYears` | 500 | Latest entry year for staggered polity emergence. Wider = older empires meet younger neighbors; narrower = a crowded simultaneous dawn. |

## Genesis — the native late-emergence schedule (slice H)

Pre-spaceflight natives carry dates projected onto the native window;
where a domain covers the homeworld, the host's native policy resolves
the firing (the AI's policy-by-temperament map is structural:
openness ≥ 0.70 uplift, ≥ 0.55 integrate, militancy ≥ 0.55 exploit,
else protectorate; uplift's Life-tier-2 gate is structural too).

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Genesis.NativeWindowYears` | 900 | Emergence crises land late (claimed galaxies, more suppressions). | Natives emerge among the founding powers. |
| `Genesis.NativePopulationSize` | 1.0 | Native minorities matter demographically (real accommodation strain). | Token peoples. |
| `Genesis.ProtectorateDelayEpochs` | 4 | Reserves genuinely buy time (and turn cage later). | Protectorate is a label. |
| `Genesis.UpliftAccelerationEpochs` | 4 | Uplift hosts mint client states a century early. | Uplift is patience. |

## Corporate — niches, charters, operations, deaths (slice G)

The niche watcher raises merchant factions where profit persists
unclaimed; the charter graduation incorporates them (cartels and pirate
bands skip the charter — chartered nowhere). Suffix names per niche are
catalog data (`CorporationOps.Suffix`).

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Corporate.CharterPersistenceEpochs` | 3 | Only durable opportunities incorporate. | Every price blip births a company. |
| `Corporate.CharterOpennessGate` | 0.4 | Closed societies never charter (their merchant factions stew). | Everyone charters everything. |
| `Corporate.CharterCapitalFloor` | 200 | Only well-funded ventures incorporate (fewer, real corps — the stillbirth cure). | Penniless charters die in their build-out. |
| `Corporate.FoundingGraceEpochs` | 4 | Long build-out runway before the lean clock starts. | Foundings must earn immediately or die. |
| `Corporate.FreightPullComponents` | 6 | Freight lines pull hull components toward their frontier homes. | Lines depend on yard-port sourcing alone. |
| `Corporate.FreightNicheMargin` | 0.6 | Only stark gradients read as freight niches. | Freight lines on every lane. |
| `Corporate.DepositNichePotential` | 0.65 | Only prime rock draws conglomerates. | Corp mines on marginal terrain. |
| `Corporate.FabricationPriceRatio` | 2.5 | Industrial gaps must gape before combines form. | Combines chase every markup. |
| `Corporate.CartelValueFloor` | 15 | Cartels need deep black books. | Prohibition instantly breeds cartels. |
| `Corporate.CartelSkim` | 0.3 | Contraband margins bleed buyer wealth fast. | Cartels are ornamental. |
| `Corporate.RaidCapacityFloor` | 8 | Pirate bands need rich unguarded lanes. | The black flag over every backwater. |
| `Corporate.MaxFacilities` | 4 | Bigger conglomerate chains. | Single-asset shops. |
| `Corporate.LobbyShare` | 0.01 | Corporate money floods host politics (corporate factions bulk up). | Influence rides dividends only. |
| `Corporate.MagnateReceipts` | 50 | Only booms mint magnates. | Every shopkeeper is famous. |
| `Corporate.LeanReceiptsFloor` / `NicheDeathEpochs` | 1 / 5 | Corps die the moment margins thin (heavy churn, rich residue). | Zombie companies linger for eras. |
| `Corporate.NationalizeWealthFactor` | 2.0 | States tolerate de facto powers longer. | Seizure at the first good quarter. (Note: deficit-financed states — negative treasuries — never trigger it; the megacorp outlives the indebted host.) |
| `Corporate.NationalizeLegitimacyHit` | 0.1 | Seizure wrecks standing (I's news will deepen this). | Consequence-free expropriation. |

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
| `Economy.ReExportWeight` | 0.5 | Hubs bid harder for through-traffic (relay bids) and traders speculate bigger cargos: stronger entrepôts. | Goods only move to final consumers. |
| `Economy.BlackMarketMarkup` | 2.5 | Juicier prohibition margins (smuggling, when H arms it, pays more). | Prohibition barely distorts. |
| `Economy.AskMarkupOnPost` | 1.0 | Fresh supply quotes above reference: sticky shelf prices, slower clears. | Below 1: producers undercut the reference — gluts clear faster, margins thinner. |
| `Economy.OrderExpiryYears` | 100 | Stale books linger longer before buys refund / sells escheat to the port. | Faster cleanup — but keep it WELL above the coarse step span or tick honesty (P7) breaks. |
| `Economy.SubsistenceBidPremium` | 1.2 | Hunger outbids everything harder (famine only under true scarcity). | Subsistence competes at par; industry can outbid dinner. |
| `Economy.SoLBidRatio` | 1.0 | Comfort spending crosses fresh asks at reference. | SoL waits for gluts; slower growth/legitimacy feedback. |
| `Economy.LuxuryBidRatio` | 0.9 | Luxury bids closer to par — elites fed sooner. | Luxuries only clear into deep gluts. |
| `Economy.ProjectBidPremium` | 1.1 | Construction outbids consumption harder for materials. | Sites queue behind households; slower build-out. |

*`Economy.ParityHeadroom` is inert since slice CE: import parity died with
the shelf — the order book prices imports through delivered cost.*

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
| `Economy.PoolIdleDecayPerYear` | 0.05 | Unspent Expansion/Development/Military points recycle back to Credits faster (less stranded accrual ahead of the Planner). | Idle points sit longer before recirculating; the Planner's under-packing bites harder. |
| `Economy.SovereignIssuanceRate` | 0.5 | Deeper shortfalls get minted away (negative treasuries recover faster) — more fiat chasing the same goods. | Tighter bounded mint; `Polity.NegativeTreasuries` breathes less, stays negative longer. |
| `Economy.ConditionDecayPerYear` | 0.01 | Neglect ruins facilities fast (upkeep becomes existential). | Facilities coast through shortages. |
| `Economy.ConditionRecoveryPerYear` | 0.05 | Repairs snap back. | Long scars from every shortage/war. |
| `Economy.StockpileDecayPerYear` | 0.002 | Reserves cost more to hold (provisions ×10, organics ×5, medicine ×3 in code). | Cheap insurance; sieges (H) get longer. |
| `Economy.DepotDecayFactor` | 0.5 | (Toward 1) depots barely slow the rot — dedicated storage stops paying. | Depots near-freeze decay; one depot makes an eternal larder. |
| `Economy.StockCapPerPortTier` | 100 | (Raise) ports bank deep larders without depots. | Tiny caps: reserve policy impossible without depots everywhere. |
| `Economy.StockCapPerDepotTier` | 400 | (Raise) one depot holds a war economy's stores. | Depot storage stops mattering next to the port's own floor. |
| `Economy.WarWearinessPerYear` | 0.003 | (Inert until H.) | — |
| `Economy.WealthTaxFloorPerPop` | 20.0 | Wider per-capita exemption shields more segment wealth (subsistence and mid-tier households untouched). | More wealth is taxable — even modest households get levied. |
| `Economy.WealthTaxRatePerYear` | 0.02 | Faster drain on wealth above the floor: a stronger inflation-control valve, but poorer elites/segments. | Wealth above the floor sits longer; a weaker sink against sovereign issuance. |
| `Economy.CourierFeePerUnitPerHex` | 0.02 | State hauling costs real freight rates: fees drain treasuries, self-fulfillment pays back more. | Near-free requisitions (against the contract economy's grain). |
| `Economy.ProjectAbandonYears` | 30 | Starved works squat on yard slots for generations before the abandon clock cancels them. | Hopeless work cancels fast — ruins appear sooner, slots free up. |

*`Economy.TechTierStub` retired (slice G): producer tech is per-polity,
per-domain — see the Tech family.*

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

## Interior — legitimacy, cohesion, enforcement (slice G)

The polity's inside: how fast the official line chases the people, what
makes a government legitimate, and what strains a realm apart. Form
multipliers (catalog) scale the legitimacy weights per government form.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Interior.OfficialDriftPerYear` | 0.008 | Governments track their people (smaller ideology gaps, fewer ideological factions). | Frozen official lines; gap-driven grievance builds. |
| `Interior.SoLTrendGain` | 3.0 | Downturns crater legitimacy immediately (volatile politics). | Only SoL *levels* matter; slow declines pass unpunished. |
| `Interior.LegitimacyProsperityWeight` | 0.30 | Bread buys loyalty everywhere. | Prosperity politically irrelevant. |
| `Interior.LegitimacyIdeologyWeight` | 0.25 | Alignment with the popular line dominates (assemblies/theocracies swing harder). | Doctrine gaps forgiven. |
| `Interior.LegitimacyRulerWeight` | 0.20 | Rulers carry the state (autocracies live and die by prestige — G2). | Impersonal states. |
| `Interior.LegitimacyWarWeight` | 0.10 | (Neutral 0.5 until H wires war outcomes.) | — |
| `Interior.LegitimacyAccommodationWeight` | 0.15 | Minority cultures strain legitimacy hard (conquest empires wobble — H). | Multicultural realms stable by default. |
| `Interior.StrainPerPort` | 0.008 | Big realms fray: cohesion caps empire size. | Size costs nothing. |
| `Interior.StrainPerCulture` | 0.05 | Every absorbed culture is a future schism seed. | Homogeneous and mosaic empires equally solid. |
| `Interior.StrainDistanceWeight` | 0.15 | Far-flung domains slip the capital's grip (frontier factions). | Distance is administrative trivia. |
| `Interior.EnforcementBase` | 0.4 | States keep order without navies (graduations rarer). | Order rests entirely on hulls. |
| `Interior.EnforcementPerWarshipPerPort` | 0.04 | Navies double as interior police (militant realms suppress factions). | Fleets are outward-facing only. |

## Character — mortality, succession, renown (slice G)

Species lifespans (human-analog 80y, aquatic 90, cryophilic 120, lithic
400, hive/machine effectively ageless) are structural catalog data —
`CharacterOps.Lifespan`. These dials shape how lives end and what deaths
cost.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Character.MortalityShapePerYear` | 0.15 | Shorter reigns, faster court turnover, more successions per era. | Rulers linger far past their span. |
| `Character.AssassinationBasePerYear` | 0.02 | Illegitimate thrones are death sentences (ambition × unpopularity). | Palace intrigue toothless. |
| `Character.MachineDeprecationPerYear` | 0.002 | Machine consensus nodes cycle visibly. | Effectively immortal machine minds. |
| `Character.CrisisLegitimacyHit` | 0.15 | Heirless successions destabilize hard (G5 graduations feed on it). | Crises are paperwork. |
| `Character.DynastyPrestigePerReignYear` | 0.01 | Old houses accumulate towering legitimacy. | Dynasties are just names. |
| `Character.PrestigePerRenown` | 0.02 | Famous rulers carry states single-handed. | Legitimacy ignores the person. |
| `Character.RenownAscension` / `RenownNotable` | 2 / 5 | Seats and feats mint reputation faster. | Renown must be earned across eras. |
| `Character.RulerMintAgeFraction` / `HeirMintAgeFraction` | 0.45 / 0.25 | Older courts: shorter reigns, more successions. | Child-kings reign for generations. |
| `Character.MaxNotablesPerPolity` | 6 | More chronicle color per realm. | Only the singular get remembered. |

## Faction — formation, pressure, appeasement, grievance (slice G)

Six bases coalesce from real state; strength presses budgets and the
official line; the appeasement budget line buys peace priced off the same
base it draws on; the unmet fraction compounds into the grievance that
graduation (task 5) spends. Per-basis budget agendas are catalog data
(`FactionOps.BasisBudget`).

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Faction.FormMinShare` | 0.15 | Only mass movements coalesce (fewer, bigger factions). | Every grumble organizes. |
| `Faction.IdeologyGapToForm` | 0.25 | Dissent must be radical to organize. | Politics fragments on nuance. |
| `Faction.FrontierDistanceFraction` | 0.5 | Only the deep frontier feels neglected. | Every suburb is a separatist. |
| `Faction.SacralAxisLine` | 0.35 | Only the devout base faith movements. | Broad revivalist politics. |
| `Faction.MilitaryRenownToForm` | 12 | Officer factions need storied brass (they'll mostly wait for H's wars). | Peacetime juntas everywhere. |
| `Faction.PressureRatePerYear` | 0.01 | Budgets and doctrine bend fast to strong factions. | Standing policy shrugs off politics. |
| `Faction.MaxBudgetPressure` | 0.35 | One faction can redirect a third of spending per epoch. | Pressure is cosmetic. |
| `Faction.AppeasementDemandShare` | 0.2 | Peace is expensive: appeasement lines fall short, grievance compounds (more graduations). **The interior-drama dial.** | Cheap peace; factions stay bought. |
| `Faction.GrievancePerYear` | 0.02 | Neglect radicalizes within a generation. | Long-suffering interests. |
| `Faction.GrievanceDecayPerYear` | 0.008 | Paying late still forgives. | Grievance is forever (every polity eventually cracks). |
| `Faction.DissolveStrengthFloor` | 0.05 | Factions need standing membership to persist. | Zombie movements linger. |
| `Faction.PatronRenownWeight` / `WealthStrengthWeight` | 0.01 / 0.1 | Famous leaders and fat war chests carry factions past their base. | Strength is purely demographic. |
| `Faction.GraduationGripFactor` | 4.0 | States hold together; factions grumble for centuries. **The graduation-pacing dial** (seed 42 r12: 9 graduations / 40 epochs at default). | Every polity cracks within a generation. |
| `Faction.CoupIdeologyLurch` | 0.5 | Coups remake the official line overnight (forms reseat often). | Palace coups change faces, not policy. |
| `Faction.CoupLegitimacyHit` / `RevoltLegitimacyHit` | 0.15 / 0.1 | Political violence delegitimizes hard (cascading instability). | Consequence-free putsches. |
| `Faction.RevoltGrievanceKeep` | 0.75 | Repression compounds: crushed movements return angrier. | Crushing a revolt actually settles it. |

## Relations — contact, warmth/tension sources (slice H)

The pressure gauge war reads and the ladder peace climbs: polities meet
when reach overlaps, then warmth and tension drift toward targets
recomputed from live sources each epoch. Tension decays only when its
sources resolve (the target holds while they stand). The five-stance
bucket thresholds Intent maps net warmth−tension to are structural
controller behavior (`GenesisController.StanceOf`).

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Relations.ContactReachHexes` | 24 | Empires "meet" across wilds they can't reach. | Neighbors stay strangers until borders touch. |
| `Relations.WarmthDriftPerYear` | 0.02 | Friendships (and estrangements) form within a generation. | Centuries-old stances outlive their causes. |
| `Relations.TensionRisePerYear` | 0.05 | Borders load within an epoch of friction appearing. | Slow-burn buildups; sparks find less powder. |
| `Relations.TensionRelaxPerYear` | 0.012 | Resolved grudges cool fast (short memories). | Tension outlives its sources for centuries. **Keep below the rise rate.** |
| `Relations.StrangenessWeight` | 0.35 | Alien embodiment poisons first contact (xenophobic galaxy). | Everyone meets as potential friends. |
| `Relations.TradeWarmthWeight` / `TradeSaturation` | 0.30 / 10 | Trade is peace: posted cross-border freight buys real warmth. | Commerce is diplomatically inert. |
| `Relations.TreatyWarmthWeight` | 0.25 | Honored rungs compound into trust (federation gate nears). | Treaties are paper. |
| `Relations.DynasticTieWarmth` | 0.10 | Marriages buy real peace this generation (H4). | Dynastic instruments are ceremony. |
| `Relations.DynasticTieLapseYears` | 75 | Marriage peace holds three reigns before the claim surfaces. | Every wedding is a war of succession in waiting (seed 42: 19 instruments → 5 succession claims). |
| `Relations.IdeologyGapCooling` | 0.20 | Doctrinal opposites can't stay friends. | Ideology no bar to friendship. |
| `Relations.OverlapTensionWeight` / `OverlapSaturation` | 0.35 / 4 | Contested service areas are the war engine (organic borders). | Interleaved empires coexist calmly. |
| `Relations.ClaimTensionWeight` | 0.18 | Each standing claim keeps a border hot (grudges drive history). | Claims are legal fictions. |
| `Relations.IdeologyTensionWeight` | 0.30 | Zealot thrones read doctrine gaps as war material. | Crusades need more than doctrine. |
| `Relations.MilitancyTensionWeight` | 0.20 | Hawkish compositions keep every border loaded. | Only concrete friction counts. |
| `Relations.AgitationTensionWeight` | 0.15 | Military factions drag their polity toward war. | The sword waits quietly. |
| `Relations.InterdictionTensionWeight` | 0.40 | Blockades near-guarantee escalation. | Sieges read as negotiation. |
| `Relations.KinClaimSegmentFloor` | 0.5 | Only large stranded kin populations raise claims. | Every diaspora is an irredenta. |
| `Relations.TreatyGateBase` / `TreatyGateStep` | 0.40 / 0.12 | Rungs demand deep warmth (alliances rare and meaningful). | Everyone allies with everyone (seed 42: pairs reach alliance in ~3 epochs once warm). |
| `Relations.BreakWarmthPenalty` | 0.25 | Broken treaties end friendships for good. | Rungs churn — break and re-sign. |
| `Relations.NonAggressionDamping` | 0.30 | The second rung genuinely calms borders (fewer sparks reach powder). | Non-aggression is paper. |
| `Relations.FederationAllianceEpochs` | 3 | Fusions need a generation of proven alliance. | Whirlwind federations. |
| `Relations.FederationIdeologyGapMax` / `CohesionFloor` / `OpennessFloor` | 0.20 / 0.55 / 0.40 | Only aligned, healthy, open pairs merge (openness is the PAIR MEAN — one open partner carries a warier one; seed 42: three federations chaining out of the crowded core). | Everything fuses; the galaxy consolidates to a blob. |
| `Relations.FederationOverlapDiscount` | 0.25 | Entangled friendly borders fuse readily (the interleaved core federates or fights, never simmers). | Entanglement is diplomatically inert. |
| `Relations.EncroachmentTensionBump` | 0.10 | Every colony in a neighbor's sphere is an incident. | Settling someone's sphere is free. |
| `Relations.VassalStrengthRatio` | 0.35 | Only the genuinely outmatched kneel (chosen vassalage rare). | Peers vassalize on a bad epoch. |
| `Relations.VassalTributeShare` | 0.15 | Protection is expensive; vassal economies drag. | Vassalage is symbolic. |
| `Relations.VassalAbsorptionEpochs` / `AbsorptionWarmth` | 8 / 0.60 | Annexation takes two centuries of warm bond. | Vassals dissolve into overlords within a lifetime. |
| `Relations.VassalSecessionCohesion` | 0.40 | Only crumbling overlords lose vassals. | Every wobble frees the periphery. |
| `Relations.PactTariffFactor` | 0.40 | Pacts keep most of the tariff wall (mild teeth). | Trade pacts erase tariffs outright — commerce floods pact borders. |

## News — the news graph (slice I)

Word travels the lane network at traffic-derived speed and crawls the
wilds otherwise (perception-and-news.md). The delay field these speeds
produce is what stales every belief: decisions run on perception,
consequences on truth.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `News.BaseLaneSpeedHexPerYear` | 4.0 | Even dead lanes gossip — the rim barely lags. | Unposted lanes go dark; only trade carries word. |
| `News.TrafficSpeedBonus` | 12.0 | Busy corridors are near-instant (the core thinks as one). | Traffic doesn't matter; all lanes crawl alike. |
| `News.TrafficSaturationTripsPerYear` | 4.0 | Only huge convoys speed the mail (most lanes stay slow). | A single posted hauler maxes the lane's carriage. |
| `News.OffLaneSpeedHexPerYear` | 0.5 | Wilds leak news; isolation stops working. | Off-network polities live years behind the times. |
| `News.PulseMagnitudeFloor` | 0.5 | Only landmark events travel; the log stays quiet abroad. | Every public hiccup pulses galaxy-wide. |
| `News.PulseMaxYears` | 150 | Ancient rumors still land on the far rim. | Word that misses its window is lost to distance. |
| `News.StanceDecayPerYear` | 0.005 | Reputations wash out within a generation. | The galaxy never forgets a broken treaty. |
| `Relations.ReputationWarmthWeight` | 0.20 | What the galaxy has heard dominates every table (a shock reprices borders). | Reputation is gossip — only concrete sources move warmth. |

The per-event stance deltas and their temperament tilts (open traders
sanction treaty-breakers, militants respect bold conquest, dogmatic
distance amplifies condemnation) are structural constants in
`ReputationOps.Judge`, like the stance buckets.

## Plague — contagion on the lanes (slice I)

Outbreaks roll where people crowd; spread rides posted traffic exactly as
news does; quarantines and blockades stop contagion as surely as freight;
Life tech blunts the toll; machine minds never sicken (structural). Deaths
shrink segments and never touch a credit — the dead leave inheritances.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Plague.OutbreakChancePerYear` | 0.0004 | Every crowded century has its pestilence. | Plagues are once-a-history events. |
| `Plague.SpreadChancePerYear` | 0.06 | The trade web is a death web — quarantine or perish. | Plagues stay local embarrassments. |
| `Plague.SpreadTrafficSaturation` | 2.0 | Only the busiest corridors carry contagion at full odds. | A single posted hauler is a vector. |
| `Plague.MortalityPerYear` | 0.008 | Black-death demographics (~18%/epoch unmitigated). | Plagues inconvenience rather than kill. |
| `Plague.MortalityLifeTierDiscount` | 0.2 | Medicine ends plagues as a threat by tier 3–4. | Tech is no shield. |
| `Plague.BurnoutYears` | 30 | Infections smolder for generations. | One epoch and done. |
| `Plague.ImmunityYears` | 75 | Survivors are safe for living memory. | The same strain returns within a reign. |
| `Plague.QuarantineYears` | 30 | One act seals a lane for a generation. | Quarantines lapse before the plague does. |

## Poi — the incremental POI compiler (slice I)

Residue becomes anchored places every Chronicle: battlefields from
wreckage, ruins from dead cities, fallen capitals, memorials, and
precursor sites charted as expansion reaches them. One live anchor per
hex, arbitrated by magnitude (chronicle-and-poi.md).

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Poi.BattlefieldHullFloor` | 4 | Only great slaughters anchor a field; skirmish wrecks stay noise. | Every lost patrol pins a hex. |
| `Poi.MemorialShortfallFloor` | 0.75 | Only true horrors are remembered in stone. | Every lean winter gets a shrine. |
| `Poi.PermanentMagnitude` | 20 | Most salvaged-out fields fade; archaeology is rare. | Every old battlefield litters the map forever. |
| `Poi.RuinsDeadEpochs` | 2 | Cities must lie long dead before ruins anchor (migration blips forgiven). | Any evacuation reads as a fall. |
| `Poi.SurveyReachHexes` | 10 | Precursor sites chart from far off — the deep past surfaces early. | Sites stay unknown until someone builds next door. |
| `Poi.SalvageNicheHullFloor` | 6 | Only rich fields draw salvors. | Every skirmish spawns an expedition. |
| `Poi.SalvageReachHexes` | 12 | Salvors work fields deep in the wilds. | Only battlefields at the doorstep get stripped. |
| `Poi.SalvageHullsPerYear` | 0.2 | Fields strip within an epoch or two (salvage booms are short). | Wrecks outlast the wars that made them. |
| `Poi.SalvageAlloysPerHull` / `ComponentsPerHull` | 3 / 1 | Salvage floods the alloy market (frontier yards run on the dead). | Stripping barely pays. |
| `Poi.DigExoticsPerYear` | 0.15 | Precursor digs rival exotics mines. | Digs are archaeology, not industry. |
| `Poi.DigMagnitudeDecayPerYear` | 0.02 | Sites dig out within centuries. | The deep past is effectively bottomless. |
| `Poi.DigResearchPerYear` | 0.01 | Digging precursors is a tech strategy. | Ruins yield goods, not insight. |
| `Poi.LawlessnessReachHexes` | 3 | Ruins shadow whole regions with piracy. | Only the lane at the ruin's gate reads lawless. |
| `Poi.LawlessRaidFactor` | 0.4 | Havens barely tempt (near-normal cargo needed). | Any trickle of freight past a ruin draws a band. |
| `Poi.MemorialStanceAnchor` | 0.25 | Atrocities are held forever at real depth — permanent pariahs. | Memorials are ornament; every horror fades to indifference. |

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
| `Infrastructure.ConstructionScoreFloor` | 0.12 | Only prime sites develop. | Junk facilities on marginal rock. |
| `Infrastructure.FoodSecurityPremium` | 1.25 | Colonies farm unless extraction is overwhelming (safe, boring). | Every belt colony mines and gambles on food imports. |

## Expansion — colonization pace and development costs

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Expansion.EncroachmentPenalty` | 1.5 | Contiguous borders: only truly rich contested sites get settled; boxed-in realms consolidate (**the map-soup dial**, slice H eyeball). | Land-rush interleaving returns. |
| `Expansion.ColonyCost` | 15 | Fewer, better-funded expeditions (the cost lands in settler pockets). | Colony spam. |
| `Expansion.ColonizationReachHexes` | 24 | Bolder leaps into the dark (further from lane relief). | Tight incremental sprawl. |
| `Expansion.LaneCost` | 25 | Sparser networks; more isolated famine pockets. | Everything connects fast; blockades matter less each. |
| `Expansion.PortUpgradeCostBase` | 40 | Rarer nexuses; flatter hierarchy. | Tier inflation. |
| `Expansion.PortUpgradeYears` | 5 | Tier raises are multi-generation works — a raise begun late in a reign finishes under an heir. | Ports leap tiers within a step (near-instant, pre-Task-7 feel). |
| `Expansion.PortUpgradeAlloysPerYearPerTier` | 2 | Raises pull harder on the alloy market each year (higher tiers cost more per year). | Cheaper raises; the market barely feels them. |
| `Expansion.PortUpgradeMachineryPerYearPerTier` | 1 | (as above, machinery) | — |
| `Expansion.PortUpgradeExoticsPerYearPerTier` | 0.25 | Raises want refined exotics — exotics-poor realms stall their nexuses. | Raises ignore the exotics chain. |
| `Expansion.HomeworldSegmentSize` / `ColonySegmentSize` | 3 / 0.5 | Bigger founding populations (more labor, more mouths). | Thin seeds; slower starts. |
| `Expansion.SegmentGrowthPerYear` | 0.01 | Faster natural increase — caps bind sooner, migration pressure builds. | Population is precious; losses take centuries to heal. |
| `Expansion.SegmentCapPerTier` | 2 | Ports carry more people per tier (development = population). | Tier raises become the only growth path. |

## Controller — the genesis AI's standing-policy magnitudes

These tune the *stock AI*, not the world: smarter controllers (and the
player, P2) replace the AI and bring their own numbers.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Controller.RealmHungerGate` | 0.7 | Cautious expansion: consolidate until well-fed. | Expand while starving (colonial graveyards). Dropped 0.8→0.7 in slice CE: the book economy's honest famine prints gated too hard. |
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

### Controller additions (slice H)

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Controller.BaseTariffRate` | 0.15 | Insular societies wall off trade (pact cuts matter more). | Free trade everywhere; trade-pact teeth bite nothing. |

### Controller additions (slice t1 — the planner)

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Controller.MaxPlanEntries` | 16 | Longer standing schedules — a realm queues more concurrent works (packing still caps spend by income). | Short horizons; only the top-scoring few projects get scheduled. |
| `Controller.PortRaisePlanScore` | 0.5 | Port raises out-compete new facilities for the income rate — realms deepen hubs over spreading industry. | Facilities crowd port raises out of the plan; ports stay shallow. |

### Controller additions (slice CE — the contract economy)

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Controller.PlanSavingsDrawdownYears` | 5 | Slower treasury drawdown backs the plan — leaner packing, deeper reserves. | Realms spend their savings into works faster (boom-bust plans). |
| `Controller.ColonyNeedBoost` | 6.0 | A realm sitting on expansion points with no colony hull screams for one — yards drop everything. | Colony hulls compete at par and expansion stalls behind freight (the pre-fix famine). |

## Tech — ladder costs, research, diffusion (slice G)

Four per-polity domains (Industrial / Military / Astrogation / Life) on
geometric tier ladders; research consumes Refined Exotics × Compute in
Allocation; trade and salvage keep laggards in the race. Grade ceilings
per tier are structural (`Grades.TechCeiling`).

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Tech.BaseThreshold` | 25 | Slow, era-scale advancement. | Tier churn every few epochs. |
| `Tech.ThresholdGrowth` | 2.0 | High tiers are civilizational projects (runaway impossible). | Leaders sprint away from the pack. |
| `Tech.ProgressPerExotic` | 1.0 | Research cheap in feedstock (exotics labs matter less). | Every tier costs mountains of exotics. |
| `Tech.ComputeBoost` | 0.5 | Compute cores double research; digital economies lead. | Compute ornamental. |
| `Tech.ResearchPullExotics` / `PullCompute` | 6 / 3 | Stronger lab price signal: exotics/compute chains spin up early. | Research starves for want of markets (tiers freeze). |
| `Tech.TradeDiffusionPerYear` | 0.15 | Open borders equalize tech in a few generations. | Gaps persist for eras. |
| `Tech.TradeVolumeSaturation` | 10 | Only heavy trade teaches. | A trickle of goods carries blueprints. |
| `Tech.SalvagePerHullPerYear` | 0.02 | Battlefields are universities (H's wars will spread arms tech fast). | Wrecks are just metal. |
| `Tech.AstroRadiusPerTierHexes` | 3 | Astrogation leaders visibly out-reach neighbors (the tech map gap). | Reach is pure port tier. |
| `Tech.AstroRangePerTierHexes` | 4 | High-astro realms lace longer lanes. | Lane webs identical across tech. |
| `Tech.LifeGrowthPerTier` | 0.15 | Medicine compounds: Life leaders out-populate everyone. | Demography ignores the clinics. |

## Fleet — yards, posted freight, supply, attrition, lineages

Slice E's family: the physical carriers. Freight only moves on Posted
hulls, so these dials gate the whole trade layer; watch `emap traffic`,
the shipment volume in the Markets note, and the `fleet` readiness column.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `Fleet.YardHullsPerTierPerYear` | 0.2 | Bigger navies and merchant marines per yard tier (components permitting). | Hulls trickle; expansion and freight both thin. |
| `Fleet.HullComponentsBase` | 3 | Dearer hulls: yards drain components and treasuries faster, fleets stay small. | Cheap hulls; the components market barely notices a navy. |
| `Fleet.HullArmamentsBase` | 1.5 | Warships bid up armaments — arsenals pay (H inherits armed yards). | Guns nearly free at the slip. |
| `Fleet.HullBuildYearsBase` | 1.5 | Hull batches take longer to build (scaled by size) — the planner reserves more of the income rate per batch. | Near-instant hulls; yards spit out batches each step. |
| `Fleet.FreightTripsPerYearBase` | 0.3 | More capacity per posted hull (fewer hulls needed per lane). **The freight-throughput master dial.** | Lanes need big fleets to matter. |
| `Fleet.EnduranceHexesPerPoint` | 3 | Longer off-lane legs: convoys reach past the colonization radius easily. | Below ~2.7, Medium pioneers can't cover the default 24-hex reach — expansion stalls hard. |
| `Fleet.FuelPerHullPerHexMoved` | 0.02 | Expeditions burn real fuel; staging ports feel convoys. | Movement approaches free. |
| `Fleet.ExpeditionHexesPerYear` | 6 | Faster off-lane convoys: colony expeditions arrive sooner, founding lags less behind the decision. | Slower: distant colonies take many years to found, expansion feels sluggish. |
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

## War — spark, appetite, causes (slice H)

Tension discharges through a casus belli: the menu computes from real
state, incidents roll in contested overlap, and the declaration gate
prices the defender's whole coalition. The AI's cause priority order
and the incident freshness window (2 epochs) are structural.

| Knob | Default | Raise it | Lower it |
|---|---|---|---|
| `War.IncidentRatePerEpoch` | 0.25 | Contested borders spark constantly (chronicle noise, more powder lit). | Quiet frontiers; wars need standing causes. |
| `War.IncidentTensionBump` | 0.08 | Incidents themselves load the gauge (escalation spirals). | Sparks without heat. |
| `War.WarTensionFloor` | 0.35 | Only truly loaded borders ignite (rarer wars). | Skirmishes escalate readily. |
| `War.WarAppetiteThreshold` | 0.38 | Doves need overwhelming tension; hawks still march. **The war-frequency dial** (seed 42: 7 declarations, 3 settlements, 4 live / 40 epochs). | Everyone fights at the first grievance. |
| `War.AttackStrengthRatio` | 0.60 | Attackers need near-parity with the coalition (alliances truly deter). | Hopeless wars of principle. |
| `War.PriceShockMultiple` | 2.0 | Only famine-grade shocks justify seizure wars. | Every price spike is a casus belli. |
| `War.CrusadeThreshold` | 0.30 | Crusades need zealot thrones over deep doctrine gaps. | Ideology alone marches armies. |
| `War.GrievanceDischargeFloor` | 0.35 | Military factions must be loud AND bitter to drag the state to war. | Standing armies find their own wars. |
| `War.AllySupportFactor` | 0.5 | Coalitions fight near-united (deterrence and dogpiles). | Allies are moral support. |
| `War.MobileResponseShare` | 0.3 | Defenders concentrate fast (fronts harden). | Objectives fall while the navy sits home. |
| `War.SupplyPenaltyPerHex` | 0.01 | Deep strikes wither at the tether's end. | Distance means nothing; blitz wars. |
| `War.FortressDefensePerTier` | 0.25 | Fortress worlds anchor whole wars. | Fortifications are decoration. |
| `War.LossDecisiveLoser` / `Winner` / `Attrition` / `Stalemate` | 0.35 / 0.10 / 0.15 / 0.05 | Bloodier engagements: short sharp wars, fat wreckage fields (salvage tech diffusion). | Wars of maneuver; navies survive decades of fighting. |
| `War.BattleFacilityDamage` | 0.15 | Decisive days raze the ground (postwar rebuilding decades). | Industry shrugs off the front line. |
| `War.SiegeBaseEpochs` / `SiegeProvisionEpochsCap` | 1 / 3 | Sieges grind for generations (relief attempts matter). | Ports fall the epoch the fleet arrives. |
| `War.BlockadeHoldEpochs` | 2 | Lane objectives need sustained interdiction. | A single patrol sweep counts as control. |
| `War.FleetDestroyedShare` | 0.25 | The navy objective needs near-annihilation. | First blood breaks the fleet. |
| `War.CommanderDeathOnRout` | 0.25 | Decisive days kill admirals (biographies end at the front). | Commanders always swim home. |
| `War.RenownPerVictory` / `WarHeroRenown` | 2 / 6 | Heroes mint from a battle or two. | Only lifetime campaigners are remembered. |
| `War.ExhaustionPerLoss` | 0.4 | Blood exhausts faster than years (losses end wars). | Only time wearies; attrition wars run forever. |
| `War.LegitimacyCollapseFloor` | 0.25 | Shaky thrones sue early ("a polity breaks when its politics break"). | Wars outlive the governments fighting them. |
| `War.FleetExhaustionShare` | 0.15 | Navies fight to the last squadron. | First serious losses end the war. |
| `War.ReparationsShare` | 0.25 | Losing is expensive (postwar debt overhang decades). | Reparations are symbolic. |
| `War.SettlementTensionRelief` | 0.5 | Peace genuinely clears the air (until claims restock it). | The next war starts where the last one ended. |
| `War.VeteranMilitancyBump` | 0.10 | Every war hardens the sword parties (militarization ratchets). | Veterans retire quietly. |
| `War.VictoryLegitimacy` / `DefeatLegitimacy` | 0.08 / 0.12 | War outcomes make and break governments (defeat → graduation risk). | Thrones indifferent to the front. |
| `War.AnnihilationHatred` | 0.75 | Only saturated hatred with stacked claims turns total (wars of annihilation rare). | Every grudge is a war of extermination. |
| `War.MobilizationFactor` | 3.0 | Wartime economies pivot hard to the front (fabricators boom, stockpiles corner markets). | War is fought from peacetime stocks. |
| `War.MobilizationYears` | 3.0 | The war-economy surge takes longer to build (early battles fight at a lower ramp). | Mobilization is nearly instant; fronts fight at full strength from day one. |
| `War.MobilizationArmamentsPerYear` / `MobilizationFuelPerYear` | 3.0 / 4.0 | Raising readiness draws harder on the war-materiel markets (mobilization competes with the front's own upkeep). | The ramp is cheap; mobilizing costs nothing real. |
| `War.DemobilizationPerYear` | 0.15 | Standing forces stand down faster once the fighting stops. | Peacetime mobilization lingers for generations. |
| `War.WarBudgetMilitaryShift` | 0.20 | Guns before butter: development and expansion starve at war. | The exchequer ignores the front. |
| `War.RationsPerHullPerYear` | 0.04 | Armies eat: extended war means rationing at home (**the SoL-cost dial**); unfed fleets rot. | Navies march on nothing. |
| `War.InterdictionReachHexes` | 4 | War-stationed squadrons contest lanes farther out — wider denial zones around every front (slice CE). | Interdiction only at the gate's doorstep; convoys route past fleets untouched. |
| `War.InterdictionLossPerContestedYear` | 0.12 | Contested legs bleed convoys fast: cut supply lines decide wars in epochs. | Interdiction is harassment; fronts supply through enemy fleets. |
| `War.EscortDampPerHull` | 0.15 | A few escorts near-neutralize seizure (convoy doctrine pays). | Escorts are decoration; only mass decides the lane. |

**Ignition recalibration (slice t1 — the world-time economy).** `WarTensionFloor`
0.55 → 0.35 and `WarAppetiteThreshold` 0.60 → 0.38. The project-model economy
(multi-year hull batches, per-year treasury streaming, world-time colony
expeditions) expands the map more slowly, so contested-overlap tension — the
war engine — builds later and to a lower ceiling: a seed-42 40-epoch history
now tops out around 0.55 and sustains only 0.42–0.51 on its hottest borders,
where the old 0.55 floor meant *zero* declarations. War strength itself was
never suppressed (17 live polities, avg war strength ~11, 288 hulls at the
default run) — the blocker was purely that the ignition thresholds were
calibrated for the old, higher tension regime. Dropping the two dials
proportionally to the new ceiling restores believable dynamics: 7 declarations,
3 settlements, 4 wars still live at year 1000 (≈ main's pre-slice ~10/5). The
appetite gate `tension × (0.5 + militancy)` still gates by temperament —
doves effectively never march (their `(0.5 + M)` can't clear 0.38 below the
floor), hawks fight at the floor, moderates need the hotter borders. No
assertion was weakened and no seed-specific behavior was hard-coded; this is a
recalibration of a calibration constant to a changed emergent regime.

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
  over `StockpileDecayPerYear`: `AllocationPhase.DecayStockpiles` (per-port
  since stage 2; each active depot tier multiplies by `DepotDecayFactor`).
- **Budget weights & policy defaults** — `PolityPolicies.Default`
  (`Policies.cs`): the seven-way budget split (Development .25 / Military
  .20 / Research .15 / Expansion .15 / Appeasement .05 / Reserves .10 /
  Operations .10), default tax rate 0.10. `BudgetWeights.Operations` is the
  one share never subtracted from `Credits` at allocation — it stays as the
  cash margin that pays upkeep, loan service, and tribute (monetary-
  equilibrium design §2). These are *standing policies* — Intent-phase
  outputs, the controller's to change — not world calibration.
- **Siting score weights** — `src/Core/Substrate/Siting.cs`.
- **Colony founding wealth = `Expansion.ColonyCost`** (recycled, not a
  separate dial) and the **homeworld starter industry** composition
  (`InteriorPhase.StarterIndustry`): agri t2 + mine/skimmer/refinery/foundry t1.
- **Ideology drift shape** — the prosperity comfort ×3 factor and famine
  severity scaling: `InteriorPhase.DriftIdeology`.
- **Faction basis agendas** — the per-basis budget-emphasis vectors and the
  basis name suffixes: `FactionOps.BasisBudget` / `Suffix` (slice G).
- **Temperament composition maps** — the ideology→trait map, the per-basis
  faction pulls, and the ruler boldness/zeal skews:
  `Interior/Temperament.cs` (slice G). The *weights* between the four terms
  are per-form catalog data (`GovernmentForm.Composition`).
- **Species lifespans** — human-analog 80 / aquatic 90 / cryophilic 120 /
  lithic 400 / hive & machine 10,000 world-years, and the age curve's
  0.55-of-span onset: `CharacterOps.Lifespan` / `AgeHazardPerYear`
  (slice G). Species-real mortality is a mechanic, not a dial.
- **Government form catalog** — the eight forms' ideology seats, species
  gates, succession rules, inertia/tolerance/floor values, legitimacy
  multipliers, and temperament-composition weights:
  `src/Core/Epoch/Interior/GovernmentForm.cs` (slice G, chassis-catalog
  pattern). The **species→ideology entry tilt** coefficients live beside
  them (`SpeciesIdeologyTilt`) — they define where societies *start*, not
  how the sim is tuned.
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
