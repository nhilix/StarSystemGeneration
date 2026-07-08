using System.Collections.Generic;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>The persisted Tier 2 artifact root (spec §3.1).</summary>
public sealed class GalaxySkeleton
{
    public const int SchemaVersion = 1;

    public GalaxyConfig Config { get; }
    public RegionCell[] Cells { get; }
    public List<SpeciesProfile> Species { get; } = new();
    public List<Polity> Polities { get; } = new();
    public List<GalaxyEvent> Events { get; } = new();

#warning HEXMIGRATION: GridSize is a placeholder square grid sized off GalaxyRadiusCells; the real hex-lattice cell store (RegionCell keyed by superhex CellOf/CellCenter) lands in its own task.
    /// <summary>Temporary square-grid width/height, standing in for the old
    /// SizeSectors-derived CellsX/CellsY until the hex-lattice cell store lands.</summary>
    public int GridSize { get; }

    public GalaxySkeleton(GalaxyConfig config)
    {
        Config = config;
        GridSize = GridSizeFor(config);
        Cells = new RegionCell[GridSize * GridSize];
        for (int cy = 0; cy < GridSize; cy++)
            for (int cx = 0; cx < GridSize; cx++)
                Cells[cy * GridSize + cx] = new RegionCell { Cx = cx, Cy = cy };
    }

    /// <summary>Shared placeholder sizing so callers that only hold a GalaxyConfig
    /// (e.g. RegionCell.LinearIndex) agree with the instance GridSize above.</summary>
    internal static int GridSizeFor(GalaxyConfig config) => config.GalaxyRadiusCells * 2 + 1;

    public RegionCell CellAt(int cx, int cy) => Cells[cy * GridSize + cx];

    public RegionCell CellForHex(HexCoordinate hex) => CellAt(hex.Q / 8, hex.R / 10);
}
