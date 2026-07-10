using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Hulls conserve into wreckage at the hex where they died
/// (fleets/ships-and-fleets.md §Attrition): salvage sites today, the
/// battlefield POIs the narrative layer compiles in slice I. Registry in
/// SimState.Wreckage, id order fixed (P6); records are immutable history.</summary>
public sealed class WreckageRecord
{
    public int Id { get; }
    /// <summary>Where they died — a real hex address (P4).</summary>
    public HexCoordinate Hex { get; }
    /// <summary>The design the lost hulls belonged to.</summary>
    public int DesignId { get; }
    public int Hulls { get; }
    public int Year { get; }

    public WreckageRecord(int id, HexCoordinate hex, int designId, int hulls,
                          int year)
    {
        Id = id;
        Hex = hex;
        DesignId = designId;
        Hulls = hulls;
        Year = year;
    }
}
