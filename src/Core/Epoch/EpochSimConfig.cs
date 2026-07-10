namespace StarGen.Core.Epoch;

/// <summary>Epoch-sim input: identity + the calibration knob families, seeded
/// defaults. All rates are per world-year — the epoch is an integration step,
/// never a unit (frame/time.md, P7). Every dial here is indexed by
/// <see cref="KnobRegistry"/> (which drives the artifact's KNOB lines and the
/// REPL `knobs` command) and documented with its consequences in
/// docs/TUNING.md — a dial must never exist outside that index.</summary>
public sealed class EpochSimConfig
{
    public ulong MasterSeed { get; set; }
    public SimKnobs Sim { get; } = new SimKnobs();
    public GenesisKnobs Genesis { get; } = new GenesisKnobs();
    public EconomyKnobs Economy { get; } = new EconomyKnobs();
    public PopulationKnobs Population { get; } = new PopulationKnobs();
    public InfrastructureKnobs Infrastructure { get; } = new InfrastructureKnobs();
    public ExpansionKnobs Expansion { get; } = new ExpansionKnobs();
    public ControllerKnobs Controller { get; } = new ControllerKnobs();
    public FleetKnobs Fleet { get; } = new FleetKnobs();
    public InteriorKnobs Interior { get; } = new InteriorKnobs();
}

/// <summary>Polity-interior dials (polity/factions-and-government.md):
/// official-ideology drift, the legitimacy term weights (form multipliers
/// scale them — catalog data), structural strain, and enforcement. Slice G.</summary>
public sealed class InteriorKnobs
{
    /// <summary>Official-ideology drift toward the popular line per
    /// world-year at zero form inertia (forms scale it down).</summary>
    public double OfficialDriftPerYear { get; set; } = 0.008;
    /// <summary>How strongly the SoL *trend* (this epoch vs last) moves the
    /// prosperity legitimacy term beyond the SoL level itself.</summary>
    public double SoLTrendGain { get; set; } = 3.0;
    /// <summary>Base weight of the prosperity term in legitimacy.</summary>
    public double LegitimacyProsperityWeight { get; set; } = 0.30;
    /// <summary>Base weight of the official-vs-popular alignment term.</summary>
    public double LegitimacyIdeologyWeight { get; set; } = 0.25;
    /// <summary>Base weight of the ruler-prestige term.</summary>
    public double LegitimacyRulerWeight { get; set; } = 0.20;
    /// <summary>Base weight of the war-outcome term (neutral until H).</summary>
    public double LegitimacyWarWeight { get; set; } = 0.10;
    /// <summary>Base weight of the cultural-accommodation term.</summary>
    public double LegitimacyAccommodationWeight { get; set; } = 0.15;
    /// <summary>Cohesion strain per owned port beyond the first — size
    /// carries its successor states inside it.</summary>
    public double StrainPerPort { get; set; } = 0.008;
    /// <summary>Cohesion strain per culture beyond the founding one.</summary>
    public double StrainPerCulture { get; set; } = 0.05;
    /// <summary>Cohesion strain at mean port distance = colonization reach.</summary>
    public double StrainDistanceWeight { get; set; } = 0.15;
    /// <summary>Enforcement floor — a state with no navy still polices.</summary>
    public double EnforcementBase { get; set; } = 0.4;
    /// <summary>Enforcement per warship hull per owned port.</summary>
    public double EnforcementPerWarshipPerPort { get; set; } = 0.04;
}

/// <summary>Clock and stepping knobs (frame/time.md). Structural — serialized
/// on the ESIM line, not as KNOB records.</summary>
public sealed class SimKnobs
{
    /// <summary>World-years integrated per generational step: one epoch ≈ a generation.</summary>
    public int YearsPerEpoch { get; set; } = 25;
    /// <summary>Default history depth: 40 epochs ≈ 1,000 years.</summary>
    public int EpochCount { get; set; } = 40;
}

/// <summary>Genesis-side knobs: only the stub emergence schedule until the
/// real cosmic/evolutionary families land with Slice F.</summary>
public sealed class GenesisKnobs
{
    /// <summary>Latest world-year a polity may enter — staggered emergence
    /// (frame/time.md §Asymmetric emergence).</summary>
    public int EmergenceWindowYears { get; set; } = 500;
}

/// <summary>Economy dials, per world-year where a rate: the market engine's
/// absolute magnitudes, price mechanics, freight costs, credit, and the
/// facility lifecycle. C's demand profiles are normalized shares; these
/// knobs supply what they multiply.</summary>
public sealed class EconomyKnobs
{
    /// <summary>War-weariness accrual per world-year at war (unused until H).</summary>
    public double WarWearinessPerYear { get; set; } = 0.003;
    /// <summary>Fractional durable-stockpile decay per world-year; perishable
    /// goods scale it up in code (provisions rot, alloys do not).</summary>
    public double StockpileDecayPerYear { get; set; } = 0.002;

    // -- Demand: absolute per-capita rates the normalized profiles multiply --
    /// <summary>Subsistence-band units per population unit per world-year
    /// (× embodiment SubsistenceScale) — the famine dial.</summary>
    public double SubsistenceUnitsPerPopPerYear { get; set; } = 0.6;
    /// <summary>Standard-of-living-band units per population unit per world-year.</summary>
    public double SoLUnitsPerPopPerYear { get; set; } = 0.4;
    /// <summary>Luxury-band units per population unit per world-year (elastic).</summary>
    public double LuxuryUnitsPerPopPerYear { get; set; } = 0.15;
    /// <summary>Price response of subsistence demand — near-inelastic
    /// (hunger barely bends to price).</summary>
    public double SubsistenceElasticity { get; set; } = 0.1;
    /// <summary>Price response of standard-of-living demand — moderate.</summary>
    public double SoLElasticity { get; set; } = 0.5;
    /// <summary>Price response of luxury demand — elastic (first to vanish
    /// when prices rise).</summary>
    public double LuxuryElasticity { get; set; } = 1.3;
    /// <summary>Floor on demand's price response — want never fully dies.</summary>
    public double ElasticFloor { get; set; } = 0.25;
    /// <summary>Ceiling on demand's price response — cheapness doubles
    /// appetite at most.</summary>
    public double ElasticCeiling { get; set; } = 2.0;

    // -- Prices --
    public double BasePriceRaw { get; set; } = 1.0;
    public double BasePriceProcessed { get; set; } = 3.0;
    public double BasePriceCapital { get; set; } = 8.0;
    /// <summary>Rate limit on price drift toward clearing, fractional per
    /// world-year (markets never perfectly clear).</summary>
    public double PriceDriftMaxPerYear { get; set; } = 0.04;
    /// <summary>Shape of the drift: price moves by (demand/supply)^exponent,
    /// then rate-clamped.</summary>
    public double PriceDriftExponent { get; set; } = 0.5;
    /// <summary>Absolute price floor — gluts bottom out, never reach zero.</summary>
    public double PriceFloor { get; set; } = 0.01;
    /// <summary>Absolute ceiling as a multiple of the founding price — spikes
    /// stay legible without minting paper fortunes through the wage share.</summary>
    public double MaxPriceMultiple { get; set; } = 100.0;
    /// <summary>How strongly outbound lane gradients bid up a hub's price —
    /// the re-export demand term (entrepôts emerge from it).</summary>
    public double ReExportWeight { get; set; } = 0.5;
    /// <summary>Black-book margin over the open price — prohibition converts
    /// demand, it never deletes it (commodities.md legality).</summary>
    public double BlackMarketMarkup { get; set; } = 2.5;
    /// <summary>Headroom over the arbitrage break-even in the import-parity
    /// price cap — imports must stay profitable to actually flow.</summary>
    public double ParityHeadroom { get; set; } = 1.15;

    // -- Production --
    /// <summary>Supply elasticity floor: producers throttle toward this
    /// utilization as their good's price falls under its founding price —
    /// nobody mines ore nobody buys, so gluts clear instead of pegging.</summary>
    public double MinUtilization { get; set; } = 0.15;

    // -- Income --
    /// <summary>Share of realized facility revenue paid to the staffing
    /// segments; the remainder goes to the owner (economy/markets.md
    /// §Household income). Also the construction-wage share.</summary>
    public double LaborShare { get; set; } = 0.4;
    /// <summary>Credits minted once per polity at entry; conserved
    /// thereafter (P4).</summary>
    public double InitialCreditsPerPolity { get; set; } = 500.0;
    /// <summary>Household credits minted per population unit at homeworld
    /// founding — first-epoch purchasing power before wages exist.</summary>
    public double InitialWealthPerPop { get; set; } = 15.0;

    // -- Freight --
    /// <summary>Credits per unit of goods per hex of lane distance.</summary>
    public double FreightCostPerUnitPerHex { get; set; } = 0.02;
    /// <summary>Fuel demand per unit shipped per hex — movement is never free.</summary>
    public double FuelPerUnitPerHex { get; set; } = 0.005;
    /// <summary>Share of a market's stock arbitrage may lift per step —
    /// damping against flow oscillation. Procurement uses it too.</summary>
    public double ExportShare { get; set; } = 0.5;
    /// <summary>Per-unit friction on restricted goods as a share of the
    /// destination price (permits, inspections, seizure risk) — evadable at
    /// margin cost by design.</summary>
    public double RestrictedFriction { get; set; } = 0.5;
    /// <summary>Subsistence fraction below which a polity releases provisions
    /// reserves into the starving port's market.</summary>
    public double ReserveReleaseTrigger { get; set; } = 0.9;

    // -- Credit --
    public double LoanRatePerYear { get; set; } = 0.02;
    public int LoanTermYears { get; set; } = 50;

    // -- Facility lifecycle --
    /// <summary>Fractional condition decay per world-year toward the unmet
    /// upkeep fraction.</summary>
    public double ConditionDecayPerYear { get; set; } = 0.01;
    /// <summary>Fractional condition recovery per world-year toward the met
    /// upkeep fraction.</summary>
    public double ConditionRecoveryPerYear { get; set; } = 0.05;
    /// <summary>Config-level producer tech tier until tech domains land
    /// (slice G). 2 = standard capital recipes run, advanced stay gated.</summary>
    public int TechTierStub { get; set; } = 2;
}

/// <summary>Population dials, per world-year where a rate: demographics,
/// migration, and ideology drift (polity/population-and-identity.md). Segment
/// growth base rate and caps stay in ExpansionKnobs where slice B put them.</summary>
public sealed class PopulationKnobs
{
    /// <summary>Fraction of a segment migrating per world-year at full
    /// gradient pull.</summary>
    public double MigrationRatePerYear { get; set; } = 0.002;
    /// <summary>Minimum attractiveness gradient before anyone packs.</summary>
    public double MigrationMinGradient { get; set; } = 0.05;
    /// <summary>Subsistence fraction below which migration turns refugee.</summary>
    public double RefugeeLine { get; set; } = 0.5;
    /// <summary>Refugee flight speed as a multiple of the base migration rate.</summary>
    public double RefugeeMultiplier { get; set; } = 8.0;
    /// <summary>Subsistence fraction below which segments shrink and famine
    /// events chronicle — famine is when people die; scarcity is just prices.</summary>
    public double FamineLine { get; set; } = 0.75;
    /// <summary>Fractional segment shrink per world-year of unmet subsistence.</summary>
    public double FamineShrinkPerYear { get; set; } = 0.02;
    /// <summary>Ideology-axis drift toward lived conditions per world-year.</summary>
    public double IdeologyDriftPerYear { get; set; } = 0.01;
    /// <summary>Subsistence fraction below which ideology turns Authority/Sacral.</summary>
    public double HungerIdeologyLine { get; set; } = 0.7;
    /// <summary>SoL above which ideology turns Individual/Open.</summary>
    public double ProsperityIdeologyLine { get; set; } = 0.7;
    /// <summary>SoL movement toward the cleared-demand fraction per world-year.
    /// (Machine populations need no separate fab dial: their subsistence IS
    /// fab inputs, so LastSubsistence already gates their growth.)</summary>
    public double SoLDriftPerYear { get; set; } = 0.02;
}

/// <summary>Port/lane physical knobs plus the construction dials
/// (frame/space-and-travel.md, assets-and-investment.md). Radii and ranges in
/// hexes; the two port growth axes step per tier, independently.</summary>
public sealed class InfrastructureKnobs
{
    /// <summary>Local service radius of a tier-1 port, in hexes.</summary>
    public int ServiceRadiusBaseHexes { get; set; } = 4;
    /// <summary>Additional service radius per tier above 1.</summary>
    public int ServiceRadiusPerTierHexes { get; set; } = 4;
    /// <summary>Inter-port (lane) reach of a tier-1 port, in hexes.</summary>
    public int InterPortRangeBaseHexes { get; set; } = 18;
    /// <summary>Additional inter-port reach per tier above 1.</summary>
    public int InterPortRangePerTierHexes { get; set; } = 8;
    public int MaxPortTier { get; set; } = 3;
    /// <summary>Homeworld ports establish at this tier at emergence — a
    /// civilization at spaceflight is past "outpost".</summary>
    public int HomeworldPortTier { get; set; } = 2;
    /// <summary>Facilities a port's domain supports per port tier — the
    /// construction cap (development concentrates before it sprawls).</summary>
    public int FacilitiesPerPortTier { get; set; } = 5;
    /// <summary>Development treasury below which construction-material pull
    /// stays quiet (no point hauling alloys nobody can spend).</summary>
    public double ConstructionDevGate { get; set; } = 25.0;
    /// <summary>Construction-basket demand registered per under-capacity
    /// port per epoch: alloys.</summary>
    public double ConstructionPullAlloys { get; set; } = 12.0;
    /// <summary>Construction-basket demand: machinery.</summary>
    public double ConstructionPullMachinery { get; set; } = 8.0;
    /// <summary>Construction-basket demand: composites.</summary>
    public double ConstructionPullComposites { get; set; } = 6.0;
    /// <summary>Minimum siting score × price signal before anything gets
    /// built — don't build junk.</summary>
    public double ConstructionScoreFloor { get; set; } = 0.12;
    /// <summary>How decisively extraction must out-value farmland before a
    /// colony founds on it instead of farming — food security's premium.</summary>
    public double FoodSecurityPremium { get; set; } = 1.25;
}

/// <summary>Expansion/colonization dials, per world-year where a rate.
/// Treasuries fill from real market income split by standing budget
/// weights — slice D retired the slice-B stub income.</summary>
public sealed class ExpansionKnobs
{
    /// <summary>Expansion points consumed by one colony founding — recycled
    /// to the settlers as founding wealth (treasury spending is somebody's
    /// income, never destroyed).</summary>
    public double ColonyCost { get; set; } = 15.0;
    /// <summary>Off-lane colonization reach from any owned port, in hexes.</summary>
    public int ColonizationReachHexes { get; set; } = 24;
    /// <summary>Development points to raise a port: cost = base × current tier.</summary>
    public double PortUpgradeCostBase { get; set; } = 40.0;
    /// <summary>Development points per lane built.</summary>
    public double LaneCost { get; set; } = 25.0;
    public double HomeworldSegmentSize { get; set; } = 3.0;
    public double ColonySegmentSize { get; set; } = 0.5;
    /// <summary>Logistic population growth per world-year toward the port-tier cap.</summary>
    public double SegmentGrowthPerYear { get; set; } = 0.01;
    /// <summary>Port population cap = port tier × this, shared across segments.</summary>
    public double SegmentCapPerTier { get; set; } = 2.0;
}

/// <summary>Fleet dials (fleets/ships-and-fleets.md), per world-year where a
/// rate: yard throughput and hull costs, posted freight throughput, supply
/// and readiness, attrition, lineage drift, and the genesis starter fleet.
/// Chassis stat baselines are structural catalog data (ShipCatalog).</summary>
public sealed class FleetKnobs
{
    /// <summary>Fraction of a fleet's hulls wrecked per world-year while its
    /// readiness sits below the attrition floor — unsupplied fleets lose
    /// readiness, then hulls.</summary>
    public double AttritionHullLossPerYear { get; set; } = 0.02;
    /// <summary>Readiness below which an unsupplied fleet starts losing
    /// hulls to wreckage.</summary>
    public double AttritionReadinessFloor { get; set; } = 0.3;
    /// <summary>Hexes of off-lane range per point of a design's
    /// OffLaneEndurance stat — the endurance floor in map units (a Medium
    /// pioneer at ~9 endurance reaches ~27 hexes, just past the default
    /// colonization reach).</summary>
    public double EnduranceHexesPerPoint { get; set; } = 3.0;
    /// <summary>Round trips per world-year of a posted hull at transit
    /// speed 1 over one hex — posted capacity = cargo × trips
    /// (× TransitSpeed ÷ distance).</summary>
    public double FreightTripsPerYearBase { get; set; } = 0.3;
    /// <summary>Fuel units drawn per hull per hex of expedition movement —
    /// off-lane journeys are never free.</summary>
    public double FuelPerHullPerHexMoved { get; set; } = 0.02;
    /// <summary>Armaments per Medium warship hull at the yard
    /// (× SizeCostFactor; warship roles only).</summary>
    public double HullArmamentsBase { get; set; } = 1.5;
    /// <summary>Ship Components per Medium hull at the yard
    /// (× SizeCostFactor).</summary>
    public double HullComponentsBase { get; set; } = 3.0;
    /// <summary>Component-grade improvement over a design's build grade that
    /// mints the next mark of its lineage.</summary>
    public double MarkGradeStep { get; set; } = 0.15;
    /// <summary>Ship Components demand a funded polity registers per epoch
    /// at its yard port (capital until a yard exists) — the military-
    /// construction pull that makes shipyards worth siting; without it the
    /// components price floors and no yard ever pencils out.</summary>
    public double MilitaryPullComponents { get; set; } = 10.0;
    /// <summary>Fractional readiness decay per world-year toward the unmet
    /// supply fraction.</summary>
    public double ReadinessDecayPerYear { get; set; } = 0.02;
    /// <summary>Fractional readiness recovery per world-year toward the met
    /// supply fraction.</summary>
    public double ReadinessRecoveryPerYear { get; set; } = 0.05;
    /// <summary>Upkeep multiplier for docked Reserve-posture fleets —
    /// mothballs are cheap and readiness decays anyway.</summary>
    public double ReserveUpkeepFactor { get; set; } = 0.25;
    /// <summary>Colony hulls in the homeworld starter fleet (genesis
    /// furniture — founding needs a physical convoy from epoch one).</summary>
    public int StarterColonyHulls { get; set; } = 1;
    /// <summary>Escort hulls in the starter fleet per point of species
    /// militancy (rounded).</summary>
    public double StarterEscortPerMilitancy { get; set; } = 4.0;
    /// <summary>Freight hulls in the homeworld starter fleet — the first
    /// posted capacity; without them nothing moves at epoch one.</summary>
    public int StarterFreightHulls { get; set; } = 4;
    /// <summary>Share of fleet upkeep drawn as Fuel; the rest is Armaments
    /// for warships, Ship Components (spares) for civilian hulls — kept off
    /// Machinery deliberately: the facility-upkeep sink already dominates
    /// that market, and coupling the merchant marine to it starved freight.</summary>
    public double UpkeepFuelShare { get; set; } = 0.4;
    /// <summary>Goods units drawn per point of a fleet's Upkeep vector per
    /// world-year — the fleet-supply magnitude dial.</summary>
    public double UpkeepUnitsPerPointPerYear { get; set; } = 0.025;
    /// <summary>Hulls a yard lays down per yard tier per world-year,
    /// components permitting.</summary>
    public double YardHullsPerTierPerYear { get; set; } = 0.2;
}

/// <summary>Genesis-AI policy dials (frame/controller-contract.md): the
/// standing-policy magnitudes the stock controller writes. Smarter
/// controllers and the player replace the AI, not these defaults.</summary>
public sealed class ControllerKnobs
{
    /// <summary>No expeditions while realm mean subsistence sits below this —
    /// consolidation before expansion.</summary>
    public double RealmHungerGate { get; set; } = 0.8;
    /// <summary>Provisions reserve target per owned port — famine and siege
    /// buffering by standing policy (economy/markets.md §Stockpiles).</summary>
    public double ProvisionsReservePerPort { get; set; } = 3.0;
    /// <summary>Alloys banked per owned port — construction materials; market
    /// leftovers never hold a whole build basket at once.</summary>
    public double AlloysReservePerPort { get; set; } = 3.0;
    /// <summary>Machinery banked per owned port.</summary>
    public double MachineryReservePerPort { get; set; } = 1.5;
    /// <summary>Composites banked per owned port.</summary>
    public double CompositesReservePerPort { get; set; } = 1.0;
    /// <summary>Armaments reserve per port per point of species militancy —
    /// war materiel by temperament.</summary>
    public double ArmamentsPerPortPerMilitancy { get; set; } = 2.0;
    /// <summary>Ship Components banked per owned port — the quartermaster's
    /// stores fleet upkeep falls back on where a frontier port's market
    /// holds none (fleets doc: fleets draw from market/stockpile).</summary>
    public double ShipPartsReservePerPort { get; set; } = 3.0;
    /// <summary>Fuel banked per owned port — navy fuel dumps; the other
    /// half of the upkeep fallback (posted fleets at refinery-less
    /// frontier ports otherwise run dry and rot).</summary>
    public double FuelReservePerPort { get; set; } = 4.0;
    /// <summary>Militancy below which no armaments reserve is kept at all.</summary>
    public double MilitancyReserveGate { get; set; } = 0.2;
    /// <summary>Species openness below which narcotics are prohibited.</summary>
    public double NarcoticsProhibitBelowOpenness { get; set; } = 0.35;
    /// <summary>Species openness below which narcotics are restricted.</summary>
    public double NarcoticsRestrictBelowOpenness { get; set; } = 0.55;
}
