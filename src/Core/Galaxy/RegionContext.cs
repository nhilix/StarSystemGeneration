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
        if (!DensityField.InGalaxy(galaxy.Config, hex)) return null;
        var s = galaxy.Skeleton;
        var cell = s.CellForHex(hex);

        var region = new RegionContext
        {
            StarTypeModifier = LeanModifier(cell.Lean),
            BeltModifier = k => k == BodyKind.PlanetoidBelt ? 0.5 + cell.Metallicity : 1.0,
            SettlementScale = SmoothedSettlementScale(s, hex, cell),
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

    /// <summary>Inverse-distance weighting over the hex's own cell + its existing
    /// lattice neighbors (spec §5) — smoother than bilinear, no corner cases.</summary>
    private static double SmoothedSettlementScale(GalaxySkeleton s, HexCoordinate hex, RegionCell own)
    {
        var (hx, hy) = HexGrid.HexToWorld(hex);
        double weightSum = 0, scaleSum = 0;

        void Accumulate(RegionCell cell)
        {
            var (cx, cy) = HexGrid.HexToWorld(HexGrid.CellCenter(cell.Coord));
            double dist = Math.Sqrt((hx - cx) * (hx - cx) + (hy - cy) * (hy - cy));
            double weight = 1.0 / (1.0 + dist);
            double cellScale = cell.OwnerPolityId >= 0 ? 1.0 + 0.8 * cell.DevelopmentTier
                : cell.WarScarred ? 0.4 : 1.0;
            weightSum += weight;
            scaleSum += weight * cellScale;
        }

        Accumulate(own);
        foreach (var neighborCoord in HexGrid.Neighbors(own.Coord))
            if (s.TryGetCell(neighborCoord, out var neighbor))
                Accumulate(neighbor);
        return scaleSum / weightSum;
    }
}
