using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Minimal mobile-asset record (frame/actors.md): everything abstract
/// is physical — trade flows need hulls, strength is fleet composition. Slice B
/// carries the record shape only; design sheets, aggregation vectors, postures,
/// and movement land with slice E.</summary>
public sealed class FleetRecord
{
    public int Id { get; }
    public int OwnerActorId { get; set; }
    /// <summary>Current hex address — fleets move.</summary>
    public HexCoordinate Hex { get; set; }

    public FleetRecord(int id, int ownerActorId, HexCoordinate hex)
    {
        Id = id;
        OwnerActorId = ownerActorId;
        Hex = hex;
    }
}
