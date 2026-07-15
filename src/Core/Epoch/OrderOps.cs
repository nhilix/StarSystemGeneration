using System;
using System.Collections.Generic;

namespace StarGen.Core.Epoch;

/// <summary>One executed trade: the buy order it filled (its owner routes
/// the goods — consumption, site stock, a hold) and what changed hands.</summary>
public readonly record struct OrderFill(MarketOrder Buy, int Good,
                                        double Qty, double Grade);

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

    /// <summary>Sweep orders past their expiry (spec §2 step 2 — review
    /// wave, finding 4): a buy refunds its remaining escrow to the owner's
    /// ledger; a sell's abandoned goods escheat to the port's stockpile —
    /// dock storage lapsed, the sovereign's warehouse takes the cargo
    /// (conserving; ownership is the fee). Id order (P6).</summary>
    public static int ExpireOrders(SimState state)
    {
        int expired = 0;
        for (int i = state.Orders.Count - 1; i >= 0; i--)
        {
            var o = state.Orders[i];
            if (state.WorldYear <= o.ExpiryYear) continue;
            if (o.Side == OrderSide.Buy)
                // the escrow is in the port's local currency; a foreign buyer's
                // refund converts back into their own currency (design §1)
                state.CreditLocal(o.OwnerActorId, CancelBuy(state, o),
                                  state.LocalCurrencyOf(o.PortId));
            else
            {
                var (qty, grade) = CancelSell(state, o);
                if (qty > 0)
                    state.Ports[o.PortId].DepositStock(o.Good, qty, grade);
            }
            expired++;
        }
        return expired;
    }

    /// <summary>Trade the crossing pair: qty = min of the remainders, at
    /// MAKER price (the earlier-posted order's limit — price-time
    /// priority's resting side). The buy's escrow (held in the port's local
    /// currency) is released by <paramref name="paid"/>; the goods return to the
    /// caller, who routes them (consumption, site stock, a hold). The SELLER is
    /// NOT paid here — <see cref="SettleSale"/> owns the whole local-currency
    /// split (tax and wages stay local, only the seller's net converts into their
    /// own currency), so the local deductions land before any conversion (design
    /// §1). Bid-limit surplus stays escrowed until cancel. Zero-quantity orders
    /// leave the registry.</summary>
    public static (double Qty, double Grade, double Paid) Fill(SimState state,
        MarketOrder buy, MarketOrder sell)
    {
        double qty = Math.Min(buy.QtyRemaining, sell.QtyRemaining);
        if (qty <= 0) return (0, 0, 0);
        double price = buy.Id < sell.Id ? buy.LimitPrice : sell.LimitPrice;
        double paid = qty * price;
        buy.QtyRemaining -= qty;
        buy.EscrowCredits -= paid;
        sell.QtyRemaining -= qty;
        Prune(state, sell);
        Prune(state, buy);        // survives while surplus escrow remains
        return (qty, sell.Grade, paid);
    }

    /// <summary>Cross one port's book (spec §2 step 3): per good ascending,
    /// while best bid ≥ best ask, fill at maker price with (price, order id)
    /// priority. Each fill settles its consequences: transaction tax on the
    /// proceeds to the port's sovereign, the labor share of the seller's net
    /// to the local segments (household income is earned from realized
    /// revenue), the rest stays with the seller. Returns the fills for the
    /// caller to route the goods. Pure ordered math — no rolls; the
    /// reference price moves separately, on the book's imbalance
    /// (MarketEngine.MatchAndClear).</summary>
    public static List<OrderFill> MatchPort(SimState state, int portId)
    {
        var fills = new List<OrderFill>();
        var market = state.Markets[portId];

        var buys = new List<MarketOrder>();
        var sells = new List<MarketOrder>();
        foreach (var o in state.Orders)                   // id order (P6)
        {
            if (o.PortId != portId || o.QtyRemaining <= 0) continue;
            (o.Side == OrderSide.Buy ? buys : sells).Add(o);
        }
        // price-time priority: best price first, earlier id within a price
        buys.Sort((x, y) => x.LimitPrice != y.LimitPrice
            ? y.LimitPrice.CompareTo(x.LimitPrice) : x.Id.CompareTo(y.Id));
        sells.Sort((x, y) => x.LimitPrice != y.LimitPrice
            ? x.LimitPrice.CompareTo(y.LimitPrice) : x.Id.CompareTo(y.Id));

        for (int good = 0; good < Substrate.Goods.All.Count; good++)
        {
            int bi = 0, si = 0;
            while (true)
            {
                while (bi < buys.Count && (buys[bi].Good != good
                       || buys[bi].QtyRemaining <= 0)) bi++;
                while (si < sells.Count && (sells[si].Good != good
                       || sells[si].QtyRemaining <= 0)) si++;
                if (bi >= buys.Count || si >= sells.Count) break;
                var buy = buys[bi];
                var sell = sells[si];
                if (buy.LimitPrice < sell.LimitPrice) break;
                int sellerId = sell.OwnerActorId;
                var (qty, grade, paid) = Fill(state, buy, sell);
                if (qty <= 0) break;
                SettleSale(state, portId, sellerId, paid);
                market.LastCleared[good] += qty;
                fills.Add(new OrderFill(buy, good, qty, grade));
            }
        }
        return fills;
    }

    /// <summary>A sale's whole settlement, shared by matching and ask-lifting.
    /// <paramref name="paid"/> is the gross in the port's LOCAL currency. Tax and
    /// the labor wage share are local by construction — the sovereign owns the
    /// port and the paid segments live in it — so they deduct from the gross
    /// FIRST and credit the sovereign and local segments in local currency with no
    /// conversion. Only the seller's NET remainder crosses a currency boundary: it
    /// converts into the seller's own currency (a polity) or banks unconverted (a
    /// corporation) at the point of crediting (design §1 — local deductions before
    /// the conversion, not after). The seller is credited HERE, not by
    /// <see cref="Fill"/>/<see cref="BookOps.LiftAsks"/>.</summary>
    internal static void SettleSale(SimState state, int portId,
                                    int sellerActorId, double paid)
    {
        if (paid <= 0) return;
        var port = state.Ports[portId];
        double taxRate = (state.Actors[port.OwnerActorId].Policies
            as PolityPolicies ?? PolityPolicies.Default).TaxRate;
        var sovereign = state.PolityOf(port.OwnerActorId);
        double tax = paid * taxRate;
        double wages = (paid - tax) * state.Config.Economy.LaborShare;
        double net = paid - tax - wages;
        // local share — no conversion (sovereign IS the local polity; wages pay
        // its resident segments, whose wealth resolves to the same currency)
        sovereign.Credits += tax;
        sovereign.Receipts += tax;
        MarketEngine.PayWages(state, portId, wages);
        // the seller's net converts into their own currency (or banks unconverted
        // for a corp); Receipts mirrors the amount banked, in that denomination
        double banked = state.CreditLocal(sellerActorId, net,
                                          sovereign.CurrencyId);
        state.LedgerOf(sellerActorId).Receipts += banked;
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
