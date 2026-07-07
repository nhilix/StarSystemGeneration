using StarGen.Core.Model;

namespace StarGen.Core.Naming;

/// <summary>Catalog designation: deterministic, coordinate-derived (spec §7).</summary>
public static class Designation
{
    public static string For(HexCoordinate coord) => $"SGC {coord.X:D4}-{coord.Y:D4}";
}
