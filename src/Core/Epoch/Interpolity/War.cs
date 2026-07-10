using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>The casus-belli menu (interpolity/war.md §Causes): a war's
/// declared goal, sourced from real state. Stable ids.</summary>
public enum CasusBelli
{
    ResourceSeizure = 0,     // deficit/price shock on a good the target produces
    ChokepointControl = 1,   // the target holds a chokepoint port in reach
    PunitiveInterdiction = 2,// they blockade our ports
    Crusade = 3,             // ideology gap × zeal, sacral surge
    Liberation = 4,          // cultural kin (or a suppressed emergence) under their rule
    Containment = 5,         // a rising incompatible power
    SuccessionClaim = 6,     // a live dynastic claim on their throne
    GrievanceDischarge = 7,  // the military faction demands employment
    VassalSecession = 8,     // a vassal fights for independence
    BorderIncident = 9,      // the spark itself, when tension is loaded
    CivilWar = 10,           // a contested coup fought out (H9)
}

/// <summary>What a war objective is over (war.md §Conduct). Domains ride
/// their ports — capture transfers the domain with segments intact.</summary>
public enum WarObjectiveType
{
    CapturePort = 0,   // siege and take the port (and its domain)
    BlockadeLane = 1,  // cut the lane; interdiction strain
    DestroyFleet = 2,  // the enemy navy itself
}

public enum ObjectiveStatus
{
    Contested = 0,
    Taken = 1,      // capture complete / lane held cut / fleet broken
    Abandoned = 2,  // the attacker gave it up (settlement without it)
}

/// <summary>What the declarer demands at settlement (war.md §Termination).</summary>
public enum WarDemand
{
    CedeObjectives = 0,
    Reparations = 1,
    Vassalize = 2,
    Independence = 3,  // vassal-secession wars
    Submission = 4,    // civil wars: the provisional polity dissolves back
}

/// <summary>One objective target in a declaration act — the Intent-side
/// spec; Resolution validates against truth.</summary>
public sealed record WarObjectiveSpec(WarObjectiveType Type, int TargetId);

/// <summary>One front of a war: a port to take, a lane to cut, or a navy
/// to break. Serialized on the wars layer, id order within the war.</summary>
public sealed class WarObjective
{
    public int Id { get; }
    public WarObjectiveType Type { get; }
    /// <summary>Port id (CapturePort), lane id (BlockadeLane), or the
    /// defender polity id (DestroyFleet).</summary>
    public int TargetId { get; }
    public ObjectiveStatus Status { get; set; } = ObjectiveStatus.Contested;
    /// <summary>Consecutive epochs of attacker superiority at a port —
    /// the siege clock (war.md §Conduct 3).</summary>
    public int SiegeEpochs { get; set; }

    public WarObjective(int id, WarObjectiveType type, int targetId)
    {
        Id = id;
        Type = type;
        TargetId = targetId;
    }
}

/// <summary>A war (interpolity/war.md): declared goal, leaders and their
/// supporting belligerents, the objective set, and the exhaustion gauges
/// termination reads. Registry in SimState.Wars, id order (P6); ended wars
/// stay as history.</summary>
public sealed class War
{
    public int Id { get; }
    /// <summary>"the <X> War" — named at declaration, deterministic.</summary>
    public string Name { get; }
    /// <summary>The war leaders: settlements are negotiated between them.</summary>
    public int AttackerId { get; }
    public int DefenderId { get; }
    public CasusBelli Cause { get; }
    /// <summary>Cause context: good id, port id, dynasty id, culture id,
    /// origin id — per the cause's type; −1 none.</summary>
    public int SubjectId { get; }
    public WarDemand Demand { get; }
    public long DeclaredYear { get; }
    public bool Active { get; set; } = true;
    public long EndedYear { get; set; } = -1;
    /// <summary>Supporting belligerents under each leader (defense-alliance
    /// partners and vassals) — their gains and grievances flow through the
    /// leader's table.</summary>
    public List<int> AttackerAllies { get; } = new List<int>();
    public List<int> DefenderAllies { get; } = new List<int>();
    public List<WarObjective> Objectives { get; } = new List<WarObjective>();
    /// <summary>[0,1] per-side war weariness: time + losses. A side at 1
    /// breaks whatever its politics say.</summary>
    public double AttackerExhaustion { get; set; }
    public double DefenderExhaustion { get; set; }
    /// <summary>War strength each leader's coalition mustered at
    /// declaration — the fleet-exhaustion break condition's baseline.</summary>
    public double AttackerStrengthAtStart { get; set; }
    public double DefenderStrengthAtStart { get; set; }

    public War(int id, string name, int attackerId, int defenderId,
               CasusBelli cause, int subjectId, WarDemand demand,
               long declaredYear)
    {
        Id = id;
        Name = name;
        AttackerId = attackerId;
        DefenderId = defenderId;
        Cause = cause;
        SubjectId = subjectId;
        Demand = demand;
        DeclaredYear = declaredYear;
    }

    public bool Involves(int polityId) =>
        AttackerId == polityId || DefenderId == polityId
        || AttackerAllies.Contains(polityId)
        || DefenderAllies.Contains(polityId);

    /// <summary>True when the polity fights on the attacker's side.</summary>
    public bool OnAttackerSide(int polityId) =>
        AttackerId == polityId || AttackerAllies.Contains(polityId);
}
