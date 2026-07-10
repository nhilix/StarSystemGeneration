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

    public PerceptionView(int selfId, int worldYear, IReadOnlyList<int> knownPolityIds,
                          double expansionPoints = 0,
                          IReadOnlyList<ColonyCandidate>? colonyCandidates = null,
                          SpeciesProfile? selfSpecies = null)
    {
        SelfId = selfId;
        WorldYear = worldYear;
        KnownPolityIds = knownPolityIds;
        ExpansionPoints = expansionPoints;
        ColonyCandidates = colonyCandidates ?? NoCandidates;
        SelfSpecies = selfSpecies;
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
        var policies = PoliciesFor(perceived.SelfSpecies);
        if (perceived.ExpansionPoints >= _config.Expansion.ColonyCost
            && perceived.ColonyCandidates.Count > 0)
            return new ControllerDecision(policies, new Act[]
            {
                new FoundColonyAct(perceived.SelfId, perceived.ColonyCandidates[0].Target),
            });
        return new ControllerDecision(policies, NoActs);
    }

    /// <summary>Default policies plus a species-derived law code: closed
    /// societies prohibit narcotics, guarded ones restrict them —
    /// jurisdiction-relative legality (commodities.md), deterministic from
    /// the perceived self.</summary>
    private static PolityPolicies PoliciesFor(SpeciesProfile? species)
    {
        if (species == null || species.Openness >= 0.55)
            return PolityPolicies.Default;
        var law = new Dictionary<int, LegalityLevel>
        {
            [(int)Substrate.GoodId.Narcotics] = species.Openness < 0.35
                ? LegalityLevel.Prohibited
                : LegalityLevel.Restricted,
        };
        return PolityPolicies.Default with { LawCode = law };
    }
}
