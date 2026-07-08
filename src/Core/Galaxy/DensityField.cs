using System;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Tier 1 (spec §4): pure density field = galactic shape × local noise.</summary>
public static class DensityField
{
    private static readonly HexCoordinate Origin = new(0, 0);

    public static bool InGalaxy(GalaxyConfig config, HexCoordinate hex) =>
        HexGrid.Distance(HexGrid.CellOf(hex), Origin) <= config.GalaxyRadiusCells;

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
        double core = Math.Exp(-(r * r) / (2 * 0.18 * 0.18));            // bright center
        double disc = Math.Exp(-(r * r) / (2 * 0.55 * 0.55));            // broad falloff

        // Log-spiral arms: angular distance to the nearest arm ridge at this radius.
        double armAngle = Math.Log(Math.Max(r, 0.05)) / config.ArmTightness;
        double phase = (theta - armAngle) * config.ArmCount / (2 * Math.PI);
        double toRidge = Math.Abs(phase - Math.Round(phase)) * 2;        // 0 at ridge, 1 between
        double arms = Math.Exp(-(toRidge * toRidge) / (2 * config.ArmWidth * config.ArmWidth))
                      * (1 - core) * 0.9;

        return Math.Clamp(core + disc * 0.45 + arms * disc, 0.0, 1.0);
    }
}
