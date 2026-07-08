using System;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Tier 1 (spec §4): pure density field = galactic shape × local noise.</summary>
public static class DensityField
{
    public static double At(GalaxyConfig config, HexCoordinate hex)
    {
        double nx = (hex.Q - config.WidthHexes / 2.0) / (config.WidthHexes / 2.0);
        double ny = (hex.R - config.HeightHexes / 2.0) / (config.HeightHexes / 2.0);
        double shape = ShapeAt(config, nx, ny);
        if (shape <= 0) return 0.0;

        double noise = ValueNoise.Warped(config.MasterSeed,
            RollChannel.NoiseDensityLattice, RollChannel.NoiseWarpLattice,
            hex.Q, hex.R, octaves: 3, frequency: 0.035, warpStrength: 18.0);

        // Shape sets the envelope; noise carves clumps/filaments/voids inside it.
        // 0.25 + 1.5*noise spans [0.25, 1.75]: voids suppress, ridges overshoot (clamped).
        double v = shape * (0.25 + 1.5 * noise);
        v *= config.MeanDensityTarget / 0.5;   // shape's disc mean is calibrated to ~0.5
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
