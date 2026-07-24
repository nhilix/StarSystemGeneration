using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One war fleet on station: a blockade ring at a port's
/// approaches or an expedition mark in the field — only while its owner
/// has an active war (emap war parity: peacetime interdiction reads on
/// the lanes, not here).</summary>
public readonly record struct WarStation(
    int FleetId, HexCoordinate Hex, FleetPosture Posture, int OwnerActorId,
    Rgba Color);

/// <summary>One lane a hostile squadron can reach — the war lens's
/// contested-lane stroke (AC2.7). Endpoints mirror LaneLens.Segments so the
/// presentation can overlay it the same way.</summary>
public readonly record struct ContestedLane(
    int LaneId, HexCoordinate A, HexCoordinate B, Rgba Color);

/// <summary>The war lens — borders flaring (emap war parity): belligerent
/// domains keep their color while the peaceful fade, and war fleets on
/// station burn. The domain field already carries the relation matrix;
/// this lens adds the per-slot belligerence flags and the stations.</summary>
public static class WarLens
{
    /// <summary>War stations burn hot regardless of owner — the '!' glyph's
    /// color; the ring/mark shape is the presentation's.</summary>
    public static readonly Rgba StationBurn = new(235, 75, 55, 240);

    /// <summary>The contested-lane stroke — a cooler ember than a station's
    /// burn (that's a fleet; this is a lane merely within a hostile
    /// squadron's reach), distinct from LaneLens.SeveredColor (a hard
    /// close, not a risk).</summary>
    public static readonly Rgba ContestedLaneColor = new(235, 130, 40, 200);

    /// <summary>Blockades and expeditions of actively warring owners —
    /// EpochMapView.WarStationCells, addressed.</summary>
    public static IReadOnlyList<WarStation> Stations(AtlasReadModel model,
                                                     EyeContext eye)
    {
        var stations = new List<WarStation>();
        foreach (var fleet in model.State.Fleets)         // id order (P6)
            if (fleet.TotalHulls > 0
                && fleet.Posture is FleetPosture.Blockade
                    or FleetPosture.Expedition
                && WarOps.AtWar(model.State, fleet.OwnerActorId))
                stations.Add(new WarStation(fleet.Id, fleet.Hex, fleet.Posture,
                                            fleet.OwnerActorId, StationBurn));
        return stations;
    }

    /// <summary>Per-slot belligerence, parallel to DomainLens.PolitySlots —
    /// the field shader accents warring domains and fades the peaceful.</summary>
    public static IReadOnlyList<bool> SlotBelligerence(AtlasReadModel model,
        EyeContext eye, IReadOnlyList<int> slots)
    {
        var flags = new bool[slots.Count];
        for (int i = 0; i < flags.Length; i++)
            flags[i] = WarOps.AtWar(model.State, slots[i]);
        return flags;
    }

    /// <summary>Lanes a hostile squadron can reach (AC2.7): reuses
    /// ShipmentOps.WarPresenceMap — the same war-stationed/escort-riding
    /// read the sail rule scores its interdiction rolls against — CALLED,
    /// not copied; no reach/roll math is re-derived here. A lane is
    /// contested when any squadron on it is at active war with either
    /// endpoint port's owner (an unowned/-1 endpoint matches no war). Empty
    /// when no war is active anywhere (WarPresenceMap short-circuits).</summary>
    public static IReadOnlyList<ContestedLane> ContestedLanes(
        AtlasReadModel model, EyeContext eye)
    {
        var state = model.State;
        var presence = ShipmentOps.WarPresenceMap(state);
        if (presence == null) return Array.Empty<ContestedLane>();
        var contested = new List<ContestedLane>();
        foreach (var lane in state.Lanes)                 // id order (P6)
        {
            if (!presence.TryGetValue(lane.Id, out var squadrons)) continue;
            int ownerA = state.Ports[lane.PortAId].OwnerActorId;
            int ownerB = state.Ports[lane.PortBId].OwnerActorId;
            bool hostile = false;
            foreach (var (fleet, _) in squadrons)         // fleet-id order (P6)
                if (WarOps.ActiveWarBetween(state, fleet.OwnerActorId, ownerA)
                        != null
                    || WarOps.ActiveWarBetween(state, fleet.OwnerActorId,
                                               ownerB) != null)
                { hostile = true; break; }
            if (hostile)
                contested.Add(new ContestedLane(lane.Id,
                    state.Ports[lane.PortAId].Hex,
                    state.Ports[lane.PortBId].Hex, ContestedLaneColor));
        }
        return contested;
    }
}
