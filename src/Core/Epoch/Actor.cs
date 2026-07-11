using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The three decision-making actor kinds (frame/actors.md).
/// Populations and assets are never actors.</summary>
public enum ActorKind { Polity, Corporation, Character }

/// <summary>The common actor substrate (frame/actors.md): an identity, a
/// perception state, a controller slot. Slice A registers stub polities only;
/// Slice B replaces the seeding, later slices add the other kinds.</summary>
public sealed class Actor
{
    public int Id { get; }
    public ActorKind Kind { get; }
    public string Name { get; }
    /// <summary>Physical anchor hex — the stub polity's homeworld.</summary>
    public HexCoordinate Seat { get; }
    /// <summary>Epoch this actor enters the simulation (staggered emergence).</summary>
    public int EntryEpoch { get; }
    public bool Entered { get; set; }
    /// <summary>Left the stage for good — federated away, absorbed, or
    /// extinct (slice H). A retired actor never re-enters; its registries
    /// stay as history.</summary>
    public bool Retired { get; set; }
    /// <summary>AI or player, interchangeable (P2).</summary>
    public IController Controller { get; set; }
    /// <summary>Standing policies, written each Intent and applied mechanically
    /// by *other* phases on subsequent steps (frame/simulation-flow.md Move 1).</summary>
    public PolicySet? Policies { get; set; }
    /// <summary>The view built each Perception phase from the compressed
    /// beliefs below — the only input the controller reads (P3). Transient.</summary>
    public PerceptionView? Perception { get; set; }
    /// <summary>Compressed believed world (slice I): belief snapshots that
    /// refresh at news speed and freeze between refreshes. State — it
    /// serializes with the actor (LoadThenContinue must not re-survey).</summary>
    public BeliefState Beliefs { get; } = new BeliefState();

    public Actor(int id, ActorKind kind, string name, HexCoordinate seat,
                 int entryEpoch, IController controller)
    {
        Id = id;
        Kind = kind;
        Name = name;
        Seat = seat;
        EntryEpoch = entryEpoch;
        Controller = controller;
    }
}
