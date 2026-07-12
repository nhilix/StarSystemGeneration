using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using StarGen.Core.Tests.Epoch;
using Xunit;

namespace StarGen.Core.Tests.Atlas;

/// <summary>The traffic lens — posted trips/year per lane weighting width
/// and brightness (K2). Parity target: EpochMapView's traffic bands
/// (',' no hulls · '-' &lt;0.5 · '=' &lt;2 · '+' &lt;5 · '#' 5+), values
/// from FleetOps.TrafficPerYear (the slice-I news-speed data).</summary>
public class TrafficLensTests
{
    private static (AtlasReadModel Model, SimState State) WithLane()
    {
        var (_, state) = EpochTestKit.Seeded();
        HexCoordinate? a = null, b = null;
        foreach (var cell in state.Skeleton.Cells)
        {
            if (cell.IsVoid) continue;
            if (a == null) { a = HexGrid.CellCenter(cell.Coord); continue; }
            b = HexGrid.CellCenter(cell.Coord);
            break;
        }
        state.Ports.Add(new Port(0, state.Actors[0].Id, a!.Value, tier: 2, foundedYear: 0));
        state.Ports.Add(new Port(1, state.Actors[0].Id, b!.Value, tier: 2, foundedYear: 0));
        EpochTestKit.AddLane(state, 0, 1);
        return (new AtlasReadModel(state), state);
    }

    [Fact]
    public void AnUnpostedLaneReadsNoTraffic()
    {
        var (model, state) = WithLane();
        var seg = Assert.Single(
            TrafficLens.Segments(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(0.0, seg.TripsPerYear);
        Assert.Equal(TrafficBand.None, seg.Band);
        Assert.Equal(0.0, seg.Weight);
    }

    [Fact]
    public void TripsPerYearMatchesFleetOpsExactly()
    {
        var (model, state) = WithLane();
        EpochTestKit.PostFreight(state, state.Actors[0].Id, laneId: 0, hulls: 4);
        var seg = Assert.Single(
            TrafficLens.Segments(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(FleetOps.TrafficPerYear(state, state.Lanes[0]),
                     seg.TripsPerYear);
        Assert.True(seg.TripsPerYear > 0);
        Assert.NotEqual(TrafficBand.None, seg.Band);
    }

    [Fact]
    public void BandsMatchTheEmapGlyphThresholds()
    {
        Assert.Equal(TrafficBand.None, TrafficLens.BandOf(0.0));
        Assert.Equal(TrafficBand.Trickle, TrafficLens.BandOf(0.3));
        Assert.Equal(TrafficBand.Light, TrafficLens.BandOf(0.5));
        Assert.Equal(TrafficBand.Light, TrafficLens.BandOf(1.9));
        Assert.Equal(TrafficBand.Steady, TrafficLens.BandOf(2.0));
        Assert.Equal(TrafficBand.Heavy, TrafficLens.BandOf(5.0));
    }

    [Fact]
    public void WeightRisesWithTrafficAndSaturates()
    {
        var (model, state) = WithLane();
        EpochTestKit.PostFreight(state, state.Actors[0].Id, laneId: 0, hulls: 2);
        var light = Assert.Single(
            TrafficLens.Segments(model, EyeContext.God(state.WorldYear))).Weight;
        EpochTestKit.PostFreight(state, state.Actors[0].Id, laneId: 0, hulls: 40);
        var heavy = Assert.Single(
            TrafficLens.Segments(model, EyeContext.God(state.WorldYear))).Weight;
        Assert.True(light > 0.0);
        Assert.True(heavy > light);
        Assert.True(heavy <= 1.0);
    }

    [Fact]
    public void SegmentsCarryTheLaneEndpoints()
    {
        var (model, state) = WithLane();
        var seg = Assert.Single(
            TrafficLens.Segments(model, EyeContext.God(state.WorldYear)));
        Assert.Equal(state.Ports[0].Hex, seg.A);
        Assert.Equal(state.Ports[1].Hex, seg.B);
    }
}
