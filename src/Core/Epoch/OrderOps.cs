using System;

namespace StarGen.Core.Epoch;

/// <summary>The order-book escrow primitives (contract-economy spec §1):
/// post holds the escrow inside the order, fill moves credits→seller and
/// returns the goods to the caller at maker price, cancel releases the
/// remainder to the caller. Pure deterministic bookkeeping — matching
/// policy (who trades with whom, tax, labor share) lives in the market
/// step, not here.</summary>
public static class OrderOps
{
    /// <summary>Post goods for sale. The qty+grade are already drawn —
    /// the caller owns conservation up to this call.</summary>
    public static MarketOrder PostSell(SimState state, int ownerActorId,
        int portId, int good, double qty, double grade, double ask,
        int expiryYear)
    {
        var order = new MarketOrder(state.NextOrderId++, OrderSide.Sell,
            ownerActorId, portId, good, ask, qty, grade,
            escrowCredits: 0.0, state.WorldYear, expiryYear);
        state.Orders.Add(order);
        return order;
    }

    /// <summary>Post a bid. The escrow (qty × bid) is already drawn from
    /// wherever the poster's money lives — a ledger, or segment wealth for
    /// the port's band bids; the caller owns conservation up to this call.</summary>
    public static MarketOrder PostBuy(SimState state, int ownerActorId,
        int portId, int good, double qty, double bid, int expiryYear)
    {
        var order = new MarketOrder(state.NextOrderId++, OrderSide.Buy,
            ownerActorId, portId, good, bid, qty, grade: 0.0,
            escrowCredits: qty * bid, state.WorldYear, expiryYear);
        state.Orders.Add(order);
        return order;
    }

    /// <summary>Trade the crossing pair: qty = min of the remainders, at
    /// MAKER price (the earlier-posted order's limit — price-time
    /// priority's resting side). Credits move from the buy's escrow to the
    /// seller's ledger; the goods return to the caller, who routes them
    /// (consumption, site stock, a hold). Bid-limit surplus stays escrowed
    /// until cancel. Zero-quantity orders leave the registry.</summary>
    public static (double Qty, double Grade) Fill(SimState state,
        MarketOrder buy, MarketOrder sell)
    {
        double qty = Math.Min(buy.QtyRemaining, sell.QtyRemaining);
        if (qty <= 0) return (0, 0);
        double price = buy.Id < sell.Id ? buy.LimitPrice : sell.LimitPrice;
        double paid = qty * price;
        buy.QtyRemaining -= qty;
        buy.EscrowCredits -= paid;
        sell.QtyRemaining -= qty;
        var seller = state.LedgerOf(sell.OwnerActorId);
        seller.Credits += paid;
        seller.Receipts += paid;
        Prune(state, sell);
        Prune(state, buy);        // survives while surplus escrow remains
        return (qty, sell.Grade);
    }

    /// <summary>Release a sell order's remaining goods to the caller and
    /// retire it.</summary>
    public static (double Qty, double Grade) CancelSell(SimState state,
        MarketOrder order)
    {
        double qty = order.QtyRemaining;
        double grade = order.Grade;
        order.QtyRemaining = 0;
        Prune(state, order);
        return (qty, grade);
    }

    /// <summary>Release a buy order's remaining escrow (unfilled qty × bid
    /// plus any maker-price surplus) to the caller and retire it.</summary>
    public static double CancelBuy(SimState state, MarketOrder order)
    {
        double credits = order.EscrowCredits;
        order.EscrowCredits = 0;
        order.QtyRemaining = 0;
        Prune(state, order);
        return credits;
    }

    /// <summary>A dead order (no goods, no credits) leaves the registry —
    /// the book is ambient, not history.</summary>
    private static void Prune(SimState state, MarketOrder order)
    {
        if (order.QtyRemaining > 1e-12 || order.EscrowCredits > 1e-12)
            return;
        state.Orders.Remove(order);
    }
}
