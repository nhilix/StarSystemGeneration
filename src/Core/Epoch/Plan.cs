using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>What one plan entry orders (spec §3). Kind discriminator is
/// append-only — program-style entries join later without a schema break.</summary>
public enum PlanEntryKind { Facility = 0, PortRaise = 1, HullBatch = 2 }

/// <summary>One scheduled project order: what, where, when, how urgent.
/// TypeId: InfraTypeId (Facility) or design id (HullBatch). StartYear is
/// an absolute world-year.</summary>
public sealed record PlanEntry(
    PlanEntryKind Kind, ProjectPriority Priority, int StartYear,
    int TypeId, int PortId, HexCoordinate Hex, int Count);

/// <summary>The standing schedule Intent emits and Allocation executes
/// (spec §3) — entries in plan order; regenerated every Intent, persisted
/// like any policy so a loaded artifact resumes mid-plan.</summary>
public sealed record StandingPlan(IReadOnlyList<PlanEntry> Entries)
{
    public static StandingPlan Empty { get; } =
        new StandingPlan(new PlanEntry[0]);
}
