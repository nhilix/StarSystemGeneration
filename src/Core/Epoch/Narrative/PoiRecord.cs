using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>POI vocabulary (chronicle-and-poi.md §The POI compiler).
/// Stable ids — persisted.</summary>
public enum PoiType
{
    Battlefield = 0,     // wreckage concentration: salvage value
    Ruins = 1,           // a dead city: suppressed settlement, salvage
    RuinedCapital = 2,   // a fallen capital: cultural claim anchor
    Memorial = 3,        // famine or atrocity: stance and culture memory
    PrecursorSite = 4,   // deep-time archaeology: exotics, hazard, research
}

/// <summary>One anchored point of interest — debris with a name, a date,
/// and factions you can look up. Compiled incrementally inside Chronicle
/// every epoch and live from the moment of creation (the two-customer test
/// applies during genesis). One live anchor per hex, arbitrated by
/// magnitude; superseded or consumed POIs mark Depleted and stay as
/// history. Registry in SimState.Pois, id order (P6).</summary>
public sealed class PoiRecord
{
    public int Id { get; }
    public PoiType Type { get; }
    public HexCoordinate Hex { get; }
    /// <summary>Battlefields: total hulls in the field (grows while wars
    /// grind). Others: a fixed weight class.</summary>
    public double Magnitude { get; set; }
    public long FoundedYear { get; }
    /// <summary>Type-dependent registry id: port (ruins), polity (fallen
    /// capital), precursor wave; −1 none.</summary>
    public int SubjectId { get; }
    /// <summary>Type-dependent detail: precursor site type, memorial cause
    /// (0 famine, 1 suppression); 0 otherwise.</summary>
    public int Detail { get; }
    /// <summary>Battlefield depletion counter — salvage draws count here so
    /// WreckageRecords stay immutable and the hull ledger conserves.</summary>
    public int HullsSalvaged { get; set; }
    /// <summary>Faded: salvaged out, repopulated, or superseded by a bigger
    /// anchor. Depleted POIs no longer pin their hex.</summary>
    public bool Depleted { get; set; }
    /// <summary>Precursor sites only: a live remnant, not an inert ruin.</summary>
    public bool Dormant { get; set; }
    /// <summary>Polities (or wrecked-hull owners) whose history this is,
    /// ascending actor id.</summary>
    public List<int> ParticipantActorIds { get; } = new List<int>();
    /// <summary>Log event ids this POI compiled from.</summary>
    public List<long> SourceEventIds { get; } = new List<long>();

    public PoiRecord(int id, PoiType type, HexCoordinate hex, double magnitude,
                     long foundedYear, int subjectId = -1, int detail = 0)
    {
        Id = id;
        Type = type;
        Hex = hex;
        Magnitude = magnitude;
        FoundedYear = foundedYear;
        SubjectId = subjectId;
        Detail = detail;
    }

    /// <summary>Battlefields: hulls still lying in the field.</summary>
    public double SalvageRemaining => Magnitude - HullsSalvaged;
}
