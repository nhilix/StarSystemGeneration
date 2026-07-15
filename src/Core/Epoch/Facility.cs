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
    /// <summary>The specific body this facility claimed within its hex's
    /// system — decided once at groundbreaking (locality slice §4).
    /// None until a real system is committed (bodiless system, or a
    /// gate/support asset that rides the port body).</summary>
    public BodyRef Body { get; set; } = BodyRef.None;
    public int OwnerActorId { get; set; }
    /// <summary>Decays without upkeep, damaged by war, repaired by investment;
    /// output scales with it (economy/assets-and-investment.md).</summary>
    public double Condition { get; set; } = 1.0;
    public int BuiltYear { get; }
    /// <summary>World-year output began; −1 while under construction (the
    /// site exists before the facility does, P1 — spec §1 replaced the
    /// BuiltYear+ConstructionYears date-check with delivered work).</summary>
    public long CommissionedYear { get; set; }

    public Facility(int id, int typeId, int tier, HexCoordinate hex,
                    int ownerActorId, int builtYear)
    {
        Id = id;
        TypeId = typeId;
        Tier = tier;
        Hex = hex;
        OwnerActorId = ownerActorId;
        BuiltYear = builtYear;
        CommissionedYear = builtYear;
    }
}
