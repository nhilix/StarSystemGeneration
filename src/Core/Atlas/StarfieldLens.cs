using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Rng;

namespace StarGen.Core.Atlas;

/// <summary>One rendered star: a point in hex-world coordinates plus its
/// raster cell (for density/lean lookups) and a brightness weight.</summary>
public readonly record struct StarPoint(
    int CellIndex, double X, double Y, double Brightness);

/// <summary>The starfield — the density raster read as stars, the design
/// artifact's base layer: the disc's arms, bulge and halo emerge from
/// star density instead of filled cells. Placement and brightness are
/// stateless StableHash derivations keyed on the cell coordinate — the
/// same galaxy always wears the same sky.</summary>
public static class StarfieldLens
{
    /// <summary>Stars per cell at full density; scaled by MeanDensity.</summary>
    private const int MaxStarsPerCell = 14;
    /// <summary>Scatter radius in hex-world units — inside the superhex.</summary>
    private const double ScatterRadius = 8.0;
    private const ulong Channel = 0x51A2F1E1D;

    public static IReadOnlyList<StarPoint> Stars(AtlasReadModel model)
    {
        var stars = new List<StarPoint>(model.Cells.Count * 4);
        for (int i = 0; i < model.Cells.Count; i++)
        {
            var cell = model.Cells[i];
            int count = (int)Math.Round(cell.MeanDensity * MaxStarsPerCell);
            if (count <= 0) continue;
            var (cx, cy) = HexGrid.HexToWorld(HexGrid.CellCenter(cell.Coord));
            for (int k = 0; k < count; k++)
            {
                ulong h = StableHash.Mix(Channel,
                    (ulong)(long)cell.Coord.Q, (ulong)(long)cell.Coord.R, (ulong)k);
                double angle = Frac(h) * Math.PI * 2.0;
                double radius = Math.Sqrt(Frac(h >> 21)) * ScatterRadius;
                double brightness = 0.15 + 0.85 * Frac(h >> 42);
                stars.Add(new StarPoint(i,
                    cx + Math.Cos(angle) * radius,
                    cy + Math.Sin(angle) * radius,
                    brightness));
            }
        }
        return stars;
    }

    private static double Frac(ulong h) => (h & 0x1FFFFF) / (double)0x200000;
}
