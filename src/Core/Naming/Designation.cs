using StarGen.Core.Model;

namespace StarGen.Core.Naming;

/// <summary>Catalog designation (spec §5): axial coords with a +2048 display bias
/// so labels stay non-negative and stable-width. Origin = SGC 2048-2048.</summary>
public static class Designation
{
    public static string For(HexCoordinate coord) =>
        $"SGC {coord.Q + 2048:D4}-{coord.R + 2048:D4}";
}
