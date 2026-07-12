using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>The price lens — the parameterized one: per-good price ratio
/// vs founding price at the nearest servicing port, a plane field over
/// the service areas (market-geography.md: the most legible economic
/// layer). Banded on emap's PriceGlyph thresholds so the atlas and the
/// REPL tell the same story: spikes at blockades, gluts at cut-off
/// producers, the unserviced wilds clear.</summary>
public static class PriceLens
{
    // One shade per glyph band — diverging cool (glut) → hot (famine),
    // par sitting quiet so a healthy economy doesn't shout.
    private static readonly Rgba Glut = new(70, 115, 225, 190);      // '_'
    private static readonly Rgba Cheap = new(75, 180, 195, 140);     // '-'
    private static readonly Rgba Par = new(120, 145, 125, 70);       // '='
    private static readonly Rgba Dear = new(230, 180, 70, 140);      // '+'
    private static readonly Rgba Scarce = new(240, 130, 50, 190);    // '*'
    private static readonly Rgba Spike = new(240, 70, 50, 220);      // '#'
    private static readonly Rgba Famine = new(255, 130, 210, 240);   // '!'

    /// <summary>Live price over founding price for the nearest servicing
    /// port, NaN where no port services the hex (the wilds carry no
    /// price). EpochMapView.PriceGlyph's derivation, addressed.</summary>
    public static double RatioAt(AtlasReadModel model, EyeContext eye,
                                 HexCoordinate hex, GoodId good)
    {
        var state = model.State;
        int best = -1, bestDist = int.MaxValue;
        foreach (var p in state.Ports)                    // id order (P6)
        {
            if (!PortDomains.Services(state.Skeleton, state.Config, p, hex))
                continue;
            int dist = HexGrid.Distance(p.Hex, hex);
            if (dist < bestDist) { bestDist = dist; best = p.Id; }
        }
        if (best < 0) return double.NaN;
        return state.Markets[best].Price[(int)good]
               / Market.InitialPrice(state.Config.Economy, good);
    }

    /// <summary>Per-raster-cell price shades, parallel to model.Cells,
    /// sampled at cell centers — the baked-texture field the presentation
    /// renders (the NatureFieldLayer pattern, economics instead of gas).</summary>
    public static IReadOnlyList<Rgba> CellShades(AtlasReadModel model,
        EyeContext eye, GoodId good)
    {
        var shades = new Rgba[model.Cells.Count];
        for (int i = 0; i < shades.Length; i++)
        {
            if (model.Cells[i].IsVoid) { shades[i] = AtlasPalette.Clear; continue; }
            double ratio = RatioAt(model, eye,
                HexGrid.CellCenter(model.Cells[i].Coord), good);
            shades[i] = double.IsNaN(ratio) ? AtlasPalette.Clear : ShadeOf(ratio);
        }
        return shades;
    }

    /// <summary>Band parity with EpochMapView.PriceGlyph.</summary>
    public static Rgba ShadeOf(double ratio) => ratio switch
    {
        < 0.25 => Glut,
        < 0.6 => Cheap,
        < 1.5 => Par,
        < 3.0 => Dear,
        < 8.0 => Scarce,
        < 30.0 => Spike,
        _ => Famine,
    };
}
