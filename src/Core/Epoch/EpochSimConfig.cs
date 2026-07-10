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
    public double WarTensionFloor { get; set; } = 0.55;
    /// <summary>Declaration gate: tension × (0.5 + composed militancy)
    /// must clear this — the escalation-∝-tension dial.</summary>
    public double WarAppetiteThreshold { get; set; } = 0.60;
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
    /// <summary>Fractional tension-target damping under a non-aggression
    /// rung or above — the rung's teeth (spark de-escalation is war.md's).</summary>
    public double NonAggressionDamping { get; set; } = 0.30;
    /// <summary>Tariff multiplier between trade-pact partners — the tariff
    /// cut that is the first rung's teeth.</summary>
    public double PactTariffFactor { get; set; } = 0.40;
    /// <summary>Epochs a defense alliance must stand before the federation
    /// gate opens — sustained alliance, not a fling.</summary>
    public int FederationAllianceEpochs { get; set; } = 4;
    /// <summary>Both cohesions must sit at or above this to fuse — troubled
    /// realms don't merge, they fracture.</summary>
    public double FederationCohesionFloor { get; set; } = 0.55;
    /// <summary>Official-ideology gap above which the merge is off.</summary>
    public double FederationIdeologyGapMax { get; set; } = 0.20;
    /// <summary>Composed openness both sides need to contemplate fusion.</summary>
    public double FederationOpennessFloor { get; set; } = 0.50;
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
    /// <summary>Legitimacy a polity eats for nationalizing (the reputation
    /// damage stub until slice I's news).</summary>
    public double NationalizeLegitimacyHit { get; set; } = 0.1;
    /// <summary>Corporate credits over the host treasury at which the
    /// Intent AI reaches for nationalization (a de facto power).</summary>
    public double NationalizeWealthFactor { get; set; } = 2.0;
    /// <summary>Consecutive lean epochs that kill the niche.</summary>
    public int NicheDeathEpochs { get; set; } = 5;
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
    /// <summary>World-years integrated per generational step: one epoch ≈ a generation.</summary>
    public int YearsPerEpoch { get; set; } = 25;
    /// <summary>Default history depth: 40 epochs ≈ 1,000 years.</summary>
    public int EpochCount { get; set; } = 40;
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
    /// <summary>Flat tariff rate the AI levies on foreign goods at zero
    /// openness (scaled down by openness; trade pacts cut what remains —
    /// the teeth the PactTariffFactor bites on).</summary>
    public double BaseTariffRate { get; set; } = 0.15;
    /// <summary>Species openness below which narcotics are prohibited.</summary>
    public double NarcoticsProhibitBelowOpenness { get; set; } = 0.35;
    /// <summary>Species openness below which narcotics are restricted.</summary>
    public double NarcoticsRestrictBelowOpenness { get; set; } = 0.55;
}
