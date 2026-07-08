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

    public GalaxySkeleton(GalaxyConfig config)
    {
        Config = config;
        Cells = new RegionCell[config.CellsX * config.CellsY];
        for (int cy = 0; cy < config.CellsY; cy++)
            for (int cx = 0; cx < config.CellsX; cx++)
                Cells[cy * config.CellsX + cx] = new RegionCell { Cx = cx, Cy = cy };
    }

    public RegionCell CellAt(int cx, int cy) => Cells[cy * Config.CellsX + cx];

    public RegionCell CellForHex(HexCoordinate hex) => CellAt(hex.Q / 8, hex.R / 10);
}
