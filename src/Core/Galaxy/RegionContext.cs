using System;
using StarGen.Core.Content;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Per-hex regional read (spec §8): natural + political modifiers, pre-commitments.</summary>
public sealed class RegionContext
{
    public Func<StarTypeDef, double> StarTypeModifier { get; private set; } = _ => 1.0;
    public Func<BodyKind?, double> BeltModifier { get; private set; } = _ => 1.0;
    public double SettlementScale { get; private set; } = 1.0;
    public Anchor? AnchorAt { get; private set; }
    public int OwnerPolityId { get; private set; } = -1;
    public bool WarScarred { get; private set; }

    public static RegionContext? For(GalaxyContext galaxy, HexCoordinate hex)
    {
        if (galaxy.IsFlatspace || galaxy.Skeleton == null) return null;
        var s = galaxy.Skeleton;
        if (!DensityField.InGalaxy(galaxy.Config, hex)) return null;
#warning HEXMIGRATION - placeholder rectangular cell store cannot index negative-coordinate hexes; Task 5 replaces the store and Task 8 rewrites this method
        if (hex.Q < 0 || hex.R < 0) return null;   // defensive until the hex-lattice store lands
        var cell = s.CellForHex(hex);

        var region = new RegionContext
        {
            StarTypeModifier = LeanModifier(cell.Lean),
            BeltModifier = k => k == BodyKind.PlanetoidBelt ? 0.5 + cell.Metallicity : 1.0,
            SettlementScale = InterpolatedSettlementScale(s, hex),
            OwnerPolityId = cell.OwnerPolityId,
            WarScarred = cell.WarScarred,
        };
        foreach (var anchor in cell.Anchors)
            if (anchor.Hex.Equals(hex)) { region.AnchorAt = anchor; break; }
        return region;
    }

    private static Func<StarTypeDef, double> LeanModifier(StellarLean lean) => lean switch
    {
        StellarLean.YoungBright => def => def.Id switch
        {
            "gold_main" or "white_blaze" or "blue_titan" => 2.0,
            "ashen_remnant" or "collapsed_core" => 0.3,
            _ => 1.0,
        },
        StellarLean.OldDim => def => def.Id switch
        {
            "ember_dwarf" or "amber_dwarf" => 2.0,
            "gold_main" => 0.6,
            "white_blaze" or "blue_titan" => 0.2,
            _ => 1.0,
        },
        StellarLean.RemnantGraveyard => def => def.Id switch
        {
            "ashen_remnant" or "collapsed_core" => 4.0,
            _ => 0.4,
        },
        _ => _ => 1.0,
    };

    /// <summary>Bilinear over the 4 nearest cell centers (spec §8 smoothing).</summary>
    private static double InterpolatedSettlementScale(GalaxySkeleton s, HexCoordinate hex)
    {
#warning HEXMIGRATION: bilinear neighbor-cell clamp uses the placeholder square grid; the hex-native settlement-scale lookup lands with the RegionContext rewrite.
        double CellScale(int cx, int cy)
        {
            cx = Math.Clamp(cx, 0, s.GridSize - 1);
            cy = Math.Clamp(cy, 0, s.GridSize - 1);
            var cell = s.CellAt(cx, cy);
            if (cell.OwnerPolityId >= 0) return 1.0 + 0.8 * cell.DevelopmentTier;
            return cell.WarScarred ? 0.4 : 1.0;
        }
        // Position in cell-center space: cell centers sit at (cx*8+4, cy*10+5).
        double fx = (hex.Q - 4.0) / 8.0, fy = (hex.R - 5.0) / 10.0;
        int cx0 = (int)Math.Floor(fx), cy0 = (int)Math.Floor(fy);
        double tx = fx - cx0, ty = fy - cy0;
        double a = CellScale(cx0, cy0) * (1 - tx) + CellScale(cx0 + 1, cy0) * tx;
        double b = CellScale(cx0, cy0 + 1) * (1 - tx) + CellScale(cx0 + 1, cy0 + 1) * tx;
        return a * (1 - ty) + b * ty;
    }
}
