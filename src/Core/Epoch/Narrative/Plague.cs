using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>One contagion riding the same lanes the news does (slice I):
/// an outbreak at a port, lane-borne spread gated by posted traffic,
/// segment mortality (machine minds immune), burnout with an immunity
/// window, and quarantines cutting the arcs it travels. Registry in
/// SimState.Plagues, id order (P6); burned-out plagues stay as history.</summary>
public sealed class Plague
{
    public int Id { get; }
    public string Name { get; }
    /// <summary>The index case's port.</summary>
    public int OriginPortId { get; }
    public long StartYear { get; }
    public bool Active { get; set; } = true;
    public long EndedYear { get; set; } = -1;
    /// <summary>Infected ports: port id → world-year infected (the burnout
    /// clock), port-id order (P6).</summary>
    public SortedList<int, long> InfectedSince { get; }
        = new SortedList<int, long>();
    /// <summary>Recovered ports: port id → world-year the immunity lapses.</summary>
    public SortedList<int, long> ImmuneUntil { get; }
        = new SortedList<int, long>();
    /// <summary>Population lost to this strain — the chronicle's toll.</summary>
    public double TotalDeaths { get; set; }

    public Plague(int id, string name, int originPortId, long startYear)
    {
        Id = id;
        Name = name;
        OriginPortId = originPortId;
        StartYear = startYear;
    }

    public bool Infects(int portId) => InfectedSince.ContainsKey(portId);
}
