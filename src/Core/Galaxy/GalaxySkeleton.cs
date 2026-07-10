using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>The natural-raster root: cells on the hex lattice inside the
/// circular footprint of radius GalaxyRadiusCells lattice steps
/// (DensityField.CellInGalaxy), in deterministic spiral order, plus the
/// species the seeding passes rolled. Persisted as the artifact's raster and
/// species layers (StarGen.Core.Epoch.ArtifactSerializer); versioning lives
/// per layer there.</summary>
public sealed class GalaxySkeleton
{
    public GalaxyConfig Config { get; }
    public IReadOnlyList<RegionCell> Cells => _cells;
    public List<SpeciesProfile> Species { get; } = new();

    private readonly List<RegionCell> _cells = new();
    private readonly Dictionary<HexCoordinate, RegionCell> _byCoord = new();

    public GalaxySkeleton(GalaxyConfig config)
    {
        Config = config;
        // The circular footprint reaches past lattice ring GalaxyRadiusCells:
        // ring d's nearest cells sit at d·(√3/2) lattice steps from the origin,
        // so members can appear out to ring 2R/√3.
        int enumRadius = (int)System.Math.Ceiling(config.GalaxyRadiusCells * 2.0 / System.Math.Sqrt(3.0));
        foreach (var coord in HexGrid.Spiral(new HexCoordinate(0, 0), enumRadius))
        {
            if (!DensityField.CellInGalaxy(config, coord)) continue;
            var cell = new RegionCell { Q = coord.Q, R = coord.R, SpiralIndex = _cells.Count };
            _cells.Add(cell);
            _byCoord[coord] = cell;
        }
    }

    public RegionCell CellAt(HexCoordinate cellCoord) => _byCoord[cellCoord];

    public bool TryGetCell(HexCoordinate cellCoord, out RegionCell cell) =>
        _byCoord.TryGetValue(cellCoord, out cell!);

    public RegionCell CellForHex(HexCoordinate hex) => CellAt(HexGrid.CellOf(hex));
}
