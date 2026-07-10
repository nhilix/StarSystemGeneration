using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>An anchored, immobile asset (substrate/infrastructure.md): mine,
/// refinery, shipyard… — a pre-commitment at a hex, so the hex a player visits
/// shows the facility the simulation built (P1, P4). Registry entry in
/// SimState.Facilities. Slice B carries the shape only; the 14-type catalog and
/// siting rules are slice C's, construction/ownership lifecycles slice D's.</summary>
public sealed class Facility
{
    public int Id { get; }
    /// <summary>Infrastructure catalog type id (slice C); opaque here.</summary>
    public int TypeId { get; }
    public int Tier { get; set; }
    public HexCoordinate Hex { get; }
    public int OwnerActorId { get; set; }
    /// <summary>Decays without upkeep, damaged by war, repaired by investment;
    /// output scales with it (economy/assets-and-investment.md).</summary>
    public double Condition { get; set; } = 1.0;
    public int BuiltYear { get; }

    public Facility(int id, int typeId, int tier, HexCoordinate hex,
                    int ownerActorId, int builtYear)
    {
        Id = id;
        TypeId = typeId;
        Tier = tier;
        Hex = hex;
        OwnerActorId = ownerActorId;
        BuiltYear = builtYear;
    }
}
