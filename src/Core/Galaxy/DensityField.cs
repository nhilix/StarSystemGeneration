using System;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Tier 1 (spec §4): pure density field = galactic shape × local noise.</summary>
public static class DensityField
{
    /// <summary>World-space length of one cell-lattice step (√273 with the pinned
    /// 91-hex basis; both basis vectors have the same length).</summary>
    private static readonly double CellLatticeUnit = CellWorldDistance(new HexCoordinate(1, 0));

    public static bool InGalaxy(GalaxyConfig config, HexCoordinate hex) =>
        CellInGalaxy(config, HexGrid.CellOf(hex));

    /// <summary>Circular footprint, decided per cell so hex and cell membership
    /// always agree: a cell is in the galaxy when its center lies within
    /// GalaxyRadiusCells lattice steps of the origin in Euclidean world distance.
    /// This circle circumscribes the former hexagonal footprint (whose flat sides
    /// sat at ~86.6% of the corner distance and cropped the spiral arms), so the
    /// arms now reach full length in every direction.</summary>
    public static bool CellInGalaxy(GalaxyConfig config, HexCoordinate cellCoord) =>
        CellWorldDistance(cellCoord) <= CellLatticeUnit * config.GalaxyRadiusCells + 1e-9;

    private static double CellWorldDistance(HexCoordinate cellCoord)
    {
        var (x, y) = HexGrid.HexToWorld(HexGrid.CellCenter(cellCoord));
        return Math.Sqrt(x * x + y * y);
    }

    /// <summary>World radius used to normalize the shape function: one cell ring
    /// beyond the lattice, so density falls smoothly toward the membership rim.</summary>
    public static double WorldRimRadius(GalaxyConfig config)
    {
        var (x, y) = HexGrid.HexToWorld(
            HexGrid.CellCenter(new HexCoordinate(config.GalaxyRadiusCells + 1, 0)));
        return Math.Sqrt(x * x + y * y);
    }

    public static double At(GalaxyConfig config, HexCoordinate hex)
    {
        if (!InGalaxy(config, hex)) return 0.0;

        var (wx, wy) = HexGrid.HexToWorld(hex);
        double rim = WorldRimRadius(config);
        double shape = ShapeAt(config, wx / rim, wy / rim);
        if (shape <= 0) return 0.0;

        double noise = ValueNoise.Warped(config.MasterSeed,
            RollChannel.NoiseDensityLattice, RollChannel.NoiseWarpLattice,
            wx, wy, octaves: 3, frequency: 0.02, warpStrength: 30.0);

        double v = shape * (0.25 + 1.5 * noise);
        v *= config.MeanDensityTarget / 0.5;
        return Math.Clamp(v, 0.0, 1.0);
    }

    /// <summary>Shape only, normalized coords (|n| = 1 at rim). Disc mean ≈ 0.5.</summary>
    public static double ShapeAt(GalaxyConfig config, double nx, double ny)
    {
        double r = Math.Sqrt(nx * nx + ny * ny);
        if (r >= 1.0) return 0.0;                       // hard zero beyond the rim

        double theta = Math.Atan2(ny, nx);
        double core = Math.Exp(-(r * r) / (2 * config.CoreRadius * config.CoreRadius));            // bright center
        double disc = Math.Exp(-(r * r) / (2 * config.DiscFalloff * config.DiscFalloff));            // broad falloff

        // Log-spiral arms: angular distance to the nearest arm ridge at this radius.
        double armAngle = Math.Log(Math.Max(r, 0.05)) / config.ArmTightness;
        double phase = (theta - armAngle) * config.ArmCount / (2 * Math.PI);
        double toRidge = Math.Abs(phase - Math.Round(phase)) * 2;        // 0 at ridge, 1 between
        double arms = Math.Exp(-(toRidge * toRidge) / (2 * config.ArmWidth * config.ArmWidth))
                      * (1 - core) * config.ArmStrength;

        return Math.Clamp(core + disc * 0.45 + arms * disc, 0.0, 1.0);
    }
}
