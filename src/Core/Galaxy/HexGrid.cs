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

    private static readonly double Sqrt3 = Math.Sqrt(3.0);

    /// <summary>Flat-top unit-size world position of the hex center (spec §2.1).</summary>
    public static (double X, double Y) HexToWorld(HexCoordinate h) =>
        (1.5 * h.Q, Sqrt3 * (h.R + h.Q / 2.0));

    /// <summary>Inverse of HexToWorld: fractional axial, then cube rounding.</summary>
    public static HexCoordinate WorldToHex(double x, double y)
    {
        double q = x * (2.0 / 3.0);
        double r = y / Sqrt3 - q / 2.0;
        return CubeRound(q, r);
    }

    private static HexCoordinate CubeRound(double q, double r)
    {
        double s = -q - r;
        int rq = (int)Math.Round(q), rr = (int)Math.Round(r), rs = (int)Math.Round(s);
        double dq = Math.Abs(rq - q), dr = Math.Abs(rr - r), ds = Math.Abs(rs - s);
        if (dq > dr && dq > ds) rq = -rr - rs;
        else if (dr > ds) rr = -rq - rs;
        return new HexCoordinate(rq, rr);
    }

    /// <summary>Odd-q offset (display only, spec §2): col = q, odd columns shifted.</summary>
    public static (int Col, int Row) ToOffset(HexCoordinate h) =>
        (h.Q, h.R + (h.Q - (h.Q & 1)) / 2);

    public static HexCoordinate FromOffset(int col, int row) =>
        new(col, row - (col - (col & 1)) / 2);

    /// <summary>The six corner offsets of a flat-top unit hex, corner 0 due east,
    /// counter-clockwise 60° apart. Add to HexToWorld(hex) for mesh vertices —
    /// the atlas builds its triangulation from exactly these (single geometry
    /// authority, atlas spec §2).</summary>
    public static readonly (double X, double Y)[] CornerOffsets = BuildCorners();

    private static (double X, double Y)[] BuildCorners()
    {
        var corners = new (double X, double Y)[6];
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 3.0 * i;   // flat-top: corner 0 at 0°
            corners[i] = (Math.Cos(angle), Math.Sin(angle));
        }
        return corners;
    }
}
