using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>What an actor currently believes about the world — the only input
/// a controller may read (P3). Slice-A stub: perfect information, built from
/// truth each Perception phase; compressed belief replaces it in Slice I. The
/// contract holds either way: Decide sees the view, never global state.</summary>
public sealed class PerceptionView
{
    public int SelfId { get; }
    public int WorldYear { get; }
    /// <summary>Polities this actor knows exist (perfect-info stub: all entered).</summary>
    public IReadOnlyList<int> KnownPolityIds { get; }

    public PerceptionView(int selfId, int worldYear, IReadOnlyList<int> knownPolityIds)
    {
        SelfId = selfId;
        WorldYear = worldYear;
        KnownPolityIds = knownPolityIds;
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

/// <summary>Default genesis AI for Slice A: default policies, no acts — just
/// enough for the loop to turn. Later slices replace it with real valuation.</summary>
public sealed class TrivialController : IController
{
    private static readonly IReadOnlyList<Act> NoActs = new Act[0];

    public ControllerDecision Decide(PerceptionView perceived) =>
        new ControllerDecision(PolityPolicies.Default, NoActs);
}
