using System;
using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>
/// All hex geometry (spec §2.1). Axial coordinates, flat-top orientation.
/// The single authority consumed by the sim, the inspector, and Unity rendering.
/// </summary>
public static class HexGrid
{
    /// <summary>Pinned flat-top direction order (spec: D0..D5). Never reorder.</summary>
    public static readonly HexCoordinate[] Directions =
    {
        new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1),
    };

    public static HexCoordinate Neighbor(HexCoordinate h, int direction)
    {
        var d = Directions[direction];
        return new HexCoordinate(h.Q + d.Q, h.R + d.R);
    }

    public static IEnumerable<HexCoordinate> Neighbors(HexCoordinate h)
    {
        for (int i = 0; i < 6; i++) yield return Neighbor(h, i);
    }

    public static int Distance(HexCoordinate a, HexCoordinate b)
    {
        int dq = a.Q - b.Q, dr = a.R - b.R, ds = -dq - dr;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(ds)) / 2;
    }

    /// <summary>The ring at exactly <paramref name="radius"/> (≥ 1): starts at
    /// center + D4*radius, walks D0..D5, radius steps each. Deterministic.</summary>
    public static IEnumerable<HexCoordinate> Ring(HexCoordinate center, int radius)
    {
        var hex = new HexCoordinate(center.Q + Directions[4].Q * radius,
                                    center.R + Directions[4].R * radius);
        for (int direction = 0; direction < 6; direction++)
            for (int step = 0; step < radius; step++)
            {
                yield return hex;
                hex = Neighbor(hex, direction);
            }
    }

    /// <summary>Center, then rings 1..radius — the canonical deterministic
    /// enumeration (3r(r+1)+1 hexes).</summary>
    public static IEnumerable<HexCoordinate> Spiral(HexCoordinate center, int radius)
    {
        yield return center;
        for (int r = 1; r <= radius; r++)
            foreach (var hex in Ring(center, r))
                yield return hex;
    }
}
