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
    public CharacterKnobs Character { get; } = new CharacterKnobs();
    public FactionKnobs Faction { get; } = new FactionKnobs();
    public TechKnobs Tech { get; } = new TechKnobs();
    public CorporateKnobs Corporate { get; } = new CorporateKnobs();
    public RelationsKnobs Relations { get; } = new RelationsKnobs();
    public WarKnobs War { get; } = new WarKnobs();
    public NewsKnobs News { get; } = new NewsKnobs();
    public PoiKnobs Poi { get; } = new PoiKnobs();
    public PlagueKnobs Plague { get; } = new PlagueKnobs();
}

/// <summary>Plague dials (slice I): outbreak incidence, lane-borne spread,
/// the toll, the burnout/immunity clocks, and the quarantine hold. Machine
/// immunity and the one-strain-per-port rule are structural.</summary>
public sealed class PlagueKnobs
{
    /// <summary>Outbreak chance per port per world-year at full crowding.</summary>
    public double OutbreakChancePerYear { get; set; } = 0.0004;
    /// <summary>Chance per world-year an infection crosses a traffic-
    /// saturated lane (scaled down on quiet lanes).</summary>
    public double SpreadChancePerYear { get; set; } = 0.06;
    /// <summary>Posted round trips per world-year at which lane spread
    /// saturates — contagion rides the same traffic the news does.</summary>
    public double SpreadTrafficSaturation { get; set; } = 2.0;
    /// <summary>Fraction of an infected port's organic population lost per
    /// world-year before the Life-tech discount.</summary>
    public double MortalityPerYear { get; set; } = 0.008;
    /// <summary>Mortality discount per Life tech tier (medicine).</summary>
    public double MortalityLifeTierDiscount { get; set; } = 0.2;
    /// <summary>World-years an infection burns at one port before it
    /// clears — plagues burn out, never sterilize.</summary>
    public double BurnoutYears { get; set; } = 30.0;
    /// <summary>World-years a recovered port resists reinfection.</summary>
    public double ImmunityYears { get; set; } = 75.0;
    /// <summary>World-years one QuarantineAct holds a lane closed.</summary>
    public double QuarantineYears { get; set; } = 30.0;
}

/// <summary>POI-compiler dials (chronicle-and-poi.md §The POI compiler):
/// what qualifies as a battlefield, a memorial, permanent archaeology, and
/// how far surveyors chart precursor sites. Slice I.</summary>
public sealed class PoiKnobs
{
    /// <summary>Wrecked hulls at one hex before it anchors a battlefield.</summary>
    public double BattlefieldHullFloor { get; set; } = 4.0;
    /// <summary>Famine shortfall below which no memorial rises — ordinary
    /// hunger is not an atrocity.</summary>
    public double MemorialShortfallFloor { get; set; } = 0.75;
    /// <summary>Battlefield magnitude at which a salvaged-out field still
    /// persists as permanent archaeology instead of fading.</summary>
    public double PermanentMagnitude { get; set; } = 20.0;
    /// <summary>Epochs a founded port must sit empty before it reads as a
    /// dead city (grace precedes the death clock).</summary>
    public int RuinsDeadEpochs { get; set; } = 2;
    /// <summary>Hexes from any entered port at which a precursor site gets
    /// charted — exploration surfaces the deep past.</summary>
    public int SurveyReachHexes { get; set; } = 10;
    /// <summary>Battlefield hulls remaining below which no salvor bothers
    /// to incorporate.</summary>
    public double SalvageNicheHullFloor { get; set; } = 6.0;
    /// <summary>Hexes from an own port within which a POI reads as a
    /// workable salvage niche.</summary>
    public int SalvageReachHexes { get; set; } = 12;
    /// <summary>Hulls a working salvor strips from its field per world-year.</summary>
    public double SalvageHullsPerYear { get; set; } = 0.2;
    /// <summary>Alloys recovered per stripped hull (declining grade).</summary>
    public double SalvageAlloysPerHull { get; set; } = 3.0;
    /// <summary>Ship components recovered per stripped hull.</summary>
    public double SalvageComponentsPerHull { get; set; } = 1.0;
    /// <summary>Exotics a precursor dig yields per world-year.</summary>
    public double DigExoticsPerYear { get; set; } = 0.15;
    /// <summary>Site magnitude a dig consumes per world-year — the site
    /// depletes toward archaeology-done.</summary>
    public double DigMagnitudeDecayPerYear { get; set; } = 0.02;
    /// <summary>Astrogation research progress a precursor dig hands the
    /// host polity per world-year (Industrial at half).</summary>
    public double DigResearchPerYear { get; set; } = 0.01;
    /// <summary>Hexes from a standing ruin within which a lane endpoint
    /// reads lawless — the ruin is a piracy haven (slice J wire).</summary>
    public int LawlessnessReachHexes { get; set; } = 3;
    /// <summary>Raid-floor multiplier on a lawless lane: less cargo tempts
    /// a band when the walls to hide in already stand — and no navy roots
    /// it out of them.</summary>
    public double LawlessRaidFactor { get; set; } = 0.4;
    /// <summary>Stance depth a standing memorial holds against its
    /// perpetrator: an audience whose stance ever reached −this never
    /// fades above it while the stone stands (slice J wire).</summary>
    public double MemorialStanceAnchor { get; set; } = 0.25;
}

/// <summary>News-graph dials (narrative/perception-and-news.md, fleets doc
/// §Information carriage): how fast word travels lanes and wilds, when a
/// public event pulses, and how long a pulse stays live. Slice I. The
/// speeds are hexes per world-year; traffic is FleetOps.TrafficPerYear.</summary>
public sealed class NewsKnobs
{
    /// <summary>News carriage of a lane with no posted traffic — a standing
    /// lane always carries some word (occasional independents).</summary>
    public double BaseLaneSpeedHexPerYear { get; set; } = 4.0;
    /// <summary>Extra carriage a fully-saturated lane adds on the base —
    /// busy lanes carry news fast.</summary>
    public double TrafficSpeedBonus { get; set; } = 12.0;
    /// <summary>Posted round trips per world-year at which a lane's news
    /// carriage saturates.</summary>
    public double TrafficSaturationTripsPerYear { get; set; } = 4.0;
    /// <summary>Off-lane carriage — wilds barely carry news.</summary>
    public double OffLaneSpeedHexPerYear { get; set; } = 0.5;
    /// <summary>Magnitude below which a public event emits no pulse —
    /// the galaxy doesn't gossip about every dock fee.</summary>
    public double PulseMagnitudeFloor { get; set; } = 0.5;
    /// <summary>Age at which an undelivered pulse attenuates to rumor and
    /// stops being carried.</summary>
    public double PulseMaxYears { get; set; } = 150.0;
    /// <summary>Fractional stance drift toward indifference per world-year —
    /// memory fades; reputation must be re-earned (or re-offended).</summary>
    public double StanceDecayPerYear { get; set; } = 0.005;
}

/// <summary>War dials (interpolity/war.md): the spark's incidence, the
/// declaration appetite, casus-belli thresholds, engagement losses, siege
/// pacing, and termination/settlement magnitudes. Slice H. Weariness's
/// per-year rate stays on Economy.WarWearinessPerYear where slice A put it.</summary>
public sealed class WarKnobs
{
    /// <summary>Border-incident probability per epoch at full contested
    /// overlap (damped by non-aggression rungs).</summary>
    public double IncidentRatePerEpoch { get; set; } = 0.25;
    /// <summary>Instant tension bump per incident — sparks load the gauge
    /// they later read.</summary>
    public double IncidentTensionBump { get; set; } = 0.08;
    /// <summary>Tension below which no war fires whatever the menu says —
    /// low-tension incidents fizzle into demands and apologies.</summary>
    public double WarTensionFloor { get; set; } = 0.35;
    /// <summary>Declaration gate: tension × (0.5 + composed militancy)
    /// must clear this — the escalation-∝-tension dial.</summary>
    public double WarAppetiteThreshold { get; set; } = 0.38;
    /// <summary>Attackers price allied fleets: own strength must be at
    /// least this share of the defender's coalition to declare.</summary>
    public double AttackStrengthRatio { get; set; } = 0.60;
    /// <summary>Own-market price over founding that reads as the deficit/
    /// price shock behind a resource-seizure casus belli.</summary>
    public double PriceShockMultiple { get; set; } = 2.0;
    /// <summary>Ideology gap × ruler zeal that arms the crusade cause.</summary>
    public double CrusadeThreshold { get; set; } = 0.30;
    /// <summary>Military-faction strength × grievance that arms the
    /// grievance-discharge cause.</summary>
    public double GrievanceDischargeFloor { get; set; } = 0.35;
    /// <summary>Weight of a coalition partner's distant navy in an
    /// engagement — support without relocation.</summary>
    public double AllySupportFactor { get; set; } = 0.5;
    /// <summary>Share of the defender's away-station strength that answers
    /// an attacked objective (and bleeds there).</summary>
    public double MobileResponseShare { get; set; } = 0.3;
    /// <summary>Attacker power lost per hex from its supply base, floored
    /// at half — extended lines degrade readiness (war.md).</summary>
    public double SupplyPenaltyPerHex { get; set; } = 0.01;
    /// <summary>Fractional defender-power bonus per fortress tier standing
    /// at the objective.</summary>
    public double FortressDefensePerTier { get; set; } = 0.25;
    /// <summary>Hull-share the loser of a decisive engagement wrecks.</summary>
    public double LossDecisiveLoser { get; set; } = 0.35;
    /// <summary>Hull-share the winner of a decisive engagement wrecks.</summary>
    public double LossDecisiveWinner { get; set; } = 0.10;
    /// <summary>Hull-share both sides wreck in an attrition round.</summary>
    public double LossAttrition { get; set; } = 0.15;
    /// <summary>Hull-share both sides wreck in a stalemate.</summary>
    public double LossStalemate { get; set; } = 0.05;
    /// <summary>Facility condition lost at a port that hosts a decisive
    /// attacker victory — battles scar the ground.</summary>
    public double BattleFacilityDamage { get; set; } = 0.15;
    /// <summary>Siege epochs before any larder or fortress extends it.</summary>
    public int SiegeBaseEpochs { get; set; } = 1;
    /// <summary>Cap on the siege epochs a full larder can add.</summary>
    public double SiegeProvisionEpochsCap { get; set; } = 3.0;
    /// <summary>Epochs a lane must stay cut to count the blockade
    /// objective as taken.</summary>
    public int BlockadeHoldEpochs { get; set; } = 2;
    /// <summary>Defender coalition strength (as a share of its strength at
    /// declaration) below which the fleet objective counts as taken.</summary>
    public double FleetDestroyedShare { get; set; } = 0.25;
    /// <summary>Chance the beaten commander of a decisive engagement dies
    /// with the day — war is a hazard for commanders.</summary>
    public double CommanderDeathOnRout { get; set; } = 0.25;
    /// <summary>Renown a decisive victory mints its commander.</summary>
    public double RenownPerVictory { get; set; } = 2.0;
    /// <summary>Renown at which a victorious commander is hailed a war
    /// hero (cap-gated like every notable).</summary>
    public double WarHeroRenown { get; set; } = 6.0;
    /// <summary>Exhaustion per fully-wrecked own warship roster — losses
    /// weary a nation beyond the years alone.</summary>
    public double ExhaustionPerLoss { get; set; } = 0.4;
    /// <summary>Legitimacy below which a belligerent's politics count as
    /// broken — a polity breaks when its politics break.</summary>
    public double LegitimacyCollapseFloor { get; set; } = 0.25;
    /// <summary>Coalition strength (as a share of the strength mustered at
    /// declaration) below which a side counts fleet-exhausted.</summary>
    public double FleetExhaustionShare { get; set; } = 0.15;
    /// <summary>Share of the loser's liquid treasury a reparations
    /// settlement transfers (conserved).</summary>
    public double ReparationsShare { get; set; } = 0.25;
    /// <summary>Fractional tension unloaded by a settlement — the war
    /// discharged what the claims don't restock.</summary>
    public double SettlementTensionRelief { get; set; } = 0.5;
    /// <summary>Militancy a war's end adds to both sides' military
    /// factions — veterans come home harder.</summary>
    public double VeteranMilitancyBump { get; set; } = 0.10;
    /// <summary>Legitimacy a settlement hands the winner's throne.</summary>
    public double VictoryLegitimacy { get; set; } = 0.08;
    /// <summary>Legitimacy a settlement costs the loser's throne.</summary>
    public double DefeatLegitimacy { get; set; } = 0.12;
    /// <summary>Wartime multiplier on the yard pull and on armaments/parts/
    /// fuel stockpile targets — the mobilization surge that diverts an
    /// economy to the front (fabricators boom, households ration).</summary>
    public double MobilizationFactor { get; set; } = 3.0;
    /// <summary>World-years a belligerent's Mobilization project takes to
    /// ramp its war economy from 0 to full readiness (spec §5).</summary>
    public double MobilizationYears { get; set; } = 3.0;
    /// <summary>Armaments a Mobilization project draws per world-year —
    /// the ramp's war-materiel basket.</summary>
    public double MobilizationArmamentsPerYear { get; set; } = 3.0;
    /// <summary>Fuel a Mobilization project draws per world-year — the
    /// ramp's war-materiel basket.</summary>
    public double MobilizationFuelPerYear { get; set; } = 4.0;
    /// <summary>Mobilization readiness lost per world-year at peace — the
    /// standing force stands down once the fighting stops (spec §5).</summary>
    public double DemobilizationPerYear { get; set; } = 0.15;
    /// <summary>Budget share shifted from development and expansion into
    /// the military line while at war — guns before butter.</summary>
    public double WarBudgetMilitaryShift { get; set; } = 0.20;
    /// <summary>Tension × (1 − warmth) at which a declaration turns total:
    /// demand Annihilation, no surrender accepted (needs standing claims
    /// on the table too — hatred has history).</summary>
    public double AnnihilationHatred { get; set; } = 0.75;
    /// <summary>Provisions a belligerent's warship draws per hull per
    /// world-year on top of normal upkeep — armies eat, and the rations
    /// compete with the households' subsistence (wartime rationing).</summary>
    public double RationsPerHullPerYear { get; set; } = 0.04;
}

/// <summary>Relations dials (interpolity/relations.md): contact reach, the
/// warmth/tension drift rates, and the source-term weights behind both
/// targets. Slice H. The stance buckets Intent maps net warmth−tension to
/// are structural controller behavior.</summary>
public sealed class RelationsKnobs
{
    /// <summary>Nearest cross-owner port distance (hexes) at which two
    /// polities meet — reach overlap makes contact (news-borne contact is
    /// slice I).</summary>
    public int ContactReachHexes { get; set; } = 24;
    /// <summary>Fractional warmth drift toward its source target per
    /// world-year (both directions — warmth cools as fast as it builds).</summary>
    public double WarmthDriftPerYear { get; set; } = 0.02;
    /// <summary>Fractional tension rise toward a higher target per
    /// world-year — friction loads fast.</summary>
    public double TensionRisePerYear { get; set; } = 0.05;
    /// <summary>Fractional tension relaxation toward a lower target per
    /// world-year — grudges unload slowly, and only once sources resolve.</summary>
    public double TensionRelaxPerYear { get; set; } = 0.012;
    /// <summary>Warmth-baseline cut per point of openness-filtered
    /// strangeness (embodiment + disposition distance).</summary>
    public double StrangenessWeight { get; set; } = 0.35;
    /// <summary>Warmth-target weight of saturated cross-border trade.</summary>
    public double TradeWarmthWeight { get; set; } = 0.30;
    /// <summary>Posted cross-border freight capacity at which the trade
    /// warmth term saturates.</summary>
    public double TradeSaturation { get; set; } = 10.0;
    /// <summary>Warmth-target weight of the treaty ladder at its top rung.</summary>
    public double TreatyWarmthWeight { get; set; } = 0.25;
    /// <summary>Warmth-target weight of the pair-mean stance (reputation) —
    /// what both courts have heard about each other reprices the relation,
    /// first contact included (slice I).</summary>
    public double ReputationWarmthWeight { get; set; } = 0.20;
    /// <summary>Warmth per live dynastic instrument (marriage/wardship),
    /// counted up to three.</summary>
    public double DynasticTieWarmth { get; set; } = 0.10;
    /// <summary>World-years before a dynastic tie's generation dies out —
    /// the tie lapses into the succession claim it always carried
    /// ("two reigns later").</summary>
    public int DynasticTieLapseYears { get; set; } = 75;
    /// <summary>Warmth cut per point of official-ideology gap.</summary>
    public double IdeologyGapCooling { get; set; } = 0.20;
    /// <summary>Tension-target weight of saturated service-area overlap
    /// (the contested-influence zones).</summary>
    public double OverlapTensionWeight { get; set; } = 0.35;
    /// <summary>Overlapping cross-owner port pairs at which the overlap
    /// tension term saturates.</summary>
    public double OverlapSaturation { get; set; } = 4.0;
    /// <summary>Tension per live standing claim (saturating at 1).</summary>
    public double ClaimTensionWeight { get; set; } = 0.18;
    /// <summary>Tension-target weight of ideology gap × mean ruler zeal.</summary>
    public double IdeologyTensionWeight { get; set; } = 0.30;
    /// <summary>Tension-target weight of the pair's mean composed militancy —
    /// hawks keep borders loaded even without grievances.</summary>
    public double MilitancyTensionWeight { get; set; } = 0.20;
    /// <summary>Tension-target weight of the loudest military faction's
    /// strength × militancy on either side.</summary>
    public double AgitationTensionWeight { get; set; } = 0.15;
    /// <summary>Tension-target weight while either blockades the other's
    /// ports — interdiction strain.</summary>
    public double InterdictionTensionWeight { get; set; } = 0.40;
    /// <summary>Kin population living under the other's rule at which a
    /// cultural-kin claim raises (and below which it releases).</summary>
    public double KinClaimSegmentFloor { get; set; } = 0.5;
    /// <summary>Warmth required to offer or accept the first rung; each
    /// further rung costs +TreatyGateStep — warmth gates ascent.</summary>
    public double TreatyGateBase { get; set; } = 0.40;
    /// <summary>Additional warmth gate per rung above the first.</summary>
    public double TreatyGateStep { get; set; } = 0.12;
    /// <summary>Warmth lost outright by the pair when a rung is broken —
    /// the galaxy hears, the partner remembers.</summary>
    public double BreakWarmthPenalty { get; set; } = 0.25;
    /// <summary>Instant tension a colony founded inside a neighbor's
    /// service area costs with that neighbor — expansion into contested
    /// space is a provocation the founder chooses.</summary>
    public double EncroachmentTensionBump { get; set; } = 0.10;
    /// <summary>Warmth-gate discount per point of saturated overlap at the
    /// federation rung — entangled friendly borders push toward fusion
    /// rather than friction (slice H eyeball feedback).</summary>
    public double FederationOverlapDiscount { get; set; } = 0.25;
    /// <summary>Fractional tension-target damping under a non-aggression
    /// rung or above — the rung's teeth (spark de-escalation is war.md's).</summary>
    public double NonAggressionDamping { get; set; } = 0.30;
    /// <summary>Tariff multiplier between trade-pact partners — the tariff
    /// cut that is the first rung's teeth.</summary>
    public double PactTariffFactor { get; set; } = 0.40;
    /// <summary>Epochs a defense alliance must stand before the federation
    /// gate opens — sustained alliance, not a fling.</summary>
    public int FederationAllianceEpochs { get; set; } = 3;
    /// <summary>Both cohesions must sit at or above this to fuse — troubled
    /// realms don't merge, they fracture.</summary>
    public double FederationCohesionFloor { get; set; } = 0.55;
    /// <summary>Official-ideology gap above which the merge is off.</summary>
    public double FederationIdeologyGapMax { get; set; } = 0.20;
    /// <summary>Composed openness both sides need to contemplate fusion.</summary>
    public double FederationOpennessFloor { get; set; } = 0.40;
    /// <summary>Epochs of stable vassalage before the absorption exit can
    /// fire — cultural drift takes generations.</summary>
    public int VassalAbsorptionEpochs { get; set; } = 8;
    /// <summary>Warmth at or above which long vassalage completes as
    /// peaceful annexation.</summary>
    public double VassalAbsorptionWarmth { get; set; } = 0.60;
    /// <summary>Overlord cohesion below which vassals bid for independence
    /// (the secession exit).</summary>
    public double VassalSecessionCohesion { get; set; } = 0.40;
    /// <summary>Vassal-to-overlord war-strength ratio at or below which a
    /// chosen vassalage binds — protection is for the genuinely weaker.</summary>
    public double VassalStrengthRatio { get; set; } = 0.35;
    /// <summary>Income share a vassal ships up each epoch (tribute) —
    /// conserved vassal→overlord flow before the vassal budgets.</summary>
    public double VassalTributeShare { get; set; } = 0.15;
}

/// <summary>Corporate dials (economy/corporations.md): niche detection
/// thresholds, the charter clock and gate, operating shares, and the
/// deaths. Slice G. Name suffixes per niche are catalog data.</summary>
public sealed class CorporateKnobs
{
    /// <summary>Black-book value (demand × price) at a port that reads as
    /// a profitable prohibited niche — the cartel trigger.</summary>
    public double CartelValueFloor { get; set; } = 15.0;
    /// <summary>Share of the home market's black-book value a cartel skims
    /// from buyer wealth per epoch (a conserved transfer).</summary>
    public double CartelSkim { get; set; } = 0.3;
    /// <summary>War-chest credits a merchant faction needs before its
    /// charter fires — an unfunded venture is a stillbirth (cartels are
    /// exempt: they charter on nerve, not works).</summary>
    public double CharterCapitalFloor { get; set; } = 200.0;
    /// <summary>Host charter policy below which no charter is granted (the
    /// merchant faction waits, and grieves).</summary>
    public double CharterOpennessGate { get; set; } = 0.4;
    /// <summary>Consecutive epochs a niche must persist before the charter
    /// graduation fires.</summary>
    public int CharterPersistenceEpochs { get; set; } = 3;
    /// <summary>Extraction potential at an unexploited port that reads as
    /// a deposit niche.</summary>
    public double DepositNichePotential { get; set; } = 0.65;
    /// <summary>Price-over-founding ratio at which a missing producer reads
    /// as an industrial gap.</summary>
    public double FabricationPriceRatio { get; set; } = 2.5;
    /// <summary>Epochs after founding before the lean clock starts — the
    /// build-out grace (funding, hauling the basket, construction, spin-up).</summary>
    public int FoundingGraceEpochs { get; set; } = 4;
    /// <summary>Relative price gradient across an unserved lane that reads
    /// as a freight niche.</summary>
    public double FreightNicheMargin { get; set; } = 0.6;
    /// <summary>Ship-components demand a funded freight line registers at
    /// its home market per epoch — the price signal that hauls its hulls'
    /// makings in (the slice-E lesson applied to corporations).</summary>
    public double FreightPullComponents { get; set; } = 6.0;
    /// <summary>Relation tension at or above which a corp won't bridge two
    /// polities (non-hostility bar; no treaty required —
    /// lane-economics spec §4).</summary>
    public double GateTensionCeiling { get; set; } = 0.7;
    /// <summary>Epoch receipts below which a corporation counts as lean —
    /// enough lean epochs and the niche is dead.</summary>
    public double LeanReceiptsFloor { get; set; } = 1.0;
    /// <summary>Share of corporate credits spent lobbying the aligned
    /// faction per epoch.</summary>
    public double LobbyShare { get; set; } = 0.01;
    /// <summary>Epoch receipts that mint the executive a magnate notable.</summary>
    public double MagnateReceipts { get; set; } = 50.0;
    /// <summary>Facilities one corporation may hold (portfolio cap).</summary>
    public int MaxFacilities { get; set; } = 4;
    /// <summary>Gate-lane pairs a freight line may own.</summary>
    public int MaxGateLanes { get; set; } = 3;
    /// <summary>Legitimacy a polity eats for nationalizing — the domestic
    /// half; the foreign half travels the news graph as stance damage
    /// (slice I: capital sanctions the seizing state).</summary>
    public double NationalizeLegitimacyHit { get; set; } = 0.1;
    /// <summary>Corporate credits over the host treasury at which the
    /// Intent AI reaches for nationalization (a de facto power).</summary>
    public double NationalizeWealthFactor { get; set; } = 2.0;
    /// <summary>Consecutive lean epochs that kill the niche.</summary>
    public int NicheDeathEpochs { get; set; } = 5;
    /// <summary>Piracy exposure per hex of lane length — longer lanes tempt
    /// raiders at thinner cargo (lane-economics spec §5).</summary>
    public double PiracyLengthPerHex { get; set; } = 0.05;
    /// <summary>Posted lane capacity that reads as raid-worthy cargo where
    /// the owner keeps no warships (the pirate-band trigger).</summary>
    public double RaidCapacityFloor { get; set; } = 8.0;
}

/// <summary>Tech dials (economy/technology.md): the tier ladder's cost
/// curve, research input conversion, diffusion rates, and what Astrogation
/// and Life buy per tier. Slice G. Grade ceilings per tier are the Grade
/// system's structural ladder (Grades.TechCeiling).</summary>
public sealed class TechKnobs
{
    /// <summary>Extra inter-port lane reach per Astrogation tier above era
    /// standard, in hexes.</summary>
    public int AstroRangePerTierHexes { get; set; } = 4;
    /// <summary>Extra port service radius per Astrogation tier above era
    /// standard, in hexes — whose ports reach farther, visibly.</summary>
    public int AstroRadiusPerTierHexes { get; set; } = 3;
    /// <summary>Progress points to leave tier 1; each further tier costs
    /// ×ThresholdGrowth more (geometric investment thresholds).</summary>
    public double BaseThreshold { get; set; } = 25.0;
    /// <summary>Research effectiveness multiplier at compute parity with
    /// exotics (compute multiplies, never substitutes).</summary>
    public double ComputeBoost { get; set; } = 0.5;
    /// <summary>Population growth multiplier per Life tier above standard.</summary>
    public double LifeGrowthPerTier { get; set; } = 0.15;
    /// <summary>Research progress per Refined Exotics unit consumed.</summary>
    public double ProgressPerExotic { get; set; } = 1.0;
    /// <summary>Refined Exotics demand a funded research line registers at
    /// the capital per epoch — the price signal that sites exotics labs.</summary>
    public double ResearchPullExotics { get; set; } = 6.0;
    /// <summary>Compute demand the research line registers likewise.</summary>
    public double ResearchPullCompute { get; set; } = 3.0;
    /// <summary>Military/Industrial progress per wrecked hull per world-year
    /// while out-graded wreckage sits in reach (salvage diffusion; full
    /// consumption including precursor digging is slice I).</summary>
    public double SalvagePerHullPerYear { get; set; } = 0.02;
    /// <summary>Ladder cost growth per tier (geometric).</summary>
    public double ThresholdGrowth { get; set; } = 2.0;
    /// <summary>Progress per world-year of full-volume open trade with a
    /// partner ≥2 tiers ahead (capped one tier below the source).</summary>
    public double TradeDiffusionPerYear { get; set; } = 0.15;
    /// <summary>Posted lane capacity at which trade diffusion saturates.</summary>
    public double TradeVolumeSaturation { get; set; } = 10.0;
}

/// <summary>Faction dials (polity/factions-and-government.md): formation
/// thresholds, pressure and appeasement rates, grievance. Slice G. Per-basis
/// budget agendas are catalog data (FactionOps.BasisBudget).</summary>
public sealed class FactionKnobs
{
    /// <summary>Share of the polity's epoch receipts a full-strength faction
    /// demands as appeasement — payouts cap here (no infinite war chests),
    /// and grievance accrues on the unmet fraction.</summary>
    public double AppeasementDemandShare { get; set; } = 0.2;
    /// <summary>How far the official line lurches toward the coup faction's
    /// target when the palace falls.</summary>
    public double CoupIdeologyLurch { get; set; } = 0.5;
    /// <summary>Legitimacy lost by a successful coup.</summary>
    public double CoupLegitimacyHit { get; set; } = 0.15;
    /// <summary>Strength below which a faction dissolves (its trigger has
    /// passed; wealth returns to the segments).</summary>
    public double DissolveStrengthFloor { get; set; } = 0.05;
    /// <summary>Multiplier on legitimacy × enforcement in the graduation
    /// test — how much grip a state gets per point of either. Lower it and
    /// every polity cracks; raise it and factions grumble forever.</summary>
    public double GraduationGripFactor { get; set; } = 4.0;
    /// <summary>Population share an interest needs to coalesce (intolerant
    /// forms raise it — hives barely factionalize).</summary>
    public double FormMinShare { get; set; } = 0.15;
    /// <summary>Mean port distance (as a fraction of colonization reach)
    /// past which a domain counts as frontier.</summary>
    public double FrontierDistanceFraction { get; set; } = 0.5;
    /// <summary>Grievance decay per world-year when fully appeased.</summary>
    public double GrievanceDecayPerYear { get; set; } = 0.008;
    /// <summary>Grievance accrual per world-year of unappeased strength.</summary>
    public double GrievancePerYear { get; set; } = 0.02;
    /// <summary>Mean ideology-axis distance from the official line past
    /// which segments count as dissenters.</summary>
    public double IdeologyGapToForm { get; set; } = 0.25;
    /// <summary>Cap on how far one faction bends the budget or the official
    /// line in one epoch.</summary>
    public double MaxBudgetPressure { get; set; } = 0.35;
    /// <summary>Officer renown × species militancy needed for a military
    /// faction to coalesce.</summary>
    public double MilitaryRenownToForm { get; set; } = 12.0;
    /// <summary>Faction-strength weight of a patron's renown.</summary>
    public double PatronRenownWeight { get; set; } = 0.01;
    /// <summary>Budget/ideology drift toward agendas per world-year at full
    /// strength and tolerance.</summary>
    public double PressureRatePerYear { get; set; } = 0.01;
    /// <summary>Fraction of grievance a crushed revolt keeps (a successful
    /// graduation spends all of it) — repression compounds.</summary>
    public double RevoltGrievanceKeep { get; set; } = 0.75;
    /// <summary>Legitimacy lost crushing a revolt.</summary>
    public double RevoltLegitimacyHit { get; set; } = 0.1;
    /// <summary>Sacral-axis position below which segments read as a faith
    /// movement's base.</summary>
    public double SacralAxisLine { get; set; } = 0.35;
    /// <summary>Faction-strength weight of its wealth share.</summary>
    public double WealthStrengthWeight { get; set; } = 0.1;
}

/// <summary>Character dials (polity/characters.md): mortality, succession
/// fallout, renown magnitudes, dynastic prestige, sparsity caps. Slice G.
/// Species lifespans are structural catalog data (CharacterOps.Lifespan).</summary>
public sealed class CharacterKnobs
{
    /// <summary>Ruler assassination hazard per world-year at full ambition
    /// and zero legitimacy (scales by both).</summary>
    public double AssassinationBasePerYear { get; set; } = 0.02;
    /// <summary>Legitimacy lost outright when a succession has no heir.</summary>
    public double CrisisLegitimacyHit { get; set; } = 0.15;
    /// <summary>Dynastic prestige accrued per world-year of reign.</summary>
    public double DynastyPrestigePerReignYear { get; set; } = 0.01;
    /// <summary>Heir mint age as a fraction of species lifespan.</summary>
    public double HeirMintAgeFraction { get; set; } = 0.25;
    /// <summary>Flat deprecation hazard per world-year for machine minds
    /// (they fork replacements instead of aging).</summary>
    public double MachineDeprecationPerYear { get; set; } = 0.002;
    /// <summary>Living notables a polity carries at most (sparsity, P8).</summary>
    public int MaxNotablesPerPolity { get; set; } = 6;
    /// <summary>Death hazard per world-year at exactly one lifespan (the
    /// age curve rises quadratically past 55% of span to reach this).</summary>
    public double MortalityShapePerYear { get; set; } = 0.15;
    /// <summary>Ruler-prestige legitimacy gain per point of ruler renown +
    /// dynastic prestige (0.5 is the neutral prestige term).</summary>
    public double PrestigePerRenown { get; set; } = 0.02;
    /// <summary>Renown for taking a seat.</summary>
    public double RenownAscension { get; set; } = 2.0;
    /// <summary>Renown for being minted a notable.</summary>
    public double RenownNotable { get; set; } = 5.0;
    /// <summary>Ruler/marshal/commander mint age as a lifespan fraction.</summary>
    public double RulerMintAgeFraction { get; set; } = 0.45;
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
    /// <summary>World-years integrated per step — the integration step,
    /// nothing more (P7). Genesis steps a generation at a time; the live
    /// game steps the same machine fine-grained.</summary>
    public int YearsPerEpoch { get; set; } = 25;
    /// <summary>Default history depth: 40 epochs ≈ 1,000 years.</summary>
    public int EpochCount { get; set; } = 40;
    /// <summary>The world-year length of one generation — the calendar
    /// unit every *Epochs knob counts (frame/time.md §The epoch is a
    /// generation). Fixed at genesis scale: fine-tick stepping lowers
    /// YearsPerEpoch, never this.</summary>
    public int GenerationYears { get; set; } = 25;
    /// <summary>This step's share of a generation (1.0 at genesis scale) —
    /// the factor per-generation intensities scale by each step (P7).</summary>
    public double StepFraction =>
        (double)YearsPerEpoch / System.Math.Max(1, GenerationYears);
}

/// <summary>Genesis-side knobs: the emergence window, plus the native
/// late-emergence schedule (slice H — interpolity/relations.md §Natives).</summary>
public sealed class GenesisKnobs
{
    /// <summary>Latest world-year a polity may enter — staggered emergence
    /// (frame/time.md §Asymmetric emergence).</summary>
    public int EmergenceWindowYears { get; set; } = 500;
    /// <summary>World-year the last pre-spaceflight native's date projects
    /// onto — natives emerge between the polity window and here, order and
    /// spacing preserved (honest narrative compression).</summary>
    public int NativeWindowYears { get; set; } = 900;
    /// <summary>Population size an emerging native people starts with
    /// (its homeworld segment, whoever administers it).</summary>
    public double NativePopulationSize { get; set; } = 1.0;
    /// <summary>Epochs a protectorate policy delays the emergence date —
    /// the reserve buys time (and can turn cage).</summary>
    public int ProtectorateDelayEpochs { get; set; } = 4;
    /// <summary>Epochs an uplift host (Life tier ≥ 2 — the tech gate)
    /// advances the emergence date.</summary>
    public int UpliftAccelerationEpochs { get; set; } = 4;
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
    /// <summary>Decay multiplier per active Depot tier at the stockpile's
    /// port (stage 2, spec §4b) — the controller contract's "stockpile
    /// targets → depots/reserves" mechanism: two depot tiers keep goods
    /// 4× longer at the default 0.5.</summary>
    public double DepotDecayFactor { get; set; } = 0.5;
    /// <summary>Stockpile capacity per good per port tier (spec §4b) —
    /// what a port can bank without dedicated storage.</summary>
    public double StockCapPerPortTier { get; set; } = 100.0;
    /// <summary>Extra stockpile capacity per good per active Depot tier at
    /// the port — depots are how a polity builds deep larders.</summary>
    public double StockCapPerDepotTier { get; set; } = 400.0;

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
    /// <summary>Corp-owned gate toll as a share of the destination price —
    /// the trader→gate-owner flow (lane-economics spec §4).</summary>
    public double GateTollRate { get; set; } = 0.05;
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
    // Economy.TechTierStub retired (slice G): producer tech is per-polity,
    // per-domain — PolityRecord.TechTier via the Tech interface.
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
    public int MaxPortTier { get; set; } = 3;
    /// <summary>Homeworld ports establish at this tier at emergence — a
    /// civilization at spaceflight is past "outpost".</summary>
    public int HomeworldPortTier { get; set; } = 2;
    /// <summary>Facilities a port's domain supports per port tier — the
    /// construction cap (development concentrates before it sprawls).</summary>
    public int FacilitiesPerPortTier { get; set; } = 5;
    /// <summary>Minimum siting score × price signal before anything gets
    /// built — don't build junk.</summary>
    public double ConstructionScoreFloor { get; set; } = 0.12;
    /// <summary>How decisively extraction must out-value farmland before a
    /// colony founds on it instead of farming — food security's premium.</summary>
    public double FoodSecurityPremium { get; set; } = 1.25;
    /// <summary>Gate slots a port hosts per tier — the lane-degree cap:
    /// hub ports must grow before they fan out (lane-economics spec §1).</summary>
    public int GateSlotsPerPortTier { get; set; } = 2;
    /// <summary>Max lane length linkable by a tier-1 gate pair, in hexes.</summary>
    public int GateReachTier1Hexes { get; set; } = 8;
    public int GateReachTier2Hexes { get; set; } = 16;
    public int GateReachTier3Hexes { get; set; } = 28;
    /// <summary>Condition below which a gate stops functioning and its
    /// lane goes dead (war damage severs without touching the port).</summary>
    public double GateFunctionalCondition { get; set; } = 0.25;
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
    /// <summary>Colony-score penalty per foreign polity whose domain the
    /// new port's service area would overlap — settling someone's sphere
    /// must be outweighed by real riches (slice H: contiguous borders).</summary>
    public double EncroachmentPenalty { get; set; } = 1.5;
    /// <summary>Development points to raise a port: cost = base × current tier.</summary>
    public double PortUpgradeCostBase { get; set; } = 40.0;
    /// <summary>World-years to raise a port one tier.</summary>
    public double PortUpgradeYears { get; set; } = 5.0;
    /// <summary>Alloys drawn per world-year per current tier while a port
    /// raise is under construction.</summary>
    public double PortUpgradeAlloysPerYearPerTier { get; set; } = 2.0;
    /// <summary>Machinery drawn per world-year per current tier during a
    /// port raise.</summary>
    public double PortUpgradeMachineryPerYearPerTier { get; set; } = 1.0;
    /// <summary>Refined Exotics drawn per world-year per current tier during
    /// a port raise.</summary>
    public double PortUpgradeExoticsPerYearPerTier { get; set; } = 0.25;
    public double HomeworldSegmentSize { get; set; } = 3.0;
    public double ColonySegmentSize { get; set; } = 0.5;
    /// <summary>Logistic population growth per world-year toward the port-tier cap.</summary>
    public double SegmentGrowthPerYear { get; set; } = 0.01;
    /// <summary>Port population cap = port tier × this, shared across segments.</summary>
    public double SegmentCapPerTier { get; set; } = 2.0;
    /// <summary>A direct lane is redundant while the network path is within
    /// this factor of the direct distance (lane-economics spec §3).</summary>
    public double DetourFactor { get; set; } = 1.8;
    /// <summary>Used/capacity ratio at which a lane counts as saturated.</summary>
    public double ExpressSaturationFloor { get; set; } = 0.9;
    /// <summary>Consecutive saturated Markets steps after which a congested
    /// corridor earns a direct express bypass despite the detour rule.</summary>
    public int SaturatedEpochsForExpress { get; set; } = 3;
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
    /// <summary>Off-lane convoy speed, hexes per world-year — an expedition's
    /// crossing time is its off-lane distance over this rate (a 12-hex leg
    /// takes two years). Founding runs in world-time, never same-step.</summary>
    public double ExpeditionHexesPerYear { get; set; } = 6.0;
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
    /// <summary>World-years to build one Medium hull — the planner's hull-
    /// batch duration base, scaled by the design's component draw against
    /// Medium (spec §3).</summary>
    public double HullBuildYearsBase { get; set; } = 1.5;
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
    /// <summary>Flat tariff rate the AI levies on foreign goods at zero
    /// openness (scaled down by openness; trade pacts cut what remains —
    /// the teeth the PactTariffFactor bites on).</summary>
    public double BaseTariffRate { get; set; } = 0.15;
    /// <summary>Species openness below which narcotics are prohibited.</summary>
    public double NarcoticsProhibitBelowOpenness { get; set; } = 0.35;
    /// <summary>Species openness below which narcotics are restricted.</summary>
    public double NarcoticsRestrictBelowOpenness { get; set; } = 0.55;
    /// <summary>Entries the planner may schedule in one standing plan — the
    /// horizon's project budget (spec §3).</summary>
    public int MaxPlanEntries { get; set; } = 16;
    /// <summary>Base score of a port-raise plan entry, divided by the port's
    /// current tier — the standing bias toward deepening young ports first.</summary>
    public double PortRaisePlanScore { get; set; } = 0.5;
}
