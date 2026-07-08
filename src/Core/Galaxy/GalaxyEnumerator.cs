using System.Linq;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>The infinite deterministic hex walk from the origin — the inspector's
/// linear position space (replaces row-major walks; galaxy and flatspace share it).</summary>
public static class GalaxyEnumerator
{
    public static HexCoordinate SpiralAt(int index)
    {
        if (index <= 0) return new HexCoordinate(0, 0);
        int d = 1;
        while (3 * d * (d + 1) + 1 <= index) d++;
        int pos = index - (3 * d * (d - 1) + 1);
        int side = pos / d, step = pos % d;

        var hex = new HexCoordinate(HexGrid.Directions[4].Q * d, HexGrid.Directions[4].R * d);
        for (int i = 0; i < side; i++)
            hex = new HexCoordinate(hex.Q + HexGrid.Directions[i].Q * d,
                                    hex.R + HexGrid.Directions[i].R * d);
        return new HexCoordinate(hex.Q + HexGrid.Directions[side].Q * step,
                                 hex.R + HexGrid.Directions[side].R * step);
    }

    public static int SpiralIndexOf(HexCoordinate hex)
    {
        var origin = new HexCoordinate(0, 0);
        int d = HexGrid.Distance(origin, hex);
        if (d == 0) return 0;
        int pos = HexGrid.Ring(origin, d).ToList().IndexOf(hex);
        return 3 * d * (d - 1) + 1 + pos;
    }
}
