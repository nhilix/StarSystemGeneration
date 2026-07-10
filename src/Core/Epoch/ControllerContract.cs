using System.Collections.Generic;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Epoch;

/// <summary>What an actor currently believes about the world — the only input
/// a controller may read (P3). Perfect-information stub, built from truth each
/// Perception phase; compressed belief replaces it in Slice I. The contract
/// holds either way: Decide sees the view, never global state.</summary>
public sealed class PerceptionView
{
    private static readonly IReadOnlyList<ColonyCandidate> NoCandidates =
        new ColonyCandidate[0];

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
    /// society (null for non-polities and shape-only test skeletons). Law
    /// codes and temperament-flavored policies derive from it.</summary>
    public SpeciesProfile? SelfSpecies { get; }
    /// <summary>Own port count — scales standing policy magnitudes
    /// (stockpile targets and the like).</summary>
    public int OwnPortCount { get; }
    /// <summary>Size-weighted mean subsistence across the realm's segments
    /// (1.0 when unpeopled) — the consolidation signal: a starving realm
    /// digests before it expands.</summary>
    public double RealmSubsistence { get; }

    public PerceptionView(int selfId, int worldYear, IReadOnlyList<int> knownPolityIds,
                          double expansionPoints = 0,
                          IReadOnlyList<ColonyCandidate>? colonyCandidates = null,
                          SpeciesProfile? selfSpecies = null,
                          int ownPortCount = 0,
                          double realmSubsistence = 1.0)
    {
        SelfId = selfId;
        WorldYear = worldYear;
        KnownPolityIds = knownPolityIds;
        ExpansionPoints = expansionPoints;
        ColonyCandidates = colonyCandidates ?? NoCandidates;
        SelfSpecies = selfSpecies;
        OwnPortCount = ownPortCount;
        RealmSubsistence = realmSubsistence;
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

    /// <summary>Provisions reserve target per owned port — famine and siege
    /// buffering by standing policy (economy/markets.md §Stockpiles).</summary>
    private const double ProvisionsReservePerPort = 3.0;
    /// <summary>No expeditions while the realm starves: expansion waits for
    /// consolidation below this mean-subsistence line.</summary>
    private const double RealmHungerGate = 0.8;

    public ControllerDecision Decide(PerceptionView perceived)
    {
        var policies = PoliciesFor(perceived);
        if (perceived.ExpansionPoints >= _config.Expansion.ColonyCost
            && perceived.RealmSubsistence >= RealmHungerGate
            && perceived.ColonyCandidates.Count > 0)
            return new ControllerDecision(policies, new Act[]
            {
                new FoundColonyAct(perceived.SelfId, perceived.ColonyCandidates[0].Target),
            });
        return new ControllerDecision(policies, NoActs);
    }

    /// <summary>Default policies plus a species-derived law code (closed
    /// societies prohibit narcotics, guarded ones restrict them —
    /// jurisdiction-relative legality, commodities.md) and a provisions
    /// reserve target scaling with the realm. Deterministic from the view.</summary>
    private static PolityPolicies PoliciesFor(PerceptionView perceived)
    {
        var policies = PolityPolicies.Default;
        var species = perceived.SelfSpecies;
        if (species != null && species.Openness < 0.55)
            policies = policies with
            {
                LawCode = new Dictionary<int, LegalityLevel>
                {
                    [(int)Substrate.GoodId.Narcotics] = species.Openness < 0.35
                        ? LegalityLevel.Prohibited
                        : LegalityLevel.Restricted,
                },
            };
        if (perceived.OwnPortCount > 0)
        {
            // polity procurement (market-geography.md participants): food
            // security for everyone, war materiel by temperament — strategic
            // demand that pulls the capital chains into existence
            var targets = new Dictionary<int, double>
            {
                [(int)Substrate.GoodId.Provisions] =
                    ProvisionsReservePerPort * perceived.OwnPortCount,
                // the state banks construction materials — market leftovers
                // alone never hold a whole build basket at once
                [(int)Substrate.GoodId.Alloys] = 3.0 * perceived.OwnPortCount,
                [(int)Substrate.GoodId.Machinery] = 1.5 * perceived.OwnPortCount,
                [(int)Substrate.GoodId.Composites] = perceived.OwnPortCount,
            };
            double militancy = species?.Militancy ?? 0.5;
            if (militancy > 0.2)
                targets[(int)Substrate.GoodId.Armaments] =
                    militancy * 2.0 * perceived.OwnPortCount;
            policies = policies with { StockpileTargets = targets };
        }
        return policies;
    }
}
