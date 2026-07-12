using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>Ranking among open couriers: war logistics outbid commerce for
/// hulls (contract-economy spec §4).</summary>
public enum CourierPriority { War = 0, Normal = 1 }

public enum CourierStatus { Open = 0, InTransit = 1, Delivered = 2,
                            Lost = 3, Expired = 4 }

/// <summary>The internal-logistics record (contract-economy spec §1): move
/// MY goods from A to B for a posted fee — the EVE courier. Cargo escrows
/// from the poster's origin larder at post, the fee from the poster's
/// ledger; acceptance rides a Shipment (blockade stalls and piracy apply
/// like any freight); delivery banks the cargo in the destination larder
/// and pays the fee to the fulfiller; expiry returns both. State
/// requisitions, corp internal moves, and war-front convoys are all this
/// one record at different priorities. Registry in SimState.Couriers,
/// id = creation order (P6), live only — NextCourierId keeps identity.</summary>
public sealed class CourierContract
{
    public int Id { get; }
    public int PosterActorId { get; }
    public int OriginPortId { get; }
    public int DestPortId { get; }
    /// <summary>Escrowed cargo per good; grade rides alongside.</summary>
    public double[] Qty { get; } = new double[Goods.All.Count];
    public double[] Grade { get; } = new double[Goods.All.Count];
    /// <summary>Credits held for the fulfiller — paid at delivery,
    /// refunded at expiry.</summary>
    public double FeeEscrow { get; set; }
    public CourierPriority Priority { get; }
    public int PostedYear { get; }
    public int ExpiryYear { get; }
    public CourierStatus Status { get; set; } = CourierStatus.Open;
    public int FulfillerActorId { get; set; } = -1;
    /// <summary>The shipment carrying the cargo once accepted; −1 open.</summary>
    public int ShipmentId { get; set; } = -1;

    public CourierContract(int id, int posterActorId, int originPortId,
                           int destPortId, double feeEscrow,
                           CourierPriority priority, int postedYear,
                           int expiryYear)
    {
        Id = id;
        PosterActorId = posterActorId;
        OriginPortId = originPortId;
        DestPortId = destPortId;
        FeeEscrow = feeEscrow;
        Priority = priority;
        PostedYear = postedYear;
        ExpiryYear = expiryYear;
    }
}
