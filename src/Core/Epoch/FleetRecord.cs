using System;
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The six posture assignments (fleets/ships-and-fleets.md
/// §Postures). Stable ids — append-only. Expedition is the only moving
/// posture; everything else is stationed work.</summary>
public enum FleetPosture
{
    Posted = 0,      // freight capacity on an assigned lane — markets consume it
    Escort = 1,      // screening/tracking on a route or convoy
    Patrol = 2,      // legality enforcement in a domain
    Blockade = 3,    // stationed at enemy port approaches; cuts its lanes
    Expedition = 4,  // war fleets, colony convoys, ruin expeditions
    Reserve = 5,     // docked; minimal upkeep; readiness decays
}

/// <summary>Hull counts of one design inside a fleet, with the mean build
/// grade of those hulls (blends like inventory grade). Composition entries
/// stay design-id sorted (P6).</summary>
public sealed class HullGroup
{
    public int DesignId { get; }
    public int Count { get; set; }
    public double Grade { get; set; }

    public HullGroup(int designId, int count, double grade)
    {
        DesignId = designId;
        Count = count;
        Grade = grade;
    }
}

/// <summary>The fleet object (fleets/ships-and-fleets.md): (id, owner,
/// location, composition, posture, commander, supply state). Everything
/// abstract is physical — trade flows need hulls (frame/actors.md).
/// Vectors compute on demand (FleetMath), never stored. The commander role
/// slot stays empty until slice G fills it with characters.</summary>
public sealed class FleetRecord
{
    public int Id { get; }
    public int OwnerActorId { get; set; }
    /// <summary>Current hex address — fleets move.</summary>
    public HexCoordinate Hex { get; set; }
    public FleetPosture Posture { get; set; } = FleetPosture.Reserve;
    /// <summary>Posture context: lane id for Posted/Escort, port id for
    /// Patrol/Blockade, −1 otherwise (Expedition journeys resolve within
    /// the step at this clock; Reserve docks at the home port).</summary>
    public int TargetId { get; set; } = -1;
    /// <summary>Supply source: fuel and upkeep draw from this port's market.</summary>
    public int HomePortId { get; set; } = -1;
    /// <summary>Supply state [0,1]: drifts toward the met upkeep fraction;
    /// below the attrition floor, hulls wreck.</summary>
    public double Readiness { get; set; } = 1.0;
    /// <summary>Commander role slot — a character id once slice G mints
    /// them; −1 (vacant) until then.</summary>
    public int CommanderId { get; set; } = -1;
    /// <summary>Hull counts per design, design-id sorted (P6).</summary>
    public List<HullGroup> Hulls { get; } = new List<HullGroup>();

    public FleetRecord(int id, int ownerActorId, HexCoordinate hex)
    {
        Id = id;
        OwnerActorId = ownerActorId;
        Hex = hex;
    }

    public int TotalHulls
    {
        get
        {
            int total = 0;
            foreach (var g in Hulls) total += g.Count;
            return total;
        }
    }

    /// <summary>Blend hulls into the composition (grade mixes by count,
    /// like market inventory), keeping design-id order.</summary>
    public void AddHulls(int designId, int count, double grade)
    {
        if (count <= 0) return;
        int at = 0;
        foreach (var g in Hulls)
        {
            if (g.DesignId == designId)
            {
                g.Grade = (g.Count * g.Grade + count * grade) / (g.Count + count);
                g.Count += count;
                return;
            }
            if (g.DesignId < designId) at++;
        }
        Hulls.Insert(at, new HullGroup(designId, count, grade));
    }

    /// <summary>Remove up to <paramref name="count"/> hulls of a design;
    /// returns how many actually left. Empty groups are dropped.</summary>
    public int RemoveHulls(int designId, int count)
    {
        for (int i = 0; i < Hulls.Count; i++)
        {
            var g = Hulls[i];
            if (g.DesignId != designId) continue;
            int removed = Math.Min(g.Count, count);
            g.Count -= removed;
            if (g.Count <= 0) Hulls.RemoveAt(i);
            return removed;
        }
        return 0;
    }
}
