using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>One good's market row: everything `market` prints (reference
/// price / resting ask depth + grade / cleared / black book) plus the
/// located larder (T2) — the port's strategic stock, its grade, and the
/// effective per-year decay it rots at where it sits. Inventory is the
/// book's ask depth since slice CE retired the anonymous shelf.</summary>
public sealed record MarketGoodRow(
    GoodId Good, string GoodName, double Price, double Inventory,
    GradeBand GradeBand, double Grade, double LastCleared,
    double BlackBookDemand, double BlackBookPrice,
    double StockQty, double StockGrade, double StockDecayPerYear);

/// <summary>One household segment trading at the port.</summary>
public sealed record SegmentRow(int Id, string CultureName, double Size,
                                double SoL, double Wealth,
                                double LastSubsistence);

/// <summary>One facility attached to the port's market.</summary>
public sealed record FacilityRow(int Id, string TypeName, int Tier,
                                 HexCoordinate Hex, bool Active,
                                 double Condition);

/// <summary>One lane out of the port; Cut = severed by blockade or
/// standing quarantine (FleetOps.SeveredLaneIds).</summary>
public sealed record LaneLink(int LaneId, int OtherPortId, bool Cut);

/// <summary>The Market panel's card — `market &lt;portId&gt;` typed.</summary>
public sealed record MarketCard(
    int PortId, int Tier, HexCoordinate Hex, int OwnerActorId,
    string OwnerName, long FoundedYear, double StockCapacity,
    IReadOnlyList<MarketGoodRow> Goods, IReadOnlyList<SegmentRow> Segments,
    IReadOnlyList<FacilityRow> Facilities, IReadOnlyList<LaneLink> Lanes);

/// <summary>K3: the port click target — MarketView.Render parity plus the
/// located larder. Capacity and decay ride the SAME Core derivations the
/// sim uses (MarketEngine.StockCapacityAt / ActiveDepotTiersAt /
/// StockPerishFactor × EconomyKnobs.StockpileDecayPerYear) — zero drift
/// by construction.</summary>
public static class MarketPanel
{
    public static MarketCard? Card(AtlasReadModel model, EyeContext eye,
                                   int portId)
    {
        var state = model.State;
        if (portId < 0 || portId >= state.Ports.Count) return null;
        var port = state.Ports[portId];
        var market = state.Markets[portId];
        var eco = state.Config.Economy;

        double decayCut = Math.Pow(eco.DepotDecayFactor,
            MarketEngine.ActiveDepotTiersAt(state, port));
        var goods = new List<MarketGoodRow>(Goods.All.Count);
        for (int g = 0; g < Goods.All.Count; g++)
        {
            var id = (GoodId)g;
            double askQty = BookOps.AskQty(state, portId, g);
            double askGrade = BookOps.AskGrade(state, portId, g);
            goods.Add(new MarketGoodRow(id, Substrate.Goods.Get(id).Name,
                market.Price[g], askQty,
                Grades.BandOf(askGrade),
                askGrade, market.LastCleared[g],
                market.BlackBookDemand[g], market.BlackBookPrice[g],
                port.StockQty[g], port.StockGrade[g],
                eco.StockpileDecayPerYear
                    * MarketEngine.StockPerishFactor(id) * decayCut));
        }

        var segments = new List<SegmentRow>();
        foreach (var s in state.Segments)                 // id order (P6)
        {
            if (s.PortId != portId || s.Size <= 0.001) continue;
            string culture = s.CultureId >= 0
                && s.CultureId < state.Cultures.Count
                ? state.Cultures[s.CultureId].Name
                : FormattableString.Invariant($"culture{s.CultureId}");
            segments.Add(new SegmentRow(s.Id, culture, s.Size, s.SoL,
                                        s.Wealth, s.LastSubsistence));
        }

        var facilities = new List<FacilityRow>();
        foreach (var f in state.Facilities)               // id order (P6)
        {
            if (MarketEngine.AttachedMarketIndex(state, f) != portId) continue;
            facilities.Add(new FacilityRow(f.Id,
                Infrastructure.Get((InfraTypeId)f.TypeId).Name, f.Tier,
                f.Hex, MarketEngine.IsActive(state, f), f.Condition));
        }

        var severed = FleetOps.SeveredLaneIds(state);
        var lanes = new List<LaneLink>();
        foreach (var l in state.Lanes)                    // id order (P6)
        {
            if (l.PortAId != portId && l.PortBId != portId) continue;
            int other = l.PortAId == portId ? l.PortBId : l.PortAId;
            lanes.Add(new LaneLink(l.Id, other, severed.Contains(l.Id)));
        }

        return new MarketCard(portId, port.Tier, port.Hex,
            port.OwnerActorId, state.Actors[port.OwnerActorId].Name,
            port.FoundedYear, MarketEngine.StockCapacityAt(state, port),
            goods, segments, facilities, lanes);
    }
}
