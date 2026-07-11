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
    private const int MaxStarsPerCell = 52;
    /// <summary>Scatter radius in hex-world units — a little past the
    /// superhex so neighboring cells' fields blend.</summary>
    private const double ScatterRadius = 9.5;
    /// <summary>Share of stars in the bright population.</summary>
    private const double BrightShare = 0.08;
    /// <summary>Clump filaments ride finer noise than the nebula fields.</summary>
    private const double ClumpFrequency = 0.15;
    private const ulong Channel = 0x51A2F1E1D;

    public static IReadOnlyList<StarPoint> Stars(AtlasReadModel model)
    {
        ulong seed = model.Skeleton.Config.MasterSeed;
        var stars = new List<StarPoint>(model.Cells.Count * 12);
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
                // Second independent hash: shifting h past 43 bits starves
                // the 21-bit Frac mask (a always-zero draw).
                ulong h2 = StableHash.Mix(Channel + 1,
                    (ulong)(long)cell.Coord.Q, (ulong)(long)cell.Coord.R, (ulong)k);
                double angle = Frac(h) * Math.PI * 2.0;
                double radius = Math.Sqrt(Frac(h >> 21)) * ScatterRadius;
                double x = cx + Math.Cos(angle) * radius;
                double y = cy + Math.Sin(angle) * radius;
                // Two populations: a dim majority gives the disc its
                // body, rare bright stars give it sparkle.
                double brightness = Frac(h2) < BrightShare
                    ? 0.72 + 0.28 * Frac(h2 >> 21)
                    : 0.10 + 0.42 * Frac(h2 >> 21);
                // Filament clumping: the same deterministic noise family
                // as the nebula fields, finer wavelength — texture, not
                // erasure (the dim majority carries the disc's body).
                double clump = ValueNoise.Sample(seed, RollChannel.AtlasNebula,
                    x, y, octaves: 2, ClumpFrequency);
                brightness *= 0.55 + 1.0 * clump;
                stars.Add(new StarPoint(i, x, y,
                    Math.Clamp(brightness, 0.03, 1.0)));
            }
        }
        return stars;
    }

    private static double Frac(ulong h) => (h & 0x1FFFFF) / (double)0x200000;
}
