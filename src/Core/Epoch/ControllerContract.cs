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

public sealed class PerceptionView
{
    private static readonly IReadOnlyList<ColonyCandidate> NoCandidates =
        new ColonyCandidate[0];
    private static readonly IReadOnlyList<DesignBrief> NoDesigns =
        new DesignBrief[0];

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

    public PerceptionView(int selfId, int worldYear, IReadOnlyList<int> knownPolityIds,
                          double expansionPoints = 0,
                          IReadOnlyList<ColonyCandidate>? colonyCandidates = null,
                          SpeciesProfile? selfSpecies = null,
                          int ownPortCount = 0,
                          double realmSubsistence = 1.0,
                          IReadOnlyList<DesignBrief>? ownDesigns = null,
                          int colonyHullsAvailable = 0,
                          Temperament? selfTemperament = null)
    {
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
        if (perceived.ExpansionPoints >= _config.Expansion.ColonyCost
            && perceived.RealmSubsistence >= _config.Controller.RealmHungerGate
            && perceived.ColonyCandidates.Count > 0
            && perceived.ColonyHullsAvailable > 0)   // founding needs a convoy
            return new ControllerDecision(policies, new Act[]
            {
                new FoundColonyAct(perceived.SelfId, perceived.ColonyCandidates[0].Target),
            });
        return new ControllerDecision(policies, NoActs);
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
            policies = policies with { StockpileTargets = targets };
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
                        builds[brief.DesignId] = 1.0;
                        break;
                    case ShipRole.Colony:
                        builds[brief.DesignId] =
                            perceived.ColonyHullsAvailable == 0 ? 0.6 : 0.05;
                        break;
                    case ShipRole.Scout:
                        builds[brief.DesignId] = 0.1;
                        break;
                    case ShipRole.Escort:
                        builds[brief.DesignId] = 0.5 * militancy;
                        break;
                    case ShipRole.Line:
                        builds[brief.DesignId] = 0.35 * militancy;
                        break;
                }
            policies = policies with { ShipbuildingPriorities = builds };
        }
        return policies;
    }
}
