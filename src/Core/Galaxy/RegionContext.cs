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
        // The dictionary-backed cell store (Task 5) indexes negative-coordinate hexes
        // fine, so the old defensive early-return is gone: InGalaxy already guarantees
        // CellForHex resolves to a cell that exists.
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

#warning HEXMIGRATION: bilinear 4-neighbor-cell smoothing removed pending hex-native geometry (the old square-grid cell-center math no longer applies); settlement scale currently reads only the hex's own cell, no interpolation, until the RegionContext rewrite (Task 8).
    private static double InterpolatedSettlementScale(GalaxySkeleton s, HexCoordinate hex)
    {
        var cell = s.CellForHex(hex);
        if (cell.OwnerPolityId >= 0) return 1.0 + 0.8 * cell.DevelopmentTier;
        return cell.WarScarred ? 0.4 : 1.0;
    }
}
