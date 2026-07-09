using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The single global append-only event stream (P4's backbone).
/// Indexes are views computed over the log — per-place, per-actor — never
/// stored state (narrative/chronicle-and-poi.md §Indexes).</summary>
public sealed class EventLog
{
    private readonly List<WorldEvent> _events = new List<WorldEvent>();

    public IReadOnlyList<WorldEvent> Events => _events;

    /// <summary>Appends with the next sequential id (deterministic: callers
    /// stage and finalize in fixed iteration order).</summary>
    public WorldEvent Append(int worldYear, ClockStratum stratum, WorldEventType type,
                             IReadOnlyList<int> actors, HexCoordinate location,
                             double magnitude, double valence,
                             EventVisibility visibility, EventPayload? payload)
    {
        var e = new WorldEvent(_events.Count, worldYear, stratum, type, actors,
                               location, magnitude, valence, visibility, payload);
        _events.Add(e);
        return e;
    }

    /// <summary>Per-place view: everything that happened at this hex, in log order.</summary>
    public IEnumerable<WorldEvent> AtPlace(HexCoordinate location)
    {
        foreach (var e in _events)
            if (e.Location.Equals(location))
                yield return e;
    }

    /// <summary>Per-actor view: every event this actor participates in — the
    /// biography index for free.</summary>
    public IEnumerable<WorldEvent> ForActor(int actorId)
    {
        foreach (var e in _events)
            for (int i = 0; i < e.Actors.Count; i++)
                if (e.Actors[i] == actorId)
                {
                    yield return e;
                    break;
                }
    }
}
