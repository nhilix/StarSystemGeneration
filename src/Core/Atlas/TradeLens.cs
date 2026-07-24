using System;
using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Atlas;

/// <summary>Spread bands, matching EpochMapView's trade glyph thresholds
/// (',' &lt;0.05 · '-' &lt;0.25 · '=' &lt;0.50 · '+' &lt;1 · '#' 1+) so the
/// atlas and the REPL tell the same story.</summary>
public enum TradeBand { Flat = 0, Mild = 1, Firm = 2, Strong = 3, Steep = 4 }

/// <summary>One live lane's trade reading: the steepest ACTIONABLE relative
/// price gradient across all goods (the spread a trader could act on), a
/// saturating weight for width/brightness, and a color on the margin gold
/// whose loudness follows the gradient.</summary>
public readonly record struct TradeSegment(
    int LaneId, HexCoordinate A, HexCoordinate B, double Spread,
    TradeBand Band, double Weight, Rgba Color);

/// <summary>The trade lens — ported verbatim from EpochMapView.TradeCells
/// (AC2.1): live lanes only, and per good the cheap end must hold resting
/// asks — a price gap over an empty book is scarcity, not margin. Spreads
/// derive from market and book state at query time; nothing here is stored.</summary>
public static class TradeLens
{
    // Margin reads in the money family — WorksLens.SiteAmber's gold, so
    // economic surfaces share one hue and loudness is the only variable.
    public static readonly Rgba MarginGold = new(240, 195, 95);
    /// <summary>The flat-spread alpha floor — public because it is the
    /// shared source value for the dead-lane idle read: TrafficLens.IdleAlpha
    /// mirrors it (both 45, one economic-surface idle treatment), and
    /// LaneLayer's Trade-mode dead-lane stroke reads it directly rather than
    /// keeping its own copy (AC4.4).</summary>
    public const byte FlatAlpha = 45;
    private const byte FullAlpha = 220;
    /// <summary>Spread where the weight saturates — the '#' band's floor;
    /// a doubled price (or steeper) reads as a full margin run.</summary>
    private const double SaturationSpread = 1.0;

    /// <summary>Band parity with EpochMapView.TradeGlyph.</summary>
    public static TradeBand BandOf(double spread) => spread switch
    {
        < 0.05 => TradeBand.Flat,
        < 0.25 => TradeBand.Mild,
        < 0.50 => TradeBand.Firm,
        < 1.00 => TradeBand.Strong,
        _ => TradeBand.Steep,
    };

    /// <summary>Saturating width/brightness weight: 0 on a flat lane,
    /// sqrt-eased toward 1 at SaturationSpread (thin margins stay visible,
    /// steep ones don't blow out).</summary>
    public static double WeightOf(double spread) =>
        spread <= 0 ? 0.0
        : Math.Sqrt(Math.Min(1.0, spread / SaturationSpread));

    /// <summary>The one spread computation Cells and Segments share — the
    /// max over goods of hi/lo - 1, counting only gradients a trader can
    /// ACT on: the cheap end must have resting asks to lift.</summary>
    private static double SpreadOf(SimState state, Lane lane)
    {
        var mA = state.Markets[lane.PortAId];
        var mB = state.Markets[lane.PortBId];
        double spread = 0;
        for (int g = 0; g < mA.Price.Length; g++)
        {
            double lo = Math.Min(mA.Price[g], mB.Price[g]);
            double hi = Math.Max(mA.Price[g], mB.Price[g]);
            if (lo <= 1e-9) continue;
            // only gradients a trader can ACT on: the cheap end must
            // have resting asks to lift — a price gap over an empty
            // book is scarcity, not margin
            int cheap = mA.Price[g] <= mB.Price[g]
                ? lane.PortAId : lane.PortBId;
            if (BookOps.AskQty(state, cheap, g) <= 1e-9) continue;
            spread = Math.Max(spread, hi / lo - 1.0);
        }
        return spread;
    }

    /// <summary>Per-lane segments for the drawn layer — live lanes only
    /// (a dead lane has no trade reading; the lane lens owns the wound).</summary>
    public static IReadOnlyList<TradeSegment> Segments(AtlasReadModel model,
                                                       EyeContext eye)
    {
        var state = model.State;
        var segments = new List<TradeSegment>();
        foreach (var lane in state.Lanes)
        {
            if (!LaneMath.IsLive(state, lane)) continue;
            double spread = SpreadOf(state, lane);
            double weight = WeightOf(spread);
            segments.Add(new TradeSegment(
                lane.Id,
                state.Ports[lane.PortAId].Hex,
                state.Ports[lane.PortBId].Hex,
                spread, BandOf(spread), weight,
                new Rgba(MarginGold.R, MarginGold.G, MarginGold.B,
                         (byte)(FlatAlpha + (FullAlpha - FlatAlpha) * weight))));
        }
        return segments;
    }

    /// <summary>The REPL parity surface — each live lane's spread painted
    /// along its hex line (cube lerp + round, max where strokes cross);
    /// exactly what EpochMapView.TradeCells returns.</summary>
    public static Dictionary<HexCoordinate, double> Cells(AtlasReadModel model)
    {
        var state = model.State;
        var cells = new Dictionary<HexCoordinate, double>();
        foreach (var lane in state.Lanes)
        {
            if (!LaneMath.IsLive(state, lane)) continue;
            double spread = SpreadOf(state, lane);
            var a = state.Ports[lane.PortAId].Hex;
            var b = state.Ports[lane.PortBId].Hex;
            int n = HexGrid.Distance(a, b);
            for (int i = 0; i <= n; i++)
            {
                double t = n == 0 ? 0.0 : (double)i / n;
                var cell = HexGrid.CellOf(HexGrid.Round(
                    a.Q + (b.Q - a.Q) * t, a.R + (b.R - a.R) * t));
                cells.TryGetValue(cell, out double held);
                cells[cell] = Math.Max(held, spread);
            }
        }
        return cells;
    }
}
