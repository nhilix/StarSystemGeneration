using System;

namespace StarGen.Core.Epoch;

/// <summary>The book-step policy layer (contract-economy spec §2): how
/// production reaches the book, how quotes move, and how a buyer with a
/// budget lifts asks. OrderOps holds the neutral escrow mechanics; this is
/// the market step's behavior on top of them. Everything deterministic:
/// orders by (price, id), owners by id, no rolls.</summary>
public static class BookOps
{
    /// <summary>Land production on the book: one resting sell per (owner,
    /// port, good) — new output blends into the surviving order (its quote
    /// stands; glut keeps pressing it down via DecayAsks), a fresh order
    /// quotes at the reference price × the posting markup. The goods are
    /// already drawn — the caller owns conservation up to this call.</summary>
    public static MarketOrder PostSupply(SimState state, int portId,
        int ownerActorId, int good, double qty, double grade)
    {
        foreach (var o in state.Orders)                   // id order (P6)
            if (o.Side == OrderSide.Sell && o.PortId == portId
                && o.OwnerActorId == ownerActorId && o.Good == good
                && o.QtyRemaining > 0)
            {
                double total = o.QtyRemaining + qty;
                o.Grade = (o.QtyRemaining * o.Grade + qty * grade) / total;
                o.QtyRemaining = total;
                return o;
            }
        var eco = state.Config.Economy;
        double ask = Math.Max(eco.PriceFloor,
            state.Markets[portId].Price[good] * eco.AskMarkupOnPost);
        return OrderOps.PostSell(state, ownerActorId, portId, good, qty,
            grade, ask,
            state.WorldYear + (int)Math.Round(eco.OrderExpiryYears));
    }

    /// <summary>Unsold quotes give ground: every surviving sell order cuts
    /// its ask per world-year sat unsold — the glut half of the old price
    /// drift, now visibly in sellers' hands. Compounded per year (P7).</summary>
    public static void DecayAsks(SimState state)
    {
        var eco = state.Config.Economy;
        double keep = Math.Pow(1.0 - eco.AskDecayPerYear,
                               state.Config.Sim.YearsPerEpoch);
        foreach (var o in state.Orders)                   // id order (P6)
            if (o.Side == OrderSide.Sell)
                o.LimitPrice = Math.Max(eco.PriceFloor, o.LimitPrice * keep);
    }

    /// <summary>Buy up to qty off the port's asks, cheapest first, within
    /// the budget — the immediate-consumption path (recipe inputs, upkeep,
    /// a fleet provisioning). Sellers are paid at their ask and settled
    /// (tax, wages) like any fill; the caller owns debiting the buyer for
    /// the returned cost. Returns what was drawn, its blended grade, and
    /// what it cost.</summary>
    public static (double Drawn, double Grade, double Cost) LiftAsks(
        SimState state, int portId, int good, double qty, double budget)
    {
        var sells = OrderedAsks(state, portId, good);
        double drawn = 0, gradeSum = 0, cost = 0;
        foreach (var o in sells)
        {
            if (qty - drawn <= 1e-12 || budget - cost <= 1e-12) break;
            double take = Math.Min(qty - drawn, o.QtyRemaining);
            if (o.LimitPrice > 0)
                take = Math.Min(take, (budget - cost) / o.LimitPrice);
            if (take <= 0) break;                // even the cheapest ask is
                                                 // beyond the budget
            double paid = take * o.LimitPrice;
            o.QtyRemaining -= take;
            var seller = state.LedgerOf(o.OwnerActorId);
            seller.Credits += paid;
            seller.Receipts += paid;
            OrderOps.SettleSale(state, portId, o.OwnerActorId, paid);
            state.Markets[portId].LastCleared[good] += take;
            drawn += take;
            gradeSum += take * o.Grade;
            cost += paid;
            if (o.QtyRemaining <= 1e-12 && o.EscrowCredits <= 1e-12)
                state.Orders.Remove(o);
        }
        return (drawn, drawn > 0 ? gradeSum / drawn : 0, cost);
    }

    /// <summary>Live asks at (port, good), cheapest first, id within a
    /// price — the lift order and the "best ask" definition.</summary>
    private static System.Collections.Generic.List<MarketOrder> OrderedAsks(
        SimState state, int portId, int good)
    {
        var sells = new System.Collections.Generic.List<MarketOrder>();
        foreach (var o in state.Orders)                   // id order (P6)
            if (o.Side == OrderSide.Sell && o.PortId == portId
                && o.Good == good && o.QtyRemaining > 0)
                sells.Add(o);
        sells.Sort((x, y) => x.LimitPrice != y.LimitPrice
            ? x.LimitPrice.CompareTo(y.LimitPrice) : x.Id.CompareTo(y.Id));
        return sells;
    }
}
