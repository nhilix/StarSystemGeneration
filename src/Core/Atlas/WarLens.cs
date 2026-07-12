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

/// <summary>The war lens — borders flaring (emap war parity): belligerent
/// domains keep their color while the peaceful fade, and war fleets on
/// station burn. The domain field already carries the relation matrix;
/// this lens adds the per-slot belligerence flags and the stations.</summary>
public static class WarLens
{
    /// <summary>War stations burn hot regardless of owner — the '!' glyph's
    /// color; the ring/mark shape is the presentation's.</summary>
    public static readonly Rgba StationBurn = new(235, 75, 55, 240);

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
}
