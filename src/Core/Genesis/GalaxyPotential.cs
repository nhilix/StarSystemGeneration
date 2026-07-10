using System;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Genesis;

/// <summary>The potential prior (genesis/cosmic-genesis.md): the analytic
/// shape function read as gravitational potential — where matter *wants* to
/// be; the cosmic sim decides where it actually ends up. Mildly time-varying:
/// the arm pattern is a fixed density wave matter moves through, the core
/// deepens slowly with formation progress, and merger events add transient
/// decaying perturbations (composed by CosmicSim, not here). Same core / disc
/// / log-spiral-arm math as the retired analytic DensityField paint, so the
/// shape knobs keep their meaning as potential parameters.</summary>
public static class GalaxyPotential
{
    /// <summary>Fraction of the core's final depth already present at t01 = 0
    /// (structural: the bulge assembles over the whole formation history).</summary>
    private const double CoreDepthAtBirth = 0.55;

    /// <summary>Potential at normalized coords (|n| = 1 at the rim), for
    /// formation progress t01 in [0, 1]. Non-negative, ~1.45 at a fully
    /// deepened core, zero beyond the rim. Unlike the retired density paint
    /// there is no upper clamp: the potential is relative — transport reads
    /// gradients and the present-day field is normalized at finalization
    /// (MeanDensityTarget), so clipping the well would only flatten the
    /// core's pull.</summary>
    public static double At(GalaxyConfig config, double nx, double ny, double t01)
    {
        double r = Math.Sqrt(nx * nx + ny * ny);
        if (r >= 1.0) return 0.0;                       // hard zero beyond the rim

        double theta = Math.Atan2(ny, nx);
        double coreDepth = CoreDepthAtBirth
                           + (1 - CoreDepthAtBirth) * Math.Clamp(t01, 0.0, 1.0);
        double core = coreDepth
            * Math.Exp(-(r * r) / (2 * config.CoreRadius * config.CoreRadius));
        double disc = Math.Exp(-(r * r) / (2 * config.DiscFalloff * config.DiscFalloff));

        // Log-spiral arms: angular distance to the nearest arm ridge at this
        // radius — the pattern itself never moves (a density wave).
        double armAngle = Math.Log(Math.Max(r, 0.05)) / config.ArmTightness;
        double phase = (theta - armAngle) * config.ArmCount / (2 * Math.PI);
        double toRidge = Math.Abs(phase - Math.Round(phase)) * 2;   // 0 at ridge, 1 between
        double arms = Math.Exp(-(toRidge * toRidge) / (2 * config.ArmWidth * config.ArmWidth))
                      * (1 - core) * config.ArmStrength;

        return Math.Max(core + disc * 0.45 + arms * disc, 0.0);
    }

    /// <summary>Potential at a region cell's center, normalized against the
    /// same world rim radius the density field uses.</summary>
    public static double AtCell(GalaxyConfig config, HexCoordinate cellCoord, double t01)
    {
        var (wx, wy) = HexGrid.HexToWorld(HexGrid.CellCenter(cellCoord));
        double rim = DensityField.WorldRimRadius(config);
        return At(config, wx / rim, wy / rim, t01);
    }
}
