using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>What an actor currently believes about the world — the only input
/// a controller may read (P3). Perfect-information stub, built from truth each
/// Perception phase; compressed belief replaces it in Slice I. The contract
/// holds either way: Decide sees the view, never global state.</summary>
/// <summary>What a controller sees of one own ship design — enough to key
/// ShipbuildingPriorities by design id (fleets/ships-and-fleets.md).</summary>
public sealed record DesignBrief(int DesignId, ShipRole Role, ShipSize Size, int Mark);

/// <summary>What a polity perceives of a corporation it charters — enough
/// to notice a de facto power (economy/corporations.md §Influence).</summary>
public sealed record CorporateBrief(int CorpId, string Name, double Credits);

/// <summary>What a polity perceives of one relation it holds — the gauges
/// and the table state its diplomacy works (interpolity/relations.md).
/// Perfect-info stub: reads the true relation until slice I stales it.</summary>
public sealed record RelationBrief(
    int OtherPolityId, double Warmth, double Tension, TreatyRung Rung,
    TreatyRung OfferedRung, int OfferedById, int LiveClaimsHeld,
    int LiveClaimsAgainst, double IdeologyGap, int EpochsAtRung,
    double OtherStrength, int VassalPolityId, bool OtherDynastic,
    int DynasticTies, IReadOnlyList<CasusBelliOption> CasusBelli,
    double OtherDefensiveStrength,
    IReadOnlyList<WarObjectiveSpec> ObjectiveCandidates,
    double OverlapShare);

/// <summary>One viable declared goal on the menu (war.md §Causes).</summary>
public sealed record CasusBelliOption(CasusBelli Cause, int SubjectId);

/// <summary>What a belligerent perceives of a war it is in — the surface
/// settlement decisions read (perfect-info stub until slice I; wars can
/// then run past their rational end).</summary>
public sealed record WarBrief(
    int WarId, string Name, int OtherLeaderId, bool OnAttackerSide,
    bool IsLeader, double OwnSideExhaustion, double OwnSideStrengthShare,
    int ObjectivesTaken, int ObjectivesTotal);

/// <summary>An open lane from one of the observer's ports to a port it can
/// SEE is infected (plague on the doorstep is locally observable) — the
/// QuarantineAct's menu (slice I).</summary>
public sealed record QuarantineCandidate(
    int LaneId, int OwnPortId, int InfectedPortId);

public sealed class PerceptionView
{
    private static readonly IReadOnlyList<ColonyCandidate> NoCandidates =
        new ColonyCandidate[0];
    private static readonly IReadOnlyList<DesignBrief> NoDesigns =
        new DesignBrief[0];
    private static readonly IReadOnlyList<CorporateBrief> NoCorporations =
        new CorporateBrief[0];
    private static readonly IReadOnlyList<RelationBrief> NoRelations =
        new RelationBrief[0];
    private static readonly IReadOnlyList<WarBrief> NoWars = new WarBrief[0];
    private static readonly IReadOnlyList<QuarantineCandidate> NoFrontier =
        new QuarantineCandidate[0];

    public int SelfId { get; }
    public int WorldYear { get; }
    /// <summary>Polities this actor knows exist (perfect-info stub: all entered).</summary>
    public IReadOnlyList<int> KnownPolityIds { get; }
    /// <summary>Own accrued expansion treasury (polities; 0 otherwise).</summary>
    public double ExpansionPoints { get; }
    /// <summary>Scored colonization targets within reach, best first
    /// (mechanical enumeration; choosing is the controller's).</summary>
    public IReadOnlyList<ColonyCandidate> ColonyCandidates { get; }
    /// <summary>The actor's own species profile — an actor perceives its own
    /// society (null for non-polities and shape-only test skeletons).
    /// Embodiment facts live here; decision personality does NOT — that is
    /// the temperament composition below.</summary>
    public SpeciesProfile? SelfSpecies { get; }
    /// <summary>The temperament composition (slice G): species × official
    /// ideology × ruler × faction pressure, weighted by government form.
    /// The ONLY personality Intent may read — fixed species vectors retired.</summary>
    public Temperament SelfTemperament { get; }
    /// <summary>Own port count — scales standing policy magnitudes
    /// (stockpile targets and the like).</summary>
    public int OwnPortCount { get; }
    /// <summary>Size-weighted mean subsistence across the realm's segments
    /// (1.0 when unpeopled) — the consolidation signal: a starving realm
    /// digests before it expands.</summary>
    public double RealmSubsistence { get; }
    /// <summary>Own current-mark ship designs — the ids
    /// ShipbuildingPriorities are keyed by (polities; empty otherwise).</summary>
    public IReadOnlyList<DesignBrief> OwnDesigns { get; }
    /// <summary>Colony hulls sitting in own Reserve-posture fleets — an
    /// expedition needs a physical convoy (fleets doc; 0 for non-polities).</summary>
    public int ColonyHullsAvailable { get; }
    /// <summary>Own liquid treasury — the yardstick a hosted corporation's
    /// wealth is measured against (slice G).</summary>
    public double OwnCredits { get; }
    /// <summary>Corporations this polity charters, by corp registry id —
    /// the nationalization act's targets (empty otherwise).</summary>
    public IReadOnlyList<CorporateBrief> HostedCorporations { get; }
    /// <summary>The polity's relations, one brief per met polity in
    /// relation-registry order (empty otherwise) — what the diplomatic
    /// postures and treaty acts are written from (slice H).</summary>
    public IReadOnlyList<RelationBrief> Relations { get; }
    /// <summary>Own headline war weight (strike + sustained, readiness-
    /// discounted) — what vassal choices size threats against (slice H).</summary>
    public double OwnStrength { get; }
    /// <summary>Own throne is a lineage — dynastic instruments bind only
    /// between such forms (slice H).</summary>
    public bool SelfDynastic { get; }
    /// <summary>Wars this polity is in, war-id order (empty at peace).</summary>
    public IReadOnlyList<WarBrief> Wars { get; }
    /// <summary>Open lanes from own ports to visibly infected ports —
    /// what a QuarantineAct closes (slice I; empty when healthy).</summary>
    public IReadOnlyList<QuarantineCandidate> PlagueFrontier { get; }

    public PerceptionView(int selfId, int worldYear, IReadOnlyList<int> knownPolityIds,
                          double expansionPoints = 0,
                          IReadOnlyList<ColonyCandidate>? colonyCandidates = null,
                          SpeciesProfile? selfSpecies = null,
                          int ownPortCount = 0,
                          double realmSubsistence = 1.0,
                          IReadOnlyList<DesignBrief>? ownDesigns = null,
                          int colonyHullsAvailable = 0,
                          Temperament? selfTemperament = null,
                          double ownCredits = 0,
                          IReadOnlyList<CorporateBrief>? hostedCorporations = null,
                          IReadOnlyList<RelationBrief>? relations = null,
                          double ownStrength = 0,
                          bool selfDynastic = false,
                          IReadOnlyList<WarBrief>? wars = null,
                          IReadOnlyList<QuarantineCandidate>? plagueFrontier
                              = null)
    {
        PlagueFrontier = plagueFrontier ?? NoFrontier;
        OwnCredits = ownCredits;
        HostedCorporations = hostedCorporations ?? NoCorporations;
        Relations = relations ?? NoRelations;
        OwnStrength = ownStrength;
        SelfDynastic = selfDynastic;
        Wars = wars ?? NoWars;
        SelfId = selfId;
        WorldYear = worldYear;
        KnownPolityIds = knownPolityIds;
        ExpansionPoints = expansionPoints;
        ColonyCandidates = colonyCandidates ?? NoCandidates;
        SelfSpecies = selfSpecies;
        OwnPortCount = ownPortCount;
        RealmSubsistence = realmSubsistence;
        OwnDesigns = ownDesigns ?? NoDesigns;
        ColonyHullsAvailable = colonyHullsAvailable;
        SelfTemperament = selfTemperament
            ?? (selfSpecies != null
                ? Temperament.FromSpecies(selfSpecies) : Temperament.Neutral);
    }
}

/// <summary>Root of the per-actor-kind standing-policy records
/// (frame/controller-contract.md). Applied mechanically by other phases on
/// subsequent steps — never by the controller itself.</summary>
public abstract record PolicySet;

/// <summary>One controller answer: standing policies + discrete acts
/// (frame/simulation-flow.md Move 1).</summary>
public sealed record ControllerDecision(PolicySet Policies, IReadOnlyList<Act> Acts);

/// <summary>The single decision interface (P2): perceived state in, intents
/// out. The genesis AI, a smarter AI, and the player are interchangeable
/// behind it; nothing inside the sim may care who is driving.</summary>
public interface IController
{
    ControllerDecision Decide(PerceptionView perceived);
}

/// <summary>Inert controller: default policies, no acts — the do-nothing
/// baseline for tests and not-yet-modeled actor kinds.</summary>
public sealed class TrivialController : IController
{
    private static readonly IReadOnlyList<Act> NoActs = new Act[0];

    public ControllerDecision Decide(PerceptionView perceived) =>
        new ControllerDecision(PolityPolicies.Default, NoActs);
}

/// <summary>The genesis expansion AI: species-flavored standing policies;
/// founds toward the top colony candidate whenever the expansion treasury
/// affords it. Constructed with the config — its own policy costs, not world
/// state; the P2 contract (decide from the view alone) holds.</summary>
public sealed class GenesisController : IController
{
    private static readonly IReadOnlyList<Act> NoActs = new Act[0];
    private readonly EpochSimConfig _config;

    public GenesisController(EpochSimConfig config)
    {
        _config = config;
    }

    public ControllerDecision Decide(PerceptionView perceived)
    {
        var policies = PoliciesFor(perceived);
        var acts = new List<Act>();
        if (perceived.ExpansionPoints >= _config.Expansion.ColonyCost
            && perceived.RealmSubsistence >= _config.Controller.RealmHungerGate
            && perceived.ColonyCandidates.Count > 0
            // a site must be worth its provocation: when every option in
            // reach is net-negative (encroachment outweighs the riches),
            // a wise realm consolidates instead of settling trouble
            && perceived.ColonyCandidates[0].Score > 0
            && perceived.ColonyHullsAvailable > 0)   // founding needs a convoy
            acts.Add(new FoundColonyAct(perceived.SelfId,
                                        perceived.ColonyCandidates[0].Target));
        // vassalage's foreign-policy lock: the bound run no diplomacy of
        // their own (interpolity/relations.md §Vassalage)
        bool selfBound = false;
        foreach (var rel in perceived.Relations)
            if (rel.VassalPolityId == perceived.SelfId) selfBound = true;
        // the treaty ladder: climb toward friends one rung at a time, answer
        // standing offers, and tear up rungs with the hostile — warmth gates
        // ascent (interpolity/relations.md §Treaties)
        if (!selfBound)
            foreach (var rel in perceived.Relations)
            {
                if (rel.VassalPolityId >= 0) continue;   // bonded table is closed
                var stance = StanceOf(rel);
                if (rel.Rung > TreatyRung.None
                    && stance == DiplomaticPosture.Hostile)
                {
                    acts.Add(new TreatyAct(perceived.SelfId, rel.OtherPolityId,
                        (int)rel.Rung, TreatyVerb.Break));
                    continue;
                }
                if (stance < DiplomaticPosture.Cordial) continue;
                if (rel.OfferedRung > rel.Rung
                    && rel.OfferedById == rel.OtherPolityId
                    && rel.Warmth >= EffectiveGate(rel, rel.OfferedRung)
                    && FederationTermsAgreeable(perceived, rel, rel.OfferedRung))
                    acts.Add(new TreatyAct(perceived.SelfId, rel.OtherPolityId,
                        (int)rel.OfferedRung, TreatyVerb.Accept));
                else if (rel.OfferedRung == TreatyRung.None
                    && rel.Rung < TreatyRung.Federation
                    && rel.Warmth >= EffectiveGate(rel, rel.Rung + 1)
                    && FederationTermsAgreeable(perceived, rel, rel.Rung + 1))
                    acts.Add(new TreatyAct(perceived.SelfId, rel.OtherPolityId,
                        (int)(rel.Rung + 1), TreatyVerb.Offer));
            }
        // dynastic instruments: one warm lineage courts another — the
        // marriage buys warmth now and seeds a claim two reigns later
        // (interpolity/relations.md §Dynastic instruments)
        if (!selfBound && perceived.SelfDynastic)
            foreach (var rel in perceived.Relations)
            {
                if (rel.VassalPolityId >= 0 || !rel.OtherDynastic
                    || rel.DynasticTies >= 3
                    || StanceOf(rel) < DiplomaticPosture.Cordial
                    || rel.Warmth < 0.45) continue;
                var instrument = perceived.OwnStrength
                        < rel.OtherStrength * 0.5
                    ? DynasticInstrument.Wardship   // the weaker sends a ward
                    : DynasticInstrument.Marriage;
                acts.Add(new DynasticInstrumentAct(perceived.SelfId,
                    rel.OtherPolityId, instrument));
                break;   // one wedding a generation is plenty
            }
        // plague at the gates: close every open lane from a healthy own
        // port to a visibly infected one (the QuarantineAct's consequence
        // is Resolution's — slice I)
        foreach (var q in perceived.PlagueFrontier)
            acts.Add(new QuarantineAct(perceived.SelfId, q.LaneId));
        // suing for peace: a leader concedes when the war is visibly lost —
        // exhaustion deep or the navy gone (perceived cost of continuing
        // exceeds the settlement; staleness arrives with slice I)
        foreach (var war in perceived.Wars)
            if (war.IsLeader && (war.OwnSideExhaustion > 0.7
                                 || war.OwnSideStrengthShare < 0.3))
                acts.Add(new SettlementResponseAct(perceived.SelfId,
                                                   war.WarId, Accept: true));
        // war: tension discharges through a casus belli (war.md) — the
        // loaded border with a viable declared goal, priced against the
        // defender's whole coalition; one war at a time keeps a realm's
        // story legible
        if (!selfBound && perceived.Wars.Count == 0)
        {
            RelationBrief? front = null;
            foreach (var rel in perceived.Relations)
            {
                if (rel.CasusBelli.Count == 0
                    || rel.Tension < _config.War.WarTensionFloor) continue;
                if (rel.Tension
                    * (0.5 + perceived.SelfTemperament.Militancy)
                    < _config.War.WarAppetiteThreshold) continue;
                if (perceived.OwnStrength < _config.War.AttackStrengthRatio
                    * rel.OtherDefensiveStrength) continue;
                if (front == null || rel.Tension > front.Tension) front = rel;
            }
            if (front != null)
            {
                var pick = PickCause(front.CasusBelli);
                // hatred deep enough turns any cause into a war of
                // annihilation: no surrender accepted, everything taken
                bool hatred = front.Tension * (1.0 - front.Warmth)
                        >= _config.War.AnnihilationHatred
                    && front.LiveClaimsHeld + front.LiveClaimsAgainst >= 2;
                acts.Add(new DeclareWarAct(perceived.SelfId,
                    front.OtherPolityId, (int)pick.Cause, pick.SubjectId,
                    PickObjectives(front, pick.Cause, pick.SubjectId, hatred),
                    (int)(hatred ? WarDemand.Annihilation
                        : DemandFor(pick.Cause))));
            }
        }
        // the protection market: a genuinely outmatched polity facing a
        // hostile giant offers itself to its strongest friend (§Vassalage)
        if (!selfBound && perceived.Relations.Count > 0)
        {
            double worstThreat = 0;
            foreach (var rel in perceived.Relations)
                if (StanceOf(rel) <= DiplomaticPosture.Wary
                    && rel.OtherStrength > perceived.OwnStrength * 2
                    && rel.OtherStrength > worstThreat)
                    worstThreat = rel.OtherStrength;
            if (worstThreat > 0)
            {
                int protector = -1;
                double protectorStrength = 0;
                foreach (var rel in perceived.Relations)
                    if (rel.VassalPolityId < 0
                        && StanceOf(rel) >= DiplomaticPosture.Cordial
                        && rel.OtherStrength >= worstThreat
                        && rel.OtherStrength > protectorStrength)
                    {
                        protector = rel.OtherPolityId;
                        protectorStrength = rel.OtherStrength;
                    }
                if (protector >= 0)
                    acts.Add(new VassalageAct(perceived.SelfId, protector,
                                              IsDemand: false));
            }
        }
        // a chartered corporation that out-wealths the state is a de facto
        // power; the counter-move is nationalization (corporations.md)
        foreach (var corp in perceived.HostedCorporations)
            if (corp.Credits > perceived.OwnCredits
                    * _config.Corporate.NationalizeWealthFactor
                && perceived.OwnCredits > 0)
            {
                acts.Add(new NationalizeAct(perceived.SelfId, corp.CorpId));
                break;   // one scandal per epoch is plenty
            }
        return new ControllerDecision(policies,
            acts.Count == 0 ? NoActs : acts);
    }

    /// <summary>Default policies plus a temperament-derived law code (closed
    /// societies prohibit narcotics, guarded ones restrict them —
    /// jurisdiction-relative legality, commodities.md) and reserve targets
    /// scaling with the realm (ControllerKnobs). Personality comes from the
    /// temperament composition (slice G), never a fixed species vector — a
    /// nation's laws liberalize as its politics do. Deterministic from the view.</summary>
    private PolityPolicies PoliciesFor(PerceptionView perceived)
    {
        var knobs = _config.Controller;
        var policies = PolityPolicies.Default;
        var temperament = perceived.SelfTemperament;
        bool atWar = perceived.Wars.Count > 0;
        // guns before butter: a belligerent shifts development and
        // expansion spending into the military line (slice H eyeball:
        // war diverts large portions of the economy)
        if (atWar)
        {
            var b = policies.Budget;
            double shift = _config.War.WarBudgetMilitaryShift;
            double fromDev = System.Math.Min(
                System.Math.Max(0.0, b.Development - 0.05), shift * 0.6);
            double fromExp = System.Math.Min(
                System.Math.Max(0.0, b.Expansion - 0.02), shift * 0.4);
            policies = policies with
            {
                Budget = new BudgetWeights(
                    Development: b.Development - fromDev,
                    Military: b.Military + fromDev + fromExp,
                    Research: b.Research,
                    Expansion: b.Expansion - fromExp,
                    Appeasement: b.Appeasement,
                    Reserves: b.Reserves),
            };
        }
        // the research split follows temperament: hawks fund the arsenal,
        // expansionists the astrogators; the rest splits industry and life
        double militarySplit = 0.10 + 0.20 * temperament.Militancy;
        double astroSplit = 0.20 + 0.20 * temperament.Expansionism;
        double lifeSplit = 0.15;
        policies = policies with
        {
            Research = new ResearchSplit(
                Industrial: 1.0 - militarySplit - astroSplit - lifeSplit,
                Military: militarySplit, Astrogation: astroSplit,
                Life: lifeSplit),
        };
        // the native policy follows the composition: the open uplift or
        // integrate, the militant exploit, the rest hold the reserve
        // (interpolity/relations.md §Natives — ideology-weighted)
        policies = policies with
        {
            NativePolicy = temperament.Openness >= 0.70 ? NativePolicy.Uplift
                : temperament.Openness >= 0.55 ? NativePolicy.Integrate
                : temperament.Militancy >= 0.55 ? NativePolicy.Exploit
                : NativePolicy.Protectorate,
        };
        // the tariff wall scales with insularity — open societies trade
        // near-free, closed ones tax the foreign; trade pacts cut what
        // stands (the PactTariffFactor's teeth)
        double tariffRate = knobs.BaseTariffRate * (1.0 - temperament.Openness);
        if (tariffRate > 0.005)
        {
            var tariffs = new Dictionary<int, double>();
            for (int g = 0; g < Substrate.Goods.All.Count; g++)
                tariffs[g] = tariffRate;
            policies = policies with { TariffSchedule = tariffs };
        }
        if (perceived.SelfSpecies != null
            && temperament.Openness < knobs.NarcoticsRestrictBelowOpenness)
            policies = policies with
            {
                LawCode = new Dictionary<int, LegalityLevel>
                {
                    [(int)Substrate.GoodId.Narcotics] =
                        temperament.Openness < knobs.NarcoticsProhibitBelowOpenness
                            ? LegalityLevel.Prohibited
                            : LegalityLevel.Restricted,
                },
            };
        if (perceived.OwnPortCount > 0)
        {
            // polity procurement (market-geography.md participants): food
            // security for everyone, construction materials banked (market
            // leftovers never hold a whole build basket at once), war
            // materiel by temperament
            var targets = new Dictionary<int, double>
            {
                [(int)Substrate.GoodId.Provisions] =
                    knobs.ProvisionsReservePerPort * perceived.OwnPortCount,
                [(int)Substrate.GoodId.Alloys] =
                    knobs.AlloysReservePerPort * perceived.OwnPortCount,
                [(int)Substrate.GoodId.Machinery] =
                    knobs.MachineryReservePerPort * perceived.OwnPortCount,
                [(int)Substrate.GoodId.Composites] =
                    knobs.CompositesReservePerPort * perceived.OwnPortCount,
                // the quartermaster's stores: fleet upkeep falls back on
                // these where frontier markets hold no ship parts
                [(int)Substrate.GoodId.ShipComponents] =
                    knobs.ShipPartsReservePerPort * perceived.OwnPortCount,
                [(int)Substrate.GoodId.Fuel] =
                    knobs.FuelReservePerPort * perceived.OwnPortCount,
            };
            double militancy = temperament.Militancy;
            if (militancy > knobs.MilitancyReserveGate)
                targets[(int)Substrate.GoodId.Armaments] =
                    militancy * knobs.ArmamentsPerPortPerMilitancy
                    * perceived.OwnPortCount;
            if (atWar)
            {
                // mobilization: the quartermaster corners armaments, ship
                // parts, and fuel — real purchases that divert the markets
                double surge = _config.War.MobilizationFactor;
                targets[(int)Substrate.GoodId.Armaments] = System.Math.Max(
                    targets.TryGetValue((int)Substrate.GoodId.Armaments,
                        out double arms) ? arms : 0,
                    knobs.ArmamentsPerPortPerMilitancy
                    * perceived.OwnPortCount * 0.5) * surge;
                targets[(int)Substrate.GoodId.ShipComponents] *= surge;
                targets[(int)Substrate.GoodId.Fuel] *= surge;
            }
            policies = policies with { StockpileTargets = targets };
        }
        if (perceived.Relations.Count > 0)
        {
            // the standing stance per met polity: net warmth − tension in
            // five buckets — what treaty seeking and the war appetite read
            var postures = new Dictionary<int, DiplomaticPosture>();
            foreach (var rel in perceived.Relations)
                postures[rel.OtherPolityId] = StanceOf(rel);
            policies = policies with { DiplomaticPostures = postures };
        }
        if (perceived.OwnDesigns.Count > 0)
        {
            // the yard queue by temperament: everyone hauls; a realm without
            // a colony convoy ready keeps one building whenever it means to
            // expand; warships by militancy (doctrine flavors, not war — H)
            double militancy = temperament.Militancy;
            var builds = new Dictionary<int, double>();
            foreach (var brief in perceived.OwnDesigns)
                switch (brief.Role)
                {
                    case ShipRole.Freight:
                        // wartime yards build warships, not haulers
                        builds[brief.DesignId] = atWar ? 0.3 : 1.0;
                        break;
                    case ShipRole.Colony:
                        builds[brief.DesignId] = atWar ? 0.02
                            : perceived.ColonyHullsAvailable == 0 ? 0.6 : 0.05;
                        break;
                    case ShipRole.Scout:
                        builds[brief.DesignId] = 0.1;
                        break;
                    case ShipRole.Escort:
                        builds[brief.DesignId] = atWar ? 1.0
                            : 0.5 * militancy;
                        break;
                    case ShipRole.Line:
                        builds[brief.DesignId] = atWar ? 1.0
                            : 0.35 * militancy;
                        break;
                }
            policies = policies with { ShipbuildingPriorities = builds };
        }
        return policies;
    }

    /// <summary>The stock AI's cause priority: dynastic and liberation
    /// causes over economics over ideology over the bare spark.</summary>
    private static readonly CasusBelli[] CausePriority =
    {
        CasusBelli.SuccessionClaim, CasusBelli.Liberation,
        CasusBelli.VassalSecession, CasusBelli.Expulsion,
        CasusBelli.ResourceSeizure, CasusBelli.GrievanceDischarge,
        CasusBelli.Crusade, CasusBelli.ChokepointControl,
        CasusBelli.PunitiveInterdiction, CasusBelli.Containment,
        CasusBelli.BorderIncident,
    };

    private static CasusBelliOption PickCause(
        IReadOnlyList<CasusBelliOption> menu)
    {
        foreach (var cause in CausePriority)
            foreach (var option in menu)
                if (option.Cause == cause) return option;
        return menu[0];
    }

    /// <summary>Objectives per aim: an expulsion war targets exactly the
    /// port that came to us; territorial causes take ports, punitive ones
    /// cut lanes; annihilation takes everything on the list; the navy is
    /// always on it.</summary>
    private static IReadOnlyList<WarObjectiveSpec> PickObjectives(
        RelationBrief front, CasusBelli cause, int subjectId, bool hatred)
    {
        var specs = new List<WarObjectiveSpec>();
        if (cause == CasusBelli.Expulsion && subjectId >= 0)
            specs.Add(new WarObjectiveSpec(WarObjectiveType.CapturePort,
                                           subjectId));
        bool territorial = hatred || cause is CasusBelli.ResourceSeizure
            or CasusBelli.ChokepointControl or CasusBelli.Liberation
            or CasusBelli.SuccessionClaim or CasusBelli.GrievanceDischarge;
        int ports = specs.Count;
        int portCap = hatred ? 3 : 2;
        foreach (var candidate in front.ObjectiveCandidates)
            switch (candidate.Type)
            {
                case WarObjectiveType.CapturePort
                    when territorial && ports < portCap
                         && candidate.TargetId != subjectId:
                    specs.Add(candidate);
                    ports++;
                    break;
                case WarObjectiveType.BlockadeLane
                    when hatred || cause is CasusBelli.PunitiveInterdiction
                        or CasusBelli.ResourceSeizure
                        or CasusBelli.Containment:
                    specs.Add(candidate);
                    break;
                case WarObjectiveType.DestroyFleet:
                    specs.Add(candidate);
                    break;
            }
        return specs;
    }

    private static WarDemand DemandFor(CasusBelli cause) => cause switch
    {
        CasusBelli.SuccessionClaim => WarDemand.Vassalize,
        CasusBelli.VassalSecession => WarDemand.Independence,
        CasusBelli.ResourceSeizure or CasusBelli.ChokepointControl
            or CasusBelli.Liberation or CasusBelli.GrievanceDischarge
            or CasusBelli.Expulsion
            => WarDemand.CedeObjectives,
        _ => WarDemand.Reparations,
    };

    /// <summary>The warmth gate for a rung, discounted at the top rung by
    /// entanglement: interleaved friendly borders push toward fusion.</summary>
    private double EffectiveGate(RelationBrief rel, TreatyRung rung)
    {
        double gate = RelationsOps.TreatyGate(_config, rung);
        if (rung == TreatyRung.Federation)
            gate -= _config.Relations.FederationOverlapDiscount
                    * rel.OverlapShare;
        return gate;
    }

    /// <summary>Rungs below federation carry no extra conditions; the top
    /// one asks what the controller can see of the merge gate — ideology
    /// compatibility, a sustained alliance, its own openness (Resolution
    /// verifies the rest on truth).</summary>
    private bool FederationTermsAgreeable(PerceptionView perceived,
                                          RelationBrief rel, TreatyRung rung)
    {
        if (rung != TreatyRung.Federation) return true;
        var knobs = _config.Relations;
        return rel.Rung == TreatyRung.DefenseAlliance
               && rel.EpochsAtRung >= knobs.FederationAllianceEpochs
               && rel.IdeologyGap <= knobs.FederationIdeologyGapMax
               // its own half of the pair-mean gate Resolution verifies —
               // a warier partner still offers when a friend can carry it
               && perceived.SelfTemperament.Openness
                  >= knobs.FederationOpennessFloor * 0.5;
    }

    /// <summary>Net warmth − tension mapped to the five-stance scale
    /// (structural controller behavior, not a knob).</summary>
    private static DiplomaticPosture StanceOf(RelationBrief rel)
    {
        double net = rel.Warmth - rel.Tension;
        return net <= -0.35 ? DiplomaticPosture.Hostile
            : net <= -0.10 ? DiplomaticPosture.Wary
            : net < 0.15 ? DiplomaticPosture.Neutral
            : net < 0.40 ? DiplomaticPosture.Cordial
            : DiplomaticPosture.Friendly;
    }
}
