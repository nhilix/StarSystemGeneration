using System.Collections.Generic;

namespace StarGen.Core.Galaxy;

public enum StellarLean { Balanced, YoungBright, OldDim, RemnantGraveyard }

public sealed class RegionCell
{
    public int Cx { get; set; }
    public int Cy { get; set; }
    public double MeanDensity { get; set; }
    public bool IsVoid { get; set; }
    public bool IsChokepoint { get; set; }
    public StellarLean Lean { get; set; }
    public double Metallicity { get; set; }
    public List<Anchor> Anchors { get; } = new();
    public int OwnerPolityId { get; set; } = -1;   // -1 = unclaimed
    public int DevelopmentTier { get; set; }
    public bool Contested { get; set; }
    public bool WarScarred { get; set; }

#warning HEXMIGRATION: LinearIndex uses the placeholder square-grid width; real hex-lattice indexing lands with the GalaxySkeleton cell-store rewrite.
    public int LinearIndex(GalaxyConfig config) => Cy * GalaxySkeleton.GridSizeFor(config) + Cx;
}
