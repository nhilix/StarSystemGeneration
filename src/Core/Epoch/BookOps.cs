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

    /// <summary>Resting sells re-anchor to the CURRENT reference each step
    /// (nobody quotes yesterday's market): price discovery lives in the
    /// reference itself, which drifts on book imbalance in MatchAndClear —
    /// the old rate-limited, tick-honest drift, now fed by real unfilled
    /// bids and unsold asks instead of phantom signals.</summary>
    public static void RepriceAsks(SimState state)
    {
        var eco = state.Config.Economy;
        foreach (var o in state.Orders)                   // id order (P6)
            if (o.Side == OrderSide.Sell)
                o.LimitPrice = Math.Max(eco.PriceFloor,
                    state.Markets[o.PortId].Price[o.Good]
                    * eco.AskMarkupOnPost);
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

    /// <summary>Total live ask quantity at (port, good) — what a buyer or
    /// a recipe could possibly obtain locally this step.</summary>
    public static double AskQty(SimState state, int portId, int good)
    {
        double qty = 0;
        foreach (var o in state.Orders)                   // id order (P6)
            if (o.Side == OrderSide.Sell && o.PortId == portId
                && o.Good == good)
                qty += o.QtyRemaining;
        return qty;
    }

    /// <summary>Quantity-weighted grade of the live asks at (port, good) —
    /// 0.5 (the grade system's neutral midpoint) when the book is bare.</summary>
    public static double AskGrade(SimState state, int portId, int good)
    {
        double qty = 0, sum = 0;
        foreach (var o in state.Orders)                   // id order (P6)
            if (o.Side == OrderSide.Sell && o.PortId == portId
                && o.Good == good && o.QtyRemaining > 0)
            { qty += o.QtyRemaining; sum += o.QtyRemaining * o.Grade; }
        return qty > 0 ? sum / qty : 0.5;
    }

    /// <summary>The cheapest live ask at (port, good); MaxValue when none.</summary>
    public static double BestAsk(SimState state, int portId, int good)
    {
        double best = double.MaxValue;
        foreach (var o in state.Orders)                   // id order (P6)
            if (o.Side == OrderSide.Sell && o.PortId == portId
                && o.Good == good && o.QtyRemaining > 0
                && o.LimitPrice < best)
                best = o.LimitPrice;
        return best;
    }

    /// <summary>Live bid quantity at (port, good) with a limit at or above
    /// the floor — the real demand a hauler can sell into (the bridge's
    /// absorption term; the old phantom demand signal is dead).</summary>
    public static double BidDepthAbove(SimState state, int portId, int good,
                                       double floor)
    {
        double qty = 0;
        foreach (var o in state.Orders)                   // id order (P6)
            if (o.Side == OrderSide.Buy && o.PortId == portId
                && o.Good == good && o.LimitPrice >= floor)
                qty += o.QtyRemaining;
        return qty;
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
