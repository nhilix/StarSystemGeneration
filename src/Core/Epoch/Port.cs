using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The keystone infrastructure (space-and-travel.md): a starport at a
/// specific hex with two independent growth axes — local service radius and
/// inter-port range — both derived from tier, never stored. Registry entry in
/// SimState.Ports, id order fixed (P6). Claiming space is building a port;
/// homeworlds are simply the first ports.</summary>
public sealed class Port
{
    public int Id { get; }
    /// <summary>Owning polity's actor id. Settable: conquest transfers ports
    /// intact (slice H).</summary>
    public int OwnerActorId { get; set; }
    /// <summary>The physical carrier — no political fact without one (P4).</summary>
    public HexCoordinate Hex { get; }
    /// <summary>1..MaxPortTier (outpost → starport → nexus), raised by
    /// Allocation-phase investment.</summary>
    public int Tier { get; set; }
    public int FoundedYear { get; }

    public Port(int id, int ownerActorId, HexCoordinate hex, int tier, int foundedYear)
    {
        Id = id;
        OwnerActorId = ownerActorId;
        Hex = hex;
        Tier = tier;
        FoundedYear = foundedYear;
    }
}
