using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>One public event's word traveling the news graph
/// (perception-and-news.md §News pulses): born at the event's hex, it
/// reaches each polity when its age covers the traffic-derived delay —
/// arrival, not emission, updates belief. The delivery record is the
/// pulse's journey (the REPL's news panel); pulses expire at MaxYears
/// (attenuated to rumor). Registry in SimState.Pulses, id order (P6);
/// records stay as history.</summary>
public sealed class NewsPulse
{
    public int Id { get; }
    /// <summary>The log event this pulse carries.</summary>
    public long EventId { get; }
    /// <summary>Where the word was born — the event's hex.</summary>
    public HexCoordinate Origin { get; }
    public long EmitYear { get; }
    public double Magnitude { get; }
    /// <summary>Arrivals so far: (polity actor id, world-year heard), in
    /// delivery order — epochs ascending, actor id within an epoch.</summary>
    public List<(int ActorId, long Year)> Delivered { get; }
        = new List<(int ActorId, long Year)>();

    public NewsPulse(int id, long eventId, HexCoordinate origin, long emitYear,
                     double magnitude)
    {
        Id = id;
        EventId = eventId;
        Origin = origin;
        EmitYear = emitYear;
        Magnitude = magnitude;
    }

    public bool DeliveredTo(int actorId)
    {
        foreach (var (id, _) in Delivered)
            if (id == actorId) return true;
        return false;
    }
}
