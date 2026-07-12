namespace StarGen.Core.Epoch;

public enum OrderSide { Buy = 0, Sell = 1 }

/// <summary>One open order on a port's book (contract-economy spec §1).
/// Escrow is physical: a sell order HOLDS the goods (QtyRemaining at Grade —
/// the anonymous market shelf is dead, the shelf is the union of live sell
/// orders), a buy order HOLDS the credits (EscrowCredits, drawn whole at
/// post). A fill can never bounce — the escrow was checked at the door.
/// The escrow is already drawn when an order is posted; the caller owns
/// conservation up to that call (the ShipmentOps convention). Registry in
/// SimState.Orders, id = creation order (P6); filled and cancelled orders
/// leave the registry (the book is ambient, not history — NextOrderId keeps
/// identity stable).</summary>
public sealed class MarketOrder
{
    public int Id { get; }
    public OrderSide Side { get; }
    /// <summary>Settable: estates pass — dissolution abandons a corp's
    /// resting sells to the port's sovereign, nationalization seizes them.</summary>
    public int OwnerActorId { get; set; }
    public int PortId { get; }
    public int Good { get; }
    /// <summary>Ask for sells, bid for buys — per unit. Settable: sellers
    /// decay unsold quotes (the glut half of the old price drift).</summary>
    public double LimitPrice { get; set; }
    public double QtyRemaining { get; set; }
    /// <summary>The held goods' grade (sells; 0 for buys).</summary>
    public double Grade { get; set; }
    /// <summary>Credits held by a buy order. Fills pay the seller at maker
    /// price; any bid-limit surplus stays here until cancel/expiry, so
    /// refunds return where the escrow came from (a ledger, or segment
    /// wealth for the port's band bids — the poster knows, the book
    /// doesn't).</summary>
    public double EscrowCredits { get; set; }
    public int PostedYear { get; }
    public int ExpiryYear { get; }

    public MarketOrder(int id, OrderSide side, int ownerActorId, int portId,
                       int good, double limitPrice, double qtyRemaining,
                       double grade, double escrowCredits, int postedYear,
                       int expiryYear)
    {
        Id = id;
        Side = side;
        OwnerActorId = ownerActorId;
        PortId = portId;
        Good = good;
        LimitPrice = limitPrice;
        QtyRemaining = qtyRemaining;
        Grade = grade;
        EscrowCredits = escrowCredits;
        PostedYear = postedYear;
        ExpiryYear = expiryYear;
    }
}
