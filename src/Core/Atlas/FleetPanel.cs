using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>Where a posture stands (FleetView.Station made typed): the
/// lane for Posted/Escort, the port for Patrol/Blockade, in transit for
/// Expedition, docked at home otherwise.</summary>
public enum StationKind { Unassigned, Lane, Port, InTransit, Docked, Adrift }

/// <summary>One fleet-registry row (`fleet` parity — hull-less slots
/// skipped).</summary>
public sealed record FleetRow(int Id, int OwnerActorId, string OwnerName,
    FleetPosture Posture, StationKind Station, int StationId,
    HexCoordinate Hex, int TotalHulls, double Readiness);

/// <summary>One composition line of a fleet card.</summary>
public sealed record HullLine(int Count, int DesignId, string DesignName,
    int Mark, ShipRole Role, ShipSize Size, double Grade);

/// <summary>One fleet's full sheet (`fleet &lt;id&gt;` parity): posture,
/// station, composition, and the computed-never-stored vectors.
/// ForwardDepotPortId/-DistanceHexes name where a deployed (Blockade or
/// Expedition) fleet draws supply (AC2.7, FleetOps.SupplyFleets' own
/// forward-depot criterion) — -1/-1 for any other posture, or a deployed
/// fleet whose owner holds no port.</summary>
public sealed record FleetCard(FleetRow Row, int HomePortId,
    int CommanderId, string? CommanderName,
    IReadOnlyList<HullLine> Composition, FleetVectors Vectors,
    double EnduranceHexesOffLane, int ForwardDepotPortId,
    int ForwardDepotDistanceHexes);

/// <summary>One design lineage row (`designs` parity).</summary>
public sealed record DesignRow(int Id, int OwnerActorId, string OwnerName,
    string Name, int Mark, ShipRole Role, ShipSize Size,
    double ComponentGrade, int TechTier, int DesignedYear);

/// <summary>K3: the fleet glyph click target — FleetView parity.</summary>
public static class FleetPanel
{
    /// <summary>The fleet registry, id order, hull-less slots skipped.</summary>
    public static List<FleetRow> Rows(AtlasReadModel model, EyeContext eye)
    {
        var state = model.State;
        var rows = new List<FleetRow>();
        foreach (var f in state.Fleets)                   // id order (P6)
        {
            if (f.TotalHulls == 0) continue;              // idle slots
            rows.Add(RowOf(state, f));
        }
        return rows;
    }

    /// <summary>One fleet by id, with composition and vectors.</summary>
    public static FleetCard? Card(AtlasReadModel model, EyeContext eye,
                                  int fleetId)
    {
        var state = model.State;
        if (fleetId < 0 || fleetId >= state.Fleets.Count) return null;
        var f = state.Fleets[fleetId];
        var composition = new List<HullLine>();
        foreach (var g in f.Hulls)
        {
            var d = state.Designs[g.DesignId];
            composition.Add(new HullLine(g.Count, d.Id, d.Name, d.Mark,
                                         d.Role, d.Size, g.Grade));
        }
        var vectors = FleetOps.Vectors(state, f);
        // forward depot (AC2.7): only a deployed fleet victuals at the
        // nearest owned port instead of home — FleetOps.SupplyFleets'
        // own criterion, read here rather than re-derived
        bool deployed = f.Posture is FleetPosture.Blockade
            or FleetPosture.Expedition;
        int depotPortId = deployed
            ? FleetOps.NearestOwnedPortId(state, f.OwnerActorId, f.Hex) : -1;
        int depotDistance = depotPortId >= 0
            ? HexGrid.Distance(state.Ports[depotPortId].Hex, f.Hex) : -1;
        return new FleetCard(RowOf(state, f), f.HomePortId, f.CommanderId,
            f.CommanderId >= 0 ? state.Characters[f.CommanderId].Name : null,
            composition, vectors,
            vectors.EnduranceFloor
                * state.Config.Fleet.EnduranceHexesPerPoint,
            depotPortId, depotDistance);
    }

    /// <summary>Design lineages, optionally one actor's (`designs`).</summary>
    public static List<DesignRow> Designs(AtlasReadModel model,
        EyeContext eye, int ownerActorId = -1)
    {
        var state = model.State;
        var rows = new List<DesignRow>();
        foreach (var d in state.Designs)                  // id order (P6)
        {
            if (ownerActorId >= 0 && d.OwnerActorId != ownerActorId)
                continue;
            rows.Add(new DesignRow(d.Id, d.OwnerActorId,
                state.Actors[d.OwnerActorId].Name, d.Name, d.Mark, d.Role,
                d.Size, d.ComponentGrade, d.TechTier, d.DesignedYear));
        }
        return rows;
    }

    private static FleetRow RowOf(SimState state, FleetRecord f)
    {
        var (kind, id) = StationOf(state, f);
        return new FleetRow(f.Id, f.OwnerActorId,
            state.Actors[f.OwnerActorId].Name, f.Posture, kind, id,
            f.Hex, f.TotalHulls, f.Readiness);
    }

    /// <summary>FleetView.Station's derivation, typed.</summary>
    private static (StationKind Kind, int Id) StationOf(SimState state,
                                                        FleetRecord f)
    {
        switch (f.Posture)
        {
            case FleetPosture.Posted:
            case FleetPosture.Escort:
                return f.TargetId >= 0 && f.TargetId < state.Lanes.Count
                    ? (StationKind.Lane, f.TargetId)
                    : (StationKind.Unassigned, -1);
            case FleetPosture.Patrol:
            case FleetPosture.Blockade:
                return f.TargetId >= 0
                    ? (StationKind.Port, f.TargetId)
                    : (StationKind.Unassigned, -1);
            case FleetPosture.Expedition:
                return (StationKind.InTransit, -1);
            default:
                return f.HomePortId >= 0
                    ? (StationKind.Docked, f.HomePortId)
                    : (StationKind.Adrift, -1);
        }
    }
}
