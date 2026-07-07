using StarGen.Core.Model;
using StarGen.Core.Tables;

namespace StarGen.Core.Content;

/// <summary>Satellite counts/eligibility (plan resolution #3).</summary>
public static class SatelliteTables
{
    public static readonly WeightedTable<int> GasGiantCount = new(
        (0, 10), (1, 25), (2, 30), (3, 20), (4, 15));

    public static readonly WeightedTable<int> WorldCount = new(
        (0, 40), (1, 35), (2, 20), (3, 5));

    public static readonly WeightedTable<BodyKind> Kind = new(
        (BodyKind.RockyWorld, 70), (BodyKind.IceWorld, 30));
}
