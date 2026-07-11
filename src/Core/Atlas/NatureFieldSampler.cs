using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Rng;

namespace StarGen.Core.Atlas;

/// <summary>Turns per-cell nature shades into a smooth nebular field:
/// every sample point Gaussian-blends the surrounding cells (center + two
/// rings), alpha scales with the field's presence above the floor (rich
/// areas glow, poor areas fade to starfield), the void feathers to
/// transparency at the disc edge, and deterministic value noise
/// (RollChannel.AtlasNebula — view-only) breaks the result into clouds.
/// Pure read: same model, same sky.</summary>
public sealed class NatureFieldSampler
{
    /// <summary>Adjacent cell centers sit |A|=√(16.5²+0.87²)≈16.5 world
    /// units apart; σ at ~0.55 spacing keeps structure while erasing the
    /// lattice.</summary>
    private const double Sigma = 9.0;
    private const double TwoSigmaSq = 2.0 * Sigma * Sigma;
    private const int GatherRings = 2;
    private const double NoiseFrequency = 0.07;   // wavelength ≈ 14 world

    private readonly AtlasReadModel _model;
    private readonly IReadOnlyList<Rgba> _shades;
    private readonly ulong _seed;

    public NatureFieldSampler(AtlasReadModel model, EyeContext eye,
                              NatureLayer layer)
    {
        _model = model;
        _shades = NatureLens.Shades(model, eye, layer);
        _seed = model.Skeleton.Config.MasterSeed;
    }

    public Rgba Sample(double x, double y)
    {
        var c0 = HexGrid.CellOf(HexGrid.WorldToHex(x, y));
        double wsum = 0, asum = 0, r = 0, g = 0, b = 0;
        foreach (var cellCoord in HexGrid.Spiral(c0, GatherRings))
        {
            var (cx, cy) = HexGrid.HexToWorld(HexGrid.CellCenter(cellCoord));
            double dx = x - cx, dy = y - cy;
            double w = Math.Exp(-(dx * dx + dy * dy) / TwoSigmaSq);
            wsum += w;
            // Cells outside the disc (and void cells, via presence 0)
            // contribute weight but no light — the field feathers out.
            if (!_model.TryIndexOfCell(cellCoord, out int i)) continue;
            var s = _shades[i];
            double presence = PresenceOf(s);
            if (presence <= 0) continue;
            double wa = w * presence;
            asum += wa;
            r += s.R * wa;
            g += s.G * wa;
            b += s.B * wa;
        }
        if (wsum <= 1e-9 || asum <= 1e-9) return AtlasPalette.Clear;

        double alpha = asum / wsum;
        // Cloud breakup: deterministic, view-only noise channel. The dip
        // floor stays high enough that thin regions never wink out.
        double n = ValueNoise.Sample(_seed, RollChannel.AtlasNebula,
                                     x, y, octaves: 2, NoiseFrequency);
        alpha *= 0.75 + 0.5 * n;

        return new Rgba(
            (byte)Math.Clamp(r / asum, 0, 255),
            (byte)Math.Clamp(g / asum, 0, 255),
            (byte)Math.Clamp(b / asum, 0, 255),
            (byte)Math.Clamp(alpha * 255.0, 0, 255));
    }

    /// <summary>How much this shade rises above the floor. Every LIVE
    /// cell keeps a visible base presence — the disc reads as a blended
    /// whole with the arms as bright features (the REPL map's read);
    /// only true void goes dark.</summary>
    private static double PresenceOf(Rgba s)
    {
        if (s == AtlasPalette.Void) return 0;
        double luma = (0.299 * s.R + 0.587 * s.G + 0.114 * s.B) / 255.0;
        const double floorLuma = 0.104;   // luma of AtlasPalette.Floor
        double raw = Math.Clamp((luma - floorLuma) / 0.55, 0.0, 1.0);
        return 0.30 + 0.70 * raw;
    }
}
