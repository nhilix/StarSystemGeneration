using System;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>What kind of work a project delivers when its years are done
/// (spec §1) — a closed, versioned vocabulary; append-only, never renumber.</summary>
public enum ProjectKind
{
    FacilityConstruction = 0,
    PortRaise = 1,
    GatePair = 2,
    HullBatch = 3,
    ColonyExpedition = 4,
    Mobilization = 5,
}

/// <summary>Draw order among one funder's projects: lower drinks first
/// (the priority-ordered starvation cascade, spec §4).</summary>
public enum ProjectPriority { War = 0, Core = 1, Growth = 2 }

/// <summary>One piece of in-flight work (spec §1): a rate contract in
/// world-years — per-year basket, wages stream, years required/delivered.
/// Registry in SimState.Projects, id = creation order (P6). The site
/// exists at groundbreaking; completion fires the kind's payload. NOTHING
/// here counts epochs or ticks — time is state (P7).</summary>
public sealed class Project
{
    public int Id { get; }
    public ProjectKind Kind { get; }
    /// <summary>Who gets the result. Settable: conquest transfers
    /// site-anchored work at current progress (spec §1).</summary>
    public int OwnerActorId { get; set; }
    /// <summary>Whose treasury and reserves feed it (differs from owner
    /// for corp-built gates on a host polity's port).</summary>
    public int FunderActorId { get; set; }
    /// <summary>Anchor port: the site market draws come from and the wage
    /// sink. Travel kinds anchor at the staging port. Settable: conquest.</summary>
    public int PortId { get; set; }
    /// <summary>Site or travel-target hex — the P1 residue address.</summary>
    public HexCoordinate Hex { get; }
    public ProjectPriority Priority { get; set; }
    /// <summary>Position in the standing plan — the tie-break inside a
    /// priority class. Mechanical spawns use 0.</summary>
    public int PlanOrder { get; set; }
    /// <summary>Per-good consumption per world-year, indexed by GoodId
    /// (Goods.All.Count wide). All-zero for travel kinds.</summary>
    public double[] PerYearBasket { get; } =
        new double[Substrate.Goods.All.Count];
    /// <summary>Credits streamed to the site's households per world-year,
    /// drawn from the funder's treasury (construction employment).</summary>
    public double WagesPerYear { get; set; }
    public double YearsRequired { get; }
    public double YearsDelivered { get; set; }
    /// <summary>Scheduled groundbreaking world-year — may sit mid-span of
    /// a coarse step; Advance only credits years after it.</summary>
    public int StartedYear { get; }
    public bool Completed { get; set; }
    public bool Cancelled { get; set; }
    /// <summary>Fraction of the year-scaled basket met last Advance —
    /// the REPL's starvation readout.</summary>
    public double LastFedFraction { get; set; } = 1.0;

    // Completion payload, sparse by kind:
    /// <summary>FacilityConstruction/GatePair: InfraTypeId. HullBatch:
    /// ship design id. Others −1.</summary>
    public int TypeId { get; set; } = -1;
    /// <summary>FacilityConstruction: facility id under construction.
    /// PortRaise: port id. GatePair: lane id. ColonyExpedition: convoy
    /// fleet id. Mobilization: war id. Others −1.</summary>
    public int TargetId { get; set; } = -1;
    /// <summary>HullBatch: hulls in the batch. Others 0.</summary>
    public int Count { get; set; }
    /// <summary>Quantity-weighted mean grade of Ship Components drawn so
    /// far (HullBatch — the hull's grade at commissioning).</summary>
    public double AccumGrade { get; set; }
    public double AccumGradeWeight { get; set; }

    public bool InFlight => !Completed && !Cancelled;
    public double Progress => YearsRequired <= 0 ? 1.0
        : Math.Min(1.0, YearsDelivered / YearsRequired);

    public Project(int id, ProjectKind kind, int ownerActorId,
                   int funderActorId, int portId, HexCoordinate hex,
                   double yearsRequired, int startedYear)
    {
        Id = id;
        Kind = kind;
        OwnerActorId = ownerActorId;
        FunderActorId = funderActorId;
        PortId = portId;
        Hex = hex;
        YearsRequired = yearsRequired;
        StartedYear = startedYear;
    }
}
