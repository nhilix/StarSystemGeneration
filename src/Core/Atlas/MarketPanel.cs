using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>One resting order on a good's book (AC2.4, `ebook` parity):
/// owner identity resolved to a name (never a bare id), the quantity and
/// grade it carries (grade is 0 for a bid — buys are quality-agnostic),
/// the limit price it quotes, that limit's delta against the good's
/// reference price (positive = above reference), and the escrow credits a
/// bid still holds (0 for an ask — a sell escrows goods, not credits, and
/// those already live in <see cref="MarketGoodRow.Inventory"/>).</summary>
public sealed record BookOrderRow(
    int OrderId, int OwnerActorId, string OwnerName, double Qty,
    double Grade, double LimitPrice, double RefDelta, double EscrowCredits);

/// <summary>One good's market row: everything `market` prints (reference
/// price / resting ask depth + grade / cleared / black book) plus the
/// located larder (T2) — the port's strategic stock, its grade, and the
/// effective per-year decay it rots at where it sits. Inventory is the
/// book's ask depth since slice CE retired the anonymous shelf. Asks/Bids
/// (AC2.4) are the SAME book at order granularity — price-time priority
/// order (asks cheapest first, bids dearest first, id breaks ties), the
/// `ebook` parity surface.</summary>
public sealed record MarketGoodRow(
    GoodId Good, string GoodName, double Price, double Inventory,
    GradeBand GradeBand, double Grade, double LastCleared,
    double BlackBookDemand, double BlackBookPrice,
    double StockQty, double StockGrade, double StockDecayPerYear,
    IReadOnlyList<BookOrderRow> Asks, IReadOnlyList<BookOrderRow> Bids);

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

/// <summary>The Market panel's card — `market &lt;portId&gt;` typed.
/// <see cref="CurrencyId"/>/<see cref="CurrencyName"/> (AC3.3) are the
/// currency every price/order/escrow in <see cref="Goods"/> is denominated
/// in — <see cref="Epoch.SimState.LocalCurrencyOf"/> resolved once, headline
/// (not per-row). CurrencyName is null and CurrencyId is the pre-genesis
/// sentinel (−1) for a currencyless port — the same absent convention
/// <see cref="PolityPanel.MonetaryLine"/> uses.</summary>
public sealed record MarketCard(
    int PortId, int Tier, HexCoordinate Hex, int OwnerActorId,
    string OwnerName, long FoundedYear, double StockCapacity,
    int CurrencyId, string? CurrencyName,
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

        // ---- the book at order granularity (AC2.4): one pass over this
        // port's live orders, bucketed by good and side — the SAME orders
        // BookOps.AskQty/AskGrade already fold into the aggregate row, now
        // exposed per order for the `ebook` parity surface.
        var askBuckets = new List<MarketOrder>[Goods.All.Count];
        var bidBuckets = new List<MarketOrder>[Goods.All.Count];
        foreach (var o in state.Orders)                   // id order (P6)
        {
            if (o.PortId != portId || o.QtyRemaining <= 0) continue;
            var buckets = o.Side == OrderSide.Sell ? askBuckets : bidBuckets;
            (buckets[o.Good] ??= new List<MarketOrder>()).Add(o);
        }

        var goods = new List<MarketGoodRow>(Goods.All.Count);
        for (int g = 0; g < Goods.All.Count; g++)
        {
            var id = (GoodId)g;
            double askQty = BookOps.AskQty(state, portId, g);
            double askGrade = BookOps.AskGrade(state, portId, g);
            double refPrice = market.Price[g];
            var asks = OrderRows(state, askBuckets[g], refPrice, cheapestFirst: true);
            var bids = OrderRows(state, bidBuckets[g], refPrice, cheapestFirst: false);
            goods.Add(new MarketGoodRow(id, Substrate.Goods.Get(id).Name,
                refPrice, askQty,
                Grades.BandOf(askGrade),
                askGrade, market.LastCleared[g],
                market.BlackBookDemand[g], market.BlackBookPrice[g],
                port.StockQty[g], port.StockGrade[g],
                eco.StockpileDecayPerYear
                    * MarketEngine.StockPerishFactor(id) * decayCut,
                asks, bids));
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

        int currencyId = state.LocalCurrencyOf(portId);
        string? currencyName = currencyId >= 0
            ? state.CurrencyOf(currencyId).Name : null;

        return new MarketCard(portId, port.Tier, port.Hex,
            port.OwnerActorId, state.Actors[port.OwnerActorId].Name,
            port.FoundedYear, MarketEngine.StockCapacityAt(state, port),
            currencyId, currencyName,
            goods, segments, facilities, lanes);
    }

    /// <summary>One good's bucket of live orders, sorted price-time
    /// priority (asks cheapest first, bids dearest first; id breaks a tie
    /// — the SAME order the REPL's <c>ebook</c> and <see cref="BookOps"/>'s
    /// <c>OrderedAsks</c> use) and mapped to display rows. Null bucket (no
    /// orders this good/side) returns the shared empty list.</summary>
    private static IReadOnlyList<BookOrderRow> OrderRows(SimState state,
        List<MarketOrder> bucket, double refPrice, bool cheapestFirst)
    {
        if (bucket == null || bucket.Count == 0)
            return Array.Empty<BookOrderRow>();
        bucket.Sort((x, y) => x.LimitPrice != y.LimitPrice
            ? (cheapestFirst ? x.LimitPrice.CompareTo(y.LimitPrice)
                             : y.LimitPrice.CompareTo(x.LimitPrice))
            : x.Id.CompareTo(y.Id));
        var rows = new List<BookOrderRow>(bucket.Count);
        foreach (var o in bucket)
            rows.Add(new BookOrderRow(o.Id, o.OwnerActorId,
                OwnerName(state, o.OwnerActorId), o.QtyRemaining, o.Grade,
                o.LimitPrice, o.LimitPrice - refPrice, o.EscrowCredits));
        return rows;
    }

    /// <summary>Owner id → name, the same fallback the REPL's `ebook`
    /// renders for an out-of-range actor id.</summary>
    private static string OwnerName(SimState state, int actorId) =>
        actorId >= 0 && actorId < state.Actors.Count
            ? state.Actors[actorId].Name : "—";
}
