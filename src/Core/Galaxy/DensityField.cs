using System;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Galaxy;

/// <summary>Tier 1 (spec §4): the per-hex density field. Since slice F it
/// reads the simulated present-day cell layer (× hex-scale clumping noise)
/// instead of the analytic shape paint; the shape math lives on as the
/// potential prior (StarGen.Core.Genesis.GalaxyPotential). Geometry
/// (membership, rim) is unchanged.</summary>
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

    /// <summary>Tier-1 per-hex density since slice F: interpolated
    /// present-day cell density × hex-scale clumping noise
    /// (cosmic-genesis.md §Tier-1 consequence). The cell layer is the
    /// simulated residue — itself a pure function of config — so the hex
    /// tier remains a pure, never-persisted function of (config,
    /// coordinate). Interpolation is inverse-distance-squared over the
    /// containing cell and its in-galaxy lattice neighbors, so cell edges
    /// never print through to hex-level presence.</summary>
    public static double At(GalaxySkeleton skeleton, HexCoordinate hex)
    {
        var config = skeleton.Config;
        if (!InGalaxy(config, hex)) return 0.0;

        var (wx, wy) = HexGrid.HexToWorld(hex);
        var cellCoord = HexGrid.CellOf(hex);
        double weighted = 0, weights = 0;
        Accumulate(skeleton, cellCoord, wx, wy, ref weighted, ref weights);
        foreach (var neighbor in HexGrid.Neighbors(cellCoord))
            Accumulate(skeleton, neighbor, wx, wy, ref weighted, ref weights);
        if (weights <= 0) return 0.0;
        double cellDensity = weighted / weights;

        double noise = ValueNoise.Warped(config.MasterSeed,
            RollChannel.NoiseDensityLattice, RollChannel.NoiseWarpLattice,
            wx, wy, octaves: 3, frequency: 0.02, warpStrength: 30.0);

        return Math.Clamp(cellDensity * (0.25 + 1.5 * noise), 0.0, 1.0);
    }

    private static void Accumulate(GalaxySkeleton skeleton, HexCoordinate cellCoord,
        double wx, double wy, ref double weighted, ref double weights)
    {
        if (!skeleton.TryGetCell(cellCoord, out var cell)) return;
        var (cx, cy) = HexGrid.HexToWorld(HexGrid.CellCenter(cellCoord));
        double dx = wx - cx, dy = wy - cy;
        double w = 1.0 / (1.0 + (dx * dx + dy * dy) / (CellLatticeUnit * CellLatticeUnit));
        weighted += cell.MeanDensity * w;
        weights += w;
    }
}
