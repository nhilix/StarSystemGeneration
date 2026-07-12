using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>One anchored point of interest (`poi` parity). Salvage rides
/// battlefields; Dormant marks something still awake.</summary>
public sealed record PoiRow(int Id, PoiType Type, string TypeName,
    HexCoordinate Hex, double Magnitude, long FoundedYear, bool Dormant,
    bool Depleted, double SalvageRemaining, int HullsSalvaged,
    IReadOnlyList<int> ParticipantActorIds);

/// <summary>One POI with its compiled source events described.</summary>
public sealed record PoiCard(PoiRow Row, IReadOnlyList<string> Chronicle);

/// <summary>K3: the POI mark click target — NarrativeView RenderPois/
/// RenderPoi parity (typed names incl. the memorial detail split).</summary>
public static class PoiPanel
{
    public static List<PoiRow> Rows(AtlasReadModel model, EyeContext eye)
    {
        var state = model.State;
        var rows = new List<PoiRow>();
        foreach (var poi in state.Pois)                   // id order (P6)
            rows.Add(RowOf(poi));
        return rows;
    }

    /// <summary>Anchored (not faded) count — the registry header line.</summary>
    public static int LiveCount(AtlasReadModel model, EyeContext eye)
    {
        int live = 0;
        foreach (var poi in model.State.Pois)
            if (!poi.Depleted) live++;
        return live;
    }

    public static PoiCard? Card(AtlasReadModel model, EyeContext eye,
                                int poiId)
    {
        var state = model.State;
        if (poiId < 0 || poiId >= state.Pois.Count) return null;
        var poi = state.Pois[poiId];
        var chronicle = new List<string>();
        foreach (var id in poi.SourceEventIds)
            if (id >= 0 && id < state.Log.Events.Count)
                chronicle.Add(SimTraceView.Describe(
                    state.Log.Events[(int)id]));
        return new PoiCard(RowOf(poi), chronicle);
    }

    private static PoiRow RowOf(PoiRecord poi) =>
        new(poi.Id, poi.Type, TypeName(poi), poi.Hex, poi.Magnitude,
            poi.FoundedYear, poi.Dormant, poi.Depleted,
            poi.SalvageRemaining, poi.HullsSalvaged,
            poi.ParticipantActorIds);

    /// <summary>NarrativeView.TypeName's mapping, shared with the panel.</summary>
    public static string TypeName(PoiRecord poi) => poi.Type switch
    {
        PoiType.Battlefield => "battlefield",
        PoiType.Ruins => "ruins",
        PoiType.RuinedCapital => "ruined capital",
        PoiType.Memorial => poi.Detail == 1
            ? "memorial (suppression)" : "memorial (famine)",
        PoiType.PrecursorSite => "precursor site",
        _ => "poi",
    };
}
