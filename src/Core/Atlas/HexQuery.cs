using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Generation;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>The hover-hex tooltip's content (K3): what's here — a
/// one-line system summary from the hex tier (pure function, computed on
/// demand, never persisted), the servicing domain owners, the port if one
/// stands here, and the live POIs.</summary>
public sealed record HexInfo(HexCoordinate Hex, string SystemSummary,
    IReadOnlyList<int> OwnerActorIds, IReadOnlyList<string> OwnerNames,
    int PortId, int PortTier, string? PortOwnerName,
    IReadOnlyList<PoiRow> LivePois);

/// <summary>K3: SelectionModel's hover query.</summary>
public static class HexQuery
{
    public static HexInfo At(AtlasReadModel model, EyeContext eye,
                             HexCoordinate hex)
    {
        var state = model.State;

        var owners = new List<int>();
        DomainLens.OwnersAt(model, eye, hex, owners);
        var ownerNames = new List<string>(owners.Count);
        foreach (var id in owners) ownerNames.Add(state.Actors[id].Name);

        int portId = -1, portTier = 0;
        string? portOwner = null;
        foreach (var port in state.Ports)                 // id order (P6)
            if (port.Hex.Equals(hex))
            {
                portId = port.Id;
                portTier = port.Tier;
                portOwner = state.Actors[port.OwnerActorId].Name;
                break;
            }

        var pois = new List<PoiRow>();
        foreach (var poi in state.Pois)                   // id order (P6)
            if (!poi.Depleted && poi.Hex.Equals(hex))
                pois.Add(new PoiRow(poi.Id, poi.Type, PoiPanel.TypeName(poi),
                    poi.Hex, poi.Magnitude, poi.FoundedYear, poi.Dormant,
                    poi.Depleted, poi.SalvageRemaining, poi.HullsSalvaged,
                    poi.ParticipantActorIds));

        return new HexInfo(hex, SystemSummary(model, hex), owners,
                           ownerNames, portId, portTier, portOwner, pois);
    }

    /// <summary>One line of the hex tier: designation, arrangement, star
    /// count — deterministic (same seed, same sky), or "empty reach".</summary>
    private static string SystemSummary(AtlasReadModel model,
                                        HexCoordinate hex)
    {
        var context = new GalaxyContext(model.Skeleton.Config)
        { Skeleton = model.Skeleton };
        var result = Generator.Generate(context, hex);
        if (result.System == null) return "empty reach";
        var s = result.System;
        string name = s.GivenName != null
            ? $"{s.Designation} “{s.GivenName}”" : s.Designation;
        string stars = s.Stars.Count == 1
            ? s.Stars[0].TypeName
            : System.FormattableString.Invariant(
                $"{s.Stars.Count} stars, {s.Arrangement.ToString().ToLowerInvariant()}");
        return name + " · " + stars
            + (s.OverlayId != null ? $" · {s.OverlayId}" : "");
    }
}
