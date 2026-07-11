using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>The REPL `map` raster layers as the NATURE lens group
/// (unity-atlas-design.md): base layers under the political ones, one per
/// genesis residue field. Ports GalaxyMapView's value derivations to colors
/// — never paint, always a read of the residue.</summary>
public enum NatureLayer
{
    Density, Lean, Gas, Metal, Age, Minerals, Bio, Emergence, Features,
}

public static class NatureLens
{
    private static readonly Rgba DensityBase = new(200, 200, 210);
    private static readonly Rgba GasBase = new(110, 150, 230);
    private static readonly Rgba MetalBase = new(230, 150, 90);
    private static readonly Rgba AgeBase = new(210, 170, 120);
    private static readonly Rgba MineralsBase = new(240, 190, 70);
    private static readonly Rgba BioBase = new(110, 220, 140);

    // Lean categories carry the PoC LayerPalette's colors.
    private static readonly Rgba LeanBalanced = new(110, 110, 110);
    private static readonly Rgba LeanYoung = new(120, 170, 255);
    private static readonly Rgba LeanOld = new(200, 120, 80);
    private static readonly Rgba LeanRemnant = new(150, 60, 150);

    private static readonly Rgba OriginCurrent = new(120, 230, 255);
    private static readonly Rgba OriginPrecursor = new(190, 120, 255);
    private static readonly Rgba OriginOther = new(140, 240, 140);
    private static readonly Rgba SterilizationScar = new(180, 70, 50);

    /// <summary>Nature reads the same under every eye (surveyed-detail
    /// gating is reserved for the play tier) — the eye parameter keeps the
    /// query shape uniform across the lens stack.</summary>
    public static IReadOnlyList<Rgba> Shades(AtlasReadModel model, EyeContext eye,
                                             NatureLayer layer)
    {
        var overlay = layer switch
        {
            NatureLayer.Emergence => EmergenceOverlay(model.Skeleton),
            NatureLayer.Features => FeaturesOverlay(model.Skeleton),
            _ => null,
        };
        var shades = new Rgba[model.Cells.Count];
        for (int i = 0; i < shades.Length; i++)
            shades[i] = CellShade(model.Cells[i], layer, overlay);
        return shades;
    }

    private static Rgba CellShade(RegionCell c, NatureLayer layer,
                                  Dictionary<HexCoordinate, Rgba>? overlay)
    {
        if (overlay != null && overlay.TryGetValue(c.Coord, out var marked))
            return marked;
        // Dark-wilds are value-poor, never blank: IsVoid is only the
        // traversability flag — CosmicResidue writes real fields for
        // every cell, and the atlas renders them (the sampler keeps the
        // wilds dim; the REPL map retains its blank-glyph convention).
        return layer switch
        {
            NatureLayer.Lean => c.Lean switch
            {
                StellarLean.YoungBright => LeanYoung,
                StellarLean.OldDim => LeanOld,
                StellarLean.RemnantGraveyard => LeanRemnant,
                _ => LeanBalanced,
            },
            NatureLayer.Gas => AtlasPalette.Ramp(GasBase, c.GasFraction / 0.5),
            NatureLayer.Metal => AtlasPalette.Ramp(MetalBase, c.Metallicity),
            NatureLayer.Age => AtlasPalette.Ramp(AgeBase, c.CohortOld + c.CohortRemnant),
            NatureLayer.Minerals => AtlasPalette.Ramp(MineralsBase, c.MineralRichness),
            NatureLayer.Bio => c.BiosphereRichness <= 0
                ? AtlasPalette.Floor
                : AtlasPalette.Ramp(BioBase, c.BiosphereRichness),
            NatureLayer.Emergence => c.BiosphereRichness > 0
                ? AtlasPalette.Ramp(BioBase, 0.35)
                : AtlasPalette.Floor,
            NatureLayer.Features => AtlasPalette.Floor,
            _ => AtlasPalette.Ramp(DensityBase, c.MeanDensity),
        };
    }

    /// <summary>Origins and sterilization scars at their cells (the REPL
    /// emergence layer's C/P/N and x marks, as colors).</summary>
    private static Dictionary<HexCoordinate, Rgba> EmergenceOverlay(GalaxySkeleton s)
    {
        var marks = new Dictionary<HexCoordinate, Rgba>();
        foreach (var wave in s.PrecursorWaves)
            foreach (var site in wave.Sites)
                if (site.Type == PrecursorSiteType.SterilizationScar)
                    marks[HexGrid.CellOf(site.Hex)] = SterilizationScar;
        foreach (var origin in s.Origins)
            marks[origin.CellCoord] = origin.Era switch
            {
                OriginEra.Current => OriginCurrent,
                OriginEra.Precursor => OriginPrecursor,
                _ => OriginOther,
            };
        return marks;
    }

    /// <summary>Galactic features colored by type; low-priority types write
    /// first so rarer marks win overlapping cells (REPL convention).</summary>
    private static Dictionary<HexCoordinate, Rgba> FeaturesOverlay(GalaxySkeleton s)
    {
        var marks = new Dictionary<HexCoordinate, Rgba>();
        Mark(s, marks, GalacticFeatureType.AgnOutburst, new Rgba(200, 80, 160));
        Mark(s, marks, GalacticFeatureType.MergerStream, new Rgba(90, 140, 220));
        Mark(s, marks, GalacticFeatureType.DarkCloud, new Rgba(60, 50, 70));
        Mark(s, marks, GalacticFeatureType.EmissionNebula, new Rgba(230, 90, 120));
        Mark(s, marks, GalacticFeatureType.SupernovaRemnant, new Rgba(255, 140, 60));
        Mark(s, marks, GalacticFeatureType.GlobularCluster, new Rgba(240, 220, 140));
        return marks;
    }

    private static void Mark(GalaxySkeleton s, Dictionary<HexCoordinate, Rgba> marks,
                             GalacticFeatureType type, Rgba color)
    {
        foreach (var feature in s.Features)
            if (feature.Type == type)
                foreach (var coord in feature.Cells)
                    marks[coord] = color;
    }
}
