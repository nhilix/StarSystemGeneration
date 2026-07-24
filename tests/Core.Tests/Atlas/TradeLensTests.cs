using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The trade lens — each live lane's steepest ACTIONABLE relative
/// price gradient across goods (AC2.1, ported verbatim from EpochMapView.
/// TradeCells). Parity targets: the emap trade glyph bands (',' &lt;0.05 ·
/// '-' &lt;0.25 · '=' &lt;0.50 · '+' &lt;1 · '#' 1+) and the saturation
/// filter — a price gap over an empty cheap-end book is scarcity, not
/// margin, and must not read as spread.</summary>
public class TradeLensTests
{
    private const int Good = 0;
    private const int OtherGood = 1;

    /// <summary>Two markets on a live lane, all prices flat at founding —
    /// each test then opens the gaps and books it wants.</summary>
    private static (AtlasReadModel Model, SimState State) WithTradedLane(
        int ports = 2)
    {
        var (_, state) = EpochTestKit.Seeded();
        var hexes = new List<HexCoordinate>();
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            hexes.Add(HexGrid.CellCenter(cell.Coord));
            if (hexes.Count == ports) break;
        }
        for (int i = 0; i < ports; i++)
        {
            state.Ports.Add(new Port(i, state.Actors[0].Id, hexes[i], tier: 2,
                                     foundedYear: 0));
            state.Markets.Add(new Market(i, state.Config.Economy));
        }
        EpochTestKit.AddLane(state, 0, 1);
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void ADeadLaneLeavesNoCellsAndNoSegments()
    {
        var (model, state) = WithTradedLane();
        state.Markets[0].Price[Good] = 10.0;
        state.Markets[1].Price[Good] = 20.0;
        EpochTestKit.Stock(state, 0, Good, qty: 5.0);
        // Down one gate below functional condition — the lane goes dark.
        state.Facilities[state.Lanes[0].GateAId].Condition = 0.0;
        Assert.False(LaneMath.IsLive(state, state.Lanes[0]));
        Assert.Empty(TradeLens.Cells(model));
        Assert.Empty(TradeLens.Segments(model, EyeContext.God(state.WorldYear)));
    }

    [Fact]
    public void AGapOverAnEmptyCheapBookIsScarcityNotMargin()
    {
        var (model, state) = WithTradedLane();
        state.Markets[0].Price[Good] = 10.0;   // cheap end: port 0
        state.Markets[1].Price[Good] = 20.0;
        // No asks anywhere — the gap must not read as spread.
        var seg = Assert.Single(
            TradeLens.Segments(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(0.0, seg.Spread);
        // Asks at the EXPENSIVE end don't rescue it either — nothing to lift.
        EpochTestKit.Stock(state, 1, Good, qty: 5.0);
        seg = Assert.Single(
            TradeLens.Segments(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(0.0, seg.Spread);
        // Resting asks at the cheap end make the gradient actionable.
        EpochTestKit.Stock(state, 0, Good, qty: 5.0);
        seg = Assert.Single(
            TradeLens.Segments(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(1.0, seg.Spread, precision: 9);   // 20/10 - 1
    }

    [Fact]
    public void LaneSpreadIsTheMaxAcrossGoods()
    {
        var (model, state) = WithTradedLane();
        state.Markets[0].Price[Good] = 10.0;
        state.Markets[1].Price[Good] = 15.0;           // 0.5 spread
        EpochTestKit.Stock(state, 0, Good, qty: 5.0);
        state.Markets[0].Price[OtherGood] = 30.0;      // cheap end: port 1
        state.Markets[1].Price[OtherGood] = 10.0;      // 2.0 spread
        EpochTestKit.Stock(state, 1, OtherGood, qty: 5.0);
        var seg = Assert.Single(
            TradeLens.Segments(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(2.0, seg.Spread, precision: 9);
    }

    [Fact]
    public void CellsPaintBothEndpointsWithTheLaneSpread()
    {
        var (model, state) = WithTradedLane();
        state.Markets[0].Price[Good] = 10.0;
        state.Markets[1].Price[Good] = 15.0;
        EpochTestKit.Stock(state, 0, Good, qty: 5.0);
        var cells = TradeLens.Cells(model);
        double atA = cells[HexGrid.CellOf(state.Ports[0].Hex)];
        double atB = cells[HexGrid.CellOf(state.Ports[1].Hex)];
        Assert.Equal(0.5, atA, precision: 9);
        Assert.Equal(0.5, atB, precision: 9);
    }

    [Fact]
    public void CrossingStrokesKeepTheSteeperSpread()
    {
        // Two lanes share port 0 — its cell is where the strokes cross.
        var (model, state) = WithTradedLane(ports: 3);
        EpochTestKit.AddLane(state, 0, 2);
        state.Markets[0].Price[Good] = 10.0;
        state.Markets[1].Price[Good] = 12.0;           // lane 0-1: 0.2
        state.Markets[2].Price[Good] = 18.0;           // lane 0-2: 0.8
        EpochTestKit.Stock(state, 0, Good, qty: 5.0);  // cheap end of both
        var cells = TradeLens.Cells(model);
        Assert.Equal(0.8, cells[HexGrid.CellOf(state.Ports[0].Hex)],
                     precision: 9);
        Assert.Equal(0.2, cells[HexGrid.CellOf(state.Ports[1].Hex)],
                     precision: 9);
        Assert.Equal(0.8, cells[HexGrid.CellOf(state.Ports[2].Hex)],
                     precision: 9);
    }

    [Fact]
    public void SegmentsAndCellsShareOneSpreadReading()
    {
        var (model, state) = WithTradedLane();
        state.Markets[0].Price[Good] = 10.0;
        state.Markets[1].Price[Good] = 17.0;
        EpochTestKit.Stock(state, 0, Good, qty: 5.0);
        var seg = Assert.Single(
            TradeLens.Segments(model, EyeContext.God(state.WorldYear)));
        var cells = TradeLens.Cells(model);
        Assert.Equal(seg.Spread, cells[HexGrid.CellOf(seg.A)], precision: 12);
        Assert.Equal(state.Ports[0].Hex, seg.A);
        Assert.Equal(state.Ports[1].Hex, seg.B);
        Assert.Equal(TradeLens.BandOf(seg.Spread), seg.Band);
    }

    [Fact]
    public void BandsMatchTheEmapGlyphThresholds()
    {
        Assert.Equal(TradeBand.Flat, TradeLens.BandOf(0.0));        // ','
        Assert.Equal(TradeBand.Flat, TradeLens.BandOf(0.049));
        Assert.Equal(TradeBand.Mild, TradeLens.BandOf(0.05));       // '-'
        Assert.Equal(TradeBand.Mild, TradeLens.BandOf(0.249));
        Assert.Equal(TradeBand.Firm, TradeLens.BandOf(0.25));       // '='
        Assert.Equal(TradeBand.Firm, TradeLens.BandOf(0.499));
        Assert.Equal(TradeBand.Strong, TradeLens.BandOf(0.50));     // '+'
        Assert.Equal(TradeBand.Strong, TradeLens.BandOf(0.999));
        Assert.Equal(TradeBand.Steep, TradeLens.BandOf(1.0));       // '#'
        Assert.Equal(TradeBand.Steep, TradeLens.BandOf(42.0));
    }
}
