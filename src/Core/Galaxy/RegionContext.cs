using System;
using StarGen.Core.Content;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Per-hex regional read (spec §8): the natural raster's modifiers
/// and anchor pre-commitments. Political modifiers left with the prototype's
/// per-cell state; development returns as proximity-to-port when the epoch
/// sim's registries wire into per-hex generation (post-slice-B). The
/// SettlementScale mechanism stays — natural 1.0 until then.</summary>
public sealed class RegionContext
{
    public Func<StarTypeDef, double> StarTypeModifier { get; private set; } = _ => 1.0;
    public Func<BodyKind?, double> BeltModifier { get; private set; } = _ => 1.0;
    public double SettlementScale { get; private set; } = 1.0;
    public Anchor? AnchorAt { get; private set; }

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

}
